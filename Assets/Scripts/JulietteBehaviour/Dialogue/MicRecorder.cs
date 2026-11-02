using FMODUnity;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

/// <summary>
/// Records the default microphone into an FMOD sound buffer and hands back the raw PCM16 bytes.
/// Owned by the VoiceInteractionManager.
/// </summary>
public class MicRecorder : IDisposable
{
    public const int MAX_RECORDING_SECONDS = 60;

    private FMOD.Sound micSound;
    private int recordDeviceId = 0; // Default microphone
    private float recordingStartTime = 0f; // Tracks when the recording started

    public bool IsRecording { get; private set; }
    public bool IsInitialized => micSound.hasHandle();
    public int SampleRate { get; private set; }
    public int Channels { get; private set; }
    public float Elapsed => IsRecording ? Time.time - recordingStartTime : 0f;

    public void Init()
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
        SampleRate = 16000;
        Channels = 1;

        Debug.Log($"Initialized FMOD Mic: {name} | Original: {originalRate}Hz | Forced for STT: {SampleRate}Hz Mono");

        // Prepare an FMOD struct to allocate memory for the recording
        FMOD.CREATESOUNDEXINFO exinfo = new FMOD.CREATESOUNDEXINFO();
        exinfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = Channels;
        exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.defaultfrequency = SampleRate;
        exinfo.length = (uint)(SampleRate * sizeof(short) * Channels * MAX_RECORDING_SECONDS);

        // Create the custom sound object in memory
        RuntimeManager.CoreSystem.createSound("", FMOD.MODE.DEFAULT | FMOD.MODE.OPENUSER, ref exinfo, out micSound);
    }

    /// <returns>False if the microphone could not be initialized.</returns>
    public bool StartRecording()
    {
        // Fallback: Delaying initialization ensures the user had time to accept Android Mic permissions
        if (!IsInitialized)
        {
            Init();
            // Abort if initialization failed
            if (!IsInitialized) { return false; }
        }

        IsRecording = true;
        recordingStartTime = Time.time; // Store the exact time the recording started

        // Start recording into our custom micSound object (false = no looping)
        RuntimeManager.CoreSystem.recordStart(recordDeviceId, micSound, false);
        Debug.Log("FMOD Recording started...");
        return true;
    }

    /// <summary>Stops recording and returns the recorded PCM16 bytes, or null if nothing was recorded.</summary>
    public byte[] StopRecording()
    {
        if (!IsRecording) { return null; }
        IsRecording = false;

        // Retrieve the current recording position before stopping
        RuntimeManager.CoreSystem.getRecordPosition(recordDeviceId, out uint recordPosition);

        // Stop the FMOD recording process
        RuntimeManager.CoreSystem.recordStop(recordDeviceId);
        Debug.Log($"FMOD Recording stopped. Recorded {recordPosition} samples. Processing STT...");

        // Extract the raw PCM bytes
        return GetRecordedPCMData(recordPosition);
    }

    public void DiscardRecording()
    {
        if (!IsRecording) { return; }
        IsRecording = false;

        // Stop FMOD without processing the data
        RuntimeManager.CoreSystem.recordStop(recordDeviceId);
        Debug.Log("Recording discarded.");
    }

    public void Dispose()
    {
        // Ensure the FMOD recording sound buffer is released
        if (micSound.hasHandle()) { micSound.release(); }
    }

    private byte[] GetRecordedPCMData(uint recordPosition)
    {
        // Abort if no data was recorded
        if (recordPosition == 0) { return null; }

        // Calculate the file size in bytes (Samples * 2 bytes per short * number of channels)
        uint lengthInBytes = recordPosition * sizeof(short) * (uint)Channels;

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
}
