using FMOD.Studio;
using FMODUnity;
using System;
using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Handles playing character voice events from FMOD and intercepts audio data via a custom DSP for real-time lip-syncing
/// </summary>
public class PlayVoice : MonoBehaviour
{
    private static PlayVoice _activeInstance;

    private LipSyncController lipSyncController;
    private FMOD.DSP_READ_CALLBACK dspCallback;
    private FMOD.Studio.EventInstance currentInstance;

    private void Start()
    {
        EventBus<OnJulietteTalk>.OnEvent += PlayVoiceSound;
        lipSyncController = FindFirstObjectByType<LipSyncController>();
    }

    private void OnDestroy()
    {
        EventBus<OnJulietteTalk>.OnEvent -= PlayVoiceSound;
    }

    public void StopVoiceSound()
    {
        if (currentInstance.isValid()) { currentInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE); }
    }

    private void PlayVoiceSound(OnJulietteTalk evt)
    {
        if (!evt.phrase.IsNull)
        {
            StopVoiceSound();
            StartCoroutine(PlayAudioRoutine(evt.phrase));
        }
    }

    private IEnumerator PlayAudioRoutine(EventReference eventRef)
    {
        EventInstance instance = RuntimeManager.CreateInstance(eventRef);

        if (!instance.isValid())
        {
            EventBus<OnJulietteFinishedTalk>.Publish(new OnJulietteFinishedTalk());
            yield break;
        }

        instance.set3DAttributes(RuntimeUtils.To3DAttributes(gameObject));
        currentInstance = instance;
        _activeInstance = this;

        // Setup custom DSP description to capture waveform data
        FMOD.DSP dsp = new FMOD.DSP();
        FMOD.DSP_DESCRIPTION dspDesc = new FMOD.DSP_DESCRIPTION();
        dspDesc.name = new byte[32];
        System.Text.Encoding.UTF8.GetBytes("LipSyncDSP", 0, "LipSyncDSP".Length, dspDesc.name, 0);

        dspDesc.numinputbuffers = 1;
        dspDesc.numoutputbuffers = 1;

        dspCallback = new FMOD.DSP_READ_CALLBACK(CaptureDSPRead);
        dspDesc.read = dspCallback;

        FMOD.ChannelGroup coreChannelGroup = new FMOD.ChannelGroup();
        bool dspAttached = false;

        instance.start();

        PLAYBACK_STATE state;
        instance.getPlaybackState(out state);

        // Wait for FMOD to fully start the event
        while (state == PLAYBACK_STATE.STOPPED)
        {
            yield return null;
            instance.getPlaybackState(out state);
        }

        // Wait for channel group registration to be available
        while (instance.getChannelGroup(out coreChannelGroup) != FMOD.RESULT.OK)
        {
            yield return null;
        }

        // Create and attach custom DSP to the head of the channel group
        if (RuntimeManager.CoreSystem.createDSP(ref dspDesc, out dsp) == FMOD.RESULT.OK)
        {
            coreChannelGroup.addDSP(FMOD.CHANNELCONTROL_DSP_INDEX.HEAD, dsp);
            dspAttached = true;
        }

        while (state != PLAYBACK_STATE.STOPPED && state != PLAYBACK_STATE.STOPPING)
        {
            yield return null;
            instance.getPlaybackState(out state);
        }

        // Clean up and release the native DSP instance
        if (dspAttached && dsp.hasHandle())
        {
            coreChannelGroup.removeDSP(dsp);
            dsp.release();
        }

        _activeInstance = null;
        instance.release();
        EventBus<OnJulietteFinishedTalk>.Publish(new OnJulietteFinishedTalk());
    }

    [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
    private static FMOD.RESULT CaptureDSPRead(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        int samples = (int)(length * inchannels);
        float[] buffer = new float[samples];

        Marshal.Copy(inbuffer, buffer, 0, samples);
        Marshal.Copy(buffer, 0, outbuffer, samples);

        // Forward raw float array buffer directly to the lip-sync controller
        if (_activeInstance != null && _activeInstance.lipSyncController != null)
        {
            _activeInstance.lipSyncController.EnqueueLiveAudio(buffer, inchannels == 2, _activeInstance.currentInstance);
        }

        return FMOD.RESULT.OK;
    }
}