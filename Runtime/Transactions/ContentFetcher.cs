using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using StrixSDK.Runtime.Utils;
using StrixSDK.Runtime.Models;
using StrixSDK.Runtime.Config;
using System.Net.Http;

using StrixSDK.Runtime.APIClient;

namespace StrixSDK.Runtime.Db
{
    public class ContentFetcher : MonoBehaviour
    {
        private static CancellationTokenSource cts = new CancellationTokenSource();

        private static ContentFetcher _instance;

        public static ContentFetcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ContentFetcher>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<ContentFetcher>();
                        obj.name = typeof(ContentFetcher).ToString();
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

        private static List<string> mediaIDs = new List<string>();

        /// <summary>
        /// Makes a checksum checkup for specified content types. If server and local checksums differ, initiate fetch for such content. Updates only mismatched content.
        /// </summary>
        /// <param name="contentTypes"></param>
        /// <returns>True if operation is successful and something was updated. False if no update was done for some reason.</returns>
        public async Task<bool> UpdateContentByTypes(List<string> contentTypes)
        {
            var typesToFetch = await ChecksumCheckup(contentTypes);
            if (typesToFetch != null && typesToFetch.Count > 0)
            {
                var fetchContent = new List<Task>();
                foreach (string type in typesToFetch)
                {
                    fetchContent.Add(FetchContentByType(type));
                }
                if (fetchContent.Count > 0)
                {
                    await Task.WhenAll(fetchContent);
                }
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Takes content types we want to get checksum for.
        /// </summary>
        /// <param name="contentTypes"></param>
        /// <returns>A list of content types thas has checksum mismatch and need to be updated.</returns>
        public async Task<List<string>> ChecksumCheckup(List<string> contentTypes)
        {
            var contentToUpdate = new List<string>();
            try
            {
                // Loading config file
                StrixSDKConfig config = StrixSDKConfig.Instance;

                var buildType = config.branch;

                var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
                if (string.IsNullOrEmpty(clientID))
                {
                    Debug.LogError("Error while making Strix's checksum checkup: client ID or Session ID is invalid.");
                    return new List<string>();
                }

                var checkupBody = new Dictionary<string, object>()
                {
                    {"device", clientID},
                    {"secret", config.apiKey},
                    {"build", buildType},
                    {"tableNames", contentTypes}
                };
                var result = await Client.Req(API.ChecksumCheckup, checkupBody);
                var doc = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
                if (doc != null)
                {
                    if ((bool)doc["success"])
                    {
                        var checksums = JsonConvert.DeserializeObject<Dictionary<string, object>>(doc["data"].ToString());

                        // Iterate through each field in the data
                        foreach (var field in checksums)
                        {
                            // Compare the checksum in data with storedChecksum
                            int remoteChecksum = Convert.ToInt32(field.Value);
                            int storedChecksum = Content.GetCacheChecksum(field.Key);

                            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Comparing checksums for '{field.Key}'. Remote: {remoteChecksum}, Local: {storedChecksum}");

                            if (remoteChecksum != storedChecksum || storedChecksum == -1)
                            {
                                Debug.LogWarning($"Checksum mismatch for table '{field.Key}'. Remote: {remoteChecksum}, Local: {storedChecksum}");
                                contentToUpdate.Add(field.Key);
                            }
                            else
                            {
                                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Table '{field.Key}' is up to date!");
                            }
                        }
                    }
                }
                return contentToUpdate;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while checking for content updates for type '{contentTypes}': {ex.Message}");
                return contentToUpdate;
            }
        }

        public async Task<bool> FetchContentByType(string contentType)
        {
            try
            {
                StrixSDKConfig config = StrixSDKConfig.Instance;
                var buildType = config.branch;

                var body = new Dictionary<string, object>()
                {
                    {"device", StrixSDK.Strix.clientID},
                    {"secret", config.apiKey},
                    {"build", buildType},
                    {"tableName", contentType},
                };

                var result = await Client.Req(API.UpdateContent, body);

                var doc = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
                if (doc != null)
                {
                    int totalChecksum = 0;

                    if ((bool)doc["success"])
                    {
                        var data = doc["data"] as JArray;

                        mediaIDs.Clear();
                        Content.ClearContent(contentType); // <- if data will be null, we just clear the content. If not, we'll place a new content

                        bool recache_Offers = false;
                        bool recache_Tests = false;
                        bool recache_Localization = false;
                        bool recache_Entities = false;
                        bool recache_StatTemplates = false;
                        bool recache_PositionOffers = false;
                        bool recache_Flows = false;
                        bool recache_GameEvents = false;

                        if (data != null)
                        {
                            // If Data isnt null, update the content and set dependent content to refresh
                            foreach (JObject contentItem in data)
                            {
                                int itemChecksum = (int)contentItem["checksum"];

                                switch (contentType)
                                {
                                    case "offers":
                                        await ProcessOffersMedia(contentItem);
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_Offers = true;
                                        break;

                                    case "entities":
                                        await ProcessEntityConfigMedia(contentItem);
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_Entities = true;
                                        break;

                                    case "abtests":
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_Tests = true;
                                        break;

                                    case "localization":
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_Localization = true;
                                        break;

                                    case "stattemplates":
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_StatTemplates = true;
                                        break;

                                    case "events":
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_GameEvents = true;
                                        break;

                                    case "positionedOffers":
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_PositionOffers = true;
                                        break;

                                    case "flows":
                                        await ProcessFlowMedia(contentItem);
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_Flows = true;
                                        break;

                                    default:
                                        break;
                                }
                                totalChecksum += itemChecksum;
                            }
                        }
                        else
                        {
                            // If data is null, we just remove all the content of this type (a bit earlier), and just recache to sync
                            switch (contentType)
                            {
                                case "offers":
                                    recache_Offers = true;
                                    break;

                                case "entities":
                                    recache_Entities = true;
                                    break;

                                case "abtests":
                                    recache_Tests = true;
                                    break;

                                case "localization":
                                    recache_Localization = true;
                                    break;

                                case "stattemplates":
                                    recache_StatTemplates = true;
                                    break;

                                case "events":
                                    recache_GameEvents = true;
                                    break;

                                case "positionedOffers":
                                    recache_PositionOffers = true;
                                    break;

                                case "flows":
                                    recache_Flows = true;
                                    break;

                                default:
                                    break;
                            }
                        }

                        Content.SaveCacheChecksum(contentType, totalChecksum);

                        if (recache_Offers || recache_PositionOffers)
                        {
                            Content.RecacheExistingOffers();
                            Content.ResolveCachedMedia(mediaIDs, "offers");
                        }
                        if (recache_Tests)
                        {
                            Content.RecacheExistingTests();
                        }
                        if (recache_Localization)
                        {
                            // No implementation for localization because there is no cache yet that we need to reload
                            Content.RecacheExistingOffers(); // But we still need to refresh offers in case some loc changed for any offer
                        }
                        if (recache_Entities)
                        {
                            Content.RecacheExistingEntities();
                            Content.RecacheExistingOffers();
                            Content.ResolveCachedMedia(mediaIDs, "entities");
                        }
                        if (recache_StatTemplates)
                        {
                            Content.RecacheExistingStatisticsTemplates();
                        }
                        if (recache_Flows)
                        {
                            Content.RecacheExistingFlows();
                            Content.ResolveCachedMedia(mediaIDs, "flows");
                        }
                        if (recache_GameEvents)
                        {
                            Content.RecacheExistingGameEvents();
                            Content.ResolveCachedMedia(mediaIDs, "events");
                        }
                    }
                    else
                    {
                        throw new Exception("Content request returned 'false' in 'success' field! Please report to Strix support team.");
                    }
                }
                else
                {
                    throw new Exception("Content request returned as null! Please report to Strix support team.");
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not fetch content: {ex.Message}");
                return false;
            }
        }

        #region Processing methods

        private async Task ProcessFlowMedia(JToken flow)
        {
            var rootNode = flow["nodes"].ToObject<Node>();
            await TraverseFlowNodes(rootNode);
        }

        private async Task TraverseFlowNodes(Node node)
        {
            await ProcessFlowNode(node);
            if (node.Subnodes.Count > 0)
            {
                foreach (var n in node.Subnodes)
                {
                    await TraverseFlowNodes(n);
                }
            }
        }

        private async Task ProcessFlowNode(Node flowNode)
        {
            foreach (var item in flowNode.Data)
            {
                if (item.Value.GetType() == typeof(string) && item.Value.ToString().StartsWith("https://storage.googleapis.com/"))
                {
                    var fileUrl = (string)item.Value;
                    if (!string.IsNullOrEmpty(fileUrl))
                    {
                        // Extract the file name from the URL
                        var fileName = Path.GetFileName(fileUrl);

                        // Check if the file already exists in the cache
                        if (Content.DoesMediaExist(fileName))
                        {
                            // If the file exists, update the segment value to the hash directly
                            flowNode.Data["value"] = fileName;
                            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"File {fileName} already exists in cache, skipping download.");
                        }
                        else
                        {
                            var base64File = await DownloadFileAsBase64(fileUrl);
                            if (!string.IsNullOrEmpty(base64File))
                            {
                                var hash = Content.CacheMedia(base64File, "flows");
                                flowNode.Data["value"] = hash;
                                mediaIDs.Add(hash);
                            }
                        }
                    }
                }
            }
        }

        private async Task ProcessEntityConfigMedia(JToken entity)
        {
            var entityConfig = entity["config"].ToObject<RawConfigValue[]>();
            foreach (var config in entityConfig)
            {
                await ProcessRawConfigValue(config);
            }
        }

        private async Task ProcessRawConfigValue(RawConfigValue configValue)
        {
            if (configValue.Type == "image" || configValue.Type == "video" || configValue.Type == "sound" || configValue.Type == "any file")
            {
                if (configValue.Segments != null)
                {
                    foreach (var segment in configValue.Segments)
                    {
                        var fileUrl = segment.Value;
                        if (!string.IsNullOrEmpty(fileUrl))
                        {
                            // Extract the file name from the URL
                            var fileName = Path.GetFileName(fileUrl);

                            // Check if the file already exists in the cache
                            if (Content.DoesMediaExist(fileName))
                            {
                                // If the file exists, update the segment value to the hash directly
                                segment.Value = fileName;
                                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"File {fileName} already exists in cache, skipping download.");
                            }
                            else
                            {
                                var base64File = await DownloadFileAsBase64(fileUrl);
                                if (!string.IsNullOrEmpty(base64File))
                                {
                                    var hash = Content.CacheMedia(base64File, "entities");
                                    segment.Value = hash;
                                    mediaIDs.Add(hash);
                                }
                            }
                        }
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

        private async Task ProcessOffersMedia(JToken offer)
        {
            var iconUrl = offer["icon"]?.ToString();
            if (!string.IsNullOrEmpty(iconUrl))
            {
                // Extract the file name from the URL
                var fileName = Path.GetFileName(iconUrl);

                // Check if the file already exists in the cache
                if (Content.DoesMediaExist(fileName))
                {
                    // If the file exists, update the offer icon to the hash directly
                    offer["icon"] = fileName;
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"File {fileName} already exists in cache, skipping download.");
                }
                else
                {
                    var base64Image = await DownloadFileAsBase64(iconUrl);
                    if (!string.IsNullOrEmpty(base64Image))
                    {
                        var hash = Content.CacheMedia(base64Image, "offers");
                        offer["icon"] = hash;
                        mediaIDs.Add(hash);
                    }
                }
            }
        }

        private async Task<string> DownloadFileAsBase64(string url)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    var response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            // Read the entire content of the .txt file which contains the base64 string
                            string base64String = await reader.ReadToEndAsync();
                            return base64String;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error downloading file from {url}: {ex.Message}");
                    return null;
                }
            }
        }

        #endregion Processing methods
    }
}