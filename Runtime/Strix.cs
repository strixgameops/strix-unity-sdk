using Newtonsoft.Json;
using StrixSDK.Runtime.Config;
using StrixSDK.Runtime;
using StrixSDK.Runtime.Db;
using StrixSDK.Runtime.Models;
using StrixSDK.Runtime.Utils;
using StrixSDK.Runtime.APIClient;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using System.IO;

#if UNITY_EDITOR

using UnityEditor;
using Newtonsoft.Json.Linq;

#endif

namespace StrixSDK
{
    public class Strix : MonoBehaviour
    {
        public static string StrixSDKVersion { get; private set; } = "1.0.0";
        public static string ClientID { get; private set; }
        public static string SessionID { get; private set; }
        public static string BuildVersion { get; private set; }
        public static string ClientCurrency { get; private set; } = "USD";
        public static bool IsInitialized { get; private set; }

        private static Strix _instance;
        private const int RETRY_DELAY_MS = 30000;

        private static readonly List<string> DEFAULT_CONTENT_TYPES = new List<string>
        {
            "entities",
            "localization",
            "stattemplates",
            "offers",
            "positionedOffers",
            "flows",
            "events"
        };

        public static Strix Instance
        {
            get
            {
                if (_instance == null)
                {
                    Utils.StrixDebugLogMessage("Strix instance is null, attempting to find or create it.");
                    _instance = FindObjectOfType<Strix>();
                    if (_instance == null)
                    {
                        Utils.StrixDebugLogMessage("Strix not found in the scene, creating a new instance.");
                        GameObject obj = new GameObject(typeof(Strix).ToString());
                        _instance = obj.AddComponent<Strix>();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }

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
                return;
            }

            _ = Initialize();
        }

        public static async Task<bool> Initialize()
        {
            ClientID = SystemInfo.deviceUniqueIdentifier;
            return await InitializeWithID(ClientID);
        }

        public static async Task<bool> Initialize(string customClientID)
        {
            if (string.IsNullOrEmpty(customClientID))
            {
                Debug.LogError("Cannot initialize Strix SDK with null or empty client ID");
                return false;
            }

            ClientID = customClientID;
            return await InitializeWithID(ClientID);
        }

        private static async Task<bool> InitializeWithID(string clientID)
        {
            IsInitialized = await InitSDK(clientID);
            if (!IsInitialized)
            {
                _ = RetryInitialize(clientID);
            }
            return IsInitialized;
        }

        private static async Task<bool> RetryInitialize(string clientID)
        {
            StrixSDKConfig config = StrixSDKConfig.Instance;
            if (string.IsNullOrEmpty(config.apiKey) || string.IsNullOrEmpty(config.environment))
            {
                Debug.LogError("Cannot retry initialization: API key or environment not set.");
                return false;
            }

            Utils.StrixDebugLogMessage($"Retrying SDK initialization in {RETRY_DELAY_MS / 1000} seconds...");
            await Task.Delay(RETRY_DELAY_MS);
            IsInitialized = await InitSDK(clientID);
            return IsInitialized;
        }

        private static async Task<bool> InitSDK(string clientId)
        {
            Utils.StrixDebugLogMessage("Initializing Strix SDK");

            // Load config and validate
            StrixSDKConfig config = StrixSDKConfig.Instance;
            if (!ValidateConfig(config))
            {
                return false;
            }

            PlayerPrefs.SetString("Strix_SessionStartTime", DateTime.Now.ToString());

            try
            {
                // Check SDK version in editor
#if UNITY_EDITOR
                await CheckSDKVersion();
#endif

                // Create analytics instance first to catch initialization errors
                Analytics analyticsInstance = Analytics.Instance;

                // Generate session ID
                SessionID = Guid.NewGuid().ToString();

                // Call init API
                var playerData = await SendInitializationRequest(clientId, config);
                if (playerData == null)
                {
                    InitializeServicesLocally();
                    return false;
                }

                // Initialize platform-specific services
#if UNITY_ANDROID
                bool transactionsInit = await InitializeAndroidServices(clientId, playerData.FcmData);
                if (!config.fetchUpdatesInRealTime)
                {
                    await FetchContent();
                }
#else
                await FetchContent();
#endif

                // Initialize core services
                var initResults = await InitializeCoreServices(playerData.PlayerData);

                // Check if all initializations succeeded
#if UNITY_ANDROID
                bool success = transactionsInit && AreAllServicesInitialized(initResults);
                if (success)
                {
                    Utils.StrixDebugLogMessage("Strix SDK initialized successfully!");
                    return true;
                }
                else
                {
                    LogInitializationErrors(initResults, transactionsInit);
                    InitializeServicesLocally();
                    return false;
                }
#else
                bool success = AreAllServicesInitialized(initResults);
                if (success)
                {
                    // Send session event
                    await Analytics.SendNewSessionEvent(playerData.IsNewPlayer, null);
                    Utils.StrixDebugLogMessage("Strix SDK initialized successfully!");
                    return true;
                }
                else
                {
                    LogInitializationErrors(initResults);
                    InitializeServicesLocally();
                    return false;
                }
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in InitSDK: {ex.Message}");
                InitializeServicesLocally();
                return false;
            }
        }

        private static bool ValidateConfig(StrixSDKConfig config)
        {
            if (string.IsNullOrEmpty(config.apiKey))
            {
                Debug.LogError("Could not initialize StrixSDK. API key seems to be null! Set it in your 'Project settings -> Strix SDK'");
                return false;
            }
            if (string.IsNullOrEmpty(config.environment))
            {
                Debug.LogError("Could not initialize StrixSDK. Branch seems to be null! Set it in your 'Project settings -> Strix SDK'");
                return false;
            }
            return true;
        }

#if UNITY_EDITOR

        private static async Task CheckSDKVersion()
        {
            GetPackageVersion();
            var checkerBody = new Dictionary<string, object>()
            {
                {"platform", Application.platform.ToString()},
                {"sdkVersion", StrixSDKVersion},
                {"engineVersion", Application.unityVersion},
            };

            var versionCheck = await Client.Req(API.CheckSDK, checkerBody);
            var versionCheckResponse = JsonConvert.DeserializeObject<M_SDKVersionCheckResponse>(versionCheck);
            if (versionCheckResponse.IsGood)
            {
                Utils.StrixDebugLogMessage($"{versionCheckResponse.Message}");
            }
            else
            {
                Debug.LogWarning($"{versionCheckResponse.Message}");
            }
        }

        private static void GetPackageVersion()
        {
            string scriptPath = AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(Instance));
            var currentDirectory = Path.Combine(Path.GetDirectoryName(scriptPath), "../");
            string jsonFilePath = Path.Combine(currentDirectory, "package.json");

            if (File.Exists(jsonFilePath))
            {
                string jsonContent = File.ReadAllText(jsonFilePath);
                JObject jsonObject = JObject.Parse(jsonContent);
                string version = jsonObject["version"].ToString();

                StrixSDKVersion = version;
            }
            else
            {
                Debug.LogError("File not found: " + jsonFilePath);
            }
        }

#endif

