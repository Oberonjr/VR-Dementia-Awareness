using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace VRD_AIProviders
{
    /// <summary>
    ///  ILLMService for any provider speaking the OpenAI /chat/completions protocol
    /// </summary>
    public class OpenAICompatChatAdapter: ILLMService
    {
        private readonly ChatModelDefinition chatModel;
        public OpenAICompatChatAdapter(ChatModelDefinition chatModel)
        {
            this.chatModel = chatModel;
        }

        public async Task<ChatResponse> CompleteAsync(ChatRequest request)
        {
            if(!ProviderHttp.TryResolve(chatModel, "chat/completions", out var target, out string error))
                return ChatResponse.Fail(error);
            byte[] payload = Encoding.UTF8.GetBytes(BuildBody(request).ToString());

            using (var req = new UnityWebRequest(target.Url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(payload);
                req.SetRequestHeader("Content-Type", "application/json");
                ProviderHttp.Result res = await ProviderHttp.SendAsync(req, target);
                if(!res.OK) return ChatResponse.Fail(res.Error);

                try
                {
                    JToken choice = JObject.Parse(res.Body)["choices"]?[0];
                    string text = (string)choice?["message"]?["content"];
                    if (text == null)
                        return ChatResponse.Fail($"No message content in response: {ProviderHttp.Preview(res.Body)}");
                    return ChatResponse.OK(text, (string)choice["finish_reason"]);
                }
                catch (Exception e)
                {
                    return ChatResponse.Fail($"Unreadable response ({e.Message}): {ProviderHttp.Preview(res.Body)}");
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
                ["model"] = chatModel.modelId,
                ["messages"] = messages
            };
            if (req.Temperature.HasValue && chatModel.supportsTemperature)
            {
                body["temperature"] = req.Temperature.Value;
            }

            if (req.MaxTokens.HasValue)
            {
                body[chatModel.useMaxCompletionTokens ? "max_completion_tokens" : "max_tokens"] = req.MaxTokens.Value;
            }

            return body;
        }

        private void AppendPromptAddOn(JArray messages)
        {
            if (string.IsNullOrWhiteSpace(chatModel.promptAddOn)) return;
            JToken system = messages.FirstOrDefault(m => (string)m["role"] == "system");
            if (system != null)
                system["content"] = (string)system["content"] + "\n\n" + chatModel.promptAddOn.Trim();
            else
            {
                messages.Insert(0, new JObject{["role"] = "system", ["content"] = chatModel.promptAddOn.Trim()});
            }
        }
    }
}


