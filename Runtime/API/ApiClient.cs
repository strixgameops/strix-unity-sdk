using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace StrixSDK.Runtime.APIClient
{
    // Client for handling API requests
    public static class Client
    {
        // Configurable timeout for requests (in seconds)
        private const int RequestTimeoutSeconds = 30;

        // Cache for commonly used HTTP headers
        private static readonly Dictionary<string, string> JsonContentHeaders = new Dictionary<string, string>
        {
            { "Content-Type", "application/json" }
        };

        /// <summary>
        /// Sends a POST request with a JSON body
        /// </summary>
        /// <param name="requestUrl">API endpoint URL</param>
        /// <param name="requestBody">Request payload as dictionary</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Response string or null if request failed</returns>
        public static Task<string> Req(string requestUrl, Dictionary<string, object> requestBody, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(requestUrl))
                throw new ArgumentNullException(nameof(requestUrl));

            if (requestBody == null)
                throw new ArgumentNullException(nameof(requestBody));

            return SendRequestInternal(requestUrl, "POST", JsonContentHeaders,
                JsonConvert.SerializeObject(requestBody), cancellationToken);
        }

        /// <summary>
        /// Sends a GET request
        /// </summary>
        /// <param name="requestUrl">API endpoint URL</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>Response string or null if request failed</returns>
        public static Task<string> Req(string requestUrl, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(requestUrl))
                throw new ArgumentNullException(nameof(requestUrl));

            return SendRequestInternal(requestUrl, "GET", null, null, cancellationToken);
        }

        /// <summary>
        /// Internal method for sending web requests with proper resource management
        /// </summary>
        private static async Task<string> SendRequestInternal(
            string url,
            string method,
            Dictionary<string, string> headers = null,
            string body = null,
            CancellationToken cancellationToken = default)
        {
            UnityWebRequest request = null;
            UploadHandlerRaw uploadHandler = null;
            DownloadHandlerBuffer downloadHandler = null;

            try
            {
                request = new UnityWebRequest(url, method);

                // Set up upload handler if there's a body
                if (!string.IsNullOrEmpty(body))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(body);
                    uploadHandler = new UploadHandlerRaw(bodyRaw);
                    request.uploadHandler = uploadHandler;
                }

                // Set up download handler
                downloadHandler = new DownloadHandlerBuffer();
                request.downloadHandler = downloadHandler;

                // Set request timeout
                request.timeout = RequestTimeoutSeconds;

                // Add headers
                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.SetRequestHeader(header.Key, header.Value);
                    }
                }

                // Send the request and wait for completion with timeout support
                var operation = request.SendWebRequest();

                // Create a timer task for timeout checking
                var timeoutTask = Task.Delay(RequestTimeoutSeconds * 1000, cancellationToken);

                // Wait for either completion or timeout
                while (!operation.isDone)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        request.Abort();
                        throw new OperationCanceledException("Request was canceled");
                    }

                    // Check if timeout occurred
                    if (timeoutTask.IsCompleted && !operation.isDone)
                    {
                        request.Abort();
                        throw new TimeoutException($"Request to {url} timed out after {RequestTimeoutSeconds} seconds");
                    }

                    await Task.Yield();
                }

                // Handle errors
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string errorMessage = $"Request failed: {request.error}";

                    // Try to extract error details from response
                    if (downloadHandler.data != null && downloadHandler.data.Length > 0)
                    {
                        try
                        {
                            var errorResponse = JsonUtility.FromJson<ApiResponse>(downloadHandler.text);
                            if (errorResponse != null && !string.IsNullOrEmpty(errorResponse.message))
                            {
                                errorMessage = $"API Error: {errorResponse.message}";
                            }
                        }
                        catch (Exception)
                        {
                            // Ignore JSON parsing errors for error responses
                        }
                    }

                    Debug.LogError($"Error while sending {method} request to {url}: {errorMessage}");
                    return null;
                }

                // Return successful response
                return downloadHandler.text;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Exception during {method} request to {url}: {ex.Message}");
                return null;
            }
            finally
            {
                // Disposal of all resources
                if (uploadHandler != null)
                    uploadHandler.Dispose();

                if (downloadHandler != null)
                    downloadHandler.Dispose();

                if (request != null)
                    request.Dispose();
            }
        }
    }

    /// <summary>
    /// Basic API response structure
    /// </summary>
    [Serializable]
    public class ApiResponse
    {
        public bool success;
        public string message;
    }
}