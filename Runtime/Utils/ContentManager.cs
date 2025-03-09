using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using StrixSDK.Runtime.Models;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using System.Text;

namespace StrixSDK.Runtime.Utils
{
    public static class Content
    {
        private static readonly string BaseDirectoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData");
        private static readonly string MediaBasePath = Path.Combine(BaseDirectoryPath, "cached", "media");

        #region Content Recaching Methods

        public static void RecacheExistingStatisticsTemplates()
        {
            try
            {
                var templates = LoadAllFromFile("stattemplates");
                var result = new List<ElementTemplate>();

                foreach (var tData in templates)
                {
                    var json = JsonConvert.SerializeObject(tData);
                    var temp = JsonConvert.DeserializeObject<ElementTemplate>(json);
                    result.Add(temp);
                }
                PlayerManager.Instance._templates = result.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to recache statistics templates: {ex.Message}");
            }
        }

        public static void RecacheExistingOffers()
        {
            try
            {
                OffersManager.Instance.RefreshOffers();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to recache offers: {ex.Message}");
            }
        }

        public static void RecacheExistingFlows()
        {
            try
            {
                FlowsManager.Instance.RefreshFlows();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to recache flows: {ex.Message}");
            }
        }

        public static void RecacheExistingGameEvents()
        {
            try
            {
                GameEventsManager.Instance.RefreshEvents();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to recache game events: {ex.Message}");
            }
        }

        public static void RecacheExistingEntities()
        {
            try
            {
                EntityManager.Instance.RefreshEntities();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to recache entities: {ex.Message}");
            }
        }

        public static void RecacheExistingTests()
        {
            try
            {
                PlayerManager.Instance.RefreshABTests();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to recache AB tests: {ex.Message}");
            }
        }

        #endregion Content Recaching Methods

        #region File Operations

