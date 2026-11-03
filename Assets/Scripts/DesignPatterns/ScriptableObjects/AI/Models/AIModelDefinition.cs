using UnityEngine;

namespace VRD_AIProviders
{
    /// <summary>
    /// Base for every model asset (chat, STT, TTS): which provider serves it and its exact API id.
    /// ProviderHttp only needs these two fields; subclasses add capability-specific settings.
    /// </summary>
    public abstract class AIModelDefinition : ScriptableObject
    {
        /// <summary>
        /// The provider this model is served by. Determines the URL, API key and request quirks.
        /// Must not be null — requests fail immediately without it.
        /// </summary>
        [Tooltip("Provider asset that serves this model (determines URL, key and quirks).")]
        public ProviderDefinition provider;

        /// <summary>
        /// The model's exact API identifier, sent as "model" in every request.
        /// Must match one of the IDs shown under "Available models" on the provider asset,
        /// character for character (e.g. "llama-3.3-70b-versatile", "gpt-5-mini",
        /// "mistral-small-latest"). Display names from provider websites usually do NOT work.
        /// A wrong ID typically returns HTTP 404 or 400 "model not found".
        /// </summary>
        [Tooltip("Exact API model ID, e.g. llama-3.3-70b-versatile. Copy it from the provider asset's \"Available models\" list.")]
        public string modelId;
    }
}
