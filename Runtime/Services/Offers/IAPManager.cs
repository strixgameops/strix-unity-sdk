using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Purchasing;
using UnityEngine.Purchasing.Extension;

using Unity.Services.Core;
using Unity.Services.Core.Environments;

namespace StrixSDK.Runtime
{
    public class StrixIAPManager : MonoBehaviour
    {
        private static StrixIAPManager _instance;

        public static StrixIAPManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<StrixIAPManager>();
                    if (_instance == null)
                    {
                        GameObject obj = new GameObject();
                        _instance = obj.AddComponent<StrixIAPManager>();
                        obj.name = typeof(StrixIAPManager).ToString();
                    }
                }
                return _instance;
            }
        }

        public IAPManager iapManagerInstance;

        public bool StartupIAPManager(List<string> validProductIDs)
        {
            //Debug.LogWarning($"Code for IAP manager is not enabled. Could not initialize Strix Unity Purchasing integration.");
            //return false;
            if (iapManagerInstance == null)
            {
                iapManagerInstance = new IAPManager();
                iapManagerInstance.Initialize(validProductIDs);
            }
            else
            {
                iapManagerInstance.ReInitializeAfterUpdate(validProductIDs);
            }
            return true;
        }

        public async Task<string> CallBuyIAP(string productId)
        {
            //throw new Exception($"IAP manager code is not enabled! Could not make purchase.");
            return await iapManagerInstance.CallBuyIAP(productId);
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else if (_instance != this)
            {
                Destroy(gameObject);
            }
        }
    }

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
                Debug.LogError($"IAP systemis not initialized!");
                return;
            }
            // Init unity services if they aren't yet
            await InitializeGamingService();

            var builder = ConfigurationBuilder.Instance(StandardPurchasingModule.Instance(AppStore.GooglePlay));
            foreach (var productID in productIDs)
            {
                builder.AddProduct(productID, ProductType.Consumable);
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Adding product {productID} to catalog");
            }
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Builder products total: {builder.products.Count}");

            try
            {
                UnityPurchasing.Initialize(this, builder);
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("UnityPurchasing Initialize called");
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
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Updating IAPManager: Adding product {productID} to catalog");
            }
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Updating IAPManager: Builder products total: {builder.products.Count}");

            try
            {
                UnityPurchasing.Initialize(this, builder);
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Updating IAPManager: UnityPurchasing Initialize called");
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
                        .SetEnvironmentName("development");
#else
                                .SetEnvironmentName("production");
#endif
                    await UnityServices.InitializeAsync(options).ContinueWith(task => StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Unity Gaming Service successfully initialized!"));
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
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("Unity Gaming Service already initialized!");
                return true;
            }
        }

        private bool IsInitialized()
        {
            bool initialized = controller != null && extensions != null;
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"IAPManager: IsInitialized: {initialized}. Controller is {controller}, extensions are {extensions}");
            return initialized;
        }

        /// <summary>
        /// Called when Unity IAP is ready to make purchases.
        /// </summary>
        public void OnInitialized(IStoreController c, IExtensionProvider e)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage("OnInitialized: Purchasing initialized successfully.");
            controller = c;
            extensions = e;
            IsInitialized();
        }

        /// <summary>
        /// Called when a purchase fails.
        /// </summary>
        public void OnPurchaseFailed(Product product, PurchaseFailureReason error)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"OnPurchaseFailed: FAIL. Product: '{product.definition.storeSpecificId}', PurchaseFailureReason: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"OnInitializeFailed InitializationFailureReason: {error}");
        }

        public void OnInitializeFailed(InitializationFailureReason error, string message)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"OnPurchaseFailed: {error}. PurchaseFailureReason: {message}");
        }

        public async Task<string> CallBuyIAP(string productId)
        {
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Buying: {productId}. IAPManager initialized? {IsInitialized()}");
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

                    StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Purchasing product asychronously: {product.definition.id}");

                    currentlyProcessedProductId = productId;
                    purchaseTaskCompletionSource = new TaskCompletionSource<string>();

                    controller.InitiatePurchase(product);
                    return await purchaseTaskCompletionSource.Task;
                }
                else
                {
                    Debug.LogError("Purchase failed: Not purchasing product, either is not found or is not available for purchase");
                    return null;
                }
            }
            else
            {
                Debug.LogError("Purchase failed: Not initialized.");
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
                StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Purchase successful. Receipt: {receipt}");
                purchaseTaskCompletionSource?.SetResult(receipt);
            }
            else
            {
                Debug.LogError($"'currentlyProcessedProductId' changed during In-App Purchasing. Purchase was processed for product: '{e.purchasedProduct.definition.id}', but we expected '{currentlyProcessedProductId}' ");
                purchaseTaskCompletionSource?.SetResult(null);
            }
            StrixSDK.Runtime.Utils.Utils.StrixDebugLogMessage($"Purchase processed: {e}");
            return PurchaseProcessingResult.Complete;
        }

        public void OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.LogError($"Purchase failed: {product.definition.storeSpecificId}, Reason: {failureDescription}");
            purchaseTaskCompletionSource?.SetResult(null);
        }

        void IDetailedStoreListener.OnPurchaseFailed(Product product, PurchaseFailureDescription failureDescription)
        {
            Debug.LogError($"Purchase failed. Product: {product.definition.id}. Description: {failureDescription}");
            purchaseTaskCompletionSource?.SetResult(null);
        }
    }
}