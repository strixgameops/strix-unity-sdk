using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Networking;
using UnityEngine;
using Newtonsoft.Json;

namespace StrixSDK.Runtime.APIClient
{
    public static class Client
    {
        // Actual request handler for Req() wrapper
        private static async Task<string> QueueRequest(UnityWebRequest req)
        {
            using (req)
            {
                var asyncOperation = req.SendWebRequest();

                while (!asyncOperation.isDone)
                {
                    await Task.Yield();
                }

                if (req.result != UnityWebRequest.Result.Success)
                {
                    // Try to fetch error message if request failed
                    ApiResponse errorResponse = null;
                    try
                    {
                        errorResponse = JsonUtility.FromJson<ApiResponse>(req.downloadHandler.text);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to deserialize error response: {ex.Message}");
                        return "err";
                    }

                    if (errorResponse != null)
                    {
                        Debug.LogError($"Error while sending request to {req.url}: {errorResponse.message}");
                        return "err";
                    }
                    else
                    {
                        Debug.LogError($"Error while sending request to {req.url}: {req.error}");
                        return "err";
                    }
                }
                else
                {
                    //Debug.Log($"Response from {req.url}: {req.downloadHandler.text}");
                    return req.downloadHandler.text;
                }
            }
        }

        public static async Task<string> Req(string requestUrl, Dictionary<string, object> requestBody)
        {
            try
            {
                using (var request = new UnityWebRequest(requestUrl, "POST"))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(requestBody));
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Content-Type", "application/json");

                    return await QueueRequest(request);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while sending request to {requestUrl}: {ex.Message}");
                return null;
            }
        }

        public static async Task<string> Req(string requestUrl)
        {
            try
            {
                using (var request = new UnityWebRequest(requestUrl, "GET"))
                {
                    request.downloadHandler = new DownloadHandlerBuffer();

                    return await QueueRequest(request);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception occurred while sending request to {requestUrl}: {ex.Message}");
                return null;
            }
        }
    }
}