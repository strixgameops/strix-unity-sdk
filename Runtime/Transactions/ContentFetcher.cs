using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using StrixSDK.Runtime;
using StrixSDK.Runtime.Utils;
using StrixSDK.Runtime.Models;
using StrixSDK.Runtime.Config;
using System.Net.Http;
using StrixSDK.Runtime.APIClient;

namespace StrixSDK.Runtime.Db
{
    public class ContentFetcher : MonoBehaviour
    {
        private static readonly CancellationTokenSource cts = new CancellationTokenSource();
        private static ContentFetcher _instance;
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly HashSet<string> mediaIDs = new HashSet<string>();

        public static ContentFetcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ContentFetcher>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject(typeof(ContentFetcher).ToString());
                        _instance = obj.AddComponent<ContentFetcher>();
                    }
                }
                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }

        /// <summary>
        /// Makes a checksum comparison for specified content types and fetches updated content where needed.
        /// </summary>
        /// <param name="contentTypes">List of content type names to check</param>
        /// <returns>True if any content was updated, false otherwise</returns>
        public async Task<bool> UpdateContentByTypes(List<string> contentTypes)
        {
            try
            {
                var typesToFetch = await ChecksumCheckup(contentTypes);
                if (typesToFetch == null || typesToFetch.Count == 0)
                {
                    return false;
                }

                var fetchTasks = typesToFetch.Select(type => FetchContentByType(type)).ToList();
                if (fetchTasks.Count > 0)
                {
                    await Task.WhenAll(fetchTasks);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error updating content: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Compares local and remote checksums for content types.
        /// </summary>
        /// <param name="contentTypes">List of content type names to check</param>
        /// <returns>List of content types that need updating</returns>
        public async Task<List<string>> ChecksumCheckup(List<string> contentTypes)
        {
            var contentToUpdate = new List<string>();
            try
            {
                StrixSDKConfig config = StrixSDKConfig.Instance;
                string clientID = Strix.ClientID;

                if (string.IsNullOrEmpty(clientID))
                {
                    Debug.LogError("Error making checksum checkup: client ID is invalid.");
                    return contentToUpdate;
                }

                var checkupBody = new Dictionary<string, object>
                {
                    {"device", clientID},
                    {"secret", config.apiKey},
                    {"environment", config.environment},
                    {"tableNames", contentTypes}
                };

                string result = await Client.Req(API.ChecksumCheckup, checkupBody);
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

                if (response == null || !(bool)response["success"])
                {
                    return contentToUpdate;
                }

                var checksums = JsonConvert.DeserializeObject<Dictionary<string, object>>(response["data"].ToString());
                foreach (var field in checksums)
                {
                    int remoteChecksum = Convert.ToInt32(field.Value);
                    int storedChecksum = Content.GetCacheChecksum(field.Key);

                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Comparing checksums for '{field.Key}'. Remote: {remoteChecksum}, Local: {storedChecksum}");

                    if (remoteChecksum != storedChecksum || storedChecksum == -1)
                    {
                        Debug.LogWarning($"Checksum mismatch for table '{field.Key}'. Remote: {remoteChecksum}, Local: {storedChecksum}");
                        contentToUpdate.Add(field.Key);
                    }
                }

                return contentToUpdate;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error checking content updates: {ex.Message}");
                return contentToUpdate;
            }
        }

        /// <summary>
        /// Fetches content for a specific type and updates local cache.
        /// </summary>
        /// <param name="contentType">The type of content to fetch</param>
        /// <returns>True if successful, false otherwise</returns>
        public async Task<bool> FetchContentByType(string contentType)
        {
            try
            {
                StrixSDKConfig config = StrixSDKConfig.Instance;

                var body = new Dictionary<string, object>
                {
                    {"device", StrixSDK.Strix.ClientID},
                    {"secret", config.apiKey},
                    {"environment", config.environment},
                    {"tableName", contentType}
                };

                string result = await Client.Req(API.UpdateContent, body);
                var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

                if (response == null || !(bool)response["success"])
                {
                    throw new Exception($"Content request failed for {contentType}. Please report to Strix support team.");
                }

                int totalChecksum = 0;
                mediaIDs.Clear();
                Content.ClearContent(contentType);

                bool[] recacheFlags = new bool[7];
                JArray data = response["data"] as JArray;

                if (data != null)
                {
                    foreach (JObject contentItem in data)
                    {
                        totalChecksum += (int)contentItem["checksum"];
                        await ProcessContent(contentType, contentItem, recacheFlags);
                    }
                }
                else
                {
                    // Handle removed content by setting appropriate recache flag
                    SetRecacheFlag(contentType, recacheFlags);
                }

                Content.SaveCacheChecksum(contentType, totalChecksum);
                await UpdateDependentContent(recacheFlags, mediaIDs);

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not fetch content for {contentType}: {ex.Message}");
                return false;
            }
        }

        #region Content Processing Methods

        private async Task ProcessContent(string contentType, JObject contentItem, bool[] recacheFlags)
        {
            switch (contentType)
            {
                case "offers":
                    await ProcessOffersMedia(contentItem);
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[0] = true; // offers
                    break;

                case "entities":
                    await ProcessEntityConfigMedia(contentItem);
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[1] = true; // entities
                    break;

                case "localization":
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[2] = true; // localization
                    break;

                case "stattemplates":
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[3] = true; // stat templates
                    break;

                case "events":
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[4] = true; // game events
                    break;

                case "positionedOffers":
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[5] = true; // positioned offers
                    break;

                case "flows":
                    await ProcessFlowMedia(contentItem);
                    Content.SaveToFile(contentType, contentItem);
                    recacheFlags[6] = true; // flows
                    break;
            }
        }

        private void SetRecacheFlag(string contentType, bool[] recacheFlags)
        {
            switch (contentType)
            {
                case "offers": recacheFlags[0] = true; break;
                case "entities": recacheFlags[1] = true; break;
                case "localization": recacheFlags[2] = true; break;
                case "stattemplates": recacheFlags[3] = true; break;
                case "events": recacheFlags[4] = true; break;
                case "positionedOffers": recacheFlags[5] = true; break;
                case "flows": recacheFlags[6] = true; break;
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private async Task UpdateDependentContent(bool[] recacheFlags, HashSet<string> mediaIDs)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (recacheFlags[0] || recacheFlags[5]) // offers or positioned offers
            {
                Content.RecacheExistingOffers();
                Content.ResolveCachedMedia(mediaIDs.ToList(), "offers");
            }
            if (recacheFlags[1]) // entities
            {
                Content.RecacheExistingEntities();
                Content.RecacheExistingOffers();
                Content.ResolveCachedMedia(mediaIDs.ToList(), "entities");
            }
            if (recacheFlags[2]) // localization
            {
                Content.RecacheExistingOffers();
            }
            if (recacheFlags[3]) // stat templates
            {
                Content.RecacheExistingStatisticsTemplates();
            }
            if (recacheFlags[4]) // game events
            {
                Content.RecacheExistingGameEvents();
                Content.ResolveCachedMedia(mediaIDs.ToList(), "events");
            }
            if (recacheFlags[6]) // flows
            {
                Content.RecacheExistingFlows();
                Content.ResolveCachedMedia(mediaIDs.ToList(), "flows");
            }
        }

        private async Task ProcessFlowMedia(JToken flow)
        {
            var rootNode = flow["nodes"].ToObject<Node>();
            await TraverseFlowNodes(rootNode);
        }

        private async Task TraverseFlowNodes(Node node)
        {
            await ProcessFlowNode(node);

            if (node.Subnodes != null && node.Subnodes.Count > 0)
            {
                foreach (var subnode in node.Subnodes)
                {
                    await TraverseFlowNodes(subnode);
                }
            }
        }

        private async Task ProcessFlowNode(Node flowNode)
        {
            if (flowNode?.Data == null) return;

            foreach (var item in flowNode.Data)
            {
                if (item.Value is string fileUrl && fileUrl.StartsWith("https://storage.googleapis.com/"))
                {
                    await ProcessRemoteFile(fileUrl, "flows", fileName =>
                    {
                        flowNode.Data["value"] = fileName;
                    });
                }
            }
        }

        private async Task ProcessEntityConfigMedia(JToken entity)
        {
            var entityConfig = entity["config"]?.ToObject<RawConfigValue[]>();
            if (entityConfig == null) return;

            foreach (var config in entityConfig)
            {
                await ProcessRawConfigValue(config);
            }
        }

        private async Task ProcessRawConfigValue(RawConfigValue configValue)
        {
            if (configValue == null) return;

            if (IsMediaType(configValue.Type) && configValue.Segments != null)
            {
                foreach (var segment in configValue.Segments)
                {
                    string fileUrl = segment.Value;
                    if (!string.IsNullOrEmpty(fileUrl))
                    {
                        await ProcessRemoteFile(fileUrl, "entities", fileName =>
                        {
                            segment.Value = fileName;
                            mediaIDs.Add(fileName);
                        });
                    }
                }
            }

            if (configValue.Values != null)
            {
                foreach (var subValue in configValue.Values)
                {
                    await ProcessRawConfigValue(subValue);
                }
            }
        }

        private bool IsMediaType(string type)
        {
            return type == "image" || type == "video" || type == "sound" || type == "any file";
        }

        private async Task ProcessOffersMedia(JToken offer)
        {
            var iconUrl = offer["icon"]?.ToString();
            if (!string.IsNullOrEmpty(iconUrl))
            {
                await ProcessRemoteFile(iconUrl, "offers", fileName =>
                {
                    offer["icon"] = fileName;
                });
            }
        }

        private async Task ProcessRemoteFile(string url, string mediaType, Action<string> onSuccess)
        {
            if (string.IsNullOrEmpty(url)) return;

            try
            {
                // Extract filename from URL
                string fileName = Path.GetFileName(url);

                // Check if file already exists in cache
                if (Content.DoesMediaExist(fileName))
                {
                    onSuccess(fileName);
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"File {fileName} already exists in cache, skipping download.");
                    return;
                }

                // Download file
                string base64File = await DownloadFileAsBase64(url);
                if (!string.IsNullOrEmpty(base64File))
                {
                    string hash = Content.CacheMedia(base64File, mediaType);
                    onSuccess(hash);
                    mediaIDs.Add(hash);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error processing remote file {url}: {ex.Message}");
            }
        }

        private async Task<string> DownloadFileAsBase64(string url)
        {
            try
            {
                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var reader = new StreamReader(stream))
                {
                    // Read the entire content of the file which contains the base64 string
                    return await reader.ReadToEndAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error downloading file from {url}: {ex.Message}");
                return null;
            }
        }

        #endregion Content Processing Methods
    }
}