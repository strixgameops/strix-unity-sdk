using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;
using StrixSDK.Runtime;
using System;
using System.Diagnostics;

public class OffersExample : MonoBehaviour
{
    public void GetOffers()
    {
        // Returns all offers that the game has, including IAPs and for soft/hard currencies.
        Offer[] offers = Offers.GetAllOffers();

        // Method with "true" will return all offers, even those that are not accessible due to segment restriction.
        Offer[] allOffers = Offers.GetAllOffers(true);
    }

    public void GetOfferContent()
    {
        List<OfferContent> content = Offers.GetOfferContent("myOffer");

        foreach (OfferContent item in content)
        {
            var entityId = item.EntityId; // Entity Id that can be used to call entity object
            var amount = item.Amount; // Amount of entities. For example, if you want to give 300 coins
        }
    }

    /// <summary>
    /// Example method to show (get) offer. ShowOffer should be called in cases when it is certain that offer will be shown to a player,
    /// and we just want a shortcut that will return offer object to us and automatically send an event
    /// </summary>
    public void ShowOffer()
    {
        // In case if we only want to simply get an offer
        Offer offer1 = Offers.GetOfferById("myOffer");

        // Custom data may be provided (or be null). If provided,
        // it will be appended to purchase event that is sent automatically once offer is purchased.
        var analyticsEventCustomData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };
        // analyticsEventCustomData can also be null
        Offer offer2 = Offers.ShowOffer("myOffer", analyticsEventCustomData);

        // ShowOffer with discount parameter
        Offer offer3 = Offers.ShowOffer("myOffer", 10, analyticsEventCustomData); // 10% discount

        // ShowOffer with control over expiration start
        Offer offer4 = Offers.ShowOffer("myOffer", true, 15, analyticsEventCustomData); // start expiration, 15% discount
    }

    /// <summary>
    /// Example method to buy offers (both IAP and in-game currency)
    /// </summary>
    public async void BuyOffer()
    {
        // Get the offer first
        Offer offer = Offers.GetOfferById("myOffer");
        if (offer == null) return;

        // Custom data may be provided (or be null). If provided,
        // it will be appended to purchase event that is sent automatically once offer is purchased.
        var analyticsEventCustomData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };

        // Buy offer - returns BuyOfferResult instead of bool
        var result = await Offers.BuyOffer(offer, analyticsEventCustomData);

        if (result.Success)
        {
            Debug.Log("Purchase successful!");
        }
        else
        {
            Debug.LogError($"Purchase failed with code: {result.MessageCode}");
        }

        // Buy offer with discount
        var resultWithDiscount = await Offers.BuyOffer(offer, 20, analyticsEventCustomData); // 20% discount
    }

    /// <summary>
    /// Simple example of using positioned offers
    /// </summary>
    public void GetPositionOffers()
    {
        List<Offer> offers = Offers.GetPositionedOffers("level_ending");

        // Get all positions
        PositionedOffer[] allPositions = Offers.GetAllPositions();

        // Get specific position
        PositionedOffer position = Offers.GetPosition("level_ending");
    }

    /// <summary>
    /// Example of working with offer durations and expiration
    /// </summary>
    public void OfferDurationExamples()
    {
        string offerId = "myOffer";

        // Get offer duration without starting expiration
        OfferDuration duration = Offers.GetOfferRawDuration(offerId);

        // Get offer duration and start expiration
        OfferDuration durationWithExpiration = Offers.GetOfferRawDuration(offerId, true);

        // Start offer expiration manually
        DateTime expirationDate = Offers.StartOfferExpiration(offerId);

        // Get current expiration date
        DateTime currentExpiration = Offers.GetOfferExpirationDate(offerId);

        // Get linked entities for this offer
        List<string> linkedEntities = Offers.GetOfferLinkedEntitiesIDs(offerId);
    }

    /// <summary>
    /// Example of listening to offer triggers
    /// </summary>
    private void Start()
    {
        // Subscribe to offer triggers event
        Offers.OnOffersTriggered += OnOffersTriggered;
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        Offers.OnOffersTriggered -= OnOffersTriggered;
    }

    private void OnOffersTriggered(List<Offer> triggeredOffers)
    {
        Debug.Log($"Received {triggeredOffers.Count} triggered offers");
        foreach (var offer in triggeredOffers)
        {
            Debug.Log($"Offer triggered: {offer.Id}");
        }
    }
}