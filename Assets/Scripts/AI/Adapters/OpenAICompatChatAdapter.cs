using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace VRD_AIProviders
{
    /// <summary>
    ///  ILLMService for any provider speaking the OpenAI /chat/completions protocol
    /// </summary>
    public class OpenAICompatChatAdapter: ILLMService
    {
        private const int TimeOutSeconds = 30;
        private readonly ModelDefinition model;
        public OpenAICompatChatAdapter(ModelDefinition model)
        {
            this.model = model;
        }

        public async Task<ChatResponse> CompleteAsync(ChatRequest request)
        {
            if (model == null || model.provider == null)
                return ChatResponse.Fail("ModelDefinition or its provider is not assigned.");
            ProviderDefinition provider = model.provider;

            if (!LocalKeyStore.TryGet(provider.providerId, out LocalKeyStore.ProviderKey key, out string keyError))
                return ChatResponse.Fail(keyError);
            
            if (string.IsNullOrWhiteSpace(provider.baseUrl))
                return ChatResponse.Fail($"Provider \"{provider.providerId}\" has no Base URL.");
            string url = provider.baseUrl.Trim().TrimEnd('/') + "/chat/completions";
            byte[] payload = Encoding.UTF8.GetBytes(BuildBody(request).ToString());

            using (var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = TimeOutSeconds;
                try
                {
                    req.SetRequestHeader("Content-Type", "application/json");
                    req.SetRequestHeader("Authorization", "Bearer " + key.apiKey);
                    if (!string.IsNullOrEmpty(key.organization))
                        req.SetRequestHeader("OpenAI-Organization", key.organization);
                }
                catch (InvalidOperationException)
                {
                    return ChatResponse.Fail(
                        $"The \"{provider.providerId}\" key in keys.json contains invalid characters.");
                }
                var op = req.SendWebRequest();
                while (!op.isDone) await Task.Yield();

                string body = req.downloadHandler?.text;
                if (req.result != UnityWebRequest.Result.Success)
                {
                    string detail = ProviderErrors.Extract(body);
                    if (req.responseCode == 429)
                    {
                        var headers = req.GetResponseHeaders();
                        if (headers != null)
                            UnityEngine.Debug.LogWarning("[LLM] 429 headers:\n" + string.Join("\n",
                                headers.Where(h => h.Key.IndexOf("ratelimit", StringComparison.OrdinalIgnoreCase) >= 0
                                                   || h.Key.Equals("Retry-After", StringComparison.OrdinalIgnoreCase))
                                    .Select(h => $"{h.Key}: {h.Value}")));
                    }
                    return ChatResponse.Fail(
                        $"{provider.providerId}/{model.modelId}: HTTP {req.responseCode} {req.error}" +
                        (detail != null ? $" - {detail}" : string.Empty));
                }

                try
                {
                    JToken choice = JObject.Parse(body)["choices"]?[0];
                    string text = (string)choice?["message"]?["content"];
                    if (text == null) return ChatResponse.Fail($"No message content in response: {Preview(body)}");
                    return ChatResponse.OK(text, (string)choice["finish_reason"]);
                }
                catch (Exception e)
                {
                    return ChatResponse.Fail($"Unreadable response ({e.Message}): {Preview(body)}");
                }
            }
        }

        private JObject BuildBody(ChatRequest req)
        {
            var messages = new JArray(req.Messages.Select(m => new JObject
                {
                    ["role"] = m.Role,
                    ["content"] = m.Content   
                }));
            AppendPromptAddOn(messages);

            var body = new JObject
            {
                ["model"] = model.modelId,
                ["messages"] = messages
            };
            if (req.Temperature.HasValue && model.supportsTemperature)
            {
                body["temperature"] = req.Temperature.Value;
            }

            if (req.MaxTokens.HasValue)
            {
                body[model.useMaxCompletionTokens ? "max_completion_tokens" : "max_tokens"] = req.MaxTokens.Value;
            }

            return body;
        }

        private void AppendPromptAddOn(JArray messages)
        {
            if (string.IsNullOrWhiteSpace(model.promptAddOn)) return;
            JToken system = messages.FirstOrDefault(m => (string)m["role"] == "system");
            if (system != null)
                system["content"] = (string)system["content"] + "\n\n" + model.promptAddOn.Trim();
            else
            {
                messages.Insert(0, new JObject{["role"] = "system", ["content"] = model.promptAddOn.Trim()});
            }
        }
        private static string Preview(string body) =>
            string.IsNullOrEmpty(body) ? "(empty body)" : body.Substring(0, Math.Min(200, body.Length));
    }
}


