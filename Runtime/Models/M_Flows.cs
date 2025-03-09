using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace StrixSDK
{
    public class Flow
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("nodes")]
        public Node Nodes { get; set; }
    }

    public class Node
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("data")]
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        [JsonProperty("subnodes")]
        public List<Node> Subnodes { get; set; } = new List<Node>();
    }

    public class NodeDataValue
    {
        [JsonProperty("value")]
        public object Value { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("isCustom")]
        public bool IsCustom { get; set; }
    }

    public class NodeDataEventCustomData
    {
        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("value")]
        public NodeDataValue Value { get; set; }
    }

    public class NodeDataApplyChangeEventCustomData
    {
        [JsonProperty("field")]
        public string Field { get; set; }

        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("customDataValue")]
        public NodeDataValue Value { get; set; }
    }

    public class NodeDataCases
    {
        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("isDefault")]
        public bool IsDefault { get; set; }
    }

    public class NodeDataSplits
    {
        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("share")]
        public int Share { get; set; }
    }

    public class NodeDataConditions
    {
        [JsonProperty("sid")]
        public string Sid { get; set; }

        [JsonProperty("value1")]
        public NodeDataValue Value1 { get; set; }

        [JsonProperty("value2")]
        public NodeDataValue Value2 { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; }
    }

    [Serializable]
    public class FlowVariableValue
    {
        public string Id { get; set; }
        public object Value { get; set; }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
        {
            { "Id", Id },
            { "Value", Value }
        };
        }
    }
}