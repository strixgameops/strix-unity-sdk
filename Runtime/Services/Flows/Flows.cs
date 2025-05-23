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
        /// Given custom trigger id, executes a flow
        /// </summary>
        /// <param name="customTriggerId"></param>
        /// <returns>Dictionary of all custom variables that were set during flow execution.</returns>
        public static Dictionary<string, object> ExecuteFlow(string customTriggerId)
        {
            return Instance.I_ExecuteFlow(customTriggerId);
        }

        /// <summary>
        /// Should only be used internally by StrixSDK
        /// </summary>
        /// <param name="offer"></param>
        /// <returns></returns>
        public static Offer ExecuteFlow_OfferShown(Offer offer)
        {
            return Instance.I_ExecuteFlow_OfferShown(offer);
        }

        /// <summary>
        /// Should only be used internally by StrixSDK
        /// </summary>
        /// <param name="entityConfig"></param>
        /// <returns></returns>
        public static EntityConfig ExecuteFlow_ConfigParamRetrieved(EntityConfig entityConfig)
        {
            return Instance.I_ExecuteFlow_ConfigParamRetrieved(entityConfig);
        }

        #endregion Static methods

        #region Instance methods

        private Dictionary<string, object> I_ExecuteFlow(string customTriggerId)
        {
            return FlowsManager.Instance.ExecuteCustomFlow(customTriggerId);
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