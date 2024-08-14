using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Purchasing;
using Unity.Services.Core;
using Unity.Services.Core.Environments;
using UnityEngine.Purchasing.Extension;
using System;
using System.Threading.Tasks;

namespace StrixSDK.Runtime
{
    public class IAPManager : IDetailedStoreListener
    {
        private IStoreController controller;
        private IExtensionProvider extensions;

        private static string currentlyProcessedProductId;
        private TaskCompletionSource<string> purchaseTaskCompletionSource;

        public async void Initialize(List<string> productIDs)
        {
            if (IsInitialized())
            {
                Debug.Log($"IAP systemis not initialized!");
                return;
            }
            // Init unity services if they aren't yet
            await InitializeGamingService();

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
            foreach (var productID in productIDs)
            {
                builder.AddProduct(productID, ProductType.Consumable);
                Debug.Log($"Adding product {productID} to catalog");
            }
            Debug.Log($"Builder products total: {builder.products.Count}");

            try
            {
                UnityPurchasing.Initialize(this, builder);
                Debug.Log("UnityPurchasing Initialize called");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"UnityPurchasing Initialize failed: {e.Message}");
            }
        }

        public async void ReInitializeAfterUpdate(List<string> productIDs)
        {
            // Init unity services if they aren't yet
            await InitializeGamingService();

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
            foreach (var productID in productIDs)
            {
                builder.AddProduct(productID, ProductType.Consumable);
                Debug.Log($"Updating IAPManager: Adding product {productID} to catalog");
            }
            Debug.Log($"Updating IAPManager: Builder products total: {builder.products.Count}");

            try
            {
                UnityPurchasing.Initialize(this, builder);
                Debug.Log("Updating IAPManager: UnityPurchasing Initialize called");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Updating IAPManager: UnityPurchasing Initialize failed: {e.Message}");
            }
        }

        private async Task<bool> InitializeGamingService()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                try
                {
                    var options = new InitializationOptions()
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        .SetEnvironmentName("production");
#else
                        .SetEnvironmentName("production");
#endif
                    await UnityServices.InitializeAsync(options).ContinueWith(task => Debug.Log("Unity Gaming Service successfully initialized!"));
                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                    return false;
                }
            }
            else
            {
                Debug.Log("Unity Gaming Service already initialized!");
                return true;
            }
        }

        private bool IsInitialized()
        {
            bool initialized = controller != null && extensions != null;
            Debug.Log($"IAPManager: IsInitialized: {initialized}. Controller is {controller}, extensions are {extensions}");
            return initialized;
        }

        /// <summary>
        /// Called when Unity IAP is ready to make purchases.
        /// </summary>
        public void OnInitialized(IStoreController c, IExtensionProvider e)
        {
            Debug.Log("OnInitialized: Purchasing initialized successfully.");
            controller = c;
            extensions = e;
            IsInitialized();
        }

        /// <summary>
        /// Called when a purchase fails.
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason error)
        {
            Debug.Log($"OnPurchaseFailed: FAIL. Product: '{product.definition.storeSpecificId}', PurchaseFailureReason: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            Debug.Log($"OnInitializeFailed InitializationFailureReason: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            Debug.Log($"OnPurchaseFailed: {error}. PurchaseFailureReason: {message}");
        }

        public async Task<string> CallBuyIAP(string productId)
        {
            Debug.Log($"Buying: {productId}. IAPManager initialized? {IsInitialized()}");
            if (IsInitialized())
            {
                Product product = controller.products.WithID(productId);

                if (product != null && product.availableToPurchase)
                {
                    if (purchaseTaskCompletionSource != null && !purchaseTaskCompletionSource.Task.IsCompleted)
                    {
                        Debug.LogError("Another purchase is already in progress.");
                        return null;
                    }

                    Debug.Log($"Purchasing product asychronously: {product.definition.id}");

                    currentlyProcessedProductId = productId;
                    purchaseTaskCompletionSource = new TaskCompletionSource<string>();

                    controller.InitiatePurchase(product);
                    return await purchaseTaskCompletionSource.Task;
                }
                else
                {
                    Debug.Log("Purchase failed: Not purchasing product, either is not found or is not available for purchase");
                    return null;
                }
            }
            else
            {
                Debug.Log("Purchase failed: Not initialized.");
                return null;
            }
        }

        /// <summary>
        /// Called when a purchase completes.
        ///
        /// May be called at any time after OnInitialized().
        /// </summary>
        public PurchaseProcessingResult ProcessPurchase(PurchaseEventArgs e)
        {
            if (e.purchasedProduct.definition.id == currentlyProcessedProductId)
            {
                string receipt = e.purchasedProduct.receipt;
                Debug.Log($"Purchase successful. Receipt: {receipt}");
                purchaseTaskCompletionSource?.SetResult(receipt);
            }
            else
            {
                Debug.LogError($"'currentlyProcessedProductId' changed during In-App Purchasing. Purchase was processed for product: '{e.purchasedProduct.definition.id}', but we expected '{currentlyProcessedProductId}' ");
                purchaseTaskCompletionSource?.SetResult(null);
            }
            Debug.Log($"Purchase processed: {e}");
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.LogError($"Purchase failed: {product.definition.storeSpecificId}, Reason: {failureDescription}");
            purchaseTaskCompletionSource?.SetResult(null);
        }

        void IDetailedStoreListener.OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.Log($"Purchase failed. Product: {product.definition.id}. Description: {failureDescription}");
            purchaseTaskCompletionSource?.SetResult(null);
        }
    }
}