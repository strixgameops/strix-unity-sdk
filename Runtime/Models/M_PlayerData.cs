using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class PlayerData
    {
        [JsonProperty("segments")]
        public List<string> Segments { get; set; }

        [JsonProperty("elements")]
        public List<PlayerDataElement> Elements { get; set; }

        [JsonProperty("abtests")]
        public List<string> ABTests { get; set; }

        [JsonProperty("inventory")]
        public List<string> Inventory { get; set; } // Not implemented

        [JsonProperty("offers")]
        public List<PlayerOfferData> Offers { get; set; }
    }

    public class PlayerOfferData // Used only for real-money IAPs
    {
        [JsonProperty("offerID")]
        public string Id { get; set; }

        [JsonProperty("purchasedTimes")]
        public int PurchasedTimes { get; set; }

        [JsonProperty("currentAmount")]
        public int CurrentAmount { get; set; }

        [JsonProperty("expiration")]
        public string ExpirationDate { get; set; }
    }

    public class PlayerDataElement
    {
        [JsonProperty("elementID")]
        public string Id { get; set; }

        [JsonProperty("elementValue")]
        public object Value { get; set; }
    }

    public class ElementTemplate
    {
        [JsonProperty("id")]
        public string InternalId { get; set; }

        [JsonProperty("codename")]
        public string Id { get; set; }

        [JsonProperty("defaultValue")]
        public object DefaultValue { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("rangeMin")]
        public object RangeMin { get; set; }

        [JsonProperty("rangeMax")]
        public object RangeMax { get; set; }
    }
}