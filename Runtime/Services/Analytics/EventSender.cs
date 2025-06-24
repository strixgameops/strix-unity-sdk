using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StrixSDK.Runtime.Config;
using StrixSDK.Runtime.APIClient;
using StrixSDK.Runtime.Models;
using StrixSDK.Runtime.Service;
using StrixSDK.Runtime.Utils;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using StrixSDK.Runtime;

namespace StrixSDK
{
    public class Analytics : MonoBehaviour
    {
        private static Analytics _instance;
        private static readonly List<Dictionary<string, object>> _eventQueue = new List<Dictionary<string, object>>();
        private static readonly object _queueLock = new object();
        private static bool _isBatchingEnabled = true;
        private Coroutine _batchSendCoroutine;

        public static Analytics Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Analytics>();
                    if (_instance == null)
                    {
                        GameObject obj = new();
                        _instance = obj.AddComponent<Analytics>();
                        obj.name = typeof(Analytics).ToString();
                        DontDestroyOnLoad(obj);
                    }
                }
                return _instance;
            }
        }

        private static bool endSessionWasSent = false;

        #region Types

        public enum EventTypes
        {
            newSession,
            endSession,
            offerShown,
            offerEvent,
            economyEvent,
            adEvent,
            reportEvent
        }

        #endregion Types

        private void Start()
        {
            StartBatchSending();
        }

        private void StartBatchSending()
        {
            if (_batchSendCoroutine == null)
            {
                _batchSendCoroutine = StartCoroutine(BatchSendCoroutine());
            }
        }

        private IEnumerator BatchSendCoroutine()
        {
            while (_isBatchingEnabled && Application.isPlaying)
            {
                StrixSDKConfig config = StrixSDKConfig.Instance;
                yield return new WaitForSeconds(config.eventBatchInterval);

                if (_eventQueue.Count > 0)
                {
                    yield return StartCoroutine(SendBatchedEvents());
                }
            }
        }

        private IEnumerator SendBatchedEvents()
        {
            List<Dictionary<string, object>> eventsToSend;

            lock (_queueLock)
            {
                if (_eventQueue.Count == 0)
                    yield break;

                eventsToSend = new List<Dictionary<string, object>>(_eventQueue);
                _eventQueue.Clear();
            }

            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Sending batch of {eventsToSend.Count} events");

            var task = ProcessBatchedEvents(eventsToSend, false);
            yield return new WaitUntil(() => task.IsCompleted);
        }

        public static async Task<string> ProcessBatchedEvents(List<Dictionary<string, object>> events, bool preventCaching)
        {
            if (events == null || events.Count == 0)
                return null;

            // Loading config file
            StrixSDKConfig config = StrixSDKConfig.Instance;

            var sessionID = Strix.SessionID ?? "";
            var clientID = Strix.ClientID ?? "";
            var build = Strix.BuildVersion ?? "";
            if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(clientID) || string.IsNullOrEmpty(build))
            {
                Debug.LogError("Error while sending Strix analytics event: client ID or Session ID is invalid.");
                return null;
            }

            var eventBody = new Dictionary<string, object>()
            {
                {"device", clientID},
                {"secret", config.apiKey},
                {"session", sessionID},
                {"language", Application.systemLanguage.ToString()},
                {"platform", Application.platform.ToString()},
                {"gameVersion", Application.version},
                {"engineVersion", Application.unityVersion},
                {"environment", config.environment},
                {"build", build},
                {"time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ")},
                {"payload", events}
            };

            string payload = JsonConvert.SerializeObject(events);
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Sending analytics batch: {payload}");

            var response = await Client.Req(API.SendEvent, eventBody);
            if (response == "err")
            {
                if (!preventCaching)
                {
                    AnalyticsCache.CacheFailedEvents(payload);
                    await NetworkListener.CheckNetworkAvailabilityAsync(API.HealthCheck);
                    Debug.LogError("Error while sending Strix analytics batch: no internet connection found. Events will be saved and delivered later.");
                }
                else
                {
                    await NetworkListener.CheckNetworkAvailabilityAsync(API.HealthCheck);
                    Debug.LogError("Error while sending Strix analytics batch: no internet connection found. Events will NOT be saved as preventCaching is 'true', but will be delivered later .");
                }
                return null;
            }
            else
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Analytics batch was successfully sent.");
                return response;
            }
        }

        public static async Task<string> ProcessNewEvent(string payload, bool preventCaching)
        //
        // After we formed the needed payload, put it in queue for batching or send immediately if needed
        //
        {
            var events = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(payload);

            if (events != null && events.Count > 0)
            {
                lock (_queueLock)
                {
                    _eventQueue.AddRange(events);
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Added {events.Count} events to batch queue. Queue size: {_eventQueue.Count}");
                }
            }

            return "queued";
        }

        public static async Task<string> SendImmediateEvent(string payload, bool preventCaching)
        //
        // For critical events that need to be sent immediately (like endSession)
        //
        {
            var events = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(payload);
            return await ProcessBatchedEvents(events, preventCaching);
        }

        #region Sending events

        public static async Task<string> SendNewSessionEvent(bool isNewPlayer, Dictionary<string, object> customData)
        {
            // Making event payload
            var actions = new Dictionary<string, object>
                    {
                        { "isNewPlayer", isNewPlayer }
                    };

            customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("newSession", customData);

            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "newSession" },
                    { "actions", actions },
                    { "customData", customData }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        /// <summary>
        /// Takes it's actions from the PlayerPrefs saved on Initialize()
        /// </summary>
        /// <param name="customData"></param>
        /// <returns></returns>
        public static async Task<string> SendEndSessionEvent(Dictionary<string, object> customData)
        {
            var time = PlayerPrefs.GetString("Strix_SessionStartTime", string.Empty);
            if (DateTime.TryParse(time, out DateTime savedTime))
            {
                DateTime currentTime = DateTime.Now;
                TimeSpan difference = currentTime - savedTime;
                double differenceInSeconds = difference.TotalSeconds;

                var actions = new Dictionary<string, object>
                    {
                        { "sessionLength", differenceInSeconds }
                    };

                customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("endSession", customData);

                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", "endSession" },
                        { "actions", actions },
                        { "customData", customData }
                    };

                // Send immediately for session end events
                var payloadList = new List<Dictionary<string, object>> { payloadObject };
                string payloadJson = JsonConvert.SerializeObject(payloadList);
                return await SendImmediateEvent(payloadJson, false);
            }
            else
            {
                Debug.LogError($"Could not retrieve valid time from PlayerPrefs for '{nameof(EventTypes.endSession)}' event. Got: {time}");
                return null;
            }
        }

        // offerEvent
        public static async Task<string> SendOfferBuyEvent(string offerID, float price, int? discount, string currency, Dictionary<string, object> customData)
        {
            if (String.IsNullOrEmpty(offerID))
            {
                Debug.LogError($"Null or empty offerID in event 'offerEvent'");
                return null;
            }
            if (price < 0)
            {
                Debug.LogError($"Invalid price in event 'offerEvent'. Must be equal or greater than zero (zero = free).");
                return null;
            }

            if (string.IsNullOrEmpty(currency))
            {
                Debug.LogError($"Invalid currency provided to 'SendOfferBuyEvent'. SDK probably initialized not properly!");
                return null;
            }

            var actions = new Dictionary<string, object>
                {
                    { "offerID", offerID },
                    { "price", price },
                    { "currency", currency },
                    { "discount", discount ?? 0 }
                };

            customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("offerEvent", customData);

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "offerEvent" },
                    { "actions", actions },
                    { "customData", customData }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // offerShown event
        public static async Task<string> SendOfferShownEvent(string offerID, float price, int? discount, Dictionary<string, object> customData)
        {
            if (String.IsNullOrEmpty(offerID))
            {
                Debug.LogError($"Null or empty offerID in event 'offerShown'");
                return null;
            }
            if (price < 0)
            {
                Debug.LogError($"Invalid price in event 'offerShown'. Must be equal or greater than zero (zero = free).");
                return null;
            }

            var currency = Strix.ClientCurrency ?? "";
            if (string.IsNullOrEmpty(currency))
            {
                Debug.LogError($"Invalid currency fetched. SDK probably initialized not properly!");
                return null;
            }

            var actions = new Dictionary<string, object>
                {
                    { "offerID", offerID },
                    { "price", price },
                    { "currency", currency },
                    { "discount", discount ?? 0 }
                };

            customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("offerShown", customData);

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "offerShown" },
                    { "actions", actions },
                    { "customData", customData }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // economyEvent
        public static async Task<string> SendEconomyEvent(string currencyID, float amount, EconomyTypes type, string origin, Dictionary<string, object> customData)
        {
            if (String.IsNullOrEmpty(currencyID))
            {
                Debug.LogError($"Null or empty currencyID in event 'economyEvent'. Make sure you give proper entity ID and it is marked as currency.");
                return null;
            }
            if (type != EconomyTypes.sink && type != EconomyTypes.source)
            {
                Debug.LogError($"Invalid type in event 'economyEvent'. Must be either 'sink' or 'source'.");
                return null;
            }
            if (String.IsNullOrEmpty(origin))
            {
                Debug.LogError($"Null or empty origin in event 'economyEvent'. Must be any string that identifies the origin of source/sink.");
                return null;
            }

            Entity correspondingEntity = Entities.GetEntityById(currencyID);
            if (correspondingEntity == null)
            {
                Debug.LogError($"No currency entity found in event 'economyEvent' by ID '{currencyID}'. The ID must be an existing entityID with enabled 'Is Currency' switch.");
                return null;
            }
            else
            {
                if (!correspondingEntity.IsCurrency)
                {
                    Debug.LogError($"Entity with ID '{currencyID}' provided in event 'economyEvent' is not a valid currency entity. Check if this entity has 'Is Currency' switch enabled.");
                    return null;
                }
            }

            var actions = new Dictionary<string, object>
                {
                    { "currencyID", currencyID },
                    { "amount", amount },
                    { "type", type.ToString() },
                    { "origin", origin },
                };

            customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("economyEvent", customData);

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "economyEvent" },
                    { "actions", actions },
                    { "customData", customData }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // adEvent
        public static async Task<string> SendAdEvent(string adNetwork, AdTypes adType, int timeSpent, Dictionary<string, object> customData)
        {
            if (!Enum.IsDefined(typeof(AdTypes), adType))
            {
                Debug.LogError($"Invalid ad type in event 'adEvent'.");
                return null;
            }
            if (String.IsNullOrEmpty(adNetwork))
            {
                Debug.LogError($"Null or empty ad network in event 'adEvent'. Must be any string that identifies your ad provider.");
                return null;
            }

            var actions = new Dictionary<string, string>
                 {
                     { "adNetwork", adNetwork },
                     { "adType", adType.ToString() },
                     { "timeSpent", timeSpent.ToString() }
                 };

            customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("adEvent", customData);

            // Making event payload
            var payloadObject = new Dictionary<string, object>
                 {
                     { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                     { "type", "adEvent" },
                     { "actions", actions },
                     { "customData", customData }
                 };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // reportEvent
        public static async Task<string> SendReportEvent(SeverityTypes severity, string reportID, string message, Dictionary<string, object> customData)
        {
            if (!Enum.IsDefined(typeof(SeverityTypes), severity))
            {
                Debug.LogError($"Invalid severity type in event 'reportEvent'. Must be 'debug', 'warn', 'info', 'error' or 'fatal'.");
                return null;
            }
            if (String.IsNullOrEmpty(reportID))
            {
                Debug.LogError($"Null or empty report ID in event 'reportEvent'. Must be any string that identifies message category.");
                return null;
            }
            if (String.IsNullOrEmpty(message))
            {
                Debug.LogError($"Null or empty message in event 'reportEvent'. Must be any string that describes the actual report message.");
                return null;
            }

            var actions = new Dictionary<string, string>
                {
                    { "severity", severity.ToString() },
                    { "reportID", reportID },
                    { "message", message },
                };

            customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend("reportEvent", customData);

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "reportEvent" },
                    { "actions", actions },
                    { "customData", customData }
                };

            // Send immediately for fatal errors
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);

            if (severity == SeverityTypes.fatal || severity == SeverityTypes.error)
            {
                return await SendImmediateEvent(payloadJson, false);
            }
            else
            {
                return await ProcessNewEvent(payloadJson, false);
            }
        }

        // custom design events
        public static async Task<string> SendCustomEvent(string eventID, Dictionary<string, object> customData)
        {
            var time = PlayerPrefs.GetString("Strix_SessionStartTime", string.Empty);
            if (!String.IsNullOrEmpty(time))
            {
                customData = FlowsManager.Instance.ExecuteFlow_AnalyticsEventSend(eventID, customData);

                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", eventID },
                        { "customData", customData }
                    };

                // Add body and try to send
                var payloadList = new List<Dictionary<string, object>> { payloadObject };
                string payloadJson = JsonConvert.SerializeObject(payloadList);
                return await ProcessNewEvent(payloadJson, false);
            }
            else
            {
                if (String.IsNullOrEmpty(time))
                {
                    Debug.LogError($"Could not retrieve valid time from PlayerPrefs for '{eventID}' event. Got: {time}");
                }
            }
            return null;
        }

        #endregion Sending events

        #region Session end/crash handler

        /// <summary>
        /// Called automatically from internally upon any log arise.
        /// </summary>
        /// <param name="logString"></param>
        /// <param name="stackTrace"></param>
        /// <param name="type"></param>
        private void LogCallback(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                _ = SendReportEvent(SeverityTypes.fatal, logString, stackTrace, null);
            }
            // Do not uncomment until we figure better way to handle the case where error sending reportEvent causes another reportEvent to arise
            // and until the crashlytics is on board
            //if (type == LogType.Error)
            //{
            //    SendReportEvent(SeverityTypes.error, logString, stackTrace);
            //}
        }

        private void OnApplicationPause()
        {
            FlushEvents();
            if (!endSessionWasSent)
            {
                _ = SendEndSessionEvent(null);
                endSessionWasSent = true;
            }
        }

        private void OnApplicationQuit()
        {
            FlushEvents();
            if (!endSessionWasSent)
            {
                _ = SendEndSessionEvent(null);
                endSessionWasSent = true;
            }
        }

        private void OnDestroy()
        {
            _isBatchingEnabled = false;
            if (_batchSendCoroutine != null)
            {
                StopCoroutine(_batchSendCoroutine);
                _batchSendCoroutine = null;
            }
        }

        /// <summary>
        /// Immediately sends any queued events
        /// </summary>
        public static async void FlushEvents()
        {
            List<Dictionary<string, object>> eventsToSend;

            lock (_queueLock)
            {
                if (_eventQueue.Count == 0)
                    return;

                eventsToSend = new List<Dictionary<string, object>>(_eventQueue);
                _eventQueue.Clear();
            }

            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Flushing {eventsToSend.Count} events immediately");
            await ProcessBatchedEvents(eventsToSend, false);
        }

        #endregion Session end/crash handler

        public static async Task<bool> TryToResendFailedEvents()
        {
            string cachedEvents = AnalyticsCache.LoadCachedEvents();
            if (string.IsNullOrEmpty(cachedEvents))
                return true;

            var events = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(cachedEvents);
            var response = await ProcessBatchedEvents(events, true);
            if (response != null)
            {
                AnalyticsCache.DeleteCachedEvents();
                return true;
            }
            else
            {
                await Task.Delay(5000);
                return false;
            }
        }
    }
}