        public static void BulkSaveToFile(string tableName, List<object> documents)
        {
            if (documents == null || documents.Count == 0) return;

            try
            {
                foreach (var document in documents)
                {
                    if (document is JObject jObject)
                    {
                        SaveToFile(tableName, jObject);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to bulk save documents to {tableName}: {ex.Message}");
            }
        }

        public static void SaveToFile(string tableName, JObject document)
        {
            if (document == null || document["id"] == null)
            {
                Debug.LogWarning($"SaveToFile failed. Document is null or missing id field.");
                return;
            }

            try
            {
                string id = document["id"].ToString().Replace("|", "_");
                string directoryPath = GetTableDirectoryPath(tableName);
                EnsureDirectoryExists(directoryPath);

                // Handle entity configs separately
                JArray configs = null;
                if (tableName == "entities" && document["config"] != null)
                {
                    configs = (JArray)document["config"];
                    document.Remove("config");
                }

                // Save main document
                string filePath = Path.Combine(directoryPath, $"{id}.txt");
                SaveJsonToFile(filePath, document);

                // Save config documents if they exist
                if (configs != null)
                {
                    foreach (JObject configItem in configs)
                    {
                        if (configItem["id"] != null)
                        {
                            string configId = configItem["id"].ToString();
                            string configFilePath = Path.Combine(directoryPath, $"{id}_{configId}_conf.txt");
                            SaveJsonToFile(configFilePath, configItem);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save document to {tableName}: {ex.Message}");
            }
        }

        private static void SaveJsonToFile(string filePath, JObject document)
        {
            string jsonContent = JsonConvert.SerializeObject(document, Formatting.Indented);
            string base64Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonContent));
            File.WriteAllText(filePath, base64Content);
        }

        public static string CacheMedia(string base64File, string mediaType)
        {
            if (string.IsNullOrEmpty(base64File)) return null;

            try
            {
                string directoryPath = Path.Combine(MediaBasePath, mediaType);
                EnsureDirectoryExists(directoryPath);

                // Compute the hash of the base64 file content to use as ID
                string id = ComputeHash(base64File).Replace("|", "_");
                string filePath = Path.Combine(directoryPath, $"{id}.txt");

                File.WriteAllText(filePath, base64File);
                return id;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to cache media: {ex.Message}");
                return null;
            }
        }

        public static string GetCachedMedia(string id, string mediaType)
        {
            if (string.IsNullOrEmpty(id)) return null;

            try
            {
                string directoryPath = Path.Combine(MediaBasePath, mediaType);
                string filePath = Path.Combine(directoryPath, $"{id}.txt");

                if (File.Exists(filePath))
                {
                    return File.ReadAllText(filePath);
                }

                Debug.LogWarning($"Media with ID {id} not found at {filePath}");
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get cached media {id}: {ex.Message}");
                return null;
            }
        }

        public static void ResolveCachedMedia(List<string> mediaIDs, string mediaType)
        {
            if (mediaIDs == null) return;

            try
            {
                string directoryPath = Path.Combine(MediaBasePath, mediaType);
                if (!Directory.Exists(directoryPath)) return;

                HashSet<string> validMediaIds = new HashSet<string>(mediaIDs);
                var files = Directory.GetFiles(directoryPath, "*.txt");

                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (!validMediaIds.Contains(fileName))
                    {
                        File.Delete(file);
                        Utils.StrixDebugLogMessage($"Deleted unused cached media file: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to resolve cached media for {mediaType}: {ex.Message}");
            }
        }

        public static void ClearContent(string tableName)
        {
            try
            {
                string directoryPath = GetTableDirectoryPath(tableName);
                if (!Directory.Exists(directoryPath)) return;

                foreach (var file in Directory.GetFiles(directoryPath, "*.txt"))
                {
                    File.Delete(file);
                }

                Utils.StrixDebugLogMessage($"Cleared content for table: {tableName}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to clear content for {tableName}: {ex.Message}");
            }
        }

        public static void SaveCacheChecksum(string tableName, int checksum)
        {
            try
            {
                string directoryPath = Path.Combine(BaseDirectoryPath, "cached");
                EnsureDirectoryExists(directoryPath);

                string filePath = Path.Combine(directoryPath, $"{tableName}-checksum.txt");
                File.WriteAllText(filePath, checksum.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to save cache checksum for {tableName}: {ex.Message}");
            }
        }

        public static int GetCacheChecksum(string tableName)
        {
            try
            {
                string directoryPath = Path.Combine(BaseDirectoryPath, "cached");
                string filePath = Path.Combine(directoryPath, $"{tableName}-checksum.txt");

                if (File.Exists(filePath))
                {
                    string checksumString = File.ReadAllText(filePath);
                    if (int.TryParse(checksumString, out int checksum))
                    {
                        return checksum;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read checksum for {tableName}: {ex.Message}");
            }

            return -1;
        }

        public static bool DoesMediaExist(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;

            try
            {
                // Check all media directories
                string directory = MediaBasePath;
                if (!Directory.Exists(directory)) return false;

                id = id.Replace("|", "_");

                // Check in all media type subdirectories
                foreach (var typeDir in Directory.GetDirectories(directory))
                {
                    string filePath = Path.Combine(typeDir, $"{id}.txt");
                    if (File.Exists(filePath))
                    {
                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error checking if media {id} exists: {ex.Message}");
                return false;
            }
        }

        public static T LoadFromFile<T>(string tableName, string id) where T : class
        {
            if (string.IsNullOrEmpty(id)) return null;

            try
            {
                id = id.Replace("|", "_");
                string filePath = Path.Combine(GetTableDirectoryPath(tableName), $"{id}.txt");

                if (File.Exists(filePath))
                {
                    string base64Content = File.ReadAllText(filePath);
                    string jsonContent = Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                    return JsonConvert.DeserializeObject<T>(jsonContent);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load {tableName}/{id}: {ex.Message}");
            }

            return null;
        }

        public static List<JObject> LoadAllFromFile(string tableName)
        {
            var documents = new List<JObject>();

            try
            {
                string directoryPath = GetTableDirectoryPath(tableName);
                EnsureDirectoryExists(directoryPath);

                foreach (var file in Directory.GetFiles(directoryPath, "*.txt"))
                {
                    // Skip .meta files and _conf entity files
                    if (Path.GetExtension(file) == ".meta" || file.EndsWith("_conf.txt"))
                    {
                        continue;
                    }

                    string base64Content = File.ReadAllText(file);
                    string jsonContent = Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                    var document = JsonConvert.DeserializeObject<JObject>(jsonContent);
                    documents.Add(document);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to load all from {tableName}: {ex.Message}");
            }

            return documents;
        }

        public static void DeleteFile(string tableName, string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            try
            {
                id = id.Replace("|", "_");
                string directoryPath = GetTableDirectoryPath(tableName);

                var filesToDelete = Directory.GetFiles(directoryPath, $"{id}*.txt");
                foreach (string file in filesToDelete)
                {
                    File.Delete(file);
                    Utils.StrixDebugLogMessage($"Deleted file: {file}");
                }

                if (filesToDelete.Length == 0)
                {
                    Debug.LogWarning($"No files found for {tableName}/{id}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete {tableName}/{id}: {ex.Message}");
            }
        }

        #endregion File Operations

        #region Helper Methods

        private static string GetTableDirectoryPath(string tableName)
        {
            return Path.Combine(BaseDirectoryPath, tableName);
        }

        private static void EnsureDirectoryExists(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }

        private static string ComputeHash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] data = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

                var sBuilder = new StringBuilder();
                foreach (var t in data)
                {
                    sBuilder.Append(t.ToString("x2"));
                }

                return sBuilder.ToString();
            }
        }

        #endregion Helper Methods
    }

    public static class AnalyticsCache
    {
        // Manage events cache for failed events we could not send for various reasons.
        public static void CacheFailedEvents(string payload)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData");
                string filePath = Path.Combine(directoryPath, "CachedEvents.txt");

                // Deserialize the payload to a JSON array of objects
                object[] eventsToAdd = JsonConvert.DeserializeObject<object[]>(payload);

                if (File.Exists(filePath))
                {
                    // Read existing file content
                    string existingContent = File.ReadAllText(filePath);

                    // Deserialize existing content as JSON array
                    object[] existingEvents = JsonConvert.DeserializeObject<object[]>(existingContent);

                    // Concatenate existing and new events
                    object[] allEvents = new object[existingEvents.Length + eventsToAdd.Length];
                    existingEvents.CopyTo(allEvents, 0);
                    eventsToAdd.CopyTo(allEvents, existingEvents.Length);

                    // Serialize all events back to JSON string
                    string allEventsJson = JsonConvert.SerializeObject(allEvents, Formatting.Indented);

                    // Write all events back to file
                    File.WriteAllText(filePath, allEventsJson);
                }
                else
                {
                    // Create directory if it doesn't exist
                    Directory.CreateDirectory(directoryPath);

                    // Serialize events to JSON string
                    string eventsJson = JsonConvert.SerializeObject(eventsToAdd, Formatting.Indented);

                    // Write events to file
                    File.WriteAllText(filePath, eventsJson);
                }

                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Events cached successfully to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to cache events: " + ex.Message);
            }
        }

        public static string LoadCachedEvents()
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData");
                string filePath = Path.Combine(directoryPath, "CachedEvents.txt");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (File.Exists(filePath))
                {
                    // Read content from file
                    string fileContent = File.ReadAllText(filePath);
                    return fileContent;
                }
                else
                {
                    Debug.LogWarning($"File 'CachedEvents.txt' does not exist.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load cached events: " + ex.Message);
                return null;
            }
        }

        public static void DeleteCachedEvents()
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData");
                string filePath = Path.Combine(directoryPath, $"CachedEvents.txt");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"File {filePath} deleted successfully.");
                }
                else
                {
                    Debug.LogWarning($"File {filePath} does not exist.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete file 'CachedEvents.txt': {ex.Message}");
            }
        }

        public static bool DoesCacheExists()
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData");
                string filePath = Path.Combine(directoryPath, "CachedEvents.txt");

                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to cache events: " + ex.Message);
                return false;
            }
        }
    }

