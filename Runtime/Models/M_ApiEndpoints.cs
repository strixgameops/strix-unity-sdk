using System.Web;

namespace StrixSDK.Runtime.Models
{
    public static class API
    {
        private const string server = "https://tool.strixgameops.com";

        public const string SendEvent = server + "/sdk/api/v1/analytics/sendEvent";
        public const string DesignEvent = server + "/sdk/api/v1/analytics/designEvent";

        public const string GetElementValue = server + "/sdk/api/v1/getElementValue";
        public const string SetElementValue = server + "/sdk/api/v1/setValueToStatisticElement";
        public const string AddElementValue = server + "/sdk/api/v1/addValueToStatisticElement";
        public const string SubtractElementValue = server + "/sdk/api/v1/subtractValueFromStatisticElement";
        public const string SetOfferExpiration = server + "/sdk/api/v1/setOfferExpiration";
        public const string ValidateReceipt = server + "/sdk/api/validateReceipt";

        public const string RegisterFCMToken = server + "/sdk/api/v1/regToken";

        public const string UpdateContent = server + "/sdk/api/v1/clientUpdate";

        public const string CheckSDK = server + "/sdk/api/sdkCheck";

        public const string HealthCheck = server + "/sdk/api/health";
    }
}