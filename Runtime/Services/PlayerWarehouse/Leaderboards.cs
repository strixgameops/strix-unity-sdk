using StrixSDK.Runtime;
using StrixSDK.Runtime.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace StrixSDK
{
    public class Leaderboards : MonoBehaviour
    {
        #region References

        private static Leaderboards _instance;

        public static Leaderboards Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Leaderboards>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Leaderboards>();
                        obj.name = typeof(Leaderboards).ToString();
                    }
                }
                return _instance;
            }
        }

        private PlayerManager playerManagerInstance;

        private void Start()
        {
            if (!Strix.IsInitialized)
            {
                Debug.LogError($"StrixSDK isn't initialized. Leaderboards system is not available.");
                Destroy(gameObject);
            }
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Gets current state of a given leaderboard. Returns an array of tops, where index of each top represents index of leaderboard's timeframe.
        /// So timeframes "day, week, month" would associate as 0 for "day", 1 for "week", 2 for "month" accordingly.
        /// Current player's score state is always appended to the returned top. For example, in top-5, current player will be 6th.
        /// If player isn't present in leaderboard, his top will be -1. If player has no element values, configured defaults will be returned when possible.
        /// </summary>
        /// <param name="leaderboardId"></param>
        /// <returns></returns>
        public static async Task<List<LeaderboardTimeframe>> GetLeaderboard(string leaderboardId, string? groupElementId, string? groupElementValue)
        {
            return await Instance.I_GetLeaderboard(leaderboardId, groupElementId, groupElementValue);
        }

        #endregion Static methods

        #region Instance methods

        private async Task<List<LeaderboardTimeframe>> I_GetLeaderboard(string leaderboardId, string? groupElementId, string? groupElementValue)
        {
            return await WarehouseHelperMethods.GetLeaderboard(leaderboardId, groupElementId, groupElementValue);
        }

        #endregion Instance methods
    }
}