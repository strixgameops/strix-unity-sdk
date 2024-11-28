using Newtonsoft.Json;
using StrixSDK.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Video;

namespace StrixSDK.Runtime
{
    public class EntityManager : MonoBehaviour
    {
        private static EntityManager _instance;

        public static EntityManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<EntityManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<EntityManager>();
                        obj.name = typeof(EntityManager).ToString();
                    }
                }
                return _instance;
            }
        }

        // Cache of all entities without configs
        public Entity[] _entities;

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

        public void RefreshEntities()
        {
            List<Entity> entityList = new List<Entity>();
            var entitiesDocs = Content.LoadAllFromFile("entities");

            if (entitiesDocs != null)
            {
                foreach (var doc in entitiesDocs)
                {
                    string json = JsonConvert.SerializeObject(doc);

                    Entity entity = JsonConvert.DeserializeObject<Entity>(json);
                    entityList.Add(entity);
                }

                _entities = entityList.ToArray();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Fetched {_entities.Length} entities");
            }
            else
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Could not fetch entities from persistent storage");
            }
        }

        public bool Initialize()
        {
            try
            {
                RefreshEntities();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not initialize EntityManager. Error: {e}");
                return false;
            }
        }
    }

    public static class EntityHelperMethods
    {
        private static ConfigValue FormatConfigFieldValue(RawConfigValue value, string segmentId, bool autoConvert)
        {
            // Given the segmentID, extracts the value from "segments" array with the corresponding segmentId
            var segment = value.Segments?.FirstOrDefault(s => s.SegmentID == segmentId);
            if (segment == null)
            {
                throw new NullReferenceException($"Value for segmentId '{segmentId}' not found.");
            }

            ConfigValue result = new ConfigValue();
            result.FieldKey = value.ValueID;
            result.Type = value.Type;

            if (segment.Filename != null)
            {
                result.Filename = segment.Filename;
            }

            string segmentValue = segment.Value;

            // Format value in config to the corresponding data type
            switch (value.Type)
            {
                case "string":
                    result.Value = segmentValue;
                    return result;

                case "boolean":
                case "bool":
                    // trying to output boolean
                    if (segmentValue == "false")
                    {
                        result.Value = false;
                        return result;
                    }
                    else if (segmentValue == "true")
                    {
                        result.Value = true;
                        return result;
                    }
                    throw new FormatException($"Invalid boolean value: {segmentValue}");

                case "number":
                    // trying to output number as double
                    if (double.TryParse(segmentValue, out double numberValue))
                    {
                        result.Value = numberValue;
                        return result;
                    }
                    throw new FormatException($"Invalid number value: {segmentValue}");

                case "localized text":
                    result.Value = Localization.GetLocalizedString(result.FieldKey);
                    return result;

                case "image":
                    ConfigValue imageConversion = ReturnWithTexture2D(segmentValue);
                    result.FileExtension = imageConversion.FileExtension;
                    result.Value = imageConversion.Value;
                    return result;

                case "video":
                    ConfigValue videoConversion = ReturnWithVideo(segmentValue);
                    result.FileExtension = videoConversion.FileExtension;
                    result.Value = videoConversion.Value;
                    return result;

                case "sound":
                    var base64 = Content.GetCachedMedia(segmentValue, "entities");
                    string prefix_Sound = "data:audio/";
                    int startIndex_Sound = prefix_Sound.Length;
                    int endIndex_Sound = base64.IndexOf(";base64,");
                    if (endIndex_Sound == -1)
                    {
                        throw new FormatException("Invalid base64 data format.");
                    }

                    string mimeType_Sound = base64.Substring(startIndex_Sound, endIndex_Sound - startIndex_Sound);
                    string fileExtension_Sound = mimeType_Sound.Split('/').Last().ToLower();

                    result.FileExtension = fileExtension_Sound;
                    result.Value = base64;
                    return result;

                case "any file":
                    string base64Prefix = "data:";
                    int startIndex = base64Prefix.Length;
                    int endIndex = segmentValue.IndexOf(";base64,");
                    if (endIndex == -1)
                    {
                        throw new FormatException("Invalid base64 data format.");
                    }

                    // Get type and extension
                    string mimeType = segmentValue.Substring(startIndex, endIndex - startIndex);
                    string fileExtension = mimeType.Split('/').Last();

                    string base64Data = segmentValue.Substring(endIndex + 8);

                    byte[] fileData = Convert.FromBase64String(base64Data);

                    result.FileExtension = fileExtension;
                    result.Value = fileData;
                    return result;

                default:
                    throw new NotSupportedException($"Unsupported type: {value.Type}");
            }
        }

        private static ConfigValue ReturnWithTexture2D(string mediaAddress)
        {
            var base64 = Content.GetCachedMedia(mediaAddress, "entities");

            string base64Prefix = "data:image/";
            int startIndex = base64Prefix.Length;
            int endIndex = base64.IndexOf(";base64,");
            if (endIndex == -1)
            {
                throw new FormatException("Invalid base64 data format.");
            }

            // Get type and extension
            string mimeType = base64.Substring(startIndex, endIndex - startIndex);
            string fileExtension = mimeType.Split('/').Last();

            // Get base64 string
            string base64Data = base64.Substring(endIndex + 8);
            byte[] imageData = Convert.FromBase64String(base64Data);

            ConfigValue result = new ConfigValue();
            result.FileExtension = fileExtension;

            if (fileExtension == "png" || fileExtension == "jpg" || fileExtension == "jpeg")
            {
                // Process PNG or JPEG pictures
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(imageData))
                {
                    result.Value = texture;
                }
                else
                {
                    throw new FormatException("Failed to load image data into Texture2D.");
                }
            }
            else
            {
                throw new FormatException("Unsupported image extension retrieved from config.");
            }

            return result;
        }

        private static ConfigValue ReturnWithVideo(string mediaAddress)
        {
            var base64 = Content.GetCachedMedia(mediaAddress, "entities");

            string prefix_Video = "data:video/";
            int startIndex_Video = prefix_Video.Length;
            int endIndex_Video = base64.IndexOf(";base64,");
            if (endIndex_Video == -1)
            {
                throw new FormatException("Invalid base64 data format.");
            }

            string mimeType_Video = base64.Substring(startIndex_Video, endIndex_Video - startIndex_Video);
            string fileExtension_Video = mimeType_Video.Split('/').Last().ToLower();

            // Check supported formats
            if (fileExtension_Video != "mov" && fileExtension_Video != "mp4" && fileExtension_Video != "avi" && fileExtension_Video != "webm")
            {
                throw new NotSupportedException($"Unsupported video format: {fileExtension_Video}");
            }

            // Get base64 string
            string base64Data_Video = base64.Substring(endIndex_Video + 8);
            byte[] videoData = Convert.FromBase64String(base64Data_Video);

            // Saving video to temp folder
            string tempPath = Path.Combine(Application.temporaryCachePath, $"tempVideo.{fileExtension_Video}");
            File.WriteAllBytes(tempPath, videoData);

            // Upload video to VideoPlayer
            VideoPlayer videoPlayer = new GameObject("VideoPlayer").AddComponent<VideoPlayer>();
            videoPlayer.url = tempPath;
            videoPlayer.Prepare();

            ConfigValue result = new ConfigValue();
            result.FileExtension = fileExtension_Video;
            result.Value = videoPlayer;
            return result;
        }

        public static Entity GetEntityById(string entityId)
        {
            Entity entity = EntityManager.Instance._entities.FirstOrDefault(e => e.Id == entityId);
            if (entity == null)
            {
                Debug.LogError($"GetEntityById: No entity found by id '{entityId}'");
                return null;
            }
            return entity;
        }

        public static string GetEntityIdByNodeId(string nodeId)
        {
            Entity entity = EntityManager.Instance._entities.FirstOrDefault(e => e.NodeId == nodeId);
            if (entity == null)
            {
                Debug.LogError($"GetEntityIdByNodeId: No entity found by node id '{nodeId}'");
                return null;
            }
            return entity.Id;
        }

        public static Entity GetEntityByNodeId(string nodeId)
        {
            Entity entity = EntityManager.Instance._entities.FirstOrDefault(e => e.NodeId == nodeId);
            if (entity == null)
            {
                Debug.LogError($"GetEntityIdByNodeId: No entity found by node id '{nodeId}'");
                return null;
            }
            return entity;
        }

        public static Entity[] GetAllEntities()
        {
            return EntityManager.Instance._entities;
        }

        public static EntityConfig[] GetAllEntityConfigs(string entityId)
        {
            Entity entity = EntityManager.Instance._entities.FirstOrDefault(e => e.Id == entityId);
            if (entity == null)
            {
                Debug.LogError($"GetAllEntityConfigs: No entity found by id '{entityId}'");
                return null;
            }

            List<EntityConfig> configs = EntitiesContent.LoadConfigsFromFile(entity.NodeId).ToList();
            if (configs.Count > 0)
            {
                for (int i = 0; i < configs.Count; i++)
                {
                    configs[i] = FlowsManager.Instance.ExecuteFlow_ConfigParamRetrieved(configs[i]);
                }
            }

            return configs.ToArray();
        }

        public static EntityConfig GetEntityConfig(string entityId, string configId)
        {
            Entity entity = EntityManager.Instance._entities.FirstOrDefault(e => e.Id == entityId);
            if (entity == null)
            {
                Debug.LogError($"GetEntityConfig: No entity found by id '{entityId}'");
                return null;
            }

            EntityConfig config = EntitiesContent.LoadConfigFromFile(entity.NodeId, configId);

            if (config != null)
            {
                config = FlowsManager.Instance.ExecuteFlow_ConfigParamRetrieved(config);
            }

            return config;
        }

        public static Entity[] GetEntityChildren(string entityId)
        {
            // Get target entity by entityId
            Entity parentEntity = EntityManager.Instance._entities.FirstOrDefault(e => e.Id == entityId);
            if (parentEntity == null)
            {
                Debug.LogError($"GetEntityChildren: No entity found by id '{entityId}'");
                return new List<Entity>().ToArray();
            }

            // Get all entities that have "Parent" value equal to the given id
            Entity[] children = EntityManager.Instance._entities.Where(e => e.ParentNodeId == parentEntity.NodeId).ToArray();

            return children;
        }

        public static Entity GetEntityParent(string entityId)
        {
            // Get target entity by entityId
            Entity childEntity = EntityManager.Instance._entities.FirstOrDefault(e => e.Id == entityId);
            if (childEntity == null)
            {
                Debug.LogError($"GetEntityParent: No entity found by id '{entityId}'");
                return null;
            }

            // Get entity that has "NodeId" value equal to the given id
            Entity parent = EntityManager.Instance._entities.FirstOrDefault(e => e.NodeId == childEntity.ParentNodeId);

            return parent;
        }

        public static object GetConfigValue(EntityConfig config, string fieldKey, bool autoConvert)
        {
            RawConfigValue valueConfig = FindConfigValue(config.RawValues, fieldKey);
            if (valueConfig.Values != null)
            {
                // Map type scenario
                List<ConfigValue> formattedValues = new List<ConfigValue>();
                foreach (var value in valueConfig.Values)
                {
                    var firstMatchingSegment = PickAppropriateSegmentedValue(valueConfig);

                    if (firstMatchingSegment != null)
                    {
                        // With AB test
                        formattedValues.Add(FormatConfigFieldValue(value, firstMatchingSegment.SegmentID, autoConvert));
                    }
                    else
                    {
                        // Default
                        formattedValues.Add(FormatConfigFieldValue(value, "everyone", autoConvert));
                    }
                }
                return formattedValues.ToArray();
            }
            else
            {
                // Other value types
                //
                var firstMatchingSegment = PickAppropriateSegmentedValue(valueConfig);

                if (firstMatchingSegment != null)
                {
                    // Default, but with AB test in case
                    return FormatConfigFieldValue(valueConfig, firstMatchingSegment.SegmentID, autoConvert);
                }
                else
                {
                    // Default
                    return FormatConfigFieldValue(valueConfig, "everyone", autoConvert);
                }
            }
        }

        public static RawSegmentValue PickAppropriateSegmentedValue(RawConfigValue valueConfig)
        {
            // Check if value is being AB tested and we're in the test. Pick the first matching segment for now, probably should implement priorities later.
            ABTest[] filteredABTests = PlayerManager.Instance._abTests
                    .Where(test => test.Subject?.Type == "entity")
                    .ToArray();
            var abTestSegmentIds = filteredABTests
                .Select(test => $"abtest_{test.InternalId}")
                .ToList();

            var firstMatchingSegment = valueConfig.Segments?
                .FirstOrDefault(segment => abTestSegmentIds.Contains(segment.SegmentID));

            return firstMatchingSegment;
        }

        private static RawConfigValue FindConfigValue(RawConfigValue[] values, string fieldKey)
        {
            foreach (var valueConfig in values)
            {
                if (valueConfig.ValueID == fieldKey)
                {
                    return valueConfig;
                }

                if (valueConfig.Type == "map" && valueConfig.Values != null)
                {
                    var result = FindConfigValue(valueConfig.Values, fieldKey);
                    if (result != null)
                    {
                        return result;
                    }
                }
            }

            return null;
        }
    }
}