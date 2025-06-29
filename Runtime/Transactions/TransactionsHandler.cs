﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using StrixSDK.Runtime.Utils;
using StrixSDK.Runtime.Config;

using Firebase;
using Firebase.Messaging;
using Firebase.Extensions;

using StrixSDK.Runtime.APIClient;
using StrixSDK.Runtime.Models;

namespace StrixSDK.Runtime.Db
{
    public class TransactionsHandler : MonoBehaviour
    {
        private static TransactionsHandler _instance;

        public static TransactionsHandler Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<TransactionsHandler>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<TransactionsHandler>();
                        obj.name = typeof(TransactionsHandler).ToString();
                    }
                }
                return _instance;
            }
        }

        private string fcmToken;
        private FirebaseApp FCM;
        private string clientID;

        public async Task<bool> Initialize(string clientId, FCMOptions fcmOptions)
        {
            clientID = clientId;

            // Check if FCM options are provided
            if (fcmOptions == null)
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("FCM options not provided. Skipping FCM initialization.");
                return true; // Return true since this is expected behavior when FCM is disabled
            }

            try
            {
                var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
                if (dependencyStatus == DependencyStatus.Available)
                {
                    var options = new AppOptions
                    {
                        ApiKey = fcmOptions.ApiKey,
                        AppId = fcmOptions.AppId,
                        ProjectId = fcmOptions.ProjectId,
                        MessageSenderId = fcmOptions.SenderId,
                        StorageBucket = fcmOptions.StorageBucket,
                    };
                    FirebaseApp app = FirebaseApp.Create(options);
                    FirebaseMessaging.TokenReceived += OnTokenReceived;
                    FirebaseMessaging.MessageReceived += OnMessageReceived;

                    var token = await FirebaseMessaging.GetTokenAsync();
                    Strix.FCMToken = token;
                    OnTokenReceived(null, new TokenReceivedEventArgs(token));

                    return true;
                }
                else
                {
                    Debug.LogError("Could not resolve all Firebase dependencies: " + dependencyStatus);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("CheckAndFixDependenciesAsync encountered an error: " + ex.Message);
                return false;
            }
        }

        private string FCMToken;

        public void OnTokenReceived(object sender, TokenReceivedEventArgs e)
        {
            try
            {
                if (FCMToken == null || FCMToken != e.Token)
                {
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"FCM token received: {e.Token}");
                    FCMToken = e.Token;

                    if (e.Token == null)
                    {
                        Debug.LogError($"FCM token is null and will not be sent to server.");
                        return;
                    }
                    if (e.Token == "StubToken")
                    {
                        Debug.LogError($"FCM token is a 'StubToken', possibly because of a wrong platform. Messaging won't initialize.");
                        return;
                    }

                    // Loading config file
                    StrixSDKConfig config = StrixSDKConfig.Instance;

                    if (string.IsNullOrEmpty(clientID))
                    {
                        Debug.LogError("Error while sending FCM token: client ID is invalid.");
                        return;
                    }

                    SendTokenToBackend(e.Token, clientID, config.apiKey);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception in OnTokenReceived: {ex.Message}");
            }
        }

        public void OnMessageReceived(object sender, Firebase.Messaging.MessageReceivedEventArgs e)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("FCM Message: " + e.Message.Data);

            // Assuming e.Message.Data is a JSON string, parse it into a dictionary of string, string first
            var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(JsonConvert.SerializeObject(e.Message.Data));
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"FCM Message stringified: {JsonConvert.SerializeObject(e.Message.Data)}");
            if (data != null)
            {
                switch (data["type"])
                {
                    case "update":

                        StrixSDKConfig config = StrixSDKConfig.Instance;
                        if (!config.fetchUpdatesInRealTime) break;

                        var checksumData = JsonConvert.DeserializeObject<Dictionary<string, object>>(data["checksums"].ToString());

                        // Iterate through each field in the data
                        var contentTypesToUpdate = ContentFetcher.GetUnsyncedContentTablesList(checksumData);
                        foreach (var contentType in contentTypesToUpdate)
                        {
                            // Sync each content type separately, so we don't do excess operations for the contents we don't need to update
                            var localItemsHashes = ContentFetcher.GetLocalContentItemsHashes(new List<string>() { contentType });
                            _ = ContentFetcher.Instance.FetchDeltaContentByType(contentType, localItemsHashes[contentType]);
                        }

                        break;

                    case "segment":
                        if (data["segmentID"] != null && data["changeType"] != null)
                        {
                            WarehouseHelperMethods.PlayerSegmentChange((string)data["segmentID"], (string)data["changeType"]);
                        }
                        else
                        {
                            Debug.LogWarning($"Got wrong or missing data in segment update!");
                        }
                        break;
                }
            }
        }

        private async void SendTokenToBackend(string token, string clientID, string secret)
        {
            try
            {
                // Loading config file
                StrixSDKConfig config = StrixSDKConfig.Instance;
                var build = Strix.BuildVersion ?? "";
                var body = new Dictionary<string, object>()
                {
                    {"device", clientID},
                    {"secret", secret},
                    {"token", token},
                    {"environment", config.environment},
                    {"build", build},
                };

                var response = await Client.Req(API.RegisterFCMToken, body);

                if (response != null)
                {
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Token successfully sent to backend.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error while sending token to backend: {ex.Message}");
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