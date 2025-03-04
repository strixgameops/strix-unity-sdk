namespace StrixSDK.Runtime.Models
{
    public static class API
    {
        private const string server = "https://tool.strixgameops.com";

        // For development
        //private const string server = "http://localhost:3005";

        // Session manage
        public const string Init = server + "/sdk/api/deployment/v1/init";

        // Analytics
        public const string SendEvent = server + "/sdk/api/analytics/v1/sendEvent";

        // Player Warehouse
        public const string GetElementValue = server + "/sdk/api/liveservices/v1/getElementValue";

        public const string SetElementValue = server + "/sdk/api/liveservices/v1/setValueToStatisticElement";
        public const string AddElementValue = server + "/sdk/api/liveservices/v1/addValueToStatisticElement";
        public const string SubtractElementValue = server + "/sdk/api/liveservices/v1/subtractValueFromStatisticElement";
        public const string SetOfferExpiration = server + "/sdk/api/liveservices/v1/setOfferExpiration";
        public const string ValidateReceipt = server + "/sdk/api/analytics/validateReceipt";

        // Leaderboards
        public const string GetLeaderboard = server + "/sdk/api/liveservices/v1/getLeaderboard";

        // Inventory
        public const string GetInventoryItems = server + "/sdk/api/liveservices/v1/getInventoryItems";

        public const string GetInventoryItemAmount = server + "/sdk/api/liveservices/v1/getInventoryItemAmount";
        public const string AddInventoryItem = server + "/sdk/api/liveservices/v1/addInventoryItem";
        public const string RemoveInventoryItem = server + "/sdk/api/liveservices/v1/removeInventoryItem";

        // Transactions and communications
        public const string RegisterFCMToken = server + "/sdk/api/deployment/v1/regToken";

        public const string UpdateContent = server + "/sdk/api/deployment/v1/clientUpdate";
        public const string ChecksumCheckup = server + "/sdk/api/deployment/v1/checksumCheckup";
        public const string CheckSDK = server + "/sdk/api/deployment/sdkCheck";
        public const string HealthCheck = server + "/sdk/api/liveservices/health";
        public const string BackendAction = server + "/sdk/api/liveservices/v1/backendAction";
    }
}