#if UNITY_WEBGL
using System;
using System.Collections.Generic;
using System.Text;

namespace Playgama.Common
{
    public static class JsonHelper
    {
        public static string SurroundWithBraces(this string json)
        {
            return "{" + json + "}";
        }

        public static string SurroundWithKey(this string json, string key, bool quotes = false)
        {
            if (quotes)
            {
                json = "\"" + json + "\"";
            }
            
            return "\"" + key + "\":" + json;
        }

        public static string ConvertBooleanToCSharp(this string json)
        {
            return json.Replace("true", "True").Replace("false", "False");
        }

        public static string ToJson(this Dictionary<string, object> data)
        {
            var sb = new StringBuilder();

            if (data != null)
            {
                var isFirst = true;
                
                foreach (var item in data)
                {
                    if (!isFirst)
                    {
                        sb.Append(",");
                    }

                    isFirst = false;

                    sb.Append(item.Value.ToJson().SurroundWithKey(item.Key));
                }
            }

            return sb.ToString().SurroundWithBraces();
        }
        
        public static Dictionary<string, string> FromJsonToDictionary(string json)
        {
            json = json.Trim('{', '}').Trim();
            
            var result = new Dictionary<string, string>();
            var keyValuePairs = json.Split(new[] { ",\"" }, StringSplitOptions.None);

            foreach (var keyValuePair in keyValuePairs)
            {
                var keyValue = keyValuePair.Split(new[] { "\":" }, StringSplitOptions.None);
                if (keyValue.Length == 2)
                {
                    result[keyValue[0].Trim('"')] = keyValue[1].Trim('"');
                }
            }

            return result;
        }
        
        public static List<Dictionary<string, string>> FromJsonToListOfDictionaries(string json)
        {
            json = json.Trim('[', ']').Trim();
            
            var result = new List<Dictionary<string, string>>();
            var objects = json.Split(new[] { "},{" }, StringSplitOptions.None);

            foreach (var obj in objects)
            {
                if (string.IsNullOrEmpty(obj))
                {
                    continue;
                }
                
                var formattedObject = obj.Trim('{', '}').Trim();
                var dict = new Dictionary<string, string>();
                var keyValuePairs = formattedObject.Split(new[] { ",\"" }, StringSplitOptions.None);

                foreach (var keyValuePair in keyValuePairs)
                {
                    var keyValue = keyValuePair.Split(new[] { "\":" }, StringSplitOptions.None);
                    if (keyValue.Length == 2)
                    {
                        dict[keyValue[0].Trim('"')] = keyValue[1].Trim('"');
                    }
                }

                result.Add(dict);
            }

            return result;
        }
        
        public static List<string> FromJsonToListOfStrings(string json)
        {
            json = json.Trim('[', ']').Trim();
            
            var result = new List<string>();
            var values = json.Split(new[] { ",\"" }, StringSplitOptions.None);

            foreach (var value in values)
            {
                result.Add(value.Trim('"'));
            }

            return result;
        }

        private static string ToJson(this Array data)
        {
            var sb = new StringBuilder();
            sb.Append("[");
            var isFirst = true;

            foreach (var item in data)
            {
                if (!isFirst)
                {
                    sb.Append(",");
                }
                isFirst = false;

                sb.Append(item.ToJson());
            }

            sb.Append("]");
            return sb.ToString();
        }

        private static string ToJson(this object data)
        {
            return data switch
            {
                null => "null",
                string s => "\"" + EscapeString(s) + "\"",
                int or float or double => data.ToString(),
                bool b => b ? "true" : "false",
                Array array => array.ToJson(),
                Dictionary<string, object> objects => objects.ToJson(),
                _ => "null"
            };
        }

        private static string EscapeString(string str)
        {
            return str.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif