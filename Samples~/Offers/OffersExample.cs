using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;

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
        // it will be appended to purchase event that is sent automatically once offer is purchasesd.
        var analyticsEventCustomData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };
        // analyticsEventCustomData can also be null
        Offer offer2 = Offers.ShowOffer("myOffer", analyticsEventCustomData);
    }

    /// <summary>
    /// Example method to buy IAP-offer
    /// </summary>
    public async void BuyIAP()
    {
        // Custom data may be provided (or be null). If provided,
        // it will be appended to purchase event that is sent automatically once offer is purchasesd.
        var analyticsEventCustomData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };
        // analyticsEventCustomData can also be null
        bool success = await Offers.BuyOffer("myOffer", analyticsEventCustomData); // Purchase window will appear at this point

        // Inventory.Add or any purchase event sending methods are not needed there, as Offers.BuyOffer method calls them automatically
    }

    /// <summary>
    /// Example method to buy in-game currency offer
    /// </summary>
    public async void Buy()
    {
        // Custom data may be provided (or be null). If provided,
        // it will be appended to purchase event that is sent automatically once offer is purchasesd.
        var analyticsEventCustomData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };
        // analyticsEventCustomData can also be null
        bool success = await Offers.BuyOffer("myOffer", analyticsEventCustomData); // If player has currency in it's inventory, offer will be bought.

        // Inventory.Add or any purchase event sending methods are not needed there, as Offers.BuyOffer method calls them automatically
    }

    /// <summary>
    /// Simple example of using positioned offers
    /// </summary>
    public void GetPositionOffers()
    {
        List<Offer> offers = Offers.GetPositionedOffers("level_ending");
    }
}