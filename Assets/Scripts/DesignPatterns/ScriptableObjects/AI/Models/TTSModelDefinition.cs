using UnityEngine;


namespace VRD_AIProviders
{
    /// <summary>Which emotion-tag style a TTS provider understands. Translation between them comes with the TTS adapters.</summary>
    public enum EmotionMarkup { None, InworldTags, ElevenLabsAudioTags }
    
    [CreateAssetMenu(menuName = "Scriptable Objects/AI/Models/TTS Model", fileName = "TTS_  ")]
    public class TTSModelDefinition : AIModelDefinition
    {
        [Tooltip("Provider's voice ID, used when a request doesn't override it.")]
        public string voiceId;

        [Tooltip("PCM sample rate to request. Must be one the provider supports — check its docs.")]
        public int sampleRate = 24000;

        [Tooltip("Emotion-tag style this provider understands. CoreRules currently teaches Inworld tags.")]
        public EmotionMarkup markup = EmotionMarkup.None;
    }
}

