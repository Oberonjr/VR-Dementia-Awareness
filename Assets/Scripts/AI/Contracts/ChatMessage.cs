namespace VRD_AIProviders
{
    public class ChatMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        
        public ChatMessage(){}
        public ChatMessage(string role, string content)
        {
            Role = role;
            Content = content;
        }
    }
}

