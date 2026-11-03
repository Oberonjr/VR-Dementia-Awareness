using UnityEngine;

namespace VRD_AIProviders
{
    [CreateAssetMenu(menuName = "Scriptable Objects/AI/Models/STT Model", fileName = "STT_")]
    public class STTModelDefinition : AIModelDefinition
    {
        [Tooltip("Optional vocabulary/context hint sent as the \"prompt\" field (names, places). Keep ot short.")]
        [TextArea(2, 6)]
        public string transcriptionPrompt;
        
        [Tooltip("Turn OFF if the provider rejects the \"prompt\" field (e.g. a 422 error).")]
        public bool supportsPrompt = true;
    }
}


