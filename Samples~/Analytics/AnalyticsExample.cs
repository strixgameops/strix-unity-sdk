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
        _ = Analytics.SendCustomEvent("level_1_end", null);
    }

    /// <summary>
    /// This event should not be used manually, but it is available for custom implementations.
    /// For simplicity, this event is called automatically by Offers.ShowOffer method.
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

            // Send offer shown event with discount (can be null)
            _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, 10, customData); // 10% discount

            // Send offer shown event without discount
            _ = Analytics.SendOfferShownEvent(offer.InternalId, offer.Price.Value, null, customData);
        }
    }

    /// <summary>
    /// It is called automatically, see the source code of Inventory and Offers.
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
    /// May be used in many ways as regular bug reports or abnormal behavior. Exceptions are not caught by Strix, currently.
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

    /// <summary>
    /// Example of session events - these are typically called automatically by the SDK
    /// </summary>
    public void SessionEventExamples()
    {
        var customData = new Dictionary<string, object>()
        {
            { "sessionSource", "tutorial" },
            { "playerLevel", 1 }
        };

        // New session event (called automatically on SDK initialization)
        _ = Analytics.SendNewSessionEvent(true, customData); // true = new player

        // End session event (called automatically on app quit/pause)
        _ = Analytics.SendEndSessionEvent(customData);
    }

    /// <summary>
    /// Example of progression tracking with custom events
    /// </summary>
    public void ProgressionTrackingExample()
    {
        // Level start
        var levelStartData = new Dictionary<string, object>()
        {
            { "levelNumber", 5 },
            { "difficulty", "normal" },
            { "playerLevel", 12 }
        };
        _ = Analytics.SendCustomEvent("level_start", levelStartData);

        // Level complete
        var levelCompleteData = new Dictionary<string, object>()
        {
            { "levelNumber", 5 },
            { "timeSpent", 145.5f },
            { "score", 2850 },
            { "attempts", 1 }
        };
        _ = Analytics.SendCustomEvent("level_complete", levelCompleteData);

        // Level failed
        var levelFailedData = new Dictionary<string, object>()
        {
            { "levelNumber", 6 },
            { "timeSpent", 87.2f },
            { "attempts", 3 },
            { "failReason", "timeout" }
        };
        _ = Analytics.SendCustomEvent("level_failed", levelFailedData);
    }
}