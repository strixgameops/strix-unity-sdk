﻿using Newtonsoft.Json;
using StrixSDK.Editor.Config;
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
using UnityEngine.Localization.Settings;
using UnityEngine.Purchasing;

namespace StrixSDK
{
    public class Strix : MonoBehaviour
    {
        private static string StrixSDKVersion = "1.0.0";

        private static Strix _instance;

        public static Strix Instance
        {
            get
            {
                if (_instance == null)
                {
                    Debug.Log("Strix instance is null, attempting to find or create it.");
                    _instance = FindObjectOfType<Strix>();
                    if (_instance == null)
                    {
                        Debug.Log("Strix not found in the scene, creating a new instance.");
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

        public static async Task<bool> Initialize()
        {
            PlayerPrefs.SetString("Strix_ClientID", SystemInfo.deviceUniqueIdentifier);
            clientID = SystemInfo.deviceUniqueIdentifier;
            IsInitialized = await InitSDK(clientID);
            if (!IsInitialized)
            {
                RetryInitialize(clientID);
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
                RetryInitialize(customClientID);
            }
            return IsInitialized;
        }

        private static async Task<bool> RetryInitialize(string clientID)
        {
            await Task.Delay(30000);
            IsInitialized = await InitSDK(clientID);
            return IsInitialized;
        }

        private static async Task<bool> InitSDK(string clientId)
        {
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
                var checkerBody = new Dictionary<string, object>()
                {
                    {"platform", Application.platform.ToString()},
                    {"sdkVersion", StrixSDKVersion},
                    {"engineVersion", Application.unityVersion},
                };

                Utils.LogMessage($"Sending analytics event.");

                var versionCheck = await Client.Req(API.CheckSDK, checkerBody);
                var versionCheckResponse = JsonConvert.DeserializeObject<M_SDKVersionCheckResponse>(versionCheck);
                if (versionCheckResponse.IsGood)
                {
                    Debug.Log($"{versionCheckResponse.Message}");
                }
                else
                {
                    Debug.LogWarning($"{versionCheckResponse.Message}");
                }

                // Make analytics instance. It will listen for Unity logs and report any errors. Make it before all calls to catch any errors.
                Analytics analyticsInstance = Analytics.Instance;

                // We need to send newSession event and get the key we will use to access db
                var result = await Analytics.SendSessionEvent("newSession");

                if (!string.IsNullOrEmpty(result))
                {
                    // If the key was fetched, proceed with initialization
                    var responseObj = JsonConvert.DeserializeObject<M_InitializationResponse>(result);

                    // Set client key which is used for DB access & identifies target currency for IAPs
                    PlayerPrefs.SetString("Strix_ClientKey", responseObj.Data.Key);
                    PlayerPrefs.SetString("Strix_ClientCurrency", responseObj.Data.Currency);
                    PlayerPrefs.Save();

                    // Initialize listener services so we can communicate with backend
                    bool transactionsInit = await TransactionsHandler.Instance.Initialize(clientId, responseObj.Data.FcmData);

#if !UNITY_ANDROID
// Just do this in case we would like other platforms to automatically fetch content on initialization.
                    ContentFetcher.Instance.FetchContentByType("entities");
                    ContentFetcher.Instance.FetchContentByType("analytics");
                    ContentFetcher.Instance.FetchContentByType("localization");
                    ContentFetcher.Instance.FetchContentByType("stattemplates");
                    ContentFetcher.Instance.FetchContentByType("offers");
                    ContentFetcher.Instance.FetchContentByType("positionedOffers");
                    ContentFetcher.Instance.FetchContentByType("abtests");
#endif

                    //// Initialize PlayerWarehouse elements for current player
                    Debug.Log("Starting PlayerManager initialization...");
                    PlayerManager playerManagerInstance = PlayerManager.Instance;
                    bool warehouseInit = playerManagerInstance.Initialize(responseObj.Data.PlayerData);
                    Debug.Log("PlayerManager initialization finished.");

                    //// Initialize Entities
                    Debug.Log("Starting EntityManager initialization...");
                    EntityManager entityManagerInstance = EntityManager.Instance;
                    bool entitiesInit = entityManagerInstance.Initialize();
                    Debug.Log("EntityManager initialization finished.");

                    //// Initialize offers manager
                    Debug.Log("Starting OffersManager initialization...");
                    OffersManager offersManagerInstance = OffersManager.Instance;
                    bool offersInit = offersManagerInstance.Initialize();
                    Debug.Log("OffersManager initialization finished.");

                    if (transactionsInit && offersInit && entitiesInit && warehouseInit)
                    {
                        Debug.Log("StrixSDK initialized successfuly!");
                        return true;
                    }
                    else
                    {
                        Debug.Log($"Error while initializing StrixSDK. " +
                            $"\n Initialization results: " +
                            $"OffersManager={offersInit}." +
                            $"EntityManager={entitiesInit}." +
                            $"PlayerManager={warehouseInit}." +
                            $"TransactionsHandler={transactionsInit}." +
                            $" Strix won't initialize.");
                        return false;
                    }
                }
                else
                {
                    Utils.LogError("Invalid value returned during initialization initial event. Expected non-empty string. Strix won't initialize.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Utils.LogError($"Exception in InitSDK: {ex.Message}");
                return false;
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
            }
        }
    }
}