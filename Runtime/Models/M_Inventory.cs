using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class InventoryItem
    {
        [JsonProperty("nodeID")]
        public string NodeId { get; set; }

        public string EntityId { get; set; }

        [JsonProperty("quantity")]
        public string Quantity { get; set; }

        [JsonProperty("slot")]
        public int Slot { get; set; }
    }
}