        private static async Task<Data> SendInitializationRequest(string clientId, StrixSDKConfig config)
        {
            Utils.StrixDebugLogMessage("Sending initialization request");
            var initBody = new Dictionary<string, object>()
            {
                {"device", clientId},
                {"secret", config.apiKey},
                {"session", SessionID},
                {"environment", config.environment},
            };

            var result = await Client.Req(API.Init, initBody);

            if (!string.IsNullOrEmpty(result))
            {
                var responseObj = JsonConvert.DeserializeObject<M_InitializationResponse>(result);

                // Set client target currency for IAPs
                ClientCurrency = responseObj.Data.Currency;
                BuildVersion = responseObj.Data.Version;

                return responseObj.Data;
            }
            else
            {
                Debug.LogError("Invalid value returned during initialization. Expected non-empty string.");
                return null;
            }
        }

#if UNITY_ANDROID
        private static async Task<bool> InitializeAndroidServices(string clientId, object fcmData)
        {
            return await TransactionsHandler.Instance.Initialize(clientId, fcmData);
        }
#endif

        private static async Task FetchContent()
        {
            Utils.StrixDebugLogMessage("Fetching content for StrixSDK");
            await ContentFetcher.Instance.UpdateContentByTypes(DEFAULT_CONTENT_TYPES);
        }

