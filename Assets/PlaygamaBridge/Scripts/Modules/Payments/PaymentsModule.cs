#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using Playgama.Common;
using UnityEngine;
#if !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Playgama.Modules.Payments
{
    public class PaymentsModule : MonoBehaviour
    {
        public bool isSupported
        {
            get
            {
#if !UNITY_EDITOR
                return PlaygamaBridgeIsPaymentsSupported() == "true";
#else
                return false;
#endif
            }
        }

#if !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern string PlaygamaBridgeIsPaymentsSupported();

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgePaymentsPurchase(string id);

        [DllImport("__Internal")]
        private static extern void PlaygamaBridgePaymentsConsumePurchase(string id);
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgePaymentsGetPurchases();
        
        [DllImport("__Internal")]
        private static extern void PlaygamaBridgePaymentsGetCatalog();
#endif
        
        private Action<bool, Dictionary<string, string>> _purchaseCallback;
        private Action<bool> _consumePurchaseCallback;
        private Action<bool, List<Dictionary<string, string>>> _getPurchasesCallback;
        private Action<bool, List<Dictionary<string, string>>> _getCatalogCallback;


        public void Purchase(string id, Action<bool, Dictionary<string, string>> onComplete = null)
        {
            _purchaseCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgePaymentsPurchase(id);
#else
            OnPaymentsPurchaseFailed();
#endif
        }
        
        public void ConsumePurchase(string id, Action<bool> onComplete = null)
        {
            _consumePurchaseCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgePaymentsConsumePurchase(id);
#else
            OnPaymentsConsumePurchaseCompleted("false");
#endif
        }
        
        public void GetPurchases(Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getPurchasesCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgePaymentsGetPurchases();
#else
            OnPaymentsGetPurchasesCompletedFailed();
#endif
        }
        
        public void GetCatalog(Action<bool, List<Dictionary<string, string>>> onComplete = null)
        {
            _getCatalogCallback = onComplete;

#if !UNITY_EDITOR
            PlaygamaBridgePaymentsGetCatalog();
#else
            OnPaymentsGetCatalogCompletedFailed();
#endif
        }


        // Called from JS
        private void OnPaymentsPurchaseCompleted(string result)
        {
            var purchase = new Dictionary<string, string>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    purchase = JsonHelper.FromJsonToDictionary(result);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }

            _purchaseCallback?.Invoke(true, purchase);
            _purchaseCallback = null;
        }

        private void OnPaymentsPurchaseFailed()
        {
            _purchaseCallback?.Invoke(false, null);
            _purchaseCallback = null;
        }
        
        private void OnPaymentsConsumePurchaseCompleted(string result)
        {
            var isSuccess = result == "true";
            _consumePurchaseCallback?.Invoke(isSuccess);
            _consumePurchaseCallback = null;
        }
        
        private void OnPaymentsGetPurchasesCompletedSuccess(string result)
        {
            var purchases = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    purchases = JsonHelper.FromJsonToListOfDictionaries(result);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }

            _getPurchasesCallback?.Invoke(true, purchases);
            _getPurchasesCallback = null;
        }

        private void OnPaymentsGetPurchasesCompletedFailed()
        {
            _getPurchasesCallback?.Invoke(false, null);
            _getPurchasesCallback = null;
        }
        
        private void OnPaymentsGetCatalogCompletedSuccess(string result)
        {
            var items = new List<Dictionary<string, string>>();

            if (!string.IsNullOrEmpty(result))
            {
                try
                {
                    items = JsonHelper.FromJsonToListOfDictionaries(result);
                }
                catch (Exception e)
                {
                    Debug.Log(e);
                }
            }

            _getCatalogCallback?.Invoke(true, items);
            _getCatalogCallback = null;
        }

        private void OnPaymentsGetCatalogCompletedFailed()
        {
            _getCatalogCallback?.Invoke(false, null);
            _getCatalogCallback = null;
        }
    }
}
#endif