
namespace VRD_AIProviders
{
    public class TTSResponse
    {
        public bool Success { get; private set; }
        public byte[] Pcm16 { get; private set; }
        public int SampleRate { get; private set; }
        public int Channels { get; private set; }
        public string Error { get; private set; }

        public static TTSResponse OK(byte[] pcm16, int sampleRate, int channels) =>
            new TTSResponse { Success = true, Pcm16 = pcm16, SampleRate = sampleRate, Channels = channels };

        public static TTSResponse Fail(string error) => new TTSResponse { Success = false, Error = error };
    }
}
