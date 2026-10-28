namespace VRD_AIProviders
{
    public class TranscriptionRequest
    {
        public byte[] Pcm16 { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public string Language { get; set; }
        public string Prompt { get; set; }
    }
}

