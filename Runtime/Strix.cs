using Newtonsoft.Json;
using StrixSDK.Runtime.Config;
using StrixSDK.Runtime;
using StrixSDK.Runtime.Db;
using StrixSDK.Runtime.Models;
using StrixSDK.Runtime.Utils;
using StrixSDK.Runtime.APIClient;
using System;
using System.Collections.Generic;
using System.Linq;
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
        public static string StrixSDKVersion = "1.0.0";

        private static Strix _instance;

        public static Strix Instance
        {
            get
            {
                if (_instance == null)
                {
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Strix instance is null, attempting to find or create it.");
                    _instance = FindObjectOfType<Strix>();
                    if (_instance == null)
                    {
                        StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Strix not found in the scene, creating a new instance.");
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Strix>();
                        obj.name = typeof(Strix).ToString();
                    }
                }
                return _instance;
            }
        }

        private OffersManager offersManagerInstance;
        private PlayerManager playerManagerInstance;
        public static string clientID;

        public static bool IsInitialized = false;

        private static void GetPackageVersion()
        {
#if UNITY_EDITOR
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
#endif
        }

        public static async Task<bool> Initialize()
        {
            PlayerPrefs.SetString("Strix_ClientID", SystemInfo.deviceUniqueIdentifier);
            clientID = SystemInfo.deviceUniqueIdentifier;
            IsInitialized = await InitSDK(clientID);
            if (!IsInitialized)
            {
                _ = RetryInitialize(clientID);
            }
            return IsInitialized;
        }

        public static async Task<bool> Initialize(string customClientID)
        {
            PlayerPrefs.SetString("Strix_ClientID", customClientID);
            clientID = customClientID;
            IsInitialized = await InitSDK(clientID);
            if (!IsInitialized)
            {
                _ = RetryInitialize(customClientID);
            }
            return IsInitialized;
        }

        private static async Task<bool> RetryInitialize(string clientID)
        {
            // We don't even retry if config is not set
            StrixSDKConfig config = StrixSDKConfig.Instance;
            if (string.IsNullOrEmpty(config.apiKey))
            {
                return false;
            }
            if (string.IsNullOrEmpty(config.branch))
            {
                return false;
            }

            await Task.Delay(30000);
            IsInitialized = await InitSDK(clientID);
            return IsInitialized;
        }

        private static async Task<bool> InitSDK(string clientId)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Calling InitSDK");
            // Loading config file
            StrixSDKConfig config = StrixSDKConfig.Instance;
            if (string.IsNullOrEmpty(config.apiKey))
            {
                Debug.LogError($"Could not initialize StrixSDK. API key seems to be null! Set it in your 'Project settings -> Strix SDK'");
                return false;
            }
            if (string.IsNullOrEmpty(config.branch))
            {
                Debug.LogError($"Could not initialize StrixSDK. Branch seems to be null! Set it in your 'Project settings -> Strix SDK'");
                return false;
            }

            PlayerPrefs.SetString("Strix_SessionStartTime", DateTime.Now.ToString());
            try
            {
#if UNITY_EDITOR
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
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"{versionCheckResponse.Message}");
                }
                else
                {
                    Debug.LogWarning($"{versionCheckResponse.Message}");
                }
