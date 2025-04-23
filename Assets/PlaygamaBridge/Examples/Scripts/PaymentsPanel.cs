using UnityEngine;
using UnityEngine.UI;
#if UNITY_WEBGL
using Playgama;
#endif

namespace Examples
{
    public class PaymentsPanel : MonoBehaviour
    {
        [SerializeField] private Text _isSupported;
        [SerializeField] private Button _getCatalogButton;
        [SerializeField] private Button _getPurchasesButton;
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _consumePurchaseButton;
        [SerializeField] private GameObject _overlay;

#if UNITY_WEBGL
        private void Start()
        {
            _isSupported.text = $"Is Supported: { Bridge.payments.isSupported }";

            _getCatalogButton.onClick.AddListener(OnGetCatalogButtonClicked);
            _getPurchasesButton.onClick.AddListener(OnGetPurchasesButtonClicked);
            _purchaseButton.onClick.AddListener(OnPurchaseButtonClicked);
            _consumePurchaseButton.onClick.AddListener(OnConsumePurchaseButtonClicked);
        }

        private void OnGetCatalogButtonClicked()
        {
            _overlay.SetActive(true);

            Bridge.payments.GetCatalog((success, list) =>
            {
                Debug.Log($"OnGetCatalogCompleted, success: {success}, items:");
                
                if (success)
                {
                    foreach (var item in list)
                    {
                        Debug.Log("ID: " + item["id"]);
                        Debug.Log("Price: " + item["price"]);
                        Debug.Log("Price Currency Code: " + item["priceCurrencyCode"]);
                        Debug.Log("Price Value: " + item["priceValue"]);
                    }
                }
                
                _overlay.SetActive(false);
            });
        }

        private void OnGetPurchasesButtonClicked()
        {
            _overlay.SetActive(true);

            Bridge.payments.GetPurchases((success, list) =>
            {
                Debug.Log($"OnGetPurchasesCompleted, success: {success}, items:");
                
                if (success)
                {
                    foreach (var purchase in list)
                    {
                        Debug.Log("ID: " + purchase["id"]);
                    }
                }
                
                _overlay.SetActive(false);
            });
        }
        
        private void OnPurchaseButtonClicked()
        {
            _overlay.SetActive(true);
            
            Bridge.payments.Purchase("test_product", (success, _) =>
            {
                Debug.Log($"OnPurchaseCompleted, success: {success}"); 
                _overlay.SetActive(false);
            });
        }

        private void OnConsumePurchaseButtonClicked()
        {
            _overlay.SetActive(true);
            Bridge.payments.ConsumePurchase("test_product", (success, _) =>
            {
                Debug.Log("OnConsumePurchaseCompleted, success: " + success); 
                _overlay.SetActive(false);
            });
        }
#endif
    }
}