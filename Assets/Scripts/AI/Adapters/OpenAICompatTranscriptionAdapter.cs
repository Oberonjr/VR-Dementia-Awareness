using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine.Networking;


namespace VRD_AIProviders
{
    public class OpenAICompatTranscriptionAdapter : ISTTService
    {
        private readonly STTModelDefinition sttModel;

        public OpenAICompatTranscriptionAdapter(STTModelDefinition sttModel)
        {
            this.sttModel = sttModel;
        }

        public async Task<TranscriptionResponse> TranscribeAsync(TranscriptionRequest request)
        {
            if (request?.Pcm16 == null || request.Pcm16.Length == 0)
            {
                return TranscriptionResponse.Fail("No audio to transcribe.");
            }

            if (!ProviderHttp.TryResolve(sttModel, "audio/transcriptions", out var target, out string error))
            {
                return TranscriptionResponse.Fail(error);
            }
            
            // WAV encoding is CPU heavy for long clips; keep off main thread
            byte[] wav =
                await Task.Run(() => SaveWav.SaveFromPCM16(request.Pcm16, request.SampleRate, request.Channels));
            var form = new List<IMultipartFormSection>
            {
                new MultipartFormFileSection("file", wav, "audio.wav", "audio/wav"),
                new MultipartFormDataSection("model", sttModel.modelId),
                new MultipartFormDataSection("response_format", "json")
            };
            if(!string.IsNullOrEmpty(request.Language)) form.Add(new MultipartFormDataSection("language", request.Language));
            string prompt = string.IsNullOrWhiteSpace(request.Prompt) ? sttModel.transcriptionPrompt : request.Prompt;
            if (sttModel.supportsPrompt && !string.IsNullOrWhiteSpace(prompt)) form.Add(new MultipartFormDataSection("prompt", prompt.Trim()));

            using (var req = UnityWebRequest.Post(target.Url, form))
            {
                ProviderHttp.Result res = await ProviderHttp.SendAsync(req, target);
                if (!res.OK) return TranscriptionResponse.Fail(res.Error);

                try
                {
                    string text = (string)JObject.Parse(res.Body)["text"];
                    return text == null
                        ? TranscriptionResponse.Fail($"No text in response:  {ProviderHttp.Preview(res.Body)}")
                        : TranscriptionResponse.OK(text.Trim());
                }
                catch (Exception e)
                {
                    return TranscriptionResponse.Fail($"Unreadable response ({e.Message}): {ProviderHttp.Preview(res.Body)}");
                }
            }
        }
    }
}

