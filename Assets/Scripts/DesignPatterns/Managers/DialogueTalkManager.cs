using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VRD_AIProviders
{
    /// <summary>
    /// Turns player talk requests into VIM calls.
    /// </summary>
    public class DialogueTalkManager : MonoBehaviour
    {
        private enum TalkState { Idle, Recording, Processing, Speaking }
        
        [Header("References")]
        [SerializeField] private VoiceInteractionManager voiceManager;

        [Header("Input")]
        [Tooltip("Optional hold-to-talk action (e.g. keyboard key for PC). VR uses the OnRequestTalk event.")]
        [SerializeField]
        private InputActionReference holdToTalk;

        [Header("Startup")] [SerializeField] private bool activeOnStart = true;

        private TalkState state = TalkState.Idle;
        private bool isActive = false;

        public bool IsActive => isActive;
        
        private void Start()
        {
            if (voiceManager == null)
            {
                Debug.LogError("[DialogueTalk] No VoiceInteractionManager found! Drag the AI object into the slot.", this);
                return;
            }

            if (activeOnStart)
            {
                SetActive(true);
            }
        }

        private void OnDestroy()
        {
            SetActive(false);
        }

        public void SetActive(bool active)
        {
            if (active == isActive) { return; }
            if (active && voiceManager == null) { return; }

            isActive = active;

            if (active)
            {
                EventBus<OnRequestTalk>.OnEvent += HandleRequestTalk;
                EventBus<OnJulietteFinishedTalk>.OnEvent += HandleSpeechFinished;
                voiceManager.OnProcessingStarted += HandleProcessingStarted;
                voiceManager.OnProcessingFinished += HandleProcessingFinished;

                if (holdToTalk != null)
                {
                    holdToTalk.action.started += HandleHoldStarted;
                    holdToTalk.action.canceled += HandleHoldCanceled;
                    holdToTalk.action.Enable();
                }

                if (GameManager.Instance != null) { GameManager.Instance.conversationActive = true; }

                // The HUD only shows the talk prompt when the player is "seated". There's no sit gate anymore, so unlock it.
                EventBus<OnPlayerSitDown>.Publish(new OnPlayerSitDown(true));
            }
            else
            {
                EventBus<OnRequestTalk>.OnEvent -= HandleRequestTalk;
                EventBus<OnJulietteFinishedTalk>.OnEvent -= HandleSpeechFinished;

                if (voiceManager != null)
                {
                    voiceManager.OnProcessingStarted -= HandleProcessingStarted;
                    voiceManager.OnProcessingFinished -= HandleProcessingFinished;

                    if (state == TalkState.Recording) { voiceManager.DiscardRecording(); }
                }

                if (holdToTalk != null)
                {
                    holdToTalk.action.started -= HandleHoldStarted;
                    holdToTalk.action.canceled -= HandleHoldCanceled;
                    holdToTalk.action.Disable();
                }

                if (GameManager.Instance != null) { GameManager.Instance.conversationActive = false; }

                state = TalkState.Idle;
                EventBus<OnHideTalk>.Publish(new OnHideTalk());
            }
        }

        /// <summary>Toggle for buttons / UnityEvents: starts recording when idle, sends when recording.</summary>
        public void RequestToggle()
        {
            if (!isActive) { return; }

            if (state == TalkState.Idle) { BeginRecording(); }
            else if (state == TalkState.Recording) { EndRecording(); }
            // Processing / Speaking: ignore
        }

        private void HandleRequestTalk(OnRequestTalk evt) => RequestToggle();

        private void HandleHoldStarted(InputAction.CallbackContext ctx)
        {
            if (state == TalkState.Idle)
            {
                BeginRecording();
            }
        }

        private void HandleHoldCanceled(InputAction.CallbackContext ctx)
        {
            if (state == TalkState.Recording)
            {
                EndRecording();
            }
        }
        // Also fires when VIM auto-stops at the max recording time, so state follows VIM, not our own calls.
        private void HandleProcessingStarted()
        {
            state = TalkState.Processing;
            EventBus<OnShowProcessing>.Publish(new OnShowProcessing());
        }

        // Normal turn: speech is already playing here, so wait for OnJulietteFinishedTalk.
        // Early exits (no audio / empty transcript / TTS failed): nothing is playing, so release now.
        // Otherwise those paths soft-lock, because OnJulietteFinishedTalk never comes.
        private void HandleProcessingFinished()
        {
            if (voiceManager.IsSpeaking) { state = TalkState.Speaking; }
            else { Release(); }
        }

        private void HandleSpeechFinished(OnJulietteFinishedTalk evt) => Release();

        private void BeginRecording()
        {
            voiceManager.StartRecording();
            state = TalkState.Recording;
            EventBus<OnShowMicrophonePickup>.Publish(new OnShowMicrophonePickup());
        }

        private void EndRecording()
        {
            // State and HUD switch to Processing in HandleProcessingStarted.
            voiceManager.StopRecordingAndProcess();
        }

        // Only releases from Processing/Speaking. A late OnJulietteFinishedTalk (TTS-failure path, or a PlayVoice line)
        // must not reset a recording the player has already started.
        private void Release()
        {
            if (state != TalkState.Processing && state != TalkState.Speaking) { return; }

            state = TalkState.Idle;
            EventBus<OnShowAfterDiscard>.Publish(new OnShowAfterDiscard());
        }
    }
}

