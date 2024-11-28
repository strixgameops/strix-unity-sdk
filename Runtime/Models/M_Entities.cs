using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class Entity
    {
        [JsonProperty("entityID")]
        public string Id { get; set; }

        [JsonProperty("id")]
        public string NodeId { get; set; }

        [JsonProperty("inheritedCategories")]
        public string[] InheritedCategories { get; set; }

        [JsonProperty("isCurrency")]
        public bool IsCurrency { get; set; }

        [JsonProperty("isInAppPurchase")]
        public bool IsInAppPurchase { get; set; }

        [JsonProperty("parent")]
        public string ParentNodeId { get; set; }
    }

    public class EntityConfig
    {
        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("values")]
        public RawConfigValue[] RawValues { get; set; } // For internal use. Stores all values of a config. Used to construct a personalized ready-to-use config when fetched
    }

    public class RawConfigValue
    {
        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("valueID")]
        public string ValueID { get; set; }

        // May only exist in any value type except for "map"
        [JsonProperty("segments")]
        public RawSegmentValue[] Segments { get; set; }

        // May only exist in "map" values
        [JsonProperty("values")]
        public RawConfigValue[] Values { get; set; }
    }

    public class RawSegmentValue
    {
        [JsonProperty("segmentID")]
        public string SegmentID { get; set; }

        [JsonProperty("value")]
        public string Value { get; set; }

        // May only exist for file-types values
        [JsonProperty("valueFileName")]
        public string Filename { get; set; }
    }

    public class ConfigValue
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("valueID")]
        public string FieldKey { get; set; }

        [JsonProperty("value")]
        public object Value { get; set; }

        // May only exist for file-types values
        [JsonProperty("valueFileName")]
        public object Filename { get; set; }

        // May only exist for file-types values. Calculated client-side from base64 string
        public string FileExtension { get; set; }
    }
}