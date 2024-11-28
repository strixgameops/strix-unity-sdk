using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StrixSDK;

public class AnalyticsExample : MonoBehaviour
{
    public void SendCustomDesignEvent()
    {
        var customData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };

        _ = Analytics.SendCustomEvent("level_1_end", customData);

        // Custom data can also be null in any event.
        //_ = Analytics.SendCustomEvent("level_1_end", null);
    }

    /// <summary>
    /// This event should not be used manually, but it is designed to.
    /// For simplicity, this event is called by a separate Offers.ShowOffer method, which also returns offer object
    /// </summary>
    public void SendOfferShownEvent()
    {
        Offer offer = Offers.GetOfferById("myOfferId");
        if (offer != null)
        {
            var customData = new Dictionary<string, object>()
            {
                { "someNumericData", 100 },
                { "someStringData", "myData" },
                { "someBooleanData", true }
            };
            _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, customData);
        }
    }

    /// <summary>
    /// Ideally, economy events should be used after a successful inventory operation to ensure that all data is consistent.
    /// Note that currencyId must be an existing entity's ID, and this entity must be marked as currency on the website
    /// </summary>
    public async void SendEconomyEvent()
    {
        var currencyId = "currency_gems";
        var moneyToGive = 500;
        var success = await Inventory.AddInventoryItem(currencyId, moneyToGive);
        if (success)
        {
            var customData = new Dictionary<string, object>()
            {
                { "someNumericData", 100 },
                { "someStringData", "myData" },
                { "someBooleanData", true }
            };
            _ = Analytics.SendEconomyEvent(currencyId, moneyToGive, EconomyTypes.source, "questReward", customData);
        }
    }

    /// <summary>
    /// This should be sent after an advertisement has been watched, in order to get time spent on it.
    /// </summary>
    public void SendAdEvent()
    {
        var secondsSpent = 32;
        var customData = new Dictionary<string, object>()
            {
                { "someNumericData", 100 },
                { "someStringData", "myData" },
                { "someBooleanData", true }
            };
        _ = Analytics.SendAdEvent("AdMob", AdTypes.rewarded, secondsSpent, customData);
    }

    /// <summary>
    /// May be used in many ways as regular crashlytics. Exceptions are caught automatically inside EventSender.cs script using LogCallback.
    /// </summary>
    public void SendReportEvent()
    {
        var customData = new Dictionary<string, object>()
        {
            { "someNumericData", 100 },
            { "someStringData", "myData" },
            { "someBooleanData", true }
        };
        _ = Analytics.SendReportEvent(SeverityTypes.warn, "lowFPS", "Player has FPS below 20 for more than 5 seconds", customData);
    }
}