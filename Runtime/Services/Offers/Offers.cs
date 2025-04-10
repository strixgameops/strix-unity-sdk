using StrixSDK.Runtime;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using System;
using static StrixSDK.Runtime.OffersHelperMethods;

namespace StrixSDK
{
    public class Offers : MonoBehaviour
    {
        #region References

        // Singleton instance of Offers.
        private static Offers _instance;

        /// <summary>
        /// Singleton accessor for the Offers instance. If none exists, a new GameObject is created.
        /// </summary>
        public static Offers Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Offers>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Offers>();
                        obj.name = typeof(Offers).ToString();
                    }
                }
                return _instance;
            }
        }

        // Instance of OffersManager if needed (not used in current implementation but may be extended).
        private OffersManager offersManagerInstance;

        /// <summary>
        /// Unity Awake lifecycle method.
        /// Checks if the StrixSDK is initialized and subscribes to the OnOffersTriggered event.
        /// If not initialized, the Offers system is disabled.
        /// </summary>
        private void Awake()
        {
            if (!Strix.IsInitialized)
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"StrixSDK isn't initialized. Offers system is not available.");
                Destroy(gameObject);
            }
            // Subscribe to event that triggers offers.
            OffersHelperMethods.OnOffersTriggered += InvokeTriggeredOffers;
        }

        /// <summary>
        /// Unity OnDestroy lifecycle method.
        /// Unsubscribes from the OnOffersTriggered event to prevent memory leaks.
        /// </summary>
        private void OnDestroy()
        {
            OffersHelperMethods.OnOffersTriggered -= InvokeTriggeredOffers;
        }

        #endregion References

        #region Static Methods

        /// <summary>
        /// Returns offer with given ID, if it exists. Returns a variation of this offer if AB test conditions are met.
        /// </summary>
        public static Offer GetOfferById(string offerId)
        {
            return Instance.I_GetOfferById(offerId);
        }

        /// <summary>
        /// Returns offer with given ID, if it exists. Also sends an analytics event which helps track offer conversion rate.
        /// Additionally, triggers the start of expiration if a duration is set. The offer will no longer be available after expiration.
        /// </summary>
        public static Offer ShowOffer(string offerId, Dictionary<string, object> analyticsEventCustomData)
        {
            Offer offer = Instance.I_GetOfferById(offerId);
            if (offer != null)
            {
                offer = FlowsManager.Instance.ExecuteFlow_OfferShown(offer);
                Instance.I_StartOfferExpiration(offerId);
                _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, null, analyticsEventCustomData);
            }
            return offer;
        }

        /// <summary>
        /// Returns offer with given ID, if it exists. Also sends an analytics event which helps track offer conversion rate.
        /// Additionally, triggers the start of expiration if a duration is set. The offer will no longer be available after expiration.
        /// </summary>
        public static Offer ShowOffer(string offerId, int discount, Dictionary<string, object> analyticsEventCustomData)
        {
            Offer offer = Instance.I_GetOfferById(offerId);
            if (offer != null)
            {
                offer = FlowsManager.Instance.ExecuteFlow_OfferShown(offer);
                Instance.I_StartOfferExpiration(offerId);
                _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, discount, analyticsEventCustomData);
            }
            return offer;
        }

        /// <summary>
        /// Returns offer with given ID, if it exists. Sends an analytics event and conditionally starts expiration based on parameter.
        /// </summary>
        public static Offer ShowOffer(string offerId, bool startExpiration, int discount, Dictionary<string, object> analyticsEventCustomData)
        {
            Offer offer = Instance.I_GetOfferById(offerId);

            if (offer != null)
            {
                offer = FlowsManager.Instance.ExecuteFlow_OfferShown(offer);
                if (startExpiration)
                {
                    Instance.I_StartOfferExpiration(offerId);
                }
                _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, discount, analyticsEventCustomData);
            }
            return offer;
        }

        /// <summary>
        /// Returns all offers available to the user. Variations due to AB testing are automatically resolved.
        /// </summary>
        public static Offer[] GetAllOffers()
        {
            return Instance.I_GetAllOffers();
        }

        /// <summary>
        /// Returns all offers, including those inaccessible to the user due to segment restrictions.
        /// Note: While offers can be retrieved, operations like "BuyOffer" may fail on restricted offers.
        /// </summary>
        public static Offer[] GetAllOffers(bool forceAllOffers)
        {
            return Instance.I_GetAllOffers(forceAllOffers);
        }

        /// <summary>
        /// Returns all offer content associated with a given offer.
        /// </summary>
        public static List<OfferContent> GetOfferContent(string offerId)
        {
            return Instance.I_GetOfferContent(offerId);
        }

        /// <summary>
        /// Initiates the purchase process for an offer.
        /// - For real-money IAPs, the transaction is validated on Strix's end.
        /// - For offers with a price of 0, the offer is treated as free.
        /// - For entity-currency offers, the price amount is withdrawn from the player's inventory.
        /// </summary>
        public static async Task<BuyOfferResult> BuyOffer(Offer offer, Dictionary<string, object> analyticsEventCustomData)
        {
            return await Instance.I_BuyOffer(offer, null, analyticsEventCustomData);
        }

        /// <summary>
        /// Initiates the purchase process for an offer with an additional discount parameter.
        /// For real-money IAPs, the transaction is validated on Strix's end. Other conditions remain similar.
        /// </summary>
        public static async Task<BuyOfferResult> BuyOffer(Offer offer, int discount, Dictionary<string, object> analyticsEventCustomData)
        {
            return await Instance.I_BuyOffer(offer, discount, analyticsEventCustomData);
        }

        /// <summary>
        /// Returns all positions and their associated segments and offers from Positioned Offers.
        /// </summary>
        public static PositionedOffer[] GetAllPositions()
        {
            return Instance.I_GetAllPositions();
        }

        /// <summary>
        /// Returns a specific position item including its segments and offers.
        /// </summary>
        public static PositionedOffer GetPosition(string positionId)
        {
            return Instance.I_GetPosition(positionId);
        }

        /// <summary>
        /// Returns a list of offers available at a specific position for the current user.
        /// The method automatically resolves segments between the position and the player.
        /// </summary>
        public static List<Offer> GetPositionedOffers(string positionId)
        {
            return Instance.I_GetPositionedOffers(positionId);
        }

        /// <summary>
        /// Returns the current expiration date of the specified offer.
        /// </summary>
        public static DateTime GetOfferExpirationDate(string offerId)
        {
            return Instance.I_GetOfferExpirationDate(offerId);
        }

        /// <summary>
        /// Returns the raw duration object of the offer and conditionally starts the expiration process.
        /// </summary>
        public static OfferDuration GetOfferRawDuration(string offerId, bool startExpiration)
        {
            return Instance.I_GetOfferRawDuration(offerId, startExpiration);
        }

        /// <summary>
        /// Returns the raw duration object of the offer. NOTE: This method does NOT start the expiration by default.
        /// Call GetOfferRawDuration(string offerId, bool startExpiration) instead to start expiration.
        /// </summary>
        public static OfferDuration GetOfferRawDuration(string offerId)
        {
            return Instance.I_GetOfferRawDuration(offerId);
        }

        /// <summary>
        /// Starts the expiration process for an offer and returns the resulting due date.
        /// </summary>
        public static DateTime StartOfferExpiration(string offerId)
        {
            return Instance.I_StartOfferExpiration(offerId);
        }

        /// <summary>
        /// Event triggered when a list of offers is activated by some external action.
        /// Subscribers can listen to this event to receive updates on triggered offers.
        /// </summary>
        public static event Action<List<Offer>> OnOffersTriggered;

        /// <summary>
        /// Internal helper method to invoke the OnOffersTriggered event.
        /// This method is called from OffersManager or PlayerManager when offers are triggered.
        /// </summary>
        /// <param name="offers">List of triggered offers.</param>
        private static void InvokeTriggeredOffers(List<Offer> offers)
        {
            OnOffersTriggered?.Invoke(offers);
        }

        #endregion Static Methods

        #region Instance Methods

        /// <summary>
        /// Retrieves all positioned offers.
        /// </summary>
        private PositionedOffer[] I_GetAllPositions()
        {
            return OffersHelperMethods.GetAllPositions();
        }

        /// <summary>
        /// Retrieves a specific positioned offer by its ID.
        /// </summary>
        private PositionedOffer I_GetPosition(string positionId)
        {
            return OffersHelperMethods.GetPosition(positionId);
        }

        /// <summary>
        /// Retrieves a list of offers for a specific position.
        /// </summary>
        private List<Offer> I_GetPositionedOffers(string positionId)
        {
            return OffersHelperMethods.GetPositionedOffers(positionId);
        }

        /// <summary>
        /// Retrieves the expiration date for the given offer.
        /// </summary>
        private DateTime I_GetOfferExpirationDate(string offerId)
        {
            return OffersHelperMethods.GetOfferExpirationDate(offerId);
        }

        /// <summary>
        /// Retrieves the raw duration object of the offer and optionally starts expiration.
        /// </summary>
        private OfferDuration I_GetOfferRawDuration(string offerId, bool startExpiration)
        {
            return OffersHelperMethods.GetOfferRawDuration(offerId, startExpiration);
        }

        /// <summary>
        /// Retrieves the raw duration object of the offer without starting expiration.
        /// </summary>
        private OfferDuration I_GetOfferRawDuration(string offerId)
        {
            return OffersHelperMethods.GetOfferRawDuration(offerId);
        }

        /// <summary>
        /// Starts the expiration timer for the specified offer and returns its due date.
        /// </summary>
        private DateTime I_StartOfferExpiration(string offerId)
        {
            return OffersHelperMethods.StartOfferExpiration(offerId);
        }

        /// <summary>
        /// Retrieves an offer by its unique identifier.
        /// </summary>
        private Offer I_GetOfferById(string offerId)
        {
            return OffersHelperMethods.GetOfferById(offerId);
        }

        /// <summary>
        /// Retrieves the content associated with an offer.
        /// </summary>
        private List<OfferContent> I_GetOfferContent(string offerId)
        {
            return OffersHelperMethods.GetOfferContent(offerId);
        }

        /// <summary>
        /// Retrieves all offers accessible to the user.
        /// </summary>
        private Offer[] I_GetAllOffers()
        {
            return OffersHelperMethods.GetAllOffers();
        }

        /// <summary>
        /// Retrieves all offers including those that might be restricted for the user.
        /// </summary>
        private Offer[] I_GetAllOffers(bool forceAllOffers)
        {
            return OffersHelperMethods.GetAllOffers(forceAllOffers);
        }

        /// <summary>
        /// Internal method to process the buying of an offer.
        /// Handles various cases including real-money IAPs, free offers, and entity-currency offers.
        /// </summary>
        private async Task<BuyOfferResult> I_BuyOffer(Offer offer, int? discount, Dictionary<string, object> analyticsEventCustomData)
        {
            return await OffersHelperMethods.BuyOffer(offer, discount, analyticsEventCustomData);
        }

        #endregion Instance Methods
    }
}