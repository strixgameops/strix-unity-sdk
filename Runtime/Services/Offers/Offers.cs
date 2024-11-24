using StrixSDK.Runtime;
using System.Threading.Tasks;
using UnityEngine;

using System.Collections.Generic;
using System;

namespace StrixSDK
{
    public class Offers : MonoBehaviour
    {
        #region References

        private static Offers _instance;

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

        private OffersManager offersManagerInstance;

        private void Awake()
        {
            if (!Strix.IsInitialized)
            {
                Debug.Log($"StrixSDK isn't initialized. Offers system is not available.");
                Destroy(gameObject);
            }
            OffersHelperMethods.OnOffersTriggered += InvokeTriggeredOffers;
        }

        private void OnDestroy()
        {
            OffersHelperMethods.OnOffersTriggered -= InvokeTriggeredOffers;
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Returns offer with given ID, if it exists. Returns a variation of this offer if AB test conditions met from both sides.
        /// </summary>
        public static Offer GetOfferById(string offerId)
        {
            return Instance.I_GetOfferById(offerId);
        }

        /// <summary>
        /// Returns offer with given ID, if it exists. Also sends analytics event which helps tracking offer decline rate. Also triggers the start of expiration, if duration for the offer is set. User won't be able to retrieve the offer after it's time is up.
        /// </summary>
        public static Offer ShowOffer(string offerId, Dictionary<string, object> analyticsEventCustomData)
        {
            Offer offer = Instance.I_GetOfferById(offerId);
            if (offer != null)
            {
                offer = FlowsManager.Instance.ExecuteFlow_OfferShown(offer);
                Instance.I_StartOfferExpiration(offerId);
                _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, analyticsEventCustomData);
            }
            return offer;
        }

        /// <summary>
        /// Returns offer with given ID, if it exists. Also sends analytics event which helps tracking offer decline rate. Conditionally triggers the start of expiration,
        /// if duration for the offer is set. User won't be able to retrieve the offer after it's time is up.
        /// </summary>
        public static Offer ShowOffer(string offerId, bool startExpiration, Dictionary<string, object> analyticsEventCustomData)
        {
            Offer offer = Instance.I_GetOfferById(offerId);

            if (offer != null)
            {
                offer = FlowsManager.Instance.ExecuteFlow_OfferShown(offer);
                if (startExpiration)
                {
                    Instance.I_StartOfferExpiration(offerId);
                }
                _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, analyticsEventCustomData);
            }
            return offer;
        }

        /// <summary>
        /// Returns all offers the user can access. Automatically gets variations of offers if they are AB tested and the user is in the test.
        /// </summary>
        public static Offer[] GetAllOffers()
        {
            return Instance.I_GetAllOffers();
        }

        /// <summary>
        /// Returns all offers, even those the user cannot access normally. Though the offers may be retrieved, other calls to them as like "BuyOffer" will fail.
        /// </summary>
        public static Offer[] GetAllOffers(bool forceAllOffers)
        {
            return Instance.I_GetAllOffers(forceAllOffers);
        }

        /// <summary>
        /// Returns all offer's content.
        /// </summary>
        public static List<OfferContent> GetOfferContent(string offerId)
        {
            return Instance.I_GetOfferContent(offerId);
        }

        /// <summary>
        /// Starts the process of buying, if the offer is a real-money IAP, and validate the transaction on Strix's end. If offer has a price of 0, it is treated as non-IAP and considered free. If the offer is entity-currency offer, simply sends the event to the server.
        /// </summary>
        public static async Task<bool> BuyOffer(string offerId, Dictionary<string, object> analyticsEventCustomData)
        {
            return await Instance.I_BuyOffer(offerId, analyticsEventCustomData);
        }

        /// <summary>
        /// Returns all positions and their segments and offers from Positioned Offers.
        /// </summary>
        public static PositionedOffer[] GetAllPositions()
        {
            return Instance.I_GetAllPositions();
        }

