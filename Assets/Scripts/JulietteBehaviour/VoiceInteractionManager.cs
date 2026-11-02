using FMODUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.Serialization;
using VRD_AIProviders;

/// <summary>
/// Orchestrates one conversation turn with Juliette: mic recording -> STT -> StoryBeat judgement -> LLM reply -> TTS playback.
/// The actual work is done by the helpers in JulietteBehaviour/Dialogue; this class owns their inspector settings
/// and exposes the state other scripts read (recording, speaking, emotion timeline).
/// It was made FMOD compatible, and also localization settings compatible.
/// </summary>
public class VoiceInteractionManager : MonoBehaviour
{
    // Inworld TTS is requested as 44100Hz mono (see InworldTTSClient)
    private const int INWORLD_SAMPLE_RATE = 44100;

    [Header("Language")]
    public CurrentLanguage currentLanguage;

    [Header("LLM Setup")]
    [FormerlySerializedAs("dialogueModel")] [SerializeField] private ChatModelDefinition dialogueChatModel;
    [FormerlySerializedAs("classifierModel")] [SerializeField] private ChatModelDefinition classifierChatModel; //Model used to quickly classify if the given beat is a Success or Failure
    private ILLMService dialogueLLM;
    private ILLMService classifierLLM;
    [FormerlySerializedAs("STTModelName")][SerializeField] private STTModelDefinition sttModel;
    private ISTTService stt;
    [SerializeField] private float chatLLMTemp = 0.7f;
    [SerializeField] private int chatLLMMaxTokens = 1024;
    
    
    [Header("SCENARIO")] [TextArea(5, 20)]
    [SerializeField] private string scenarioDescription = "";

    [Header("CHARACTER")]
    [TextArea(5, 20)]
    [SerializeField] private string characterDescription = "Name: \n Age: \n Description:";

    [Header("SYSTEM RULES")]
    [TextArea(5, 20)]
    [SerializeField] private string systemRules = "Output format: \n Response Constraints: \n Guardrails: ";
    [SerializeField] private CoreRules coreRules = null;

    // Stores the current language code for Whisper STT
    private string sttLanguage => currentLanguage switch
    {
        CurrentLanguage.English => "en",
        CurrentLanguage.Dutch => "nl",
        _ => "en"
    };
    private CurrentLanguage lastLanguage;

    [Header("Setup")]
    [SerializeField] private InworldTTSClient inworldTTS;
    [SerializeField] private LipSyncController lipSyncController;

    [Header("FMOD Playback Setup")]
    [SerializeField] private EventReference fmodDialogueEvent;

    private readonly MicRecorder mic = new MicRecorder();
    private readonly ConversationHistory history = new ConversationHistory();
    private BeatJudge beatJudge;
    private SpeechPlayer speechPlayer;

    public List<EmotionTimelineEvent> CurrentEmotionTimeline => speechPlayer?.Timeline ?? new List<EmotionTimelineEvent>();

    // For showing off progress during processing
    public event Action OnProcessingStarted;
    public event Action OnProcessingFinished;

    public bool IsRecording => mic.IsRecording;

    public bool IsSpeaking => speechPlayer != null && speechPlayer.IsSpeaking;

    public float GetCurrentDialogueTime() => speechPlayer?.CurrentTime ?? 0f;

    private StoryBeatManager storyBeatManager;

    private IEnumerator Start()
    {
        if (coreRules == null || coreRules.Rules == null || coreRules.Rules == string.Empty || string.IsNullOrEmpty(coreRules.OutcomeClassifierPrompt))
        {
            throw new SystemException("Core rules not assigned or the rules were empty. Please assign a valid core rules.");
        }
        lastLanguage = currentLanguage;
        storyBeatManager = StoryBeatManager.Instance;
        //Concatenate the multiple sections into one prompt for the LLM
        RebuildSystemPrompt();

        dialogueLLM = new OpenAICompatChatAdapter(dialogueChatModel);
        classifierLLM = new OpenAICompatChatAdapter(classifierChatModel);
        stt = new OpenAICompatTranscriptionAdapter(sttModel);
        beatJudge = new BeatJudge(classifierLLM, coreRules, storyBeatManager, history);
        speechPlayer = new SpeechPlayer(fmodDialogueEvent, lipSyncController);
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
        if (!mic.IsInitialized)
        {
            mic.Init();
        }
    }

    private void Update()
    {
        // Automatically stop and process if the maximum recording time is reached
        if (mic.IsRecording)
        {
            float currentTime = mic.Elapsed;
            float currentProgress = Mathf.InverseLerp(MicRecorder.MAX_RECORDING_SECONDS, 0, currentTime);
            EventBus<OnUpdateTalkTimer>.Publish(new OnUpdateTalkTimer(currentProgress));

            if(currentTime >= MicRecorder.MAX_RECORDING_SECONDS)
            {
                Debug.Log($"Maximum recording duration of {MicRecorder.MAX_RECORDING_SECONDS} seconds reached. Auto-stopping...");
                StopRecordingAndProcess();
            }
        }
    }

    public void DiscardRecording() => mic.DiscardRecording();

    public void StartRecording() => mic.StartRecording();

