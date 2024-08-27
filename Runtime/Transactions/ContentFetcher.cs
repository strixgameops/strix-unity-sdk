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
using StrixSDK.Editor.Config;
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

        public async void FetchContentByType(string contentType)
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
                        if (data != null)
                        {
                            mediaIDs.Clear();
                            Content.ClearContent(contentType);

                            bool recache_Offers = false;
                            bool recache_Tests = false;
                            bool recache_Localization = false;
                            bool recache_Entities = false;
                            bool recache_StatTemplates = false;
                            bool recache_PositionOffers = false;
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

                                    case "positionedOffers":
                                        Content.SaveToFile(contentType, contentItem);
                                        recache_PositionOffers = true;
                                        break;

                                    default:
                                        break;
                                }
                                totalChecksum += itemChecksum;
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
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not fetch content: {ex.Message}");
            }
        }

        #region Processing methods

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
                                Debug.Log($"File {fileName} already exists in cache, skipping download.");
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
                    Debug.Log($"File {fileName} already exists in cache, skipping download.");
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