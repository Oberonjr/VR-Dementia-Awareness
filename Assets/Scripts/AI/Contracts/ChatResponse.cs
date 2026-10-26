namespace VRD_AIProviders
{
    public class ChatResponse
    {
        public bool Success { get; set; }
        public string Text { get; set; }
        public string FinishReason { get; set; }
        public string Error { get; set; }

        public static ChatResponse OK(string text, string finish) => new ChatResponse
            { Success = true, Text = text, FinishReason = finish };
        public static ChatResponse Fail(string error) => new ChatResponse
            { Success = false, Error = error };
    }

}