    public async void StopRecordingAndProcess()
    {
        if (!mic.IsRecording) { return; }

        byte[] pcmData = mic.StopRecording();
        OnProcessingStarted?.Invoke();

        if (pcmData == null || pcmData.Length == 0)
        {
            Debug.LogError("Failed to extract audio data from FMOD.");
            OnProcessingFinished?.Invoke();
            return;
        }

        System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        stopwatch.Start();
        // Process Speech-To-Text
        string userText = await TranscribeAudio(pcmData);
        stopwatch.Stop();
        Debug.Log($"[MEASURE] STT: {stopwatch.ElapsedMilliseconds} ms | User: {userText}");
        if (string.IsNullOrWhiteSpace(userText))
        {
            Debug.LogWarning("[STT] Empty transcript - skipping this turn.");
            OnProcessingFinished?.Invoke();
            EventBus<OnHideTalk>.Publish(new OnHideTalk());
            return;
        }

        stopwatch.Restart();
        if (await beatJudge.ResolveAsync(userText)) RebuildSystemPrompt();
        stopwatch.Stop();
        Debug.Log($"[MEASURE] Beat resolve: {stopwatch.ElapsedMilliseconds} ms");
        // Generate AI Text Response
        stopwatch.Restart();
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
            currentLanguage = CurrentLanguage.English;
            Debug.Log($"Language switched to English (Code: {newLocale.Identifier.Code}).");
        }
        else if (newLocale.Identifier.Code.StartsWith("nl"))
        {
            currentLanguage = CurrentLanguage.Dutch;
            Debug.Log($"Language switched to Dutch (Code: {newLocale.Identifier.Code}).");
        }
        // Also applies the newly translated system prompt to the active history
        RebuildSystemPrompt();
        lastLanguage = currentLanguage;
    }

    private async Task<string> TranscribeAudio(byte[] pcmData)
    {
        TranscriptionResponse res = await stt.TranscribeAsync(new TranscriptionRequest
        {
            Pcm16 = pcmData,
            SampleRate = mic.SampleRate,
            Channels = mic.Channels,
            Language = sttLanguage
        });
        if (!res.Success)
        {
            Debug.LogError($"[STT] {res.Error}");
            return null;
        }

        return res.Text;
    }

    private async Task<string> GetAIResponse(string prompt)
    {
        var outgoing = new List<ChatMessage>(history.Messages);
        string directive = storyBeatManager != null ? storyBeatManager.GetDirective() : string.Empty;
        if (!string.IsNullOrWhiteSpace(directive))
        {
            outgoing.Add(new ChatMessage("system", "Directive for Juliette's next reply. It has NOT been carried out yet. Do it now, even if the conversation already touched on it:\n" + directive));
        }
        
        history.AddUser(prompt);
        ChatResponse res = await dialogueLLM.CompleteAsync(new ChatRequest
        {
            Messages = outgoing,
            Temperature = chatLLMTemp,
            MaxTokens = chatLLMMaxTokens
        });
        if (!res.Success)
        {
            Debug.LogError($"[LLM] {res.Error}");
            return "Error: No response generated";
        }

        string content = res.Text.Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            Debug.LogError($"[LLM] Empty reply (finish: {res.FinishReason}).");
            return string.Empty;
        }
        if(storyBeatManager != null) storyBeatManager.RegisterNpcTurn();
        history.AddAssistant(content);
        return content;
    }

    private async Task PlayInworldTTS(string aiResponseText)
    {
        Debug.Log("Parsing emotions and sending parallel TTS requests...");
        var chunks = EmotionChunker.Parse(aiResponseText);

        // Fire all requests simultaneously and wait until all of them have returned their audio bytes
        byte[][] audioDataArray = await Task.WhenAll(chunks.Select(c => inworldTTS.GenerateSpeechBytes(c.requestText)));
        if (audioDataArray.All(a => a == null || a.Length == 0))
        {
            Debug.LogWarning($"[TTS] No audio produced for response: '{aiResponseText}'. Skipping playback.");
            return;
        }

        await speechPlayer.PlayAsync(chunks.Select(c => c.timelineEvent).ToList(), audioDataArray, INWORLD_SAMPLE_RATE);
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocalizationChanged;

        // Ensure the FMOD recording sound buffer is released when the object is destroyed
        mic.Dispose();
    }

    private IEnumerator WaitForSpeechToFinish()
    {
        yield return new WaitForSeconds(0.5f);

        while (IsSpeaking) { yield return null; }

        EventBus<OnJulietteFinishedTalk>.Publish(new OnJulietteFinishedTalk());
    }

    private void RebuildSystemPrompt()
    {
        string currentDirective = storyBeatManager != null ? storyBeatManager.GetDirective() : string.Empty;
        history.SetSystem(PromptBuilder.Build(scenarioDescription, characterDescription, systemRules, coreRules, currentLanguage, currentDirective));
    }

    //Ensure that a language change in the inspector during playmode actually notifies the systems
    private void OnValidate()
    {
        if (currentLanguage != lastLanguage)
        {
            lastLanguage = currentLanguage;
            //if (!Application.isPlaying) { return; }
            RebuildSystemPrompt();
        }
    }
}
