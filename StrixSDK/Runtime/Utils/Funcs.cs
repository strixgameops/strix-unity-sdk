using Newtonsoft.Json.Linq;
using System.Net.NetworkInformation;
using UnityEngine;

namespace StrixSDK.Runtime.Utils
{
    public static class Utils
    {
        public static void LogMessage(string message)
        {
            Debug.Log(message);
        }

        public static void LogError(string message)
        {
            Debug.LogError(message);
        }

        public static string ConcatJsonArrays(string json1, string json2)
        {
            // Parsing strings to JSON to JArray
            JArray array1 = JArray.Parse(json1);
            JArray array2 = JArray.Parse(json2);

            // Concat
            array1.Merge(array2);

            // Return as string
            return array1.ToString();
        }
    }
}