        private class ServiceInitResult
        {
            public bool PlayerManager { get; set; }
            public bool EntityManager { get; set; }
            public bool OffersManager { get; set; }
            public bool FlowsManager { get; set; }
            public bool GameEventsManager { get; set; }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously

        private static async Task<ServiceInitResult> InitializeCoreServices(PlayerData playerData)
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            var result = new ServiceInitResult();

            // Initialize PlayerManager
            Utils.StrixDebugLogMessage("Starting PlayerManager initialization...");
            PlayerManager playerManagerInstance = PlayerManager.Instance;
            result.PlayerManager = playerManagerInstance.Initialize(playerData);
            Utils.StrixDebugLogMessage("PlayerManager initialization finished.");

            // Initialize EntityManager
            Utils.StrixDebugLogMessage("Starting EntityManager initialization...");
            EntityManager entityManagerInstance = EntityManager.Instance;
            result.EntityManager = entityManagerInstance.Initialize();
            Utils.StrixDebugLogMessage("EntityManager initialization finished.");

            // Initialize OffersManager
            Utils.StrixDebugLogMessage("Starting OffersManager initialization...");
            OffersManager offersManagerInstance = OffersManager.Instance;
            result.OffersManager = offersManagerInstance.Initialize();
            Utils.StrixDebugLogMessage("OffersManager initialization finished.");

            // Initialize FlowsManager
            Utils.StrixDebugLogMessage("Starting FlowsManager initialization...");
            FlowsManager flowsManagerInstance = FlowsManager.Instance;
            result.FlowsManager = flowsManagerInstance.Initialize();
            Utils.StrixDebugLogMessage("FlowsManager initialization finished.");

            // Initialize GameEventsManager
            Utils.StrixDebugLogMessage("Starting GameEventsManager initialization...");
            GameEventsManager eventsManagerInstance = GameEventsManager.Instance;
            result.GameEventsManager = eventsManagerInstance.Initialize();
            Utils.StrixDebugLogMessage("GameEventsManager initialization finished.");

            return result;
        }

        private static bool AreAllServicesInitialized(ServiceInitResult results)
        {
            return results.PlayerManager &&
                   results.EntityManager &&
                   results.OffersManager &&
                   results.FlowsManager &&
                   results.GameEventsManager;
        }

#if UNITY_ANDROID
        private static void LogInitializationErrors(ServiceInitResult results, bool transactionsInit)
        {
            Debug.LogError($"Error while initializing StrixSDK. " +
                           $"\n Initialization results: " +
                           $"OffersManager={results.OffersManager}. " +
                           $"EntityManager={results.EntityManager}. " +
                           $"PlayerManager={results.PlayerManager}. " +
                           $"TransactionsHandler={transactionsInit}. " +
                           $"FlowsManager={results.FlowsManager}. " +
                           $"GameEventsManager={results.GameEventsManager}. " +
                           $" Strix will try to initialize using local configs.");
        }
#else

        private static void LogInitializationErrors(ServiceInitResult results)
        {
            Debug.LogError($"Error while initializing StrixSDK. " +
                           $"\n Initialization results: " +
                           $"OffersManager={results.OffersManager}. " +
                           $"EntityManager={results.EntityManager}. " +
                           $"PlayerManager={results.PlayerManager}. " +
                           $"FlowsManager={results.FlowsManager}. " +
                           $"GameEventsManager={results.GameEventsManager}. " +
                           $" Strix will try to initialize using local configs.");
        }

#endif

        private static void InitializeServicesLocally()
        {
            try
            {
                Utils.StrixDebugLogMessage("Initializing services using local configurations...");

                ServiceInitResult result = InitializeCoreServices(null).Result;

                if (AreAllServicesInitialized(result))
                {
                    Utils.StrixDebugLogMessage("Local initialization completed successfully.");
                }
                else
                {
                    Debug.LogError("Some services failed to initialize locally.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not initialize StrixSDK locally: {ex.Message}");
            }
        }
    }
}