using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using StrixSDK.Editor.Config;
using StrixSDK.Runtime.APIClient;
using StrixSDK.Runtime.Models;
using StrixSDK.Runtime.Service;
using StrixSDK.Runtime.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

namespace StrixSDK
{
    public class Analytics : MonoBehaviour
    {
        private void Start()
        {
            if (!Strix.IsInitialized)
            {
                Debug.Log($"StrixSDK isn't initialized. Analytics system is not available.");
                Destroy(gameObject);
            }
            Application.logMessageReceived += LogCallback;
        }

        private static Analytics _instance;

        public static Analytics Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Analytics>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Analytics>();
                        obj.name = typeof(Analytics).ToString();
                    }
                }
                return _instance;
            }
        }

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

        public static async Task<string> ProcessNewEvent(string payload, bool preventCaching)
        //
        // After we formed the needed payload, make an event body, put it there and send
        //
        {
            // Loading config file
            StrixSDKConfig config = StrixSDKConfig.Instance;

            var buildType = config.branch;

            var sessionID = PlayerPrefs.GetString("Strix_SessionID", string.Empty);
            var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
            if (string.IsNullOrEmpty(sessionID) || string.IsNullOrEmpty(clientID))
            {
                Utils.LogError("Error while sending Strix analytics event: client ID or Session ID is invalid.");
                return null;
            }

            var eventBody = new Dictionary<string, object>()
            {
                {"device", clientID},
                {"secret", config.apiKey},
                {"session", sessionID},
                {"language", Application.systemLanguage.ToString()},
                {"platform", Application.platform.ToString()},
                {"gameVersion", Application.version.ToString()},
                {"engineVersion", Application.unityVersion},
                {"build", buildType},
                {"payload", JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(payload)}
            };

            Utils.LogMessage($"Sending analytics event.");

            var response = await Client.Req(API.SendEvent, eventBody);
            if (response == "err")
            {
                if (!preventCaching)
                {
                    AnalyticsCache.CacheFailedEvents(payload);
                    await NetworkListener.CheckNetworkAvailabilityAsync(API.HealthCheck);
                    Utils.LogError("Error while sending Strix analytics event: no internet connection found. Events will be saved and delivered later.");
                }
                else
                {
                    await NetworkListener.CheckNetworkAvailabilityAsync(API.HealthCheck);
                    Utils.LogError("Error while sending Strix analytics event: no internet connection found. Events will NOT be saved as preventCaching is 'true', but will be delivered later .");
                }
                return null;
            }
            else
            {
                Utils.LogMessage($"Analytics event was successfully sent.");
                return response;
            }
        }

        #region Sending events

        //
        // newSession event: has no actions
        // endSession event: takes it's actions from the PlayerPrefs saved on Initialize()
        //
        public static async Task<string> SendSessionEvent(string eventType)
        {
            if (eventType == EventTypes.newSession.ToString())
            {
                PlayerPrefs.SetString("Strix_SessionID", Guid.NewGuid().ToString());
                PlayerPrefs.Save();

                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", eventType },
                    { "actions", new JObject() }
                };

                // Add body and try to send
                var payloadList = new List<Dictionary<string, object>> { payloadObject };
                string payloadJson = JsonConvert.SerializeObject(payloadList);
                return await ProcessNewEvent(payloadJson, false);
            }
            else if (eventType == EventTypes.endSession.ToString())
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

                    // Making event payload
                    var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", eventType },
                        { "actions", actions }
                    };

                    // Add body and try to send
                    var payloadList = new List<Dictionary<string, object>> { payloadObject };
                    string payloadJson = JsonConvert.SerializeObject(payloadList);
                    return await ProcessNewEvent(payloadJson, false);
                }
                else
                {
                    Utils.LogError($"Could not retrieve valid time from PlayerPrefs for '{EventTypes.endSession.ToString()}' event. Got: {time}");
                    return null;
                }
            }
            else
            {
                Utils.LogError($"Not enough arguments provided for event: {eventType}");
            }
            return null;
        }

        // offerEvent
        public static async Task<string> SendOfferBuyEvent(string offerID, float price, string currency)
        {
            if (String.IsNullOrEmpty(offerID))
            {
                Utils.LogError($"Null or empty offerID in event 'offerEvent'");
                return null;
            }
            if (price < 0)
            {
                Utils.LogError($"Invalid price in event 'offerEvent'. Must be equal or greater than zero (zero = free).");
                return null;
            }

            if (string.IsNullOrEmpty(currency))
            {
                Utils.LogError($"Invalid currency provided to 'SendOfferBuyEvent'. SDK probably initialized not properly!");
                return null;
            }

            var actions = new Dictionary<string, object>
                {
                    { "offerID", offerID },
                    { "price", price },
                    { "currency", currency }
                };

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "offerEvent" },
                    { "actions", actions }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // offerShown event
        public static async Task<string> SendOfferShownEvent(string offerID, float price)
        {
            if (String.IsNullOrEmpty(offerID))
            {
                Utils.LogError($"Null or empty offerID in event 'offerShown'");
                return null;
            }
            if (price < 0)
            {
                Utils.LogError($"Invalid price in event 'offerShown'. Must be equal or greater than zero (zero = free).");
                return null;
            }

            var currency = PlayerPrefs.GetString("Strix_ClientCurrency", string.Empty);
            if (string.IsNullOrEmpty(currency))
            {
                Utils.LogError($"Invalid currency fetched. SDK probably initialized not properly!");
                return null;
            }

            var actions = new Dictionary<string, object>
                {
                    { "offerID", offerID },
                    { "price", price },
                    { "currency", currency }
                };

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "offerShown" },
                    { "actions", actions }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // economyEvent
        public static async Task<string> SendEconomyEvent(string currencyID, float amount, EconomyTypes type, string origin)
        {
            if (String.IsNullOrEmpty(currencyID))
            {
                Utils.LogError($"Null or empty currencyID in event 'economyEvent'. Make sure you give proper entity ID and it is marked as currency.");
                return null;
            }
            if (type != EconomyTypes.sink && type != EconomyTypes.source)
            {
                Utils.LogError($"Invalid type in event 'economyEvent'. Must be either 'sink' or 'source'.");
                return null;
            }
            if (String.IsNullOrEmpty(origin))
            {
                Utils.LogError($"Null or empty origin in event 'economyEvent'. Must be any string that identifies the origin of source/sink.");
                return null;
            }

            Entity correspondingEntity = Entities.GetEntityById(currencyID);
            if (correspondingEntity == null)
            {
                Utils.LogError($"No currency entity found in event 'economyEvent' by ID '{currencyID}'. The ID must be an existing entityID with enabled 'Is Currency' switch.");
                return null;
            }
            else
            {
                if (!correspondingEntity.IsCurrency)
                {
                    Utils.LogError($"Entity with ID '{currencyID}' provided in event 'economyEvent' is not a valid currency entity. Check if this entity has 'Is Currency' switch enabled.");
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

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "economyEvent" },
                    { "actions", actions }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // adEvent
        public static async Task<string> SendAdEvent(string adNetwork, AdTypes adType, int timeSpent)
        {
            if (!Enum.IsDefined(typeof(AdTypes), adType))
            {
                Utils.LogError($"Invalid ad type in event 'adEvent'.");
                return null;
            }
            if (String.IsNullOrEmpty(adNetwork))
            {
                Utils.LogError($"Null or empty ad network in event 'adEvent'. Must be any string that identifies your ad provider.");
                return null;
            }

            var actions = new Dictionary<string, string>
                 {
                     { "adNetwork", adNetwork },
                     { "adType", adType.ToString() },
                     { "timeSpent", timeSpent.ToString() }
                 };

            // Making event payload
            var payloadObject = new Dictionary<string, object>
                 {
                     { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                     { "type", "adEvent" },
                     { "actions", actions }
                 };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // reportEvent
        public static async Task<string> SendReportEvent(SeverityTypes severity, string reportID, string message)
        {
            if (!Enum.IsDefined(typeof(SeverityTypes), severity))
            {
                Utils.LogError($"Invalid severity type in event 'reportEvent'. Must be 'debug', 'warn', 'info', 'error' or 'fatal'.");
                return null;
            }
            if (String.IsNullOrEmpty(reportID))
            {
                Utils.LogError($"Null or empty report ID in event 'reportEvent'. Must be any string that identifies message category.");
                return null;
            }
            if (String.IsNullOrEmpty(message))
            {
                Utils.LogError($"Null or empty message in event 'reportEvent'. Must be any string that describes the actual report message.");
                return null;
            }

            var actions = new Dictionary<string, string>
                {
                    { "severity", severity.ToString() },
                    { "reportID", reportID },
                    { "message", message },
                };

            // Making event payload
            var payloadObject = new Dictionary<string, object>()
                {
                    { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                    { "type", "reportEvent" },
                    { "actions", actions }
                };

            // Add body and try to send
            var payloadList = new List<Dictionary<string, object>> { payloadObject };
            string payloadJson = JsonConvert.SerializeObject(payloadList);
            return await ProcessNewEvent(payloadJson, false);
        }

        // custom design events
        public static async Task<string> SendCustomEvent(string eventID, object value1, object value2, object value3)
        {
            AnalyticEvent targetEvent = Content.AnalyticEvents.FirstOrDefault(e => e.Codename == eventID);
            if (targetEvent == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return null;
            }

            // Validate all custom events' values
            var check = ValidateEventValues(targetEvent, eventID, value1, value2, value3);

            var time = PlayerPrefs.GetString("Strix_SessionStartTime", string.Empty);
            if (!String.IsNullOrEmpty(time) && check)
            {
                var actions = new Dictionary<string, object>
                    {
                        { "value1", value1 },
                        { "value2", value2 },
                        { "value3", value3 },
                    };

                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", eventID },
                        { "actions", actions }
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
                    Utils.LogError($"Could not retrieve valid time from PlayerPrefs for '{eventID}' event. Got: {time}");
                }
                else if (!check)
                {
                    Utils.LogError($"Could not validate values for '{eventID}' event.");
                }
            }
            return null;
        }

        public static async Task<string> SendCustomEvent(string eventID, object value1, object value2)
        {
            AnalyticEvent targetEvent = Content.AnalyticEvents.FirstOrDefault(e => e.Codename == eventID);
            if (targetEvent == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return null;
            }

            // Validate all custom events' values
            var check = ValidateEventValues(targetEvent, eventID, value1, value2);

            var time = PlayerPrefs.GetString("Strix_SessionStartTime", string.Empty);
            if (!String.IsNullOrEmpty(time) && check)
            {
                var actions = new Dictionary<string, object>
                    {
                        { "value1", value1 },
                        { "value2", value2 },
                    };

                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", eventID },
                        { "actions", actions }
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
                    Utils.LogError($"Could not retrieve valid time from PlayerPrefs for '{eventID}' event. Got: {time}");
                }
                else if (!check)
                {
                    Utils.LogError($"Could not validate values for '{eventID}' event.");
                }
            }
            return null;
        }

        public static async Task<string> SendCustomEvent(string eventID, object value1)
        {
            AnalyticEvent targetEvent = Content.AnalyticEvents.FirstOrDefault(e => e.Codename == eventID);
            if (targetEvent == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return null;
            }

            // Validate all custom events' values
            var check = ValidateEventValues(targetEvent, eventID, value1);

            var time = PlayerPrefs.GetString("Strix_SessionStartTime", string.Empty);
            if (!String.IsNullOrEmpty(time) && check)
            {
                var actions = new Dictionary<string, object>
                    {
                        { "value1", value1 },
                    };

                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", eventID },
                        { "actions", actions }
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
                    Utils.LogError($"Could not retrieve valid time from PlayerPrefs for '{eventID}' event. Got: {time}");
                }
                else if (!check)
                {
                    Utils.LogError($"Could not validate values for '{eventID}' event.");
                }
            }
            return null;
        }

        public static async Task<string> SendCustomEvent(string eventID)
        {
            AnalyticEvent targetEvent = Content.AnalyticEvents.FirstOrDefault(e => e.Codename == eventID);
            if (targetEvent == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return null;
            }

            // Validate all custom events' values
            var check = ValidateEventValues(targetEvent, eventID);

            var time = PlayerPrefs.GetString("Strix_SessionStartTime", string.Empty);
            if (!String.IsNullOrEmpty(time) && check)
            {
                // Making event payload
                var payloadObject = new Dictionary<string, object>()
                    {
                        { "time", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ") },
                        { "type", eventID },
                        { "actions", new JObject() }
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
                    Utils.LogError($"Could not retrieve valid time from PlayerPrefs for '{eventID}' event. Got: {time}");
                }
                else if (!check)
                {
                    Utils.LogError($"Could not validate values for '{eventID}' event.");
                }
            }
            return null;
        }

        #endregion Sending events

        #region Design event values validation

        //
        // Get the event the user tries to call, and check how many values are there. If the user provided too many or too few values, reject the event.
        // We only want to accept events that match the values designed in the web panel, and their data types must match required event's values types, too.
        //
        private static bool ValidateEventValues(AnalyticEvent eventObj, string eventID, object value1, object value2, object value3)
        {
            if (eventObj == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return false;
            }
            EventValue[] values = eventObj.Values;
            if (values.Length == 3)
            {
                return ValidateEventValue(values[0], value1) && ValidateEventValue(values[1], value2) && ValidateEventValue(values[2], value3);
            }
            else
            {
                Utils.LogError($"Too much or too few arguments provided for the event '{eventID}'. Expecting {values.Length} values to be provided.");
                return false;
            }
        }

        private static bool ValidateEventValues(AnalyticEvent eventObj, string eventID, object value1, object value2)
        {
            if (eventObj == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return false;
            }
            EventValue[] values = eventObj.Values;
            if (values.Length == 2)
            {
                return ValidateEventValue(values[0], value1) && ValidateEventValue(values[1], value2);
            }
            else
            {
                Utils.LogError($"Too much or too few arguments provided for the event '{eventID}'. Expecting {values.Length} values to be provided.");
                return false;
            }
        }

        private static bool ValidateEventValues(AnalyticEvent eventObj, string eventID, object value1)
        {
            if (eventObj == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return false;
            }
            EventValue[] values = eventObj.Values;
            if (values.Length == 1)
            {
                return ValidateEventValue(values[0], value1);
            }
            else
            {
                Utils.LogError($"Too much or too few arguments provided for the event '{eventID}'. Expecting {values.Length} values to be provided.");
                return false;
            }
        }

        private static bool ValidateEventValues(AnalyticEvent eventObj, string eventID)
        {
            if (eventObj == null)
            {
                Utils.LogError($"Custom design event named '{eventID}' was not found.");
                return false;
            }
            EventValue[] values = eventObj.Values;
            if (values.Length == 0)
            {
                return true;
            }
            else
            {
                Utils.LogError($"Too much or too few arguments provided for the event '{eventID}'. Expecting {values.Length} values to be provided.");
                return false;
            }
        }

        private static bool ValidateEventValue(EventValue valueObject, object value)
        {
            string format = valueObject.Format;

            if (format == "string")
            {
                // Check if value is string
                return value is string;
            }
            else if (format == "percentile")
            {
                // If a percentile, check if within a range
                return (value is int || value is float || value is decimal || value is double || value is long) && ((int)value - 1) * (100 - (int)value) >= 0;
            }
            else if (format == "bool")
            {
                // If bool, check if bool
                return value is bool;
            }
            else
            {
                // If other type (float, int, money), just check if it suits the number data type.
                return value is int || value is float || value is decimal || value is double || value is long;
            }
        }

        #endregion Design event values validation

        #region Session end/crash handler

        private void LogCallback(string logString, string stackTrace, LogType type)
        {
            if (type == LogType.Exception)
            {
                SendReportEvent(SeverityTypes.fatal, logString, stackTrace);
            }
            // Do not uncomment until we figure better way to handle the case where error sending reportEvent causes another reportEvent to arise
            //if (type == LogType.Error)
            //{
            //    SendReportEvent(SeverityTypes.error, logString, stackTrace);
            //}
        }

        private void OnApplicationPause()
        {
            SendSessionEvent("endSession");
        }

        private void OnApplicationQuit()
        {
            SendSessionEvent("endSession");
        }

        #endregion Session end/crash handler

        public static async Task<bool> TryToResendFailedEvents()
        {
            string cachedEvents = AnalyticsCache.LoadCachedEvents();
            var response = await ProcessNewEvent(cachedEvents, true);
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