    public static class EntitiesContent
    {
        public static EntityConfig LoadConfigFromFile(string entityNodeId, string configId)
        {
            try
            {
                // Some ids may have | in their names indicating they are bound to something. Handle those scenarios.
                entityNodeId = entityNodeId.Replace("|", "_");

                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", $"StrixSDK/StrixData/entities");
                string filePath = Path.Combine(directoryPath, $"{entityNodeId}_{configId}_conf.txt");

                if (File.Exists(filePath))
                {
                    string base64Content = File.ReadAllText(filePath);
                    string jsonContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                    var document = JsonConvert.DeserializeObject<EntityConfig>(jsonContent);

                    //StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Data from 'entities' with ID {entityNodeId} loaded from {filePath}");
                    return document;
                }
                else
                {
                    Debug.LogWarning($"File {filePath} does not exist.");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load document from file: " + ex.Message + $" Arguments: {entityNodeId} {configId}");
                return null;
            }
        }

        public static EntityConfig[] LoadConfigsFromFile(string entityNodeId)
        {
            try
            {
                // Some ids may have | in their names indicating they are bound to something. Handle those scenarios.
                entityNodeId = entityNodeId.Replace("|", "_");

                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", $"StrixSDK/StrixData/entities");

                // Get all files in the directory that start with the entityNodeId and end with _conf.txt
                string[] configFiles = Directory.GetFiles(directoryPath, $"{entityNodeId}_*_conf.txt");

                List<EntityConfig> configs = new List<EntityConfig>();

                foreach (string filePath in configFiles)
                {
                    if (File.Exists(filePath))
                    {
                        string base64Content = File.ReadAllText(filePath);
                        string jsonContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                        var config = JsonConvert.DeserializeObject<EntityConfig>(jsonContent);
                        configs.Add(config);

                        //StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Data from 'entities' with ID {entityNodeId} loaded from {filePath}");
                    }
                }

                if (configs.Count == 0)
                {
                    Debug.LogWarning($"No config files found for entityNodeId {entityNodeId}.");
                    return null;
                }

                return configs.ToArray();
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load documents from files: " + ex.Message + $" Arguments: entities {entityNodeId}");
                return null;
            }
        }
    }
}