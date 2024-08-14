using System.Net.Http;
using System;
using System.Threading;
using System.Threading.Tasks;
using StrixSDK.Runtime;
using StrixSDK.Runtime.Utils;
using UnityEngine;

namespace StrixSDK.Runtime.Service
{
    public static class NetworkListener
    {
        private static bool _isChecking;
        private static readonly HttpClient _client = new HttpClient();

        public static async Task<bool> CheckNetworkAvailabilityAsync(string apiEndpoint)
        {
            if (_isChecking)
            {
                throw new InvalidOperationException("Network check is already in progress.");
            }

            _isChecking = true;
            bool networkAvailable = false;

            try
            {
                while (!networkAvailable && Application.isPlaying)
                {
                    try
                    {
                        var healthCheckResponse = await _client.GetAsync(apiEndpoint);
                        if (healthCheckResponse.IsSuccessStatusCode)
                        {
                            Debug.Log("Reach check succeeded, resending cached events.");
                            networkAvailable = true;

                            // Wait 5 seconds to prevent behavior where it's something wrong with the request builder or backend server,
                            // as it would cause the request to fail & retry without a delay.
                            // TODO: Should handle request common and unreachable errors separately.
                            await Task.Delay(5000);
                            if (Application.isPlaying)
                            {
                                bool eventsSent = await Analytics.TryToResendFailedEvents();
                                if (!eventsSent)
                                {
                                    Debug.Log("Failed to resend events, retrying in 30 seconds.");
                                    networkAvailable = false; // Set networkAvailable to false to continue the retry loop
                                    await Task.Delay(30000); // Wait for 30 seconds before retrying
                                }
                            }
                        }
                        else
                        {
                            Debug.Log("Reach check failed, retrying in 30 seconds.");
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.Log($"Reach check failed with exception: {ex.Message}, retrying in 30 seconds.");
                    }

                    if (!networkAvailable)
                    {
                        await Task.Delay(30000); // Wait for 30 seconds before retrying
                    }
                }
            }
            finally
            {
                _isChecking = false;
            }

            return networkAvailable;
        }
    }
}