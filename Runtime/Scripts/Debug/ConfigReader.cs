#if UNITY_WEBGL && UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Playgama.Debug
{
    public static class ConfigReader
    {
        private const string ConfigPath = "Assets/WebGLTemplates/Bridge/playgama-bridge-config.json";

        public static string GetPaymentsCatalog()
        {
            if (!File.Exists(ConfigPath))
            {
                return "[]";
            }

            var json = File.ReadAllText(ConfigPath);
            json = Regex.Replace(json, @"\s+", "");
            var paymentsArray = ExtractPaymentsArray(json);

            if (string.IsNullOrEmpty(paymentsArray) || paymentsArray == "[]")
            {
                return "[]";
            }

            var items = new List<string>();
            var itemMatches = Regex.Matches(paymentsArray, @"\{[^{}]*(?:\{[^{}]*\}[^{}]*)*\}");

            foreach (Match itemMatch in itemMatches)
            {
                var itemJson = itemMatch.Value;
                var id = ExtractValue(itemJson, "id");

                if (!string.IsNullOrEmpty(id))
                {
                    var amount = ExtractAmount(itemJson) ?? "1";

                    items.Add($"{{\"id\":\"{id}\",\"price\":\"{amount} GAM\",\"priceCurrencyCode\":\"GAM\",\"priceValue\":\"{amount}\"}}");
                }
            }

            return "[" + string.Join(",", items) + "]";
        }

        private static string ExtractPaymentsArray(string json)
        {
            var match = Regex.Match(json, @"""payments""\s*:\s*(\[[\s\S]*?\])(?=\s*[,}])");
            return match.Success ? match.Groups[1].Value : "[]";
        }

        private static string ExtractValue(string json, string key)
        {
            var match = Regex.Match(json, @"""" + key + @"""\s*:\s*""([^""]*)""");
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string ExtractAmount(string json)
        {
            var match = Regex.Match(json, @"""playgama""\s*:\s*\{[^}]*""amount""\s*:\s*(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
#endif
