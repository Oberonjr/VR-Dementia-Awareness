using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central manager handling game state, input initialization, localization, and scene transitions
/// Coordinates sub-managers like post-processing and voice interaction
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Configuration")]
    public VolumeConfiguration volumeconfig;

    [Header("Controls")]
    public GameControlls gameInput;
    public bool conversationActive;

    public event Action OnInputSetup;

    private PostProcessingManager ppManager;
    private LocalizationManager localManager;
    private VoiceInteractionManager voiceInterManager;

    // To subscribe to processing events
    public VoiceInteractionManager VoiceInterManager => voiceInterManager;

    #region Inbuilt Methods

    private void Awake() { SetupSingleton(); }

    private void Start()
    {
        GetInstances();
        SetupInput();
        ChangeLocalization("en-GB");
    }

    private void OnDestroy() { if (Instance == this && gameInput != null) { gameInput.Disable(); } }

    #endregion

    #region Initialization

    private void SetupSingleton()
    {
        if (Instance == null) { Instance = this; }
        else
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    private void GetInstances()
    {
        ppManager = GetComponent<PostProcessingManager>();
        if (ppManager == null) { Debug.LogWarning("GameManager: Missing PostProcessingManager - Please attach the script to Transform of GameManager."); }
        localManager = GetComponent<LocalizationManager>();
        if (localManager == null) { Debug.LogWarning("GameManager: Missing LocalizationManager - Please attach the script to Transform of GameManager."); }
        voiceInterManager = GetComponentInChildren<VoiceInteractionManager>();
        if (voiceInterManager == null) { Debug.LogWarning("GameManager: Missing VoiceInteractionManager - Please attach AI Prefab as child of GameManager."); }
    }

    private void SetupInput()
    {
        gameInput = new GameControlls();
        gameInput.Enable();
        OnInputSetup?.Invoke();
    }

    #endregion

    #region Dialogue

    public void StartRecordingVoice()
    {
        if (voiceInterManager == null) { return; }
        voiceInterManager.StartRecording();
    }

    public void StopRecordingVoice()
    {
        if (voiceInterManager == null) { return; }
        voiceInterManager.StopRecordingAndProcess();
    }

    public void DiscardRecordingVoice()
    {
        if (voiceInterManager == null) { return; }
        voiceInterManager.DiscardRecording();
    }

    #endregion

    #region Localization Settings

    public void ChangeLocalization(string localIndex)
    {
        if (localManager == null) { return; }
        localManager.ChangeLanguage(localIndex);
    }

    #endregion

    #region Load Scene Logic

    public void LoadScene(int sceneIndex)
    {
        // If given index is invalid, load default level
        if (sceneIndex < 0 || sceneIndex > SceneManager.sceneCountInBuildSettings) { sceneIndex = 0; }
        SceneManager.LoadScene(sceneIndex);
    }

    public void LoadSceneNext()
    {
        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;

        // Check if next index is valid to load, else reset it to zero
        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings) { nextSceneIndex = 0; }
        SceneManager.LoadScene(nextSceneIndex);
    }

    public void RestartCurrentScene()
    {
        int currentSceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(currentSceneIndex);
    }

    #endregion

    #region Mood & Post Processing

    public void SetMoodPercentage(Mood mood, int percentage)
    {
        if (ppManager != null) { ppManager.SetMoodPercentage(mood, percentage); }
    }

    public void TransitionMood(VolumeConfiguration volumeConfiguration, float transitionTime)
    {
        if (ppManager != null) { ppManager.SwitchMood(volumeConfiguration, transitionTime); }
    }

    #endregion
}