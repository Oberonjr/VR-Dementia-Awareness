using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class JulietteConversation : SimulationTask
{
    [Header("Input Setup")]
    [SerializeField] private InputActionReference actionButton;

    [Header("Conversation Rules")]
    [SerializeField] private int maxQuestions = 5;

    private int questionsAsked;
    private bool isProcessing;
    private bool isSitting;

    private void Start()
    {
        EventBus<OnPlayerSitDown>.OnEvent += PlayerSatDown;
    }

    private void OnDestroy()
    {
        EventBus<OnRequestTalk>.OnEvent -= TriggerDialogue;
        EventBus<OnJulietteFinishedTalk>.OnEvent -= SpeechFinished;

        if (actionButton != null)
        {
            actionButton.action.started -= OnButtonStarted;
            actionButton.action.canceled -= OnButtonCanceled;
            actionButton.action.Disable();
            EventBus<OnPlayerSitDown>.OnEvent -= PlayerSatDown;
        }
    }

    public override void StartTask()
    {
        base.StartTask();

        EventBus<OnRequestTalk>.OnEvent += TriggerDialogue;
        GameManager.Instance.conversationActive = true;

        EventBus<OnPlayerSitDown>.Publish(new OnPlayerSitDown(isSitting));
        EventBus<OnUpdateTask>.Publish(new OnUpdateTask());
    }

    public override void FinishTask()
    {
        base.FinishTask();

        GameManager.Instance.conversationActive = false;
        EventBus<OnPlayerSitDown>.OnEvent -= PlayerSatDown;
        EventBus<OnHideTalk>.Publish(new OnHideTalk());

        if (actionButton != null)
        {
            actionButton.action.Disable();
            actionButton.action.started -= OnButtonStarted;
            actionButton.action.canceled -= OnButtonCanceled;
        }
    }

    private void TriggerDialogue(OnRequestTalk evt)
    {
        bool isRecording = GameManager.Instance.VoiceInterManager.IsRecording;

        if (!isRecording) { StartRecording(); }
        else { FinishedRecording(); }
    }

    private void StartRecording()
    {
        if (isProcessing || questionsAsked >= maxQuestions || GameManager.Instance == null) { return; }

        bool isRecording = GameManager.Instance.VoiceInterManager.IsRecording;

        if (!isRecording)
        {
            GameManager.Instance.StartRecordingVoice();
            EventBus<OnShowMicrophonePickup>.Publish(new OnShowMicrophonePickup());
        }
    }

    private void OnButtonStarted(InputAction.CallbackContext context)
    {
        StartRecording();
    }

    private void OnButtonCanceled(InputAction.CallbackContext context)
    {
        FinishedRecording();
    }

    private void FinishedRecording()
    {
        if (isProcessing || questionsAsked >= maxQuestions || GameManager.Instance == null) { return; }

        bool isRecording = GameManager.Instance.VoiceInterManager.IsRecording;

        if (isRecording)
        {
            ProcessRecording();
            EventBus<OnShowProcessing>.Publish(new OnShowProcessing());
        }
    }

    private void ProcessRecording()
    {
        GameManager.Instance.StopRecordingVoice();
        questionsAsked++;
        EventBus<OnHideTalk>.Publish(new OnHideTalk());
        EventBus<OnJulietteFinishedTalk>.OnEvent += SpeechFinished;
    }

    private void SpeechFinished(OnJulietteFinishedTalk evt)
    {
        EventBus<OnJulietteFinishedTalk>.OnEvent -= SpeechFinished;

        if (questionsAsked >= maxQuestions)
        {
            EventBus<OnHideTalk>.Publish(new OnHideTalk());
            FinishTask();
        }
        else
        {
            EventBus<OnShowAfterDiscard>.Publish(new OnShowAfterDiscard());
        }
    }

    private void PlayerSatDown(OnPlayerSitDown evt)
    {
        isSitting = evt.isSitting;

        if (isActive && evt.isSitting)
        {
            actionButton.action.Enable();
            actionButton.action.started += OnButtonStarted;
            actionButton.action.canceled += OnButtonCanceled;

            // StartCoroutine(SkipTaskDelay());
        }
        else if (isActive)
        {
            actionButton.action.Disable();
            actionButton.action.started -= OnButtonStarted;
            actionButton.action.canceled -= OnButtonCanceled;
        }
    }

    private IEnumerator SkipTaskDelay()
    {
        yield return null;
        Debug.LogWarning("Skipping dialogue");
        FinishTask();
    }
}