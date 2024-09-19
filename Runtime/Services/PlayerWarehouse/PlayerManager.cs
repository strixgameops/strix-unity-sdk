using Newtonsoft.Json;
using StrixSDK.Editor.Config;
using StrixSDK.Runtime.APIClient;
using StrixSDK.Runtime.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using StrixSDK.Runtime.Utils;

namespace StrixSDK.Runtime
{
    public class PlayerManager : MonoBehaviour
    {
        private static PlayerManager _instance;

        public static PlayerManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<PlayerManager>();
                        obj.name = typeof(PlayerManager).ToString();
                    }
                }
                return _instance;
            }
        }

        // Player data is set from initialization response
        public PlayerData _playerData;

        // Templates are set from ContentFetcher -> ContentManager after templates are fetched.
        public ElementTemplate[] _templates;

        public ABTest[] _abTests;

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

        public bool Initialize(PlayerData data)
        {
            if (data == null) return false;
            try
            {
                _playerData = data;
                RefreshABTests();
                Content.RecacheExistingStatisticsTemplates();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not initialize PlayerManager. Error: {e}");
                return false;
            }
        }

        public void RefreshABTests()
        {
            // Refresh tests list
            List<ABTest> testsList = new List<ABTest>();
            var testsDocs = Content.LoadAllFromFile("abtests");

            if (testsDocs != null)
            {
                foreach (var doc in testsDocs)
                {
                    string json = JsonConvert.SerializeObject(doc);

                    ABTest test = JsonConvert.DeserializeObject<ABTest>(json);
                    testsList.Add(test);
                }
                _abTests = testsList.ToArray();
                Debug.Log($"Fetched {_abTests.Length} currently ongoing AB tests");
            }
            else
            {
                Debug.Log($"Could not fetch AB tests from persistent storage");
            }
        }
    }

    public static class WarehouseHelperMethods
    {
        private static bool CheckElementFormatType(object value, string templateType)
        {
            switch (templateType)
            {
                case "float":
                    return value is float;

                case "integer":
                    return value is int;

                case "string":
                    return value is string;

                case "bool":
                    return value is bool;
            }
            return false;
        }

        private static object ClampElementValue(object value, ElementTemplate template)
        {
            switch (template.Type)
            {
                case "float":
                    if (template.RangeMax != null && template.RangeMin != null)
                    {
                        double min = Convert.ToDouble(template.RangeMin);
                        double max = Convert.ToDouble(template.RangeMax);
                        double val = Convert.ToDouble(value);
                        return Math.Clamp(val, min, max);
                    }
                    else
                    {
                        return value;
                    }

                case "integer":
                    if (template.RangeMax != null && template.RangeMin != null)
                    {
                        decimal min = Convert.ToDecimal(template.RangeMin);
                        decimal max = Convert.ToDecimal(template.RangeMax);
                        decimal val = Convert.ToDecimal(value);
                        return Math.Clamp(val, min, max);
                    }
                    else
                    {
                        return value;
                    }

                default:
                    return value;
            }
        }

        private static object GetCorrectDefaultValue(ElementTemplate template)
        {
            var value = template.DefaultValue;
            switch (template.Type)
            {
                case "float":
                    return Convert.ToDouble(value);

                case "integer":
                    return Convert.ToDecimal(value);

                case "string":
                    return value;

                case "bool":
                    return (string)value == "True";
            }
            return value;
        }

        private static void PropagateChangesToBackend(string elementId, object value, string endpoint)
        {
            // Loading config file
            StrixSDKConfig config = StrixSDKConfig.Instance;

            var buildType = config.branch;

            var body = new Dictionary<string, object>()
            {
                {"device", Strix.clientID},
                {"secret", config.apiKey},
                {"build", buildType},
                {"elementID", elementId},
                {"value", value},
            };
            _ = Client.Req(endpoint, body);
        }

        public static object GetPlayerElementValue(string elementId)
        {
            ElementTemplate template = PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == elementId);
            if (template == null)
            {
                Debug.LogError($"GetPlayerElement: No existing element template found by id '{elementId}'");
                return null;
            }

            PlayerDataElement element = PlayerManager.Instance._playerData.Elements.FirstOrDefault(e => e.Id == template.InternalId);
            if (element == null)
            {
                return template.DefaultValue;
            }
            return element.Value;
        }

        public static void IncrementPlayerOfferPurchase(string offerInternalId)
        {
            var offerData = PlayerManager.Instance._playerData.Offers.FirstOrDefault(e => e.Id == offerInternalId);
            if (offerData == null)
            {
                offerData = new PlayerOfferData()
                {
                    Id = offerInternalId,
                    ExpirationDate = null,
                    PurchasedTimes = 0,
                    CurrentAmount = 0
                };
            }
            else
            {
                offerData.PurchasedTimes = offerData.PurchasedTimes + 1;
                offerData.CurrentAmount = offerData.CurrentAmount + 1;
            }
        }

        public static void SetPlayerOfferExpiration(string offerInternalId, DateTime expirationDate)
        {
            var offerData = PlayerManager.Instance._playerData.Offers.FirstOrDefault(e => e.Id == offerInternalId);
            if (offerData == null)
            {
                offerData = new PlayerOfferData()
                {
                    Id = offerInternalId,
                    ExpirationDate = expirationDate.ToString("o"),
                    PurchasedTimes = 0,
                    CurrentAmount = 0
                };
            }
            else
            {
                offerData.ExpirationDate = expirationDate.ToString("o");
            }

            // Propagate changes of this element to player warehouse
            StrixSDKConfig config = StrixSDKConfig.Instance;

            var buildType = config.branch;

            var body = new Dictionary<string, object>()
            {
                {"device", Strix.clientID},
                {"secret", config.apiKey},
                {"build", buildType},
                {"offerID", offerInternalId},
                {"expiration", expirationDate.ToString("o")},
            };
            _ = Client.Req(API.SetOfferExpiration, body);
        }

        public static async Task<object> GetPlayerElementValueAsync(string elementId)
        {
            ElementTemplate template = PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == elementId);
            if (template == null)
            {
                Debug.LogError($"GetPlayerElementValueAsync: No existing element template found by id '{elementId}'");
                return null;
            }

            // Loading config file
            StrixSDKConfig config = StrixSDKConfig.Instance;

            var buildType = config.branch;

            var clientID = PlayerPrefs.GetString("Strix_ClientID", string.Empty);
            if (string.IsNullOrEmpty(clientID))
            {
                Debug.LogError("GetPlayerElementValueAsync: Client ID is invalid.");
                return null;
            }

            var body = new Dictionary<string, object>()
            {
                {"device", clientID},
                {"secret", config.apiKey},
                {"build", buildType},
                {"elementID", template.InternalId},
            };
            var result = await Client.Req(API.GetElementValue, body);
            var responseObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);

            if (responseObj != null && responseObj.TryGetValue("success", out var successValue) && successValue is bool success && success)
            {
                return responseObj.TryGetValue("data", out var data) ? data : null;
            }
            else
            {
                if (responseObj["message"] != null)
                {
                    Debug.LogError($"GetPlayerElementValueAsync: {responseObj["message"]}");
                }
                Debug.LogError($"GetPlayerElementValueAsync: Something get wrong. Could not fetch error message from backend.");
                return null;
            }
        }

        public static object SetPlayerElementValue(string elementId, object elementValue)
        {
            ElementTemplate template = PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == elementId);
            if (template == null)
            {
                Debug.LogError($"SetPlayerElementValue: No existing element template found by id '{elementId}'");
                return null;
            }
            var check = CheckElementFormatType(elementValue, template.Type);
            if (!check)
            {
                Debug.LogError($"SetPlayerElementValue: Wrong data type provided. Expected '{template.Type}' for element '{elementId}', got '{elementValue.GetType()}'");
                return null;
            }

            int elementIndex = PlayerManager.Instance._playerData.Elements.FindIndex(e => e.Id == template.InternalId);
            PlayerDataElement element = null;
            if (elementIndex != -1)
            {
                element = PlayerManager.Instance._playerData.Elements[elementIndex];
            }
            if (element == null)
            {
                element = new PlayerDataElement { Id = template.InternalId, Value = ClampElementValue(elementValue, template) };
            }
            else
            {
                element.Value = ClampElementValue(elementValue, template);
            }
            if (elementIndex != -1)
            {
                PlayerManager.Instance._playerData.Elements[elementIndex] = element;
            }
            else
            {
                PlayerManager.Instance._playerData.Elements.Add(element);
            }

            PropagateChangesToBackend(template.InternalId, elementValue, API.SetElementValue);

            InvokeOfferTrigger(element);

            return element.Value;
        }

        public static object AddPlayerElementValue(string elementId, object elementValue)
        {
            ElementTemplate template = PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == elementId);
            if (template == null)
            {
                Debug.LogError($"SetPlayerElementValue: No existing element template found by id '{elementId}'");
                return null;
            }
            var check = CheckElementFormatType(elementValue, template.Type);
            if (!check)
            {
                Debug.LogError($"SetPlayerElementValue: Wrong data type provided. Expected '{template.Type}' for element '{elementId}', got '{elementValue.GetType()}'");
                return null;
            }

            int elementIndex = PlayerManager.Instance._playerData.Elements.FindIndex(e => e.Id == template.InternalId);
            PlayerDataElement element = null;
            if (elementIndex != -1)
            {
                element = PlayerManager.Instance._playerData.Elements[elementIndex];
            }
            switch (template.Type)
            {
                case "float":
                    if (element == null)
                    {
                        element = new PlayerDataElement { Id = template.InternalId, Value = ClampElementValue((float)GetCorrectDefaultValue(template) + (float)elementValue, template) };
                    }
                    else
                    {
                        element.Value = ClampElementValue((float)element.Value + (float)elementValue, template);
                    }
                    break;

                case "integer":
                    if (element == null)
                    {
                        element = new PlayerDataElement { Id = template.InternalId, Value = ClampElementValue((int)GetCorrectDefaultValue(template) + (int)elementValue, template) };
                    }
                    else
                    {
                        element.Value = ClampElementValue((int)element.Value + (int)elementValue, template);
                    }
                    break;
            }
            if (elementIndex != -1)
            {
                PlayerManager.Instance._playerData.Elements[elementIndex] = element;
            }
            else
            {
                PlayerManager.Instance._playerData.Elements.Add(element);
            }

            PropagateChangesToBackend(template.InternalId, elementValue, API.AddElementValue);

            InvokeOfferTrigger(element);

            return element.Value;
        }

        public static object SubtractPlayerElementValue(string elementId, object elementValue)
        {
            ElementTemplate template = PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == elementId);
            if (template == null)
            {
                Debug.LogError($"SetPlayerElementValue: No existing element template found by id '{elementId}'");
                return null;
            }
            var check = CheckElementFormatType(elementValue, template.Type);
            if (!check)
            {
                Debug.LogError($"SetPlayerElementValue: Wrong data type provided. Expected '{template.Type}' for element '{elementId}', got '{elementValue.GetType()}'");
                return null;
            }

            int elementIndex = PlayerManager.Instance._playerData.Elements.FindIndex(e => e.Id == template.InternalId);
            PlayerDataElement element = null;
            if (elementIndex != -1)
            {
                element = PlayerManager.Instance._playerData.Elements[elementIndex];
            }

            switch (template.Type)
            {
                case "float":
                    if (element == null)
                    {
                        element = new PlayerDataElement { Id = template.InternalId, Value = ClampElementValue((float)GetCorrectDefaultValue(template) - (float)elementValue, template) };
                    }
                    else
                    {
                        element.Value = ClampElementValue((float)element.Value - (float)elementValue, template);
                    }
                    break;

                case "integer":
                    if (element == null)
                    {
                        element = new PlayerDataElement { Id = template.InternalId, Value = ClampElementValue((int)GetCorrectDefaultValue(template) - (int)elementValue, template) };
                    }
                    else
                    {
                        element.Value = ClampElementValue((int)element.Value - (int)elementValue, template);
                    }
                    break;
            }
            if (elementIndex != -1)
            {
                PlayerManager.Instance._playerData.Elements[elementIndex] = element;
            }
            else
            {
                PlayerManager.Instance._playerData.Elements.Add(element);
            }

            PropagateChangesToBackend(template.InternalId, elementValue, API.SubtractElementValue);

            InvokeOfferTrigger(element);

            return element.Value;
        }

        /// <summary>
        /// Called by any element change
        /// </summary>
        /// <param name="changedElement"></param>
        private static void InvokeOfferTrigger(PlayerDataElement changedElement)
        {
            List<Offer> preProcessedOffers = OffersManager.Instance._offers
                .Where(o => o.Triggers.Any(t => t.Subject == changedElement.Id))
                .ToList();
            List<Offer> processedOffers = new List<Offer> { };

            if (!preProcessedOffers.Any())
            {
                return;
            }

            ElementTemplate template = PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == changedElement.Id);
            if (template == null)
            {
                Debug.LogError($"InvokeOfferTrigger: No existing element template found by id '{changedElement.Id}'");
                return;
            }

            object changedVal;
            switch (template.Type)
            {
                case "float":
                    if (float.TryParse((string)changedElement.Value, out float floatValue))
                    {
                        changedVal = floatValue;
                    }
                    else
                    {
                        Debug.LogError($"InvokeOfferTrigger: Tried to invoke any offer's by changing '{PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == changedElement.Id).Id}' element, but failed to parse changed value!");
                        return;
                    }
                    break;

                case "integer":
                    if (int.TryParse((string)changedElement.Value, out int intValue))
                    {
                        changedVal = intValue;
                    }
                    else
                    {
                        Debug.LogError($"InvokeOfferTrigger: Tried to invoke any offer's by changing '{PlayerManager.Instance._templates.FirstOrDefault(t => t.Id == changedElement.Id).Id}' element, but failed to parse changed value!");
                        return;
                    }
                    break;
            }

            foreach (var offer in preProcessedOffers)
            {
                bool shouldInvoke = false;
                var trigger = offer.Triggers.FirstOrDefault(e => e.Subject == changedElement.Id);

                // Range value check
                if (!string.IsNullOrEmpty(trigger.ValueSecondary) && !string.IsNullOrEmpty(trigger.Value))
                {
                    if (double.TryParse(trigger.Value, out double value) && double.TryParse(trigger.ValueSecondary, out double valueSecondary))
                    {
                        switch (template.Type)
                        {
                            case "float":
                                if (float.TryParse((string)changedElement.Value, out float floatValue))
                                {
                                    shouldInvoke = floatValue >= value && floatValue <= valueSecondary;
                                }
                                else
                                {
                                    Debug.LogError($"InvokeOfferTrigger: Failed to parse float value for trigger condition on offer '{offer.Id}'");
                                    continue;
                                }
                                break;

                            case "integer":
                                if (int.TryParse((string)changedElement.Value, out int intValue))
                                {
                                    shouldInvoke = intValue >= value && intValue <= valueSecondary;
                                }
                                else
                                {
                                    Debug.LogError($"InvokeOfferTrigger: Failed to parse integer value for trigger condition on offer '{offer.Id}'");
                                    continue;
                                }
                                break;

                            default:
                                Debug.LogError($"InvokeOfferTrigger: Unsupported template type '{template.Type}' for trigger condition on offer '{offer.Id}'");
                                continue;
                        }
                    }
                }
                else
                {
                    // Simple check
                    if (!string.IsNullOrEmpty(trigger.Value))
                    {
                        switch (template.Type)
                        {
                            case "float":
                                if (float.TryParse((string)changedElement.Value, out float floatValue))
                                {
                                    shouldInvoke = floatValue == double.Parse(trigger.Value);
                                }
                                else
                                {
                                    Debug.LogError($"InvokeOfferTrigger: Failed to parse float value for trigger condition on offer '{offer.Id}'");
                                    continue;
                                }
                                break;

                            case "integer":
                                if (int.TryParse((string)changedElement.Value, out int intValue))
                                {
                                    shouldInvoke = intValue == int.Parse(trigger.Value);
                                }
                                else
                                {
                                    Debug.LogError($"InvokeOfferTrigger: Failed to parse integer value for trigger condition on offer '{offer.Id}'");
                                    continue;
                                }
                                break;

                            default:
                                Debug.LogError($"InvokeOfferTrigger: Unsupported template type '{template.Type}' for trigger condition on offer '{offer.Id}'");
                                continue;
                        }
                    }
                }

                if (shouldInvoke)
                {
                    processedOffers.Add(offer);
                }
                else
                {
                    continue;
                }

                Debug.LogError($"InvokeOfferTrigger: Tried to call '{trigger.Condition}' trigger for an offer '{offer.Id}' but condition values are null or empty!");
            }

            if (processedOffers.Any())
            {
                OffersHelperMethods.InvokeTriggeredOffers(processedOffers);
            }
        }

        /// <summary>
        /// Initially called from pubsub when player gets a message that he has entered or left some segment
        /// </summary>
        /// <param name="segmentId"></param>
        /// <param name="changeType"></param>
        private static void InvokeOfferTrigger(string segmentId, string changeType)
        {
            List<Offer> preProcessedOffers = OffersManager.Instance._offers
                .Where(o => o.Triggers.Any(t => t.Subject == segmentId && t.Condition == changeType))
                .ToList();
            List<Offer> processedOffers = new List<Offer> { };

            if (!preProcessedOffers.Any())
            {
                return;
            }

            foreach (var offer in preProcessedOffers)
            {
                bool shouldInvoke = false;
                var trigger = offer.Triggers.FirstOrDefault(t => t.Subject == segmentId && t.Condition == changeType);
                if (trigger != null)
                {
                    // Should be "true" by definition, since if we have method arguments like "segment123" & "onExit", it
                    // already means that the player has left from the segment123 and all offers with such triggers should be invoken.
                    shouldInvoke = true;
                }

                if (shouldInvoke)
                {
                    processedOffers.Add(offer);
                }
                else
                {
                    continue;
                }

                Debug.LogError($"InvokeOfferTrigger: Tried to call '{trigger.Condition}' trigger for an offer '{offer.Id}' but condition values are null or empty!");
            }

            if (processedOffers.Any())
            {
                OffersHelperMethods.InvokeTriggeredOffers(processedOffers);
            }
        }

        public static void PlayerSegmentChange(string segmentId, string changeType)
        {
            switch (changeType)
            {
                case "onExit":
                    {
                        PlayerManager.Instance._playerData.Segments.Remove(segmentId);
                        Debug.LogError($"Changed (removed) player segment '{segmentId}'");
                    }
                    break;

                case "onEnter":
                    {
                        PlayerManager.Instance._playerData.Segments.Add(segmentId);
                        Debug.LogError($"Changed (added) player segment '{segmentId}'");
                    }
                    break;

                default:
                    {
                        Debug.LogError($"Player got segment changed, but received wrong changeType! Must be either 'onEnter' or 'onExit'");
                    }
                    break;
            }

            List<Offer> offersToTrigger = OffersManager.Instance._offers
                .Where(o => o.Triggers.Any(t => t.Subject == segmentId && t.Condition == changeType))
                .ToList();

            if (offersToTrigger.Any())
            {
                OffersHelperMethods.InvokeTriggeredOffers(offersToTrigger);
            }
        }
    }
}