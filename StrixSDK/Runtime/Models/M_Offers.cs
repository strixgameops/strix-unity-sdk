using Newtonsoft.Json;
using System.Collections.Generic;

namespace StrixSDK
{
    public class Offer
    {
        [JsonProperty("codename")]
        public string Id { get; set; } // Offer code-friendly ID specified in Offers web page

        [JsonProperty("id")]
        public string InternalId { get; set; } // Offer code-friendly ID specified in Offers web page

        [JsonProperty("content")]
        public List<OfferContent> Content { get; set; } // The content of the offer

        [JsonProperty("duration")]
        public OfferDuration Duration { get; set; } // Duration in seconds, specified in Offers web page

        [JsonProperty("icon")]
        public object Icon { get; set; } // A link to the cached icon. Retrieved separately by user

        [JsonProperty("name")]
        public string Name { get; set; } // Localized offer name.

        [JsonProperty("desc")]
        public string Desc { get; set; } // Localized offer description

        [JsonProperty("pricing")]
        public OfferPricing Pricing { get; set; } // Field used internally. Contains the full pricing table (discount applied)

        public OfferPrice Price { get; set; } // Synthethic field which is made when Get methods called and contains user's currency price item. If not a real-money IAP,

        [JsonProperty("purchaseLimit")]
        public int PurchaseLimit { get; set; } // Number of times this player can buy this offer

        [JsonProperty("segments")]
        public string[] Segments { get; set; } // An array of segment's IDs. Player must be in at least one of them to be able to retrieve the offer

        [JsonProperty("triggers")]
        public OfferTrigger[] Triggers { get; set; } // An array of triggers specified in Strix

        [JsonProperty("asku")]
        public string Asku { get; set; } // If this offer is a real-money offer, and can be bought (price is not 0), this value will identify it's SKU in app store system. Strix IAPs always start with "strix_"

        [JsonProperty("isValidIAP")]
        public bool IsValidIap { get; set; } // Indicate if this offer is currently in the app store system as an active IAP. Therefore, can be bought
    }

    public class OfferContent
    {
        [JsonProperty("amount")]
        public int Amount { get; set; } // Amount of a given entity

        [JsonProperty("nodeID")]
        public string NodeId { get; set; } // Strix's internal node id of this item. Shouldn't be used

        public string EntityId { get; set; } // Code-friendly name of this item, like "my_item_id"
    }

    public class OfferPricing
    {
        [JsonProperty("discount")]
        public int Discount { get; set; } // Value in range from 0 to 100

        [JsonProperty("moneyCurr")]
        public OfferPricingCurrency[] Currencies { get; set; } // An array of regional pricing

        [JsonProperty("targetCurrency")]
        public string Type { get; set; } // May be "money" or "entity"

        [JsonProperty("nodeID")]
        public string NodeId { get; set; } // Internally-used field that indicates entity node id. Later used for "Currency" field in OfferPrice

        [JsonProperty("amount")]
        public int Amount { get; set; } // If "entity" Type, that would be an amount of "entities" a player needs to pay for this offer
    }

    public class OfferDuration
    {
        [JsonProperty("timeUnit")]
        public string TimeUnit { get; set; } // Can be "days", "hours", "minutes" and "seconds".

        [JsonProperty("value")]
        public int Value { get; set; } // Amount of days, hours e.t.c.
    }

    public class OfferTrigger
    {
        [JsonProperty("category")]
        public string Category { get; set; } // elements / segments / inventory

        [JsonProperty("condition")]
        public string Condition { get; set; } // onChange / onEnter / onExit / onRedeem / onLose

        [JsonProperty("subject")]
        public string Subject { get; set; } // Internal ID of the PW template / segment / entity nodeID

        [JsonProperty("subjectCondition")]
        public string Value { get; set; } // Value

        [JsonProperty("subjectConditionSecondary")]
        public string ValueSecondary { get; set; } // If trigger is a range value (for example 50-100), 100 would be the secondary value
    }

    public class OfferPricingCurrency
    {
        [JsonProperty("currency")]
        public string CurrencyCode { get; set; } // Example: "USD"

        [JsonProperty("region")]
        public string Region { get; set; } // Example: "BH"

        [JsonProperty("value")]
        public float Value { get; set; } // Price. Example: 1.99
    }

    public class OfferPrice
    {
        public float Value { get; set; } // Price for real-money IAP, "amount" for hard/soft currency offers
        public string Currency { get; set; } // E.g. "USD" for real-money IAPs / "myHardCurrencyGemsId" for offer that costs "49 gems"
    }

    public class OfferPurchaseResult
    {
        public bool IsSuccessful { get; set; }
        public string Message { get; set; }
        public bool IsValidated { get; set; }
    }

    //
    // Positioned offers
    //
    public class PositionedOffer
    {
        [JsonProperty("id")]
        public string InternalId { get; set; } // Internal id used in Strix

        [JsonProperty("codename")]
        public string Id { get; set; } // Example: "MyPlaceInGameWhereIWantSomeOffer"

        [JsonProperty("segments")]
        public List<PositionedOfferSegment> Segments { get; set; } // Segments array, sorted by priority ("everyone" segment is always the last & the last segment that will be checked)
    }

    public class PositionedOfferSegment
    {
        [JsonProperty("id")]
        public string SegmentId { get; set; } // Internal segment ID

        [JsonProperty("codename")]
        public List<string> Offers { get; set; } // An array of internal offer IDs
    }
}