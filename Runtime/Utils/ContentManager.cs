using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using StrixSDK.Runtime.Models;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;

namespace StrixSDK.Runtime.Utils
{
    public static class Content
    {
        public static void RecacheExistingStatisticsTemplates()
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

        public static void RecacheExistingOffers()
        {
            OffersManager.Instance.RefreshOffers();
        }

        public static void RecacheExistingFlows()
        {
            FlowsManager.Instance.RefreshFlows();
        }

        public static void RecacheExistingEntities()
        {
            EntityManager.Instance.RefreshEntities();
        }

        public static void RecacheExistingTests()
        {
            PlayerManager.Instance.RefreshABTests();
        }

        // Save fetched resources to the persistent storage
        public static void BulkSaveToFile(string tableName, List<object> documents)
        {
            try
            {
                foreach (var document in documents)
                {
                    SaveToFile(tableName, (JObject)document);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to save documents to file: " + ex.Message);
            }
        }

        public static void SaveToFile(string tableName, JObject document)
        {
            try
            {
                if (document == null)
                {
                    Debug.Log($"SaveToFile failed. Document is null.");
                }

                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", $"StrixSDK/StrixData/{tableName}");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Ensure the document has an 'id' field
                if (document != null && document["id"] != null)
                {
                    string id = document["id"].ToString();

                    // Some ids may have | in their names indicating they are bound to something. Handle those scenarios.
                    id = id.Replace("|", "_");

                    // Remove the "config" field from the main document
                    JArray configs = null;
                    if (tableName == "entities" && document["config"] != null)
                    {
                        configs = (JArray)document["config"];
                        document.Remove("config");
                    }

                    string filePath = Path.Combine(directoryPath, $"{id}.txt");
                    string jsonContent = JsonConvert.SerializeObject(document, Formatting.Indented);
                    string base64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(jsonContent));
                    File.WriteAllText(filePath, base64Content);

                    //Debug.Log($"Data from {tableName} with ID {id} saved to {filePath}");

                    // Save the config documents separately if it exists
                    if (configs != null)
                    {
                        foreach (JObject configItem in configs)
                        {
                            if (configItem["id"] != null)
                            {
                                string configId = configItem["id"].ToString();
                                string configFilePath = Path.Combine(directoryPath, $"{id}_{configId}_conf.txt");

                                string configJsonContent = JsonConvert.SerializeObject(configItem, Formatting.Indented);
                                string configBase64Content = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(configJsonContent));
                                File.WriteAllText(configFilePath, configBase64Content);

                                //Debug.Log($"Config data from {tableName} with ID {id} and config ID {configId} saved to {configFilePath}");
                            }
                            else
                            {
                                Debug.LogWarning($"Config item does not have an 'id' field: {JsonConvert.SerializeObject(configItem)}");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"Document is null or does not have an 'id' field: {JsonConvert.SerializeObject(document)}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to save documents to file: " + ex.Message + $" Arguments: {tableName} {document}");
            }
        }

        public static string CacheMedia(string base64File, string mediaType)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", "cached", "media", mediaType);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                // Compute the hash of the base64 file content to use as ID
                string id = ComputeHash(base64File);

                // Handle cases where the hash contains invalid path characters
                id = id.Replace("|", "_");

                string filePath = Path.Combine(directoryPath, $"{id}.txt");
                File.WriteAllText(filePath, base64File);

                //Debug.Log($"Media with ID {id} saved to {filePath}");

                return id;
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to cache media to file: " + ex.Message);
                return null;
            }
        }

        public static string GetCachedMedia(string id, string mediaType)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", "cached", "media", mediaType);
                string filePath = Path.Combine(directoryPath, $"{id}.txt");

                if (File.Exists(filePath))
                {
                    string base64Content = File.ReadAllText(filePath);
                    //Debug.Log($"Media with ID {id} found at {filePath}");
                    return base64Content;
                }
                else
                {
                    Debug.LogWarning($"Media with ID {id} not found at {filePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to get cached media with ID {id}: {ex.Message}");
                return null;
            }
        }

