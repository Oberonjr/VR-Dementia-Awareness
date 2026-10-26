using UnityEngine;
using Newtonsoft.Json.Linq;

namespace VRD_AIProviders
{
    public static class ProviderErrors
    {
        public static string Extract(string body)
        {
            if(string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                JObject json = JObject.Parse(body);
                return (string)json["error"]?["message"] //OpenAI, Groq, Gemini
                       ?? (string)json["message"] //Mistral
                       ?? (string)json["detail"];
            }
            catch
            {
                return null;
            }
        }
    }
}