        /// <summary>
        /// Returns a position item with it's segments and offers.
        /// </summary>
        public static PositionedOffer GetPosition(string positionId)
        {
            return Instance.I_GetPosition(positionId);
        }

        /// <summary>
        /// Returns a list of offers for this current user. Automatically resolves segments between the position and player.
        /// </summary>
        public static List<Offer> GetPositionedOffers(string positionId)
        {
            return Instance.I_GetPositionedOffers(positionId);
        }

        /// <summary>
        /// Returns current due date of the offer.
        /// </summary>
        public static DateTime GetOfferExpirationDate(string offerId)
        {
            return Instance.I_GetOfferExpirationDate(offerId);
        }

        /// <summary>
        /// Returns offer's raw duration object & conditionally start expiration.
        /// </summary>
        public static OfferDuration GetOfferRawDuration(string offerId, bool startExpiration)
        {
            return Instance.I_GetOfferRawDuration(offerId, startExpiration);
        }

        /// <summary>
        /// Returns offer's raw duration object. NOTE THIS WON'T START OFFER'S EXPIRATION BY DEFAULT! CALL GetOfferRawDuration(string offerId, bool startExpiration) INSTEAD!
        /// </summary>
        public static OfferDuration GetOfferRawDuration(string offerId)
        {
            return Instance.I_GetOfferRawDuration(offerId);
        }

        /// <summary>
        /// Starts offer's expiration and returns the resulted due date.
        /// </summary>
        public static DateTime StartOfferExpiration(string offerId)
        {
            return Instance.I_StartOfferExpiration(offerId);
        }

        /// <summary>
        /// An event that will return a list of all offers triggered by some actions.
        /// </summary>
        public static event Action<List<Offer>> OnOffersTriggered;

        /// <summary>
        /// Internal method to invoke OnOffersTriggered event. Called from OffersManager or PlayerManager
        /// </summary>
        /// <param name="offers"></param>
        private static void InvokeTriggeredOffers(List<Offer> offers)
        {
            OnOffersTriggered?.Invoke(offers);
        }

        #endregion Static methods

        #region Instance methods

        private PositionedOffer[] I_GetAllPositions()
        {
            return OffersHelperMethods.GetAllPositions();
        }

        private PositionedOffer I_GetPosition(string positionId)
        {
            return OffersHelperMethods.GetPosition(positionId);
        }

        private List<Offer> I_GetPositionedOffers(string positionId)
        {
            return OffersHelperMethods.GetPositionedOffers(positionId);
        }

        private DateTime I_GetOfferExpirationDate(string offerId)
        {
            return OffersHelperMethods.GetOfferExpirationDate(offerId);
        }

        private OfferDuration I_GetOfferRawDuration(string offerId, bool startExpiration)
        {
            return OffersHelperMethods.GetOfferRawDuration(offerId, startExpiration);
        }

        private OfferDuration I_GetOfferRawDuration(string offerId)
        {
            return OffersHelperMethods.GetOfferRawDuration(offerId);
        }

        private DateTime I_StartOfferExpiration(string offerId)
        {
            return OffersHelperMethods.StartOfferExpiration(offerId);
        }

        private Offer I_GetOfferById(string offerId)
        {
            return OffersHelperMethods.GetOfferById(offerId);
        }

        private List<OfferContent> I_GetOfferContent(string offerId)
        {
            return OffersHelperMethods.GetOfferContent(offerId);
        }

        private Offer[] I_GetAllOffers()
        {
            return OffersHelperMethods.GetAllOffers();
        }

        private Offer[] I_GetAllOffers(bool forceAllOffers)
        {
            return OffersHelperMethods.GetAllOffers(forceAllOffers);
        }

        private async Task<bool> I_BuyOffer(string offerId, Dictionary<string, object> analyticsEventCustomData)
        {
            return await OffersHelperMethods.BuyOffer(offerId, analyticsEventCustomData);
        }

        #endregion Instance methods
    }
}