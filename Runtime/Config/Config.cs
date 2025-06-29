using UnityEngine;

namespace StrixSDK.Runtime.Config
{
    [System.Serializable]
    public class StrixSDKConfig : ScriptableObject
    {
        public string apiKey;
        public string environment = "development";
        public bool fetchUpdatesInRealTime = false;
        public bool showDebugLogs = false;
        public float eventBatchInterval = 30f;

        private static StrixSDKConfig instance;

        public static StrixSDKConfig Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = Resources.Load<StrixSDKConfig>("StrixConfig");
                    if (instance == null)
                    {
                        instance = CreateInstance<StrixSDKConfig>();
#if UNITY_EDITOR
                        UnityEditor.AssetDatabase.CreateAsset(instance, "Assets/Resources/StrixConfig.asset");
                        UnityEditor.AssetDatabase.SaveAssets();
#endif
                    }
                }
                return instance;
            }
        }
    }
}