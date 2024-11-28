using UnityEngine;
using StrixSDK.Runtime;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace StrixSDK
{
    public class Flows : MonoBehaviour
    {
        #region References

        private static Flows _instance;

        public static Flows Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Flows>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Flows>();
                        obj.name = typeof(Flows).ToString();
                    }
                }
                return _instance;
            }
        }

        private void Start()
        {
            if (!Strix.IsInitialized)
            {
                Debug.LogError($"StrixSDK isn't initialized. Entity system is not available.");
                Destroy(gameObject);
            }
        }

        #endregion References

        #region Static methods

        /// <summary>
        /// Given custom trigger id, executes such flow
        /// </summary>
        /// <param name="customTriggerId"></param>
        /// <returns></returns>
        public static void ExecuteFlow(string customTriggerId)
        {
            Instance.I_ExecuteFlow(customTriggerId);
        }

        public static Offer ExecuteFlow_OfferShown(Offer offer)
        {
            return Instance.I_ExecuteFlow_OfferShown(offer);
        }

        public static EntityConfig ExecuteFlow_ConfigParamRetrieved(EntityConfig entityConfig)
        {
            return Instance.I_ExecuteFlow_ConfigParamRetrieved(entityConfig);
        }

        #endregion Static methods

        #region Instance methods

        private void I_ExecuteFlow(string customTriggerId)
        {
            FlowsManager.Instance.ExecuteCustomFlow(customTriggerId);
        }

        private Offer I_ExecuteFlow_OfferShown(Offer offer)
        {
            return FlowsManager.Instance.ExecuteFlow_OfferShown(offer);
        }

        private EntityConfig I_ExecuteFlow_ConfigParamRetrieved(EntityConfig entityConfig)
        {
            return FlowsManager.Instance.ExecuteFlow_ConfigParamRetrieved(entityConfig);
        }

        #endregion Instance methods
    }
}