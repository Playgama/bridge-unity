using System.Collections.Generic;
using Playgama;
using UnityEngine;
using UnityEngine.UI;

namespace Examples
{
    public class PaymentsPanel : MonoBehaviour
    {
        [SerializeField] private Text _isSupported;
        [SerializeField] private Text _isGetCatalogSupported;
        [SerializeField] private Text _isGetPurchasesSupported;
        [SerializeField] private Text _isConsumePurchaseSupported;
        [SerializeField] private Button _getCatalogButton;
        [SerializeField] private Button _getPurchasesButton;
        [SerializeField] private Button _purchaseButton;
        [SerializeField] private Button _consumePurchaseButton;
        [SerializeField] private GameObject _overlay;

        private void Start()
        {
            _isSupported.text = $"Is Supported: { Bridge.payments.isSupported }";
            _isGetCatalogSupported.text = $"Is Get Catalog Supported: { Bridge.payments.isGetCatalogSupported }";
            _isGetPurchasesSupported.text = $"Is Get Purchases Supported: { Bridge.payments.isGetPurchasesSupported }";
            _isConsumePurchaseSupported.text = $"Is Consume Purchase Supported: { Bridge.payments.isConsumePurchaseSupported }";
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
                    switch (Bridge.platform.id)
                    {
                        case "yandex":
                            foreach (var item in list)
                            {
                                Debug.Log("ID: " + item["id"]);
                                Debug.Log("Title: " + item["title"]);
                                Debug.Log("Description: " + item["description"]);
                                Debug.Log("Image URI: " + item["imageURI"]);
                                Debug.Log("Price: " + item["price"]);
                                Debug.Log("Price Currency Code: " + item["priceCurrencyCode"]);
                                Debug.Log("Price Currency Image: " + item["priceCurrencyImage"]);
                                Debug.Log("Price Value: " + item["priceValue"]);
                            }
                            break;
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
                    switch (Bridge.platform.id)
                    {
                        case "yandex":
                            foreach (var purchase in list)
                            {
                                Debug.Log("Product ID: " + purchase["productID"]);
                                Debug.Log("Purchase Token: " + purchase["purchaseToken"]);
                            }
                            break;
                    }
                }
                
                _overlay.SetActive(false);
            });
        }
        
        private void OnPurchaseButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "yandex":
                    options.Add("id", "YOUR_PRODUCT_ID");
                    break;
            }
            
            Bridge.payments.Purchase(options, (success, purchase) =>
            {
                if (success)
                {
                    switch (Bridge.platform.id)
                    {
                        case "yandex":
                            Debug.Log("Product ID: " + purchase["productID"]); 
                            Debug.Log("Purchase Token: " + purchase["purchaseToken"]);
                            break;
                        
                        case "facebook":
                            Debug.Log("Product ID: " + purchase["productID"]); 
                            Debug.Log("Purchase Token: " + purchase["purchaseToken"]);
                            break;
                    }
                }
                _overlay.SetActive(false);
            });
        }

        private void OnConsumePurchaseButtonClicked()
        {
            _overlay.SetActive(true);
            
            var options = new Dictionary<string, object>();
            switch (Bridge.platform.id)
            {
                case "yandex":
                    options.Add("purchaseToken", "YOUR_PURCHASE_TOKEN");
                    break;
            }
            
            Bridge.payments.ConsumePurchase(options, _ => { _overlay.SetActive(false); });
        }
    }
}