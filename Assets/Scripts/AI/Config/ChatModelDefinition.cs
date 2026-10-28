using UnityEngine;

namespace VRD_AIProviders
{
    /// <summary>
    /// One specific model on one provider, plus any per-model prompt tweaks.
    /// This is what gameplay code references (e.g. VIM's dialogueModel / classifierModel);
    /// swapping the asset swaps the model/provider without code changes.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/AI/Models/Chat Model", fileName = "Chat Model")]
    public class ChatModelDefinition : AIModelDefinition
    {
        /// <summary>
        /// Optional text appended to the end of the system prompt for this model only.
        /// Use it to compensate for model-specific weaknesses (e.g. "Answer with a single word."
        /// for a small classifier) without touching the shared prompt. Leave empty for none.
        /// Keep it short; it is sent with every request and costs tokens each time.
        /// </summary>
        [Tooltip("Optional extra instructions appended to the system prompt for this model only. Sent with every request — keep it short.")]
        [TextArea(2, 6)]
        public string promptAddOn;
        
        /// <summary>
        /// When true, the output-length limit is sent as "max_completion_tokens" instead of "max_tokens".
        /// Required by OpenAI's reasoning-era models (gpt-5 family, o-series), which reject "max_tokens"
        /// with HTTP 400. Leave false for Groq, Mistral and other providers that still use "max_tokens".
        /// Has no effect when a request's MaxTokens is null (the field is then omitted entirely).
        /// </summary>
        [Tooltip("Send the length limit as \"max_completion_tokens\" instead of \"max_tokens\". Turn ON for OpenAI gpt-5 / o-series. OFF for Groq and Mistral.")]
        public bool useMaxCompletionTokens;

        /// <summary>
        /// When false, the "temperature" field is never sent, even if a request sets one; the provider
        /// then uses its own default. Needed for models that reject any temperature other than the
        /// default (OpenAI gpt-5 / o-series return HTTP 400). Leave true for Groq and Mistral.
        /// </summary>
        [Tooltip("If OFF, temperature is never sent. Turn OFF for OpenAI gpt-5 / o-series, which reject custom temperatures. ON for Groq and Mistral.")]
        public bool supportsTemperature = true;
    }
}