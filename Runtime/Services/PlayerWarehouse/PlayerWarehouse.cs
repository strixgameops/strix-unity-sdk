using StrixSDK.Runtime;
using System.Threading.Tasks;
using UnityEngine;

namespace StrixSDK
{
    public class PlayerWarehouse : MonoBehaviour
    {
        #region References

        private static PlayerWarehouse _instance;

        public static PlayerWarehouse Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<PlayerWarehouse>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<PlayerWarehouse>();
                        obj.name = typeof(PlayerWarehouse).ToString();
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
                Debug.LogError($"StrixSDK isn't initialized. Player Warehouse system is not available.");
                Destroy(gameObject);
            }
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Gets current player's element value. If value was never set before, fallbacks to the default value (if any).
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public static object GetPlayerElementValue(string elementId)
        {
            return Instance.I_GetPlayerElementValue(elementId);
        }

        /// <summary>
        /// Same as the regular GetPlayerElementValue(), but fetches it from the server.
        /// </summary>
        /// <param name="elementId"></param>
        /// <returns></returns>
        public static async Task<object> GetPlayerElementValueAsync(string elementId)
        {
            return await Instance.I_GetPlayerElementValueAsync(elementId);
        }

        /// <summary>
        /// Sets player's element to the specified value. Clamped by min-max values, if exceeds.
        /// </summary>
        /// <param name="elementId"></param>
        /// <param name="value">Value to set to</param>
        /// <returns>The value after change</returns>
        public static object SetPlayerElementValue(string elementId, object value)
        {
            return Instance.I_SetPlayerElementValue(elementId, value);
        }

        /// <summary>
        /// Adds the specified value to the player's element. Clamped by min-max values, if exceeds.
        /// </summary>
        /// <param name="elementId"></param>
        /// <param name="value">Value to add</param>
        /// <returns>The value after change</returns>
        public static object AddPlayerElementValue(string elementId, object value)
        {
            return Instance.I_AddPlayerElementValue(elementId, value);
        }

        /// <summary>
        /// Subtracts the specified value from the player's element. Clamped by min-max values, if exceeds.
        /// </summary>
        /// <param name="elementId"></param>
        /// <param name="value">Value to subtract</param>
        /// <returns>The value after change</returns>
        public static object SubtractPlayerElementValue(string elementId, object value)
        {
            return Instance.I_SubtractPlayerElementValue(elementId, value);
        }

        #endregion Static methods

        #region Instance methods

        private object I_GetPlayerElementValue(string elementId)
        {
            return WarehouseHelperMethods.GetPlayerElementValue(elementId);
        }

        private async Task<object> I_GetPlayerElementValueAsync(string elementId)
        {
            return await WarehouseHelperMethods.GetPlayerElementValueAsync(elementId);
        }

        private object I_SetPlayerElementValue(string elementId, object value)
        {
            return WarehouseHelperMethods.SetPlayerElementValue(elementId, value);
        }

        private object I_AddPlayerElementValue(string elementId, object value)
        {
            return WarehouseHelperMethods.AddPlayerElementValue(elementId, value);
        }

        private object I_SubtractPlayerElementValue(string elementId, object value)
        {
            return WarehouseHelperMethods.SubtractPlayerElementValue(elementId, value);
        }

        #endregion Instance methods
    }
}