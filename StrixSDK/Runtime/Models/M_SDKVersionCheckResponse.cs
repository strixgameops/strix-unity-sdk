using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class M_SDKVersionCheckResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("isGood")]
        public bool IsGood { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}