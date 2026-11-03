namespace VRD_AIProviders
{
    public class TranscriptionResponse
    {
        public bool Success { get; set; }
        public string Text { get; set; }
        public string Error { get; set; }

        public static TranscriptionResponse OK(string text) => new TranscriptionResponse
            { Success = true, Text = text };
        public static TranscriptionResponse Fail(string error) => new TranscriptionResponse
            { Success = false, Error = error };
    }

}

