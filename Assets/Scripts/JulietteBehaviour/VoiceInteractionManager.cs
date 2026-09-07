using FMODUnity;
using OpenAI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using System.IO;
using System.Text.RegularExpressions;
using Groq;

/// <summary>
/// Manages all the AI processes, going from STT, to text response, to TTS.
/// It was made FMOD compatible, and also localization settings compatible
/// Especially in terms of TTS, it separates the text into their emotions - sending multiple requests simultaneously.
/// Currently, it uses Groq for STT and text responses, and Inworld for TTS.
/// </summary>
public class VoiceInteractionManager : MonoBehaviour
{
    private const int MAX_RECORDING_SECONDS = 60;

    [Header("Groq Setup")]
    private GroqApi groq;
    private List<ChatMessage> messages = new List<ChatMessage>();
    [SerializeField] private string modelName = "llama-3.3-70b-versatile";
    [SerializeField] private const int MAXHISTORYMESSAGES = 12;

    [TextArea(5, 20)]
    [SerializeField] private string systemPrompt = "";

    // Stores the current language code for Whisper STT
    private string sttLanguage = "en";

    [Header("Setup")]
    [SerializeField] private InworldTTSClient inworldTTS;
    [SerializeField] private LipSyncController lipSyncController;

    [Header("FMOD Mic Recording Setup")]
    private FMOD.Sound micSound;
    private int nativeRate;
    private int nativeChannels;
    private int recordDeviceId = 0; // Default microphone
    private bool isRecording = false;
    private float recordingStartTime = 0f; // Tracks when the recording started
    
    [Header("FMOD Playback Setup")]
    [SerializeField] private EventReference fmodDialogueEvent;
    private FMOD.Studio.EventInstance dialogueInstance;
    private GCHandle stringHandle;

    public List<EmotionTimelineEvent> CurrentEmotionTimeline { get; private set; } = new List<EmotionTimelineEvent>();

    // For showing off progress during processing
    public event Action OnProcessingStarted;
    public event Action OnProcessingFinished;

    public bool IsRecording => isRecording;

    public bool IsSpeaking
    {
        get
        {
            if (!dialogueInstance.isValid()) { return false; }
            dialogueInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state);
            return state == FMOD.Studio.PLAYBACK_STATE.PLAYING || state == FMOD.Studio.PLAYBACK_STATE.STARTING;
        }
    }

    public float GetCurrentDialogueTime()
    {
        if (!dialogueInstance.isValid()) return 0f;
        dialogueInstance.getTimelinePosition(out int posMs);
        return posMs / 1000f; // Convert ms to seconds
    }

    private IEnumerator Start()
    {
        // Load the credentials dynamically based on the current platform
        LoadGroqCredentials();

#if UNITY_ANDROID
        // Request microphone permission on Android (Meta Quest) devices
        if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(UnityEngine.Android.Permission.Microphone))
        {
            UnityEngine.Android.Permission.RequestUserPermission(UnityEngine.Android.Permission.Microphone);
        }

        // Give the OS a brief moment in case a permission popup appears
        yield return new WaitForSeconds(0.5f);