        public static void ResolveCachedMedia(List<string> mediaIDs, string mediaType)
        {
            // Remove media files we should not cache anymore
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", "cached", "media", mediaType);

                if (Directory.Exists(directoryPath))
                {
                    var files = Directory.GetFiles(directoryPath, "*.txt");

                    foreach (var file in files)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(file);

                        if (!mediaIDs.Contains(fileName))
                        {
                            File.Delete(file);
                            Debug.Log($"Deleted excess cached media file: {file}");
                        }
                    }

                    Debug.Log($"ResolveCachedMedia completed for media type: {mediaType}");
                }
                else
                {
                    Debug.Log($"No need to resolve cached media. Directory not found: {directoryPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to resolve cached media for media type {mediaType}: {ex.Message}");
            }
        }

        public static void ClearContent(string tableName)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", tableName);

                if (Directory.Exists(directoryPath))
                {
                    var files = Directory.GetFiles(directoryPath, "*.txt");

                    foreach (var file in files)
                    {
                        File.Delete(file);
                    }

                    Debug.Log($"ClearContent completed for table: {tableName}");
                }
                else
                {
                    Debug.Log($"Directory not found: {directoryPath}");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to clear content for table {tableName}: {ex.Message}");
            }
        }

        public static void SaveCacheChecksum(string tableName, int checksum)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", "cached");
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string filePath = Path.Combine(directoryPath, $"{tableName}-checksum.txt");
                File.WriteAllText(filePath, checksum.ToString());

                //Debug.Log($"Media with ID {tableName}-checksum.txt saved to {filePath}");
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to cache media to file: " + ex.Message);
            }
        }

        public static int GetCacheChecksum(string tableName)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", "cached");
                string filePath = Path.Combine(directoryPath, $"{tableName}-checksum.txt");

                if (File.Exists(filePath))
                {
                    string checksumString = File.ReadAllText(filePath);
                    if (int.TryParse(checksumString, out int checksum))
                    {
                        return checksum;
                    }
                    else
                    {
                        Debug.LogError($"Failed to parse checksum from file {filePath}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Checksum file {filePath} does not exist");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to read checksum from file: {ex.Message}");
            }

            return -1;
        }

        public static bool DoesMediaExist(string id)
        {
            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", "StrixSDK", "StrixData", "cached", "media");
                string filePath = Path.Combine(directoryPath, $"{id.Replace("|", "_")}.txt");

                return File.Exists(filePath);
            }
            catch (Exception ex)
            {
                Debug.LogError("Error checking if media exists: " + ex.Message);
                return false;
            }
        }

        private static string ComputeHash(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // Compute hash as byte array
                byte[] data = sha256Hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(input));

                // Convert byte array to a string
                var sBuilder = new System.Text.StringBuilder();
                foreach (var t in data)
                {
                    sBuilder.Append(t.ToString("x2"));
                }

                return sBuilder.ToString();
            }
        }

        public static T LoadFromFile<T>(string tableName, string id) where T : class
        {
            try
            {
                // Some ids may have | in their names indicating they are bound to something. Handle those scenarios.
                id = id.Replace("|", "_");

                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", $"StrixSDK/StrixData/{tableName}");
                string filePath = Path.Combine(directoryPath, $"{id}.txt");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (File.Exists(filePath))
                {
                    string base64Content = File.ReadAllText(filePath);
                    string jsonContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                    T document = JsonConvert.DeserializeObject<T>(jsonContent);

                    //Debug.Log($"Data from {tableName} with ID {id} loaded from {filePath}");
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
                Debug.LogError("Failed to load document from file: " + ex.Message + $" Arguments: {tableName} {id}");
                return null;
            }
        }

        public static List<JObject> LoadAllFromFile(string tableName)
        {
            var documents = new List<JObject>();

            try
            {
                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", $"StrixSDK/StrixData/{tableName}");

                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                if (Directory.Exists(directoryPath))
                {
                    var files = Directory.GetFiles(directoryPath);
                    foreach (var file in files)
                    {
                        // Skip .meta files and _conf entity files
                        if (Path.GetExtension(file) == ".meta" || file.EndsWith("_conf.txt"))
                        {
                            continue;
                        }

                        string base64Content = File.ReadAllText(file);
                        string jsonContent = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64Content));
                        var document = JsonConvert.DeserializeObject<JObject>(jsonContent);
                        documents.Add(document);

                        //Debug.Log($"Data from {tableName} loaded from {file}");
                    }
                }
                else
                {
                    Debug.LogWarning($"Directory {directoryPath} does not exist.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("Failed to load documents from files: " + ex.Message + $" Arguments: {tableName}");
            }

            return documents;
        }

        public static void DeleteFile(string tableName, string id)
        {
            try
            {
                // Some ids may have | in their names indicating they are bound to something. Handle those scenarios.
                id = id.Replace("|", "_");

                string directoryPath = Path.Combine(Application.persistentDataPath, "Plugins", $"StrixSDK/StrixData/{tableName}");

                // Get all files in the directory that start with the id and have a .txt extension
                string[] files = Directory.GetFiles(directoryPath, $"{id}*.txt");

                // Check if any files were found
                if (files.Length > 0)
                {
                    foreach (string file in files)
                    {
                        if (File.Exists(file))
                        {
                            File.Delete(file);
                            Debug.Log($"File {file} deleted successfully.");
                        }
                    }
                }
                else
                {
                    Debug.LogWarning($"No files found with ID {id} in table {tableName}.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to delete files with ID {id}: {ex.Message} Arguments: {tableName} {id}");
            }
        }
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

                Debug.Log($"Events cached successfully to {filePath}");
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
                    Debug.Log($"File {filePath} deleted successfully.");
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

                    //Debug.Log($"Data from 'entities' with ID {entityNodeId} loaded from {filePath}");
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

                        //Debug.Log($"Data from 'entities' with ID {entityNodeId} loaded from {filePath}");
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