#endif

                // Make analytics instance. It will listen for Unity logs and report any errors. Make it before all calls to catch any errors.
                Analytics analyticsInstance = Analytics.Instance;

                string sessionID = Guid.NewGuid().ToString();
                PlayerPrefs.SetString("Strix_SessionID", sessionID);
                PlayerPrefs.Save();

                // We need to send initializating request to get player data and proceed with analytics events
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Sending init req.");
                var initBody = new Dictionary<string, object>()
                {
                    {"device", clientId},
                    {"secret", config.apiKey},
                    {"session", sessionID},
                    {"build", config.branch},
                };
                var result = await Client.Req(API.Init, initBody);

                if (!string.IsNullOrEmpty(result))
                {
                    // If the key was fetched, proceed with initialization
                    var responseObj = JsonConvert.DeserializeObject<M_InitializationResponse>(result);

                    // Session event
                    await Analytics.SendNewSessionEvent(responseObj.Data.IsNewPlayer, null);

                    // Set client key which is used for DB access & identifies target currency for IAPs
                    PlayerPrefs.SetString("Strix_ClientKey", responseObj.Data.Key);
                    PlayerPrefs.SetString("Strix_ClientCurrency", responseObj.Data.Currency);
                    PlayerPrefs.Save();

#if UNITY_ANDROID
                    // Initialize listener services so we can communicate with backend
                    bool transactionsInit = await TransactionsHandler.Instance.Initialize(clientId, responseObj.Data.FcmData);
#endif

#if UNITY_ANDROID
                    if (!config.fetchUpdatesInRealTime)
                    {
                        StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Fetching content for StrixSDK");
                        await ContentFetcher.Instance.UpdateContentByTypes(new List<string>() {
                            "entities",
                            "localization",
                            "stattemplates",
                            "offers",
                            "positionedOffers",
                            "abtests",
                            "flows",
                            "events"
                        });
                    }
#else
                    // Just do this in case we would like other platforms to automatically fetch content on initialization.
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Fetching content for StrixSDK");
                    await ContentFetcher.Instance.UpdateContentByTypes(new List<string>() {
                         "entities",
                         "localization",
                         "stattemplates",
                         "offers",
                         "positionedOffers",
                         "abtests",
                         "flows",
                         "events"
                    });
#endif

                    //// Initialize PlayerWarehouse elements for current player
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting PlayerManager initialization...");
                    PlayerManager playerManagerInstance = PlayerManager.Instance;
                    bool warehouseInit = playerManagerInstance.Initialize(responseObj.Data.PlayerData);
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("PlayerManager initialization finished.");

                    //// Initialize Entities
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting EntityManager initialization...");
                    EntityManager entityManagerInstance = EntityManager.Instance;
                    bool entitiesInit = entityManagerInstance.Initialize();
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("EntityManager initialization finished.");

                    //// Initialize offers manager
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting OffersManager initialization...");
                    OffersManager offersManagerInstance = OffersManager.Instance;
                    bool offersInit = offersManagerInstance.Initialize();
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("OffersManager initialization finished.");

                    //// Initialize flows manager
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting FlowsManager initialization...");
                    FlowsManager flowsManagerInstance = FlowsManager.Instance;
                    bool flowsInit = flowsManagerInstance.Initialize();
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("FlowsManager initialization finished.");

                    //// Initialize game events manager
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting FlowsManager initialization...");
                    GameEventsManager eventsManagerInstance = GameEventsManager.Instance;
                    bool eventsInit = eventsManagerInstance.Initialize();
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("FlowsManager initialization finished.");

#if UNITY_ANDROID
                    if (transactionsInit && offersInit && entitiesInit && warehouseInit && flowsInit && eventsInit)
                    {
                        StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("StrixSDK initialized successfuly!");
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"Error while initializing StrixSDK. " +
                            $"\n Initialization results: " +
                            $"OffersManager={offersInit}." +
                            $"EntityManager={entitiesInit}." +
                            $"PlayerManager={warehouseInit}." +
                            $"TransactionsHandler={transactionsInit}." +
                            $"FlowsManager={flowsInit}." +
                            $"GameEventsManager={eventsInit}." +
                            $" Strix will try to initialize using local configs.");
                        InitializeServices();
                        return false;
                    }
#else
                    if (offersInit && entitiesInit && warehouseInit)
                    {
                        StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("StrixSDK initialized successfuly!");
                        return true;
                    }
                    else
                    {
                        Debug.LogError($"Error while initializing StrixSDK. " +
                            $"\n Initialization results: " +
                            $"OffersManager={offersInit}." +
                            $"EntityManager={entitiesInit}." +
                            $"PlayerManager={warehouseInit}." +
                            $"FlowsManager={flowsInit}." +
                            $"GameEventsManager={eventsInit}." +
                            $" Strix will try to initialize using local configs.");
                        InitializeServices();
                        return false;
                    }
#endif
                }
                else
                {
                    Debug.LogError("Invalid value returned during initialization initial event. Expected non-empty string. Strix will try to initialize using local configs.");
                    InitializeServices();
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in InitSDK: {ex.Message}");
                InitializeServices();
                return false;
            }
        }

        private static void InitializeServices()
        {
            try
            {
                //// Initialize PlayerWarehouse elements for current player
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting PlayerManager initialization...");
                PlayerManager playerManagerInstance = PlayerManager.Instance;
                bool warehouseInit = playerManagerInstance.Initialize(null);
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("PlayerManager initialization finished.");

                //// Initialize Entities
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting EntityManager initialization...");
                EntityManager entityManagerInstance = EntityManager.Instance;
                bool entitiesInit = entityManagerInstance.Initialize();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("EntityManager initialization finished.");

                //// Initialize offers manager
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting OffersManager initialization...");
                OffersManager offersManagerInstance = OffersManager.Instance;
                bool offersInit = offersManagerInstance.Initialize();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("OffersManager initialization finished.");

                //// Initialize flows manager
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting FlowsManager initialization...");
                FlowsManager flowsManagerInstance = FlowsManager.Instance;
                bool flowsInit = flowsManagerInstance.Initialize();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("FlowsManager initialization finished.");

                //// Initialize game events manager
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Starting FlowsManager initialization...");
                GameEventsManager eventsManagerInstance = GameEventsManager.Instance;
                bool eventsInit = eventsManagerInstance.Initialize();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("FlowsManager initialization finished.");
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not initialize StrixSDK partially: {ex.Message}");
            }
        }

        private async void Awake()
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
            await Initialize();
        }
    }
}