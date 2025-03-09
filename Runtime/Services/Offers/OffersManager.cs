using Newtonsoft.Json;
using StrixSDK.Runtime.Utils;
using System;
using StrixSDK.Runtime;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Globalization;
using System.Threading.Tasks;
using StrixSDK.Runtime.Config;
using StrixSDK.Runtime.APIClient;
using StrixSDK.Runtime.Models;
using System.Numerics;

namespace StrixSDK.Runtime
{
    public class OffersManager : MonoBehaviour
    {
        private static OffersManager _instance;
        private static PlayerManager PlayerManagerInstance;

        public static OffersManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<OffersManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<OffersManager>();
                        obj.name = typeof(OffersManager).ToString();
                    }
                }
                return _instance;
            }
        }

        // All in-game offers, including IAPs
        public Offer[] _offers;

        public PositionedOffer[] _positionedOffers;
        public bool IAPManagerInitiated = false;

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

        public void RefreshOffers()
        {
            // Refresh offers array
            List<Offer> offersList = new List<Offer>();
            var offersDocs = Content.LoadAllFromFile("offers");

            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Fetching offers...");
            if (offersDocs != null)
            {
                foreach (var doc in offersDocs)
                {
                    string json = JsonConvert.SerializeObject(doc);

                    Offer offer = JsonConvert.DeserializeObject<Offer>(json);
                    offersList.Add(offer);
                }
                _offers = offersList.ToArray();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Fetched {_offers.Length} offers");
            }
            else
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Could not fetch offers from persistent storage");
            }

            for (int i = 0; i < _offers.Length; i++)
            {
                _offers[i] = PopulateOfferFields(_offers[i]);
            }

            // Make IAP handler
#if UNITY_ANDROID
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Making IAPs from offers...");

            try
            {
                List<string> validProductIDs = GetValidProductIDs(_offers);
                bool IAPInit = StrixIAPManager.Instance.StartupIAPManager(validProductIDs);
                if (IAPInit)
                {
                    IAPManagerInitiated = true;
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"IAP manager initiated successfully.");
                }
                else
                {
                    IAPManagerInitiated = false;
                    Debug.LogWarning($"IAP manager was not initiated.");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error initializing IAP manager: {e.Message}");
            }
