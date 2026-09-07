using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

/// <summary>
/// Handles authentication and web requests to Inworld for TTS voice generation
/// </summary>
public class InworldTTSClient : MonoBehaviour
{
    [Header("Inworld TTS Settings")]
    [SerializeField] private string voiceId = "default-att0kitmzafhiciftzuddw__alzheimer_nederland_material";
    [SerializeField] private string modelId = "inworld-tts-1.5-mini";

    private string base64AuthToken;

    private void Awake()
    {
        LoadLocalCredentials();
    }

    private void LoadLocalCredentials()
    {
        string authFilePath = "";

#if UNITY_EDITOR || UNITY_STANDALONE
        // Running on PC (Editor or Windows Build): Use the local user profile path
        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        authFilePath = Path.Combine(userProfilePath, ".inworld", "auth.json");
#elif UNITY_ANDROID
        // Running on Meta Quest (Android): Use the persistent data path
        authFilePath = Path.Combine(Application.persistentDataPath, "inworld_auth.json");
#endif

        if (File.Exists(authFilePath))
        {
            string jsonContent = File.ReadAllText(authFilePath);
            InworldCredentials creds = JsonUtility.FromJson<InworldCredentials>(jsonContent);

            base64AuthToken = creds.base64Key;
            Debug.Log($"Inworld Base64 token loaded successfully from: {authFilePath}");
        }
        else
        {
            Debug.LogError($"Auth file missing at: {authFilePath}. Please ensure the file is placed correctly for the current platform.");
        }
    }

    public async Task<byte[]> GenerateSpeechBytes(string textToSpeak)
    {
        if (string.IsNullOrEmpty(base64AuthToken)) { Debug.LogError("Cannot generate speech. Missing auth token."); return null; }

        TTSRequest req = new TTSRequest
        {
            text = textToSpeak,
            voiceId = this.voiceId,
            modelId = this.modelId,
            audioConfig = new AudioConfig
            {
                audioEncoding = "LINEAR16",
                sampleRateHertz = 44100
            }
        };

        string jsonPayload = JsonUtility.ToJson(req);

        using (UnityWebRequest www = new UnityWebRequest("https://api.inworld.ai/tts/v1/voice", "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonPayload);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");
            www.SetRequestHeader("Authorization", $"Basic {base64AuthToken}");

            UnityWebRequestAsyncOperation operation = www.SendWebRequest();
            while (!operation.isDone) { await Task.Yield(); }

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Inworld TTS Error: {www.error} - {www.downloadHandler.text}");
                return null;
            }

            TTSResponse response = JsonUtility.FromJson<TTSResponse>(www.downloadHandler.text);
            return Convert.FromBase64String(response.audioContent);
        }
    }
}