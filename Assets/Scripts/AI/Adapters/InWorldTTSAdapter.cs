using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;

namespace VRD_AIProviders
{
    /// <summary>
    /// ITTSService for Inworld TTS (POST {baseUrl}/voice). Returns header-less mono PCM16.
    /// </summary>
    public class InWorldTTSAdapter : ITTSService
    {
        private const int WAV_HEADER_BYTES = 44;
        private readonly TTSModelDefinition ttsModel;

        public InWorldTTSAdapter(TTSModelDefinition ttsModel) => this.ttsModel = ttsModel;

        public async Task<TTSResponse> SynthesizeAsync(TTSRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Text)) return TTSResponse.Fail("No text to synthesize.");
            if (!ProviderHttp.TryResolve(ttsModel, "voice", out var target, out string error))
                return TTSResponse.Fail(error);

            var body = new JObject
            {
                ["text"] = FormatText(request),
                ["voiceId"] = string.IsNullOrWhiteSpace(request.VoiceID) ? ttsModel.voiceId : request.VoiceID,
                ["modelId"] = ttsModel.modelId,
                ["audioConfig"] = new JObject
                {
                    ["audioEncoding"] = "LINEAR16",
                    ["sampleRateHertz"] = ttsModel.sampleRate
                }
            };

            using (var req = new UnityWebRequest(target.Url, UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body.ToString()));
                req.SetRequestHeader("Content-Type", "application/json");
                ProviderHttp.Result res = await ProviderHttp.SendAsync(req, target);
                if (!res.OK) return TTSResponse.Fail(res.Error);

                try
                {
                    string audio = (string)JObject.Parse(res.Body)["audioContent"];
                    if (string.IsNullOrEmpty(audio))
                        return TTSResponse.Fail($"No audioContent in response:  {ProviderHttp.Preview(res.Body)}");
                    return TTSResponse.OK(StripWavHeader(Convert.FromBase64String(audio)), ttsModel.sampleRate, 1);
                }
                catch (Exception e)
                {
                    return TTSResponse.Fail($"Unreadable response ({e.Message}): {ProviderHttp.Preview(res.Body)}");
                }
            }
        }

        private string FormatText(TTSRequest request)
        {
            if (ttsModel.markup != EmotionMarkup.InworldTags || string.IsNullOrEmpty(request.Emotion) ||
                request.Emotion == "neutral")
            {
                return request.Text;
            }

            return $"[{request.Emotion}] {request.Text}";
        }

        private static byte[] StripWavHeader(byte[] audio)
        {
            bool hasHeader = audio.Length > WAV_HEADER_BYTES && audio[0] == 'R' && audio[1] == 'I' && audio[2] == 'F' && audio[3] == 'F';
            if (!hasHeader) return audio;
            byte[] pcm = new byte[audio.Length - WAV_HEADER_BYTES];
            Buffer.BlockCopy(audio, WAV_HEADER_BYTES, pcm, 0, pcm.Length);
            return pcm;
        }
    }
    
    
}