#endif

        // Wait for the Unity Localization system to finish initializing
        yield return LocalizationSettings.InitializationOperation;

        // Subscribe to the built-in Unity Localization change event
        LocalizationSettings.SelectedLocaleChanged += HandleLocalizationChanged;

        // Sync with the currently active language at startup
        if (LocalizationSettings.SelectedLocale != null)
        {
            HandleLocalizationChanged(LocalizationSettings.SelectedLocale);
        }

        // Initialize the microphone in the background during startup to prevent frame drops later
        if (!micSound.hasHandle())
        {
            InitFMODMicrophone();
        }
    }

    private void Update()
    {
        // Automatically stop and process if the maximum recording time is reached
        if (isRecording)
        {
            float currentTime = Time.time - recordingStartTime;
            float currentProgress = Mathf.InverseLerp(MAX_RECORDING_SECONDS, 0, currentTime);
            EventBus<OnUpdateTalkTimer>.Publish(new OnUpdateTalkTimer(currentProgress));

            if(currentTime >= MAX_RECORDING_SECONDS)
            {
                Debug.Log($"Maximum recording duration of {MAX_RECORDING_SECONDS} seconds reached. Auto-stopping...");
                StopRecordingAndProcess();
            }
        }
    }

    public void DiscardRecording()
    {
        if (!isRecording) { return; }
        isRecording = false;

        // Stop FMOD without processing the data
        RuntimeManager.CoreSystem.recordStop(recordDeviceId);
        Debug.Log("Recording discarded.");
    }

    public void StartRecording()
    {
        // Fallback: Delaying initialization ensures the user had time to accept Android Mic permissions
        if (!micSound.hasHandle())
        {
            InitFMODMicrophone();
            // Abort if initialization failed
            if (!micSound.hasHandle()) { return; }
        }

        isRecording = true;
        recordingStartTime = Time.time; // Store the exact time the recording started

        // Start recording into our custom micSound object (false = no looping)
        RuntimeManager.CoreSystem.recordStart(recordDeviceId, micSound, false);
        Debug.Log("FMOD Recording started...");
    }

    public async void StopRecordingAndProcess()
    {
        if (!isRecording) { return; }
        isRecording = false;

        OnProcessingStarted?.Invoke();

        // Retrieve the current recording position before stopping
        RuntimeManager.CoreSystem.getRecordPosition(recordDeviceId, out uint recordPosition);

        // Stop the FMOD recording process
        RuntimeManager.CoreSystem.recordStop(recordDeviceId);
        Debug.Log($"FMOD Recording stopped. Recorded {recordPosition} samples. Processing STT...");

        // Extract the raw PCM bytes
        byte[] pcmData = GetRecordedPCMData(recordPosition);

        if (pcmData == null || pcmData.Length == 0)
        {
            Debug.LogError("Failed to extract audio data from FMOD.");
            OnProcessingFinished?.Invoke();
            return;
        }

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        stopwatch.Start();
        // Process Speech-To-Text (Groq Whisper)
        string userText = await TranscribeAudio(pcmData);
        stopwatch.Stop();
        Debug.Log($"[MEASURE] STT: {stopwatch.ElapsedMilliseconds} ms | User said: {userText}");

        stopwatch.Restart();
        // Generate AI Text Response (Groq Llama)
        string aiResponseText = await GetAIResponse(userText);
        stopwatch.Stop();
        Debug.Log($"[MEASURE] LLM: {stopwatch.ElapsedMilliseconds} ms | AI Response: {aiResponseText}");

        stopwatch.Restart();
        // Process Text-To-Speech (Inworld)
        await PlayInworldTTS(aiResponseText);
        stopwatch.Stop();
        Debug.Log($"[MEASURE] TTS: {stopwatch.ElapsedMilliseconds} ms | TTS Finished.");

        OnProcessingFinished?.Invoke();
        EventBus<OnHideTalk>.Publish(new OnHideTalk());
        StartCoroutine(WaitForSpeechToFinish());

    }

    private void HandleLocalizationChanged(Locale newLocale)
    {
        // We use StartsWith to catch all variations of english language
        if (newLocale.Identifier.Code.StartsWith("en"))
        {
            sttLanguage = "en";
            // Specifically replace the response rule to keep her a "Dutch woman" in the prompt
            systemPrompt = systemPrompt.Replace("All responses must be in Dutch.", "All responses must be in English.");
            Debug.Log($"Language switched to English (Code: {newLocale.Identifier.Code}).");
        }
        else if (newLocale.Identifier.Code.StartsWith("nl"))
        {
            sttLanguage = "nl";
            systemPrompt = systemPrompt.Replace("All responses must be in English.", "All responses must be in Dutch.");
            Debug.Log($"Language switched to Dutch (Code: {newLocale.Identifier.Code}).");
        }

        // Apply the newly translated system prompt to the active history immediately if it exists
        if (messages.Count > 0 && messages[0].Role == "system")
        {
            ChatMessage updatedSystemMessage = messages[0];
            updatedSystemMessage.Content = systemPrompt;
            messages[0] = updatedSystemMessage; // Force value back into the list indexer
            Debug.Log("System prompt updated dynamically in active conversation history.");
        }
    }

    private void InitFMODMicrophone()
    {
        // Check if FMOD detects any input drivers
        RuntimeManager.CoreSystem.getRecordNumDrivers(out int numDrivers, out _);
        if (numDrivers == 0)
        {
            Debug.LogError("No recording devices found by FMOD! Check your Microphone permissions.");
            return;
        }

        // Get hardware details from the default microphone
        RuntimeManager.CoreSystem.getRecordDriverInfo(recordDeviceId, out string name, 128, out _, out int originalRate, out _, out int originalChannels, out _);

        // Whisper uses 16kHz Mono anyways, so keep it capped to save resources
        nativeRate = 16000;
        nativeChannels = 1;

        Debug.Log($"Initialized FMOD Mic: {name} | Original: {originalRate}Hz | Forced for STT: {nativeRate}Hz Mono");

        // Prepare an FMOD struct to allocate memory for the recording
        FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
        exinfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = nativeChannels;
        exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.defaultfrequency = nativeRate;
        exinfo.length = (uint)(nativeRate * sizeof(short) * nativeChannels * MAX_RECORDING_SECONDS);

        // Create the custom sound object in memory
        RuntimeManager.CoreSystem.createSound("", FMOD.MODE.DEFAULT | FMOD.MODE.OPENUSER, ref exinfo, out micSound);
    }

    private byte[] GetRecordedPCMData(uint recordPosition)
    {
        // Abort if no data was recorded
        if (recordPosition == 0) { return null; }

        // Calculate the file size in bytes (Samples * 2 bytes per short * number of channels)
        uint lengthInBytes = recordPosition * sizeof(short) * (uint)nativeChannels;

        // Lock the FMOD memory area to safely read the data
        FMOD.RESULT result = micSound.@lock(0, lengthInBytes, out IntPtr ptr1, out IntPtr ptr2, out uint len1, out uint len2);

        if (result != FMOD.RESULT.OK)
        {
            Debug.LogError($"Failed to lock FMOD sound buffer: {result}");
            return null;
        }

        byte[] audioBytes = new byte[len1 + len2];

        // Copy the data from the unmanaged FMOD memory to our managed C# byte array
        if (len1 > 0) Marshal.Copy(ptr1, audioBytes, 0, (int)len1);
        if (len2 > 0) Marshal.Copy(ptr2, audioBytes, (int)len1, (int)len2);

        // Unlock the memory area
        micSound.unlock(ptr1, ptr2, len1, len2);

        return audioBytes;
    }

    private async Task<string> TranscribeAudio(byte[] pcmData)
    {
        // Offload the heavy byte array conversion to a background thread to prevent the main thread from freezing
        byte[] wavData = await Task.Run(() =>
        {
            return SaveWav.SaveFromPCM16(pcmData, nativeRate, nativeChannels);
        });

        CreateAudioTranscriptionsRequest req = new CreateAudioTranscriptionsRequest
        {
            FileData = new FileData() { Data = wavData, Name = "audio.wav" },
            Model = "whisper-large-v3",
            Language = sttLanguage
        };

        CreateAudioResponse res = await groq.CreateAudioTranscription(req);
        return res.Text;
    }

    private async Task<string> GetAIResponse(string prompt)
    {
        ChatMessage newMessage = new ChatMessage()
        {
            Role = "user",
            Content = prompt
        };

        // Inject the system prompt if the conversation history is empty
        if (messages.Count == 0)
        {
            messages.Add(new ChatMessage() { Role = "system", Content = systemPrompt });
        }
        else
        {
            // Update the system prompt in the history dynamically if language changed mid-conversation
            if (messages[0].Role == "system")
            {
                ChatMessage updatedSystemMessage = messages[0];
                updatedSystemMessage.Content = systemPrompt;
                messages[0] = updatedSystemMessage; // Force value back into the list indexer
            }
        }

        messages.Add(newMessage);
        if (messages.Count > MAXHISTORYMESSAGES + 1)
        {
            int removeCount = messages.Count - (MAXHISTORYMESSAGES + 1);
            messages.RemoveRange(1, removeCount);
        }

        CreateChatCompletionRequest req = new CreateChatCompletionRequest
        {
            Model = modelName,
            Messages = messages,
            Temperature = 0.7f,
            MaxTokens = 300
        };

        CreateChatCompletionResponse res = await groq.CreateChatCompletion(req);

        if (res.Choices != null && res.Choices.Count > 0)
        {
            var responseMessage = res.Choices[0].Message;
            responseMessage.Content = responseMessage.Content.Trim();

            // Append the AI response to the history to maintain context
            messages.Add(responseMessage);

            return responseMessage.Content;
        }

        return "Error: No response generated.";
    }

    private async Task PlayInworldTTS(string aiResponseText)
    {
        Debug.Log("Parsing emotions and sending parallel TTS requests...");

        // Define a strict list of allowed tags to filter out any hallucinated AI brackets
        HashSet<string> validTags = new HashSet<string>
        {
            "neutral", "happy", "sad", "angry", "fearful", "surprised", "disgusted",
            "nostalgic", "laugh", "sigh", "cough", "breathe"
        };

        // Filter out any custom or non-supported tags generated by the AI
        aiResponseText = Regex.Replace(aiResponseText, @"\[(.*?)\]", match =>
        {
            string tag = match.Groups[1].Value.ToLower().Trim();
            return validTags.Contains(tag) ? match.Value : string.Empty;
        });

        // Ensure the string starts with a tag so our Regex catches the first sentence
        if (!aiResponseText.TrimStart().StartsWith("["))
        {
            aiResponseText = "[neutral] " + aiResponseText;
        }

        List<string> chunkedRequests = new List<string>();
        List<EmotionTimelineEvent> tempTimeline = new List<EmotionTimelineEvent>();

        // Regex extracts the word inside the brackets (Group 1) and the text following it (Group 2)
        // e.g., "[happy] Hello!" -> Group 1: "happy", Group 2: " Hello!"
        MatchCollection matches = Regex.Matches(aiResponseText, @"\[(.*?)\]([^\[]*)");

        bool currentNostalgic = false;
        string currentEmotion = "neutral";

        foreach (Match match in matches)
        {
            string emotionTag = match.Groups[1].Value.ToLower().Trim();
            string textSegment = match.Groups[2].Value.Trim();

            // Intercept nostalgic tag
            if (emotionTag == "nostalgic")
            {
                currentNostalgic = true;
                // If there's no text right after the nostalgic tag, just skip to the next tag (which should be the emotion)
                if (string.IsNullOrWhiteSpace(textSegment)) continue;
            }
            else
            {
                currentEmotion = emotionTag;
            }

            if (!string.IsNullOrEmpty(textSegment))
            {
                // Register this chunk in our timeline (startTime will be calculated after download)
                tempTimeline.Add(new EmotionTimelineEvent
                {
                    emotion = currentEmotion,
                    isNostalgic = currentNostalgic,
                    startTime = 0f
                });

                if (currentEmotion == "neutral")
                {
                    // Strip the [neutral] tag entirely and send only the text.
                    // This forces Inworld to use its default voice without reading the tag out loud.
                    chunkedRequests.Add(textSegment);
                }
                else
                {
                    // Keep the valid tags for Inworld (e.g., [happy] Hello there!)
                    chunkedRequests.Add($"[{currentEmotion}] {textSegment}");
                }

                // Reset nostalgic flag for the next parsed bracket
                currentNostalgic = false;
            }
        }

        // Fire all requests simultaneously
        Task<byte[]>[] fetchTasks = new Task<byte[]>[chunkedRequests.Count];
        for (int i = 0; i < chunkedRequests.Count; i++)
        {
            fetchTasks[i] = inworldTTS.GenerateSpeechBytes(chunkedRequests[i]);
        }

        // Wait until all parallel tasks have returned their audio bytes
        byte[][] audioDataArray = await Task.WhenAll(fetchTasks);

        string tempPath = Path.Combine(Application.temporaryCachePath, "stitched_voice.wav");

        // Stitch audio bytes together
        List<byte> stitchedPCM = new List<byte>();

        // Offload the heavy array stitching and file writing to a background thread
        await Task.Run(() =>
        {
            // Artificial Pause length
            float pauseDurationSeconds = 0.2f;
            byte[] silence = new byte[(int)(44100 * 2 * pauseDurationSeconds)];

            float accumulatedTime = 0f;

            for (int i = 0; i < audioDataArray.Length; i++)
            {
                byte[] audio = audioDataArray[i];
                if (audio != null && audio.Length > 0)
                {
                    // Inworld occasionally includes a 44-byte WAV header. 
                    // We strip it off here to prevent loud 'pops' between chunks.
                    int startIndex = (audio.Length > 44 && audio[0] == 'R' && audio[1] == 'I' && audio[2] == 'F' && audio[3] == 'F') ? 44 : 0;

                    // Update the timeline with the exact start time of this chunk
                    if (i < tempTimeline.Count)
                    {
                        var ev = tempTimeline[i];
                        ev.startTime = accumulatedTime;
                        tempTimeline[i] = ev;
                    }

                    // Calculate duration of this specific chunk to offset the next one
                    int dataLength = audio.Length - startIndex;
                    float chunkDuration = dataLength / (44100f * 2f * 1f);

                    for (int j = startIndex; j < audio.Length; j++)
                    {
                        stitchedPCM.Add(audio[j]);
                    }

                    accumulatedTime += chunkDuration;

                    // Add a brief silence between chunks, but not after the very last one
                    if (i < audioDataArray.Length - 1)
                    {
                        stitchedPCM.AddRange(silence);
                        accumulatedTime += pauseDurationSeconds;
                    }
                }
            }

            // Wrap a new WAV header around the combined raw PCM bytes
            // Inworld defaults to 44100Hz, 1 Channel (Mono)
            byte[] finalWavBytes = SaveWav.SaveFromPCM16(stitchedPCM.ToArray(), 44100, 1);

            // Save to cache
            File.WriteAllBytes(tempPath, finalWavBytes);
        });

        // Set the active timeline for the ExpressionController to read
        CurrentEmotionTimeline = tempTimeline;

        // Play in FMOD (Must be called on the main thread after the background task finishes)
        PlayFMODProgrammerSound(tempPath);

        // Pass raw bytes and FMOD instance to the Lipsync Controller
        if (lipSyncController != null)
        {
            // Inworld defaults to 44100Hz
            lipSyncController.PrepareAndPlayVisemes(stitchedPCM.ToArray(), 44100, dialogueInstance);
        }
    }

    private void PlayFMODProgrammerSound(string filePath)
    {
        // Stop and clean up any currently playing dialogue instance
        if (dialogueInstance.isValid())
        {
            dialogueInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            dialogueInstance.release();
        }

        dialogueInstance = RuntimeManager.CreateInstance(fmodDialogueEvent);

        // Pin the file path string in memory to prevent the garbage collector from moving it
        stringHandle = GCHandle.Alloc(filePath, GCHandleType.Pinned);
        dialogueInstance.setUserData(GCHandle.ToIntPtr(stringHandle));

        // Assign callbacks to handle the creation and destruction of the programmer sound
        dialogueInstance.setCallback(ProgrammerSoundCallback,
            FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND |
            FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND);

        dialogueInstance.start();
        dialogueInstance.release();
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.Studio.EVENT_CALLBACK))]
    private static FMOD.RESULT ProgrammerSoundCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr instancePtr, IntPtr parameterPtr)
    {
        FMOD.Studio.EventInstance instance = new FMOD.Studio.EventInstance(instancePtr);

        // Retrieve the pinned string pointer from the event instance
        instance.getUserData(out IntPtr stringPtr);

        GCHandle stringHandle = GCHandle.FromIntPtr(stringPtr);
        string audioFilePath = stringHandle.Target as string;

        switch (type)
        {
            case FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND:
                {
                    var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));

                    // Load the audio file directly into an FMOD Sound object
                    FMOD.RESULT result = RuntimeManager.CoreSystem.createSound(audioFilePath, FMOD.MODE.DEFAULT | FMOD.MODE.CREATESTREAM, out FMOD.Sound dialogueSound);

                    if (result == FMOD.RESULT.OK)
                    {
                        // Assign the sound handle to the programmer instrument parameter
                        parameter.sound = dialogueSound.handle;
                        parameter.subsoundIndex = -1;
                        Marshal.StructureToPtr(parameter, parameterPtr, false);
                    }
                    break;
                }
            case FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND:
                {
                    var parameter = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameterPtr, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));

                    // Reconstruct the FMOD Sound object and release it from memory
                    var sound = new FMOD.Sound(parameter.sound);
                    sound.release();

                    // Free the pinned string handle
                    stringHandle.Free();
                    break;
                }
        }
        return FMOD.RESULT.OK;
    }

    private void LoadGroqCredentials()
    {
        string authFilePath = "";

        #if UNITY_EDITOR || UNITY_STANDALONE
        // Running on PC: Use the standard plugin path
        string userProfilePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        authFilePath = Path.Combine(userProfilePath, ".openai", "auth.json");
        #elif UNITY_ANDROID
        // Running on Meta Quest: Use the persistent data path
        authFilePath = Path.Combine(Application.persistentDataPath, "openai_auth.json");
        #endif

        if (File.Exists(authFilePath))
        {
            string jsonContent = File.ReadAllText(authFilePath);
            GroqCredentials creds = JsonUtility.FromJson<GroqCredentials>(jsonContent);

            // Initialize Groq API by explicitly passing the key, bypassing the plugin's default PC path
            groq = new GroqApi(creds.api_key, creds.organization);
            Debug.Log($"Groq credentials loaded successfully from: {authFilePath}");
        }
        else
        {
            Debug.LogError($"Groq Auth file missing at: {authFilePath}");
            // Fallback to empty initialization (will likely fail if no environment variables are set)
            groq = new GroqApi();
        }
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocalizationChanged;

        // Ensure the FMOD recording sound buffer is released when the object is destroyed
        if (micSound.hasHandle()) { micSound.release(); }
    }

    private IEnumerator WaitForSpeechToFinish()
    {
        yield return new WaitForSeconds(0.5f);

        while (GameManager.Instance.VoiceInterManager.IsSpeaking) { yield return null; }

        EventBus<OnJulietteFinishedTalk>.Publish(new OnJulietteFinishedTalk());
    }
}