
/// <summary>
/// Holds all the structs used within the AI classes
/// </summary>

[System.Serializable]
public class InworldCredentials
{
    public string base64Key;
}

[System.Serializable]
public class GroqCredentials
{
    public string api_key;
    public string organization;
}

[System.Serializable]
public class TTSRequest
{
    public string text;
    public string voiceId;
    public string modelId;
    public AudioConfig audioConfig;
}

[System.Serializable]
public class AudioConfig
{
    public string audioEncoding;
    public int sampleRateHertz;
}

[System.Serializable]
public class TTSResponse
{
    // Base64 encoded audio string from Inworld
    public string audioContent;
}

public struct EmotionTimelineEvent
{
    public string emotion;
    public bool isNostalgic;
    public float startTime;
}
