#if UNITY_EDITOR

using UnityEngine;
using StrixSDK.Runtime.Config;
using UnityEditor;

public class StrixSettingsWindow : EditorWindow
{
    private static readonly string[] EnvOptions = { "development", "staging", "production" };
    private StrixSDKConfig config;

    [MenuItem("Tools/Strix SDK Settings")]
    public static void ShowWindow()
    {
        GetWindow<StrixSettingsWindow>("Strix SDK Settings");
    }

    private void OnEnable()
    {
        config = StrixSDKConfig.Instance;
    }

    private void OnGUI()
    {
        if (config != null)
        {
            EditorGUI.BeginChangeCheck();

            config.apiKey = EditorGUILayout.TextField("API Key", config.apiKey);

            int selectedIndex = System.Array.IndexOf(EnvOptions, config.environment);
            if (selectedIndex == -1)
            {
                selectedIndex = 0;
            }
            selectedIndex = EditorGUILayout.Popup("Environment", selectedIndex, EnvOptions);
            config.environment = EnvOptions[selectedIndex];

            float originalValue = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 250;
            config.fetchUpdatesInRealTime = EditorGUILayout.Toggle(
                "Let players refetch new content in real-time",
                config.fetchUpdatesInRealTime,
                GUILayout.ExpandWidth(true)
            );
            EditorGUIUtility.labelWidth = originalValue;

            EditorGUIUtility.labelWidth = 110;
            config.showDebugLogs = EditorGUILayout.Toggle(
                "Show debug logs",
                config.showDebugLogs,
                GUILayout.ExpandWidth(true)
            );

            config.eventBatchInterval = EditorGUILayout.FloatField(
                "Analytics Batch Send Interval (seconds)",
                config.eventBatchInterval,
                GUILayout.ExpandWidth(true)
            );
            EditorGUIUtility.labelWidth = originalValue;

            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(config);
                AssetDatabase.SaveAssets();
            }
        }
        else
        {
            GUILayout.Label("Configuration asset not found.");
        }
    }
}

#endif