#endif

            // Refresh positions array
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Fetching positioned offers...");
            List<PositionedOffer> positionedOffersList = new List<PositionedOffer>();
            var positionedOffersDocs = Content.LoadAllFromFile("positionedOffers");
            if (positionedOffersDocs != null)
            {
                foreach (var doc in positionedOffersDocs)
                {
                    string json = JsonConvert.SerializeObject(doc);

                    PositionedOffer position = JsonConvert.DeserializeObject<PositionedOffer>(json);
                    positionedOffersList.Add(position);
                }
                _positionedOffers = positionedOffersList.ToArray();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Fetched {_positionedOffers.Length} positions for offers");
            }
            else
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Could not fetch positioned offers from persistent storage");
            }
        }

        public bool Initialize()
        {
            try
            {
                RefreshOffers();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not initialize OffersManager. Error: {e}");
                return false;
            }
        }

        private List<string> GetValidProductIDs(Offer[] offers)
        {
            var validOffers = offers.Where(offer => offer.IsValidIap == true).ToList();
            var validProductIDs = validOffers.Select(offer => (string)offer.Asku).ToList();
            return validProductIDs;
        }

        private static Offer PopulateOfferFields(Offer offer)
        {
            // Localizing
            try
            {
                offer.Name = Localization.GetLocalizedString(offer.Name);
                offer.Desc = Localization.GetLocalizedString(offer.Desc);
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not make offer's localized name and description: {e.Message}");
            }

            // Making .Price field for this offer
            if (offer.Pricing.Type == "money")
            {
                string userCurrency = PlayerPrefs.GetString("Strix_ClientCurrency", string.Empty);
                if (userCurrency == string.Empty)
                {
                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Error: could not retrieve current user's currency from PlayerPrefs.");
                    return null;
                }
                offer.Price = OffersHelperMethods.GetCurrencyObjectByCurrencyCode(offer, userCurrency);
            }
            else
            {
                offer.Price = OffersHelperMethods.GetCurrencyObjectByEntityNodeId(offer, offer.Pricing.NodeId);
            }

            List<OfferContent> content = offer.Content;
            for (int i = 0; i < content.Count; i++)
            {
                content[i].EntityId = EntityHelperMethods.GetEntityIdByNodeId(content[i].NodeId);
            }

            return offer;
        }
    }

    public static class OffersHelperMethods
    {
        public static async Task<bool> BuyOffer(Offer offer, Dictionary<string, object> customData)
        {
            try
            {
                // First, check if the offer is a real-money IAP that must be bought using real money.
                // If not, procceed as it were a soft/hard currency offer.
                var asku = offer.Asku;
                bool realMoneyIAP = false;
                bool success = false;

                float resultPrice = 0;
                string currency = "";
                int discount = offer.Pricing.Discount;
                if (offer.Pricing.Type == "money")
                {
                    currency = PlayerPrefs.GetString("Strix_ClientCurrency", string.Empty);
                    if (!string.IsNullOrEmpty(currency))
                    {
                        resultPrice = OffersHelperMethods.GetCurrencyValueByCurrencyCode(offer, currency);
                    }
                }
                else
                {
                    currency = offer.Pricing.NodeId;
                    resultPrice = offer.Pricing.Amount;
                }

                // Apply discount
                if (discount > 0 && discount <= 100)
                {
                    resultPrice *= (100 - discount) / 100f;
                }

                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Offer ASKU: {asku}");

                if (asku != null && offer.Pricing.Type == "money")
                {
                    // Real-money offer
                    realMoneyIAP = true;
                    string receipt = await StrixIAPManager.Instance.CallBuyIAP(asku);
                    if (receipt != null)
                    {
                        var sessionID = PlayerPrefs.GetString("Strix_SessionID", string.Empty);
                        if (string.IsNullOrEmpty(sessionID))
                        {
                            Debug.LogWarning("Problem while validating offer receipt: Session ID is invalid.");
                        }

                        // Additional backend validation
                        StrixSDKConfig config = StrixSDKConfig.Instance;
                        var body = new Dictionary<string, object>()
                        {
                            {"device", Strix.clientID},
                            {"secret", config.apiKey},
                            {"session", sessionID},
                            {"build", config.branch},
                            {"asku", asku},
                            {"offerID", offer.InternalId},
                            {"receipt", receipt},
                            {"resultPrice", resultPrice},
                            {"currency", currency}
                        };
                        var result = await Client.Req(API.ValidateReceipt, body);
                        var resultObj = JsonConvert.DeserializeObject<Dictionary<string, object>>(result);
                        if (resultObj != null)
                        {
                            success = (bool)resultObj["isValid"];
                        }
                        else
                        {
                            Debug.LogError("Purchase failed: backend validation result is null");
                        }
                    }
                    else
                    {
                        Debug.LogError("Purchase failed.");
                    }
                }
                else if (offer.Pricing.Type == "entity")
                {
                    string entityId = EntityHelperMethods.GetEntityIdByNodeId(currency);
                    if (string.IsNullOrEmpty(entityId))
                    {
                        Debug.LogError($"Player tried to buy offer that has entity {currency} as price, but it's not present in system now!");
                        success = false;
                    }
                    else
                    {
                        var playerCurrencyAmount = await Inventory.GetInventoryItemAmount(entityId);

                        if (BigInteger.Parse(playerCurrencyAmount) >= (BigInteger)resultPrice)
                        {
                            _ = Inventory.RemoveInventoryItem(entityId, (int)resultPrice);
                            success = true;
                        }
                    }
                }

                WarehouseHelperMethods.IncrementPlayerOfferPurchase(offer.InternalId);

                // Give out offer's content
                if (realMoneyIAP)
                {
                    if (success)
                    {
                        for (int i = 0; i < offer.Content.Count; i++)
                        {
                            _ = Inventory.AddInventoryItem(offer.Content[i].EntityId, offer.Content[i].Amount);
                        }
                    }
                    return success;
                }
                else
                {
                    if (success)
                    {
                        for (int i = 0; i < offer.Content.Count; i++)
                        {
                            _ = Inventory.AddInventoryItem(offer.Content[i].EntityId, offer.Content[i].Amount);
                        }
                        // Send event
                        _ = Analytics.SendOfferBuyEvent(GetOriginalOfferInternalId(offer.InternalId), resultPrice, discount, currency, customData);
                    }
                    return success;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not buy offer '{offer.Id}' ({offer.InternalId}). Error: {e.Message}");
                return false;
            }
        }

        private static Texture2D GetOfferIcon(Offer offer)
        {
            if (!string.IsNullOrEmpty((string)offer.Icon))
            {
                string base64 = Content.GetCachedMedia((string)offer.Icon, "offers");
                Texture2D icon = Base64ToTexture2D(base64);
                return icon;
            }
            return null;
        }

        private static Texture2D Base64ToTexture2D(string base64)
        {
            string base64Prefix = "data:image/";
            int startIndex = base64Prefix.Length;
            int endIndex = base64.IndexOf(";base64,");
            if (endIndex == -1)
            {
                throw new FormatException("Invalid base64 data format.");
            }

            // Get type and extension
            string mimeType = base64.Substring(startIndex, endIndex - startIndex);
            string fileExtension = mimeType.Split('/').Last();

            // Get base64 string
            string base64Data = base64.Substring(endIndex + 8);
            byte[] imageData = Convert.FromBase64String(base64Data);

            if (fileExtension == "png" || fileExtension == "jpg" || fileExtension == "jpeg")
            {
                // Process PNG or JPEG pictures
                Texture2D texture = new Texture2D(2, 2);
                if (texture.LoadImage(imageData))
                {
                    return texture;
                }
                else
                {
                    throw new FormatException("Failed to load image data into Texture2D.");
                }
            }
            else
            {
                throw new FormatException("Unsupported image extension retrieved from config.");
            }
        }

        public static string FindAskuById(string id)
        {
            try
            {
                Offer offer = GetOfferById(id);
                return offer?.Asku;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not find ASKU by ID '{id}'. Error: {ex.Message}");
                return null;
            }
        }

        public static Offer GetOfferById(string id)
        {
            try
            {
                // Fetch the true, original offer for now. We don't want to fetch other (variations) offers with the same ID
                Offer offer = OffersManager.Instance._offers.FirstOrDefault(p => p.InternalId ==
                GetOriginalOfferInternalId(
                    OffersManager.Instance._offers.FirstOrDefault(p => p.Id == id).InternalId
                    )
                );

                if (offer == null)
                {
                    Debug.LogError($"GetOfferById: No offer found with id '{id}'.");
                    return null;
                }

                // If player participates in the AB test for this offer, find it and try to give an intended variation of the requested offer
                bool changedOfferOnce = false;
                var allTests = PlayerManager.Instance._abTests.Where(t => t.Subject.ItemId == offer.InternalId).ToList();
                if (allTests.Any())
                {
                    var playerTests = PlayerManager.Instance._playerData.ABTests;
                    if (playerTests != null && playerTests.Any())
                    {
                        foreach (var playerTestId in playerTests)
                        {
                            var matchingTest = allTests.FirstOrDefault(t => t.Id == playerTestId);
                            if (matchingTest != null)
                            {
                                // If the player IS IN the AB test and we successfully validated it, try to get the corresponding variation of the offer
                                var tempOffer = OffersManager.Instance._offers
                                    .FirstOrDefault(p => p.InternalId == offer.InternalId + "|" + matchingTest.InternalId);

                                if (tempOffer != null)
                                {
                                    changedOfferOnce = true;
                                    offer = tempOffer;
                                }
                                else
                                {
                                    Debug.LogError($"GetOfferById: Unexpected behavior while fetching offer '{id}'. Current player is seen to participate in the AB test with this offer, though the code failed to give him a variation of the offer.");
                                }
                                break;
                            }
                        }
                    }
                }
                if (!changedOfferOnce)
                {
                    var eventChangedOffer = GameEventsManager.Instance.TryGetChangedOfferFromOngoingEvents(
                            PlayerManager.Instance._playerData.Segments,
                            GetOriginalOfferInternalId(
                        OffersManager.Instance._offers.FirstOrDefault(p => p.Id == id).InternalId), OffersManager.Instance._offers.ToList());
                    if (eventChangedOffer != null)
                    {
                        changedOfferOnce = true;
                        offer = eventChangedOffer;
                    }
                }

                var check = IsAccessible(offer.InternalId);
                if (!check)
                {
                    Debug.LogError($"GetOfferById: The offer is not accessible by current user.");
                    return null;
                }
                return offer;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not find Offer by ID '{id}'. Error: {ex.Message}");
                return null;
            }
        }

        public static Offer GetOfferByInternalId(string id, bool applyModifications)
        {
            try
            {
                Offer offer = OffersManager.Instance._offers.FirstOrDefault(p => p.InternalId == id);

                if (offer == null)
                {
                    Debug.LogError($"GetOfferByInternalId: No offer found with id '{id}'.");
                    return null;
                }

                if (applyModifications)
                {
                    // If player participates in the AB test for this offer, find it and try to give an intended variation of the requested offer
                    bool changedOfferOnce = false;
                    var allTests = PlayerManager.Instance._abTests.Where(t => t.Subject.ItemId == offer.InternalId).ToList();
                    if (allTests.Any())
                    {
                        var playerTests = PlayerManager.Instance._playerData.ABTests;
                        if (playerTests != null && playerTests.Any())
                        {
                            foreach (var playerTestId in playerTests)
                            {
                                var matchingTest = allTests.FirstOrDefault(t => t.Id == playerTestId);
                                if (matchingTest != null)
                                {
                                    // If the player IS IN the AB test and we successfully validated it, try to get the corresponding variation of the offer
                                    var tempOffer = OffersManager.Instance._offers
                                        .FirstOrDefault(p => p.InternalId == offer.InternalId + "|" + matchingTest.InternalId);

                                    if (tempOffer != null)
                                    {
                                        changedOfferOnce = true;
                                        offer = tempOffer;
                                    }
                                    else
                                    {
                                        Debug.LogError($"GetOfferByInternalId: Unexpected behavior while fetching offer '{id}'. Current player is seen to participate in the AB test with this offer, though the code failed to give him a variation of the offer.");
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    if (!changedOfferOnce)
                    {
                        var eventChangedOffer = GameEventsManager.Instance.TryGetChangedOfferFromOngoingEvents(
                            PlayerManager.Instance._playerData.Segments,
                            GetOriginalOfferInternalId(
                        OffersManager.Instance._offers.FirstOrDefault(p => p.Id == id).InternalId), OffersManager.Instance._offers.ToList());
                        if (eventChangedOffer != null)
                        {
                            changedOfferOnce = true;
                            offer = eventChangedOffer;
                        }
                    }
                }

                var check = IsAccessible(offer.InternalId);
                if (!check)
                {
                    Debug.LogError($"GetOfferByInternalId: The offer is not accessible by current user.");
                    return null;
                }
                return offer;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not find Offer by ID '{id}'. Error: {ex.Message}");
                return null;
            }
        }

        public static List<OfferContent> GetOfferContent(string offerId)
        {
            Offer offer = GetOfferById(offerId);
            return offer.Content;
        }

        public static DateTime StartOfferExpiration(string offerId)
        {
            DateTime currentTime = DateTime.Now;

            // Get the original offer
            Offer offer = OffersManager.Instance._offers.FirstOrDefault(p => p.InternalId ==
                GetOriginalOfferInternalId(
                    OffersManager.Instance._offers.FirstOrDefault(p => p.Id == offerId).InternalId
                    )
                );

            if (offer.Duration == null || string.IsNullOrEmpty(offer.Duration.TimeUnit) || offer.Duration.Value <= 0)
            {
                Debug.LogError($"StartOfferExpiration: Offer '{offerId}' has no time unit or valid duration value '{offer.Duration.TimeUnit}' ");
                return currentTime;
            }

            string trueInternalId = GetOriginalOfferInternalId(offer.InternalId);

            switch (offer.Duration.TimeUnit.ToLower())
            {
                case "days":
                    currentTime = currentTime.AddDays(offer.Duration.Value);
                    WarehouseHelperMethods.SetPlayerOfferExpiration(trueInternalId, currentTime);
                    return currentTime;

                case "hours":
                    currentTime = currentTime.AddHours(offer.Duration.Value);
                    WarehouseHelperMethods.SetPlayerOfferExpiration(trueInternalId, currentTime);
                    return currentTime;

                case "minutes":
                    currentTime = currentTime.AddMinutes(offer.Duration.Value);
                    WarehouseHelperMethods.SetPlayerOfferExpiration(trueInternalId, currentTime);
                    return currentTime;

                case "seconds":
                    currentTime = currentTime.AddSeconds(offer.Duration.Value);
                    WarehouseHelperMethods.SetPlayerOfferExpiration(trueInternalId, currentTime);
                    return currentTime;

                default:
                    Debug.LogError($"StartOfferExpiration: Invalid time unit of '{offer.Duration.TimeUnit}' ");
                    return currentTime;
            }
        }

        public static OfferDuration GetOfferRawDuration(string offerId, bool startExpiration)
        {
            try
            {
                Offer offer = GetOfferById(offerId);
                if (offer != null)
                {
                    if (startExpiration)
                    {
                        StartOfferExpiration(offerId);
                    }
                    return offer.Duration;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"GetOfferRawDuration: Could not find Duration of the offer '{offerId}'. Error: {ex.Message}");
                return null;
            }
        }

        public static OfferDuration GetOfferRawDuration(string offerId)
        {
            try
            {
                Offer offer = GetOfferById(offerId);
                if (offer != null)
                {
                    return offer.Duration;
                }
                return null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"GetOfferRawDuration: Could not find Duration of the offer '{offerId}'. Error: {ex.Message}");
                return null;
            }
        }

        public static DateTime GetOfferExpirationDate(string offerId)
        {
            try
            {
                Offer offer = GetOfferById(offerId);
                if (offer == null)
                {
                    Debug.LogError($"GetOfferExpirationDate: Could not find offer by ID '{offerId}'.");
                    return DateTime.MaxValue;
                }
                List<PlayerOfferData> playerOfferDatas = PlayerManager.Instance._playerData.Offers;
                var result = playerOfferDatas.First(e => e.Id == GetOriginalOfferInternalId(offer.InternalId));
                if (result != null)
                {
                    DateTime parsed = DateTime.ParseExact(result.ExpirationDate, "yyyy-MM-ddTHH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                    return parsed;
                }
                Debug.LogError($"GetOfferExpirationDate: Could not get offer expiration date.");
                return DateTime.MaxValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"GetOfferExpirationDate: Could not get offer '{offerId}' expiration date. Error: {ex.Message}");
                return DateTime.MaxValue;
            }
        }

        private static Offer[] GetAccessibleOffers()
        {
            try
            {
                PlayerData playerData = PlayerManager.Instance._playerData;
                List<string> userSegments = playerData.Segments;
                Offer[] offers = OffersManager.Instance._offers;

                var accessibleOffers = offers.Where(offer =>
                    offer.Segments == null || offer.Segments.Length == 0 || offer.Segments.Intersect(userSegments).Any()).ToArray();

                return accessibleOffers;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not find accessible offers. Error: {ex.Message}");
                return null;
            }
        }

        public static Offer[] GetAllOffers()
        {
            // Returns all accessible offers. Accessible means user has the right conditions (segments) to see it and buy
            try
            {
                return GetAccessibleOffers();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not find offers. Error: {ex.Message}");
                return null;
            }
        }

        public static Offer[] GetAllOffers(bool forceAllOffers)
        {
            // Returns all offers including ones that are not accessible right now
            try
            {
                if (forceAllOffers)
                {
                    return GetAccessibleOffers();
                }
                else
                {
                    Offer[] offers = OffersManager.Instance._offers;

                    return offers;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"GetAllOffers: Could not find offers. Error: {ex.Message}");
                return null;
            }
        }

        public static bool IsAccessible(string offerInternalId)
        {
            try
            {
                bool purchaseLimitNotExceeded = true;
                bool notExpired = true;
                bool segmentCheck = true;

                PlayerData playerData = PlayerManager.Instance._playerData;
                Offer offer = OffersManager.Instance._offers.FirstOrDefault(p => p.InternalId == offerInternalId);

                // Segment check
                List<string> userSegments = playerData.Segments;
                userSegments = GameEvents.AddGameEventSegments(userSegments);
                segmentCheck = offer.Segments == null || offer.Segments.Length == 0 || offer.Segments.Intersect(userSegments).Any();

                // Check if user passes the offer expiration date check
                // We also must check the original offer, not the AB test variation of it.
                // Also check purchaseLimit
                var offerData = PlayerManager.Instance._playerData.Offers.FirstOrDefault(e => e.Id == GetOriginalOfferInternalId(offer.InternalId));
                if (offerData != null)
                {
                    // Only check expiration if it's not null in player's data (e.g. offer expiration was never triggered).
                    if (offerData.ExpirationDate != null)
                    {
                        DateTime parsedExpiration = DateTime.ParseExact(offerData.ExpirationDate, "yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture);
                        DateTime currentTime = DateTime.Now;
                        if (parsedExpiration != null)
                        {
                            // Expiration check
                            notExpired = parsedExpiration > currentTime;
                        }
                    }

                    // Limit check
                    if (offer.PurchaseLimit > 0)
                    {
                        purchaseLimitNotExceeded = offerData.PurchasedTimes < offer.PurchaseLimit;
                    }
                }

                return segmentCheck && notExpired && purchaseLimitNotExceeded;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not check if offer is accessible by current player. Error: {ex.Message}");
                return false;
            }
        }

        private static string GetOriginalOfferInternalId(string offerInternalId)
        {
            if (offerInternalId.Contains("|"))
            {
                string[] parts = offerInternalId.Split('|');
                return parts[0];
            }
            else
            {
                return offerInternalId;
            }
        }

        public static float GetCurrencyValueByCurrencyCode(Offer offer, string currencyCode)
        {
            var currency = offer.Pricing.Currencies.FirstOrDefault(c => c.CurrencyCode == currencyCode);
            if (currency == null)
            {
                return 0;
            }
            return currency.Value;
        }

        public static OfferPrice GetCurrencyObjectByCurrencyCode(Offer offer, string currencyCode)
        {
            try
            {
                var currency = offer.Pricing.Currencies.FirstOrDefault(c => c.CurrencyCode == currencyCode);

                if (currency == null)
                {
                    currency = offer.Pricing.Currencies.FirstOrDefault();
                    if (currency == null)
                    {
                        Debug.LogWarning($"Could not retrieve price for offer '{offer.Id}', currency '{currencyCode}'.");
                        return null;
                    }
                }

                return new OfferPrice
                {
                    Value = currency.Value,
                    Currency = currency.CurrencyCode
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"Could not retrieve price for offer '{offer.Id}', currency '{currencyCode}': {ex.Message}");
                return null;
            }
        }

        public static OfferPrice GetCurrencyObjectByEntityNodeId(Offer offer, string nodeId)
        {
            string entityId = EntityManager.Instance._entities.FirstOrDefault(e => e.NodeId == nodeId).Id;
            if (entityId == null)
            {
                Debug.LogError($"Could not find entity with node ID '{nodeId}' in offer '{offer.Id}'. Probably a mismatch/desync of entities and offers configurations!");
                return null;
            }
            OfferPrice price = new OfferPrice
            {
                Value = offer.Pricing.Amount,
                Currency = entityId
            };
            if (price == null)
            {
                Debug.LogError($"Could not construct price object in offer '{offer.Id}'.");
                return null;
            }
            return price;
        }

        public static PositionedOffer[] GetAllPositions()
        {
            return OffersManager.Instance._positionedOffers;
        }

        public static PositionedOffer GetPosition(string positionId)
        {
            return OffersManager.Instance._positionedOffers.FirstOrDefault(e => e.Id == positionId);
        }

        public static List<Offer> GetPositionedOffers(string positionId)
        {
            var position = OffersManager.Instance._positionedOffers.FirstOrDefault(e => e.Id == positionId);
            if (position == null)
            {
                Debug.LogError($"Position with ID '{positionId}' was not found!");
                return null;
            }
            var playerSegments = PlayerManager.Instance._playerData.Segments;

            string selectedSegment = "";
            foreach (var segment in position.Segments)
            {
                if (playerSegments.Contains(segment.SegmentId))
                {
                    selectedSegment = segment.SegmentId;
                }
            }
            if (string.IsNullOrEmpty(selectedSegment))
            {
                Debug.LogError($"Could not retrieve offers from position '{positionId}', which should not be normal!");
                return null;
            }

            var offers = position.Segments.FirstOrDefault(e => e.SegmentId == selectedSegment).Offers;
            if (offers == null)
            {
                Debug.LogError($"There are no offers for position '{positionId}' in segment '{selectedSegment}'.");
                return null;
            }

            List<Offer> offersList = new List<Offer>();
            foreach (var offer in offers)
            {
                var o = GetOfferByInternalId(offer, true);
                if (o != null)
                {
                    offersList.Add(o);
                }
            }
            return offersList;
        }

        /// <summary>
        /// "Offers.cs" is subscribed to this event
        /// </summary>
        public static event Action<List<Offer>> OnOffersTriggered;

        /// <summary>
        /// Called from the PlayerManager.
        /// </summary>
        /// <param name="offers">The list of offers that got triggered by some change</param>
        public static void InvokeTriggeredOffers(List<Offer> offers)
        {
            OnOffersTriggered?.Invoke(offers);
        }
    }
}