#if UNITY_EDITOR

using UnityEngine;
using StrixSDK.Runtime.Config;
using UnityEditor;

public static class StrixSettingsProvider
{
    private static readonly string[] EnvOptions = { "development", "staging", "production" };

    [SettingsProvider]
    public static SettingsProvider CreateStrixSettingsProvider()
    {
        var provider = new SettingsProvider("Project/StrixSDK", SettingsScope.Project)
        {
            label = "Strix SDK",
            guiHandler = (searchContext) =>
            {
                var config = StrixSDKConfig.Instance;
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
                    config.fetchUpdatesInRealTime = EditorGUILayout.Toggle("Let players refetch new content in real-time", config.fetchUpdatesInRealTime, GUILayout.ExpandWidth(true));
                    EditorGUIUtility.labelWidth = originalValue;

                    EditorGUIUtility.labelWidth = 110;
                    config.showDebugLogs = EditorGUILayout.Toggle("Show debug logs", config.showDebugLogs, GUILayout.ExpandWidth(true));
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
            },

            keywords = new System.Collections.Generic.HashSet<string>(new[] { "Strix", "SDK", "API", "Package" })
        };

        return provider;
    }
}

#endif