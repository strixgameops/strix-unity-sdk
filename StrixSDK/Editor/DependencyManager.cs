using UnityEditor;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager;
using UnityEngine;

[InitializeOnLoad]
public class StrixPackageManager
{
    static StrixPackageManager()
    {
        EditorApplication.delayCall += CheckAndInstallPackages;
    }

    private static void CheckAndInstallPackages()
    {
        var packages = new string[] { "com.unity.purchasing", "com.unity.localization" };
        foreach (var package in packages)
        {
            if (!IsPackageInstalled(package))
            {
                Debug.Log($"Installing package: {package}");
                Client.Add(package);
            }
        }
    }

    private static bool IsPackageInstalled(string packageName)
    {
        var listRequest = Client.List(true);
        while (!listRequest.IsCompleted) { }
        foreach (var package in listRequest.Result)
        {
            if (package.name == packageName)
            {
                return true;
            }
        }
        return false;
    }
}