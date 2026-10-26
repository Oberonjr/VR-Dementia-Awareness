using System.Collections.Generic;

namespace VRD_AIProviders
{
    public class ChatRequest
    {
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
        public float? Temperature { get; set; }
        public int? MaxTokens { get; set; }
    }

}
