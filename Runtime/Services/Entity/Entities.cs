using UnityEngine;
using StrixSDK.Runtime;
using System.Threading.Tasks;

namespace StrixSDK
{
    public class Entities : MonoBehaviour
    {
        #region References

        private static Entities _instance;

        public static Entities Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<Entities>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<Entities>();
                        obj.name = typeof(Entities).ToString();
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
        /// Get entity object by it's code-friendly entity ID.
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static Entity GetEntityById(string entityId)
        {
            return Instance.I_GetEntityById(entityId);
        }

        /// <summary>
        /// Get all existing entities. Configs are not included.
        /// </summary>
        /// <returns></returns>
        public static Entity[] GetAllEntities()
        {
            return Instance.I_GetAllEntities();
        }

        /// <summary>
        /// Get the specified config from the specified entity.
        /// </summary>
        /// <param name="entityId"></param>
        /// <param name="configId"></param>
        /// <returns></returns>
        public static EntityConfig GetEntityConfig(string entityId, string configId)
        {
            return Instance.I_GetEntityConfig(entityId, configId);
        }

        /// <summary>
        /// Get the all configs from the specified entity.
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static EntityConfig[] GetAllEntityConfigs(string entityId)
        {
            return Instance.I_GetAllEntityConfigs(entityId);
        }

        /// <summary>
        /// Given the config and target field name, returns it's value.
        /// <br/>
        /// If image, returns Texture2D | If video, returns VideoPlayer | If sound, returns raw base64
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fieldKey"></param>
        /// <returns></returns>
        public static object GetConfigValue(EntityConfig config, string fieldKey)
        {
            return Instance.I_GetConfigValue(config, fieldKey);
        }

        /// <summary>
        /// Given the config and target field name, returns it's value. "Any file" values are returned as byte[]
        /// <br/>
        /// <br/>
        /// <br/>
        /// If autoConvert = true:
        /// <br/>
        /// If image, returns Texture2D | If video, returns VideoPlayer | If sound, returns raw base64.
        /// <br/>
        /// <br/>
        /// If autoConvert = false:
        /// <br/>
        /// If image, returns raw base64 | If video, returns raw base64 | If sound, returns raw base64.
        /// </summary>
        /// <param name="config"></param>
        /// <param name="fieldKey"></param>
        /// <returns></returns>
        public static object GetConfigValue(EntityConfig config, string fieldKey, bool autoConvert)
        {
            return Instance.I_GetConfigValue(config, fieldKey, autoConvert);
        }

        /// <summary>
        /// Will return direct children of the given entity, if it is a category & children do exist.
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static Entity[] GetEntityChildren(string entityId)
        {
            return Instance.I_GetEntityChildren(entityId);
        }

        /// <summary>
        /// Given the entity, returns it's direct parent's entity object
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static Entity GetEntityParent(string entityId)
        {
            return Instance.I_GetEntityParent(entityId);
        }

        #endregion Static methods

        #region Instance methods

        private Entity I_GetEntityById(string entityId)
        {
            return EntityHelperMethods.GetEntityById(entityId);
        }

        private Entity[] I_GetAllEntities()
        {
            return EntityHelperMethods.GetAllEntities();
        }

        private EntityConfig[] I_GetAllEntityConfigs(string entityId)
        {
            return EntityHelperMethods.GetAllEntityConfigs(entityId);
        }

        private EntityConfig I_GetEntityConfig(string entityId, string configId)
        {
            return EntityHelperMethods.GetEntityConfig(entityId, configId);
        }

        private object I_GetConfigValue(EntityConfig config, string fieldKey)
        {
            return EntityHelperMethods.GetConfigValue(config, fieldKey, true);
        }

        private object I_GetConfigValue(EntityConfig config, string fieldKey, bool autoConvert)
        {
            return EntityHelperMethods.GetConfigValue(config, fieldKey, autoConvert);
        }

        private Entity[] I_GetEntityChildren(string entityId)
        {
            return EntityHelperMethods.GetEntityChildren(entityId);
        }

        private Entity I_GetEntityParent(string entityId)
        {
            return EntityHelperMethods.GetEntityParent(entityId);
        }

        #endregion Instance methods
    }
}