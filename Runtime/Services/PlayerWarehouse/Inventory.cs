using StrixSDK.Runtime;
using StrixSDK.Runtime.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace StrixSDK
{
    public class Inventory : MonoBehaviour
    {
        #region References

        private static Inventory _instance;

        public static Inventory Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Inventory>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Inventory>();
                        obj.name = typeof(Inventory).ToString();
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
                Debug.Log($"StrixSDK isn't initialized. Inventory system is not available.");
                Destroy(gameObject);
            }
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Get player's inventory. Quantities are returned as strings and must be parsed manually.
        /// </summary>
        /// <returns></returns>
        public static async Task<List<InventoryItem>> GetInventoryItems()
        {
            return await Instance.I_GetInventoryItems();
        }

        /// <summary>
        /// Gets how many items with a given entityId does player have. Always returns string that has to be parsed manually.
        /// </summary>
        /// <returns></returns>
        public static async Task<string> GetInventoryItemAmount(string entityId)
        {
            return await Instance.I_GetInventoryItemAmount(entityId);
        }

        /// <summary>
        /// Adds N amount of X items into player's inventory. Make sure to provide existing entityId. Returns true if operation was successful.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> AddInventoryItem(string entityId, int amount)
        {
            return await Instance.I_AddInventoryItem(entityId, amount);
        }

        /// <summary>
        /// Removes N amount of X items into player's inventory. Make sure to provide existing entityId. Returns true if operation was successful.
        /// </summary>
        /// <returns></returns>
        public static async Task<bool> RemoveInventoryItem(string entityId, int amount)
        {
            return await Instance.I_RemoveInventoryItem(entityId, amount);
        }

        #endregion Static methods

        #region Instance methods

        private async Task<List<InventoryItem>> I_GetInventoryItems()
        {
            return await WarehouseHelperMethods.GetInventoryItems();
        }

        private async Task<string> I_GetInventoryItemAmount(string entityId)
        {
            return await WarehouseHelperMethods.GetInventoryItemAmount(entityId);
        }

        private async Task<bool> I_AddInventoryItem(string entityId, int amount)
        {
            return await WarehouseHelperMethods.AddInventoryItem(entityId, amount);
        }

        private async Task<bool> I_RemoveInventoryItem(string entityId, int amount)
        {
            return await WarehouseHelperMethods.RemoveInventoryItem(entityId, amount);
        }

        #endregion Instance methods
    }
}