using StrixSDK.Runtime.Utils;
using System.Collections;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;
using System;

namespace StrixSDK.Runtime
{
    public class GameEventsManager : MonoBehaviour
    {
        private static GameEventsManager _instance;

        public static GameEventsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameEventsManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<GameEventsManager>();
                        obj.name = typeof(GameEventsManager).ToString();
                    }
                }
                return _instance;
            }
        }

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

        public GameEvent[] _gameEvents;

        public void RefreshEvents()
        {
            List<GameEvent> eventsList = new List<GameEvent>();
            var eventsDocs = Content.LoadAllFromFile("events");

            if (eventsDocs != null)
            {
                foreach (var doc in eventsDocs)
                {
                    string json = JsonConvert.SerializeObject(doc);

                    GameEvent ev = JsonConvert.DeserializeObject<GameEvent>(json);
                    eventsList.Add(ev);
                }

                _gameEvents = eventsList.ToArray();
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Fetched {_gameEvents.Length} game events");
            }
            else
            {
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Could not fetch game events from persistent storage");
            }
        }

        public bool Initialize()
        {
            try
            {
                RefreshEvents();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Could not initialize GameEventsManager. Error: {e}");
                return false;
            }
        }

        /// <summary>
        /// Use this method to recalculate ongoing events on the fly when trying to get some entities, offers or other stuff.
        /// This way we try to eliminate the situation where an event has already ended but user can still call stuff that should not be available anymore.
        /// </summary>
        /// <param name="playerSegments"></param>
        /// <returns></returns>
        public List<string> AddGameEventSegments(List<string> playerSegments)
        {
            List<string> modifiedSegments = playerSegments;

            // Make it to kinda reset the list in case it already has game event segments
            modifiedSegments.RemoveAll(s => s.StartsWith("gameevent_"));

            var events = GetOngoingEvents(modifiedSegments);

            foreach (var ev in events)
            {
                var check = CheckSegmentWhitelistAndBlacklist(ev.SegmentsWhitelist, ev.SegmentsBlacklist, modifiedSegments);
                if (!check) continue;
                modifiedSegments.Add($"gameevent_{ev.Id}");
            }

            return modifiedSegments;
        }

        private bool CheckSegmentWhitelistAndBlacklist(List<string> whitelist, List<string> blacklist, List<string> segments)
        {
            if (whitelist.Count > 0)
            {
                foreach (var s in whitelist)
                {
                    if (!segments.Contains(s))
                    {
                        return false;
                    }
                }
            }
            if (blacklist.Count > 0)
            {
                foreach (var s in blacklist)
                {
                    if (segments.Contains(s))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        private List<GameEvent> GetOngoingEvents(List<string> playerSegments)
        {
            var events = new List<GameEvent>();
            foreach (var ev in _gameEvents)
            {
                var check = CheckSegmentWhitelistAndBlacklist(ev.SegmentsWhitelist, ev.SegmentsBlacklist, playerSegments);
                if (!check) continue;
                List<DateTime> dates = ev.Occasions.Select(date => DateTime.Parse(date).ToUniversalTime()).ToList();

                DateTime now = DateTime.UtcNow;

                // Get the closest event occasion date before current date
                DateTime? startDate = dates
                    .Where(date => date <= now)
                    .OrderByDescending(date => date)
                    .FirstOrDefault();

                // Add event duration and calculate if we're in range
                if (startDate.HasValue)
                {
                    DateTime endDate = startDate.Value.AddMinutes(ev.Duration);

                    if (now >= startDate && now <= endDate)
                    {
                        events.Add(ev);
                    }
                }
            }
            return events;
        }

        /// <summary>
        /// Given offer's internal ID (without "|" symbol), determine if it is currently changed by any ongoing event. If so, return ABC|123 id of changed offer.
        /// </summary>
        public Offer TryGetChangedOfferFromOngoingEvents(List<string> playerSegments, string offerInternalId, List<Offer> allOffers)
        {
            var events = GetOngoingEvents(playerSegments);
            foreach (var ev in events)
            {
                var offer = allOffers.First(o => o.InternalId == $"{offerInternalId}|{ev.Id}");
                if (offer != null)
                {
                    return offer;
                }
            }
            return null;
        }
    }
}