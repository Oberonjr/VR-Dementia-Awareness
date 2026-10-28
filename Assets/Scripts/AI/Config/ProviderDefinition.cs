using UnityEngine;

namespace VRD_AIProviders
{
    /// <summary>
    /// Describes one OpenAI-compatible API endpoint: where it lives, which key to use,
    /// and which request quirks it has. One asset per provider (Groq, OpenAI, Mistral, ...).
    /// Models on this provider are described by <see cref="AIModelDefinition"/> assets pointing here.
    /// </summary>
    [CreateAssetMenu(menuName = "ScriptableObjects/AI/Provider Definition", fileName = "Provider Definition")]
    public class ProviderDefinition : ScriptableObject
    {
        /// <summary>
        /// Short, lowercase identifier for this provider, e.g. "groq", "openai", "mistral".
        /// Must exactly match (case-insensitive) a top-level name in keys.json — this is the only
        /// link between this asset and its API key. Also used in log messages.
        /// Changing it requires renaming the matching entry in every machine's keys.json.
        /// </summary>
        [Tooltip("Name of this provider's entry in keys.json, e.g. \"groq\", \"openai\", \"mistral\". Case-insensitive.")]
        public string providerId;

        /// <summary>
        /// Root URL of the provider's OpenAI-compatible API, including the version segment and
        /// without a trailing endpoint. The adapter appends "/chat/completions" and the inspector
        /// button appends "/models". Examples:
        ///   Groq    https://api.groq.com/openai/v1
        ///   OpenAI  https://api.openai.com/v1
        ///   Mistral https://api.mistral.ai/v1
        ///   Gemini  https://generativelanguage.googleapis.com/v1beta/openai
        /// A 404 from "Fetch available models" almost always means this is wrong.
        /// </summary>
        [Tooltip("API root incl. version, no endpoint. e.g. https://api.openai.com/v1 — \"/chat/completions\" and \"/models\" are appended automatically.")]
        public string baseUrl;

        // Written by the "Fetch available models" button (ProviderDefinitionEditor). Not secret:
        // contains model names only, never the key. Hidden from the default inspector and drawn read-only.
        [SerializeField, HideInInspector] private string cachedModels;
        [SerializeField, HideInInspector] private string lastFetchedStatus;
        [SerializeField, HideInInspector] private bool lastFetchOK;

        /// <summary>Newline-separated model IDs from the last successful fetch, or empty if never fetched.</summary>
        public string CachedModels => cachedModels;
    }
}