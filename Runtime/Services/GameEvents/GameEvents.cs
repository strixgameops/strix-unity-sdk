using StrixSDK.Runtime;
using System.Threading.Tasks;
using UnityEngine;

using System.Collections.Generic;
using System;

namespace StrixSDK
{
    public class GameEvents : MonoBehaviour
    {
        #region References

        private static GameEvents _instance;

        public static GameEvents Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<GameEvents>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<GameEvents>();
                        obj.name = typeof(GameEvents).ToString();
                    }
                }
                return _instance;
            }
        }

        private void Start()
        {
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Internally used method. Used to get player's segments and determine if he falls in any event. If so, adds artificial segment and use it
        /// to get stuff.
        /// </summary>
        /// <param name="playerSegments"></param>
        /// <returns></returns>
        public static List<string> AddGameEventSegments(List<string> playerSegments)
        {
            return Instance.I_AddGameEventSegments(playerSegments);
        }

        #endregion Static methods

        #region Instance methods

        private List<string> I_AddGameEventSegments(List<string> playerSegments)
        {
            return GameEventsManager.Instance.AddGameEventSegments(playerSegments);
        }

        #endregion Instance methods
    }
}