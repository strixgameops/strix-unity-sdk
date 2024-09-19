using UnityEngine;
using StrixSDK.Editor.Config;
using UnityEditor;

public static class StrixSettingsProvider
{
    private static readonly string[] BranchOptions = { "development", "staging", "production" };

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
                    int selectedIndex = System.Array.IndexOf(BranchOptions, config.branch);
                    if (selectedIndex == -1)
                    {
                        selectedIndex = 0;
                    }
                    selectedIndex = EditorGUILayout.Popup("Branch", selectedIndex, BranchOptions);
                    config.branch = BranchOptions[selectedIndex];

                    config.fetchUpdatesInRealTime = EditorGUILayout.Toggle("Fetch Updates in Real Time", config.fetchUpdatesInRealTime);

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