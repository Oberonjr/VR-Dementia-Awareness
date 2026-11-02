using FMODUnity;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Stitches the synthesized per-emotion chunks into one clip, plays it through an FMOD programmer sound
/// and drives the lip sync. Exposes the playback state the ExpressionController reads (through the VoiceInteractionManager).
/// </summary>
public class SpeechPlayer
{
    // Artificial pause between chunks
    private const float PAUSE_DURATION_SECONDS = 0.2f;

    private readonly EventReference fmodDialogueEvent;
    private readonly LipSyncController lipSyncController;

    private FMOD.Studio.EventInstance dialogueInstance;
    private GCHandle stringHandle;

    public List<EmotionTimelineEvent> Timeline { get; private set; } = new List<EmotionTimelineEvent>();

    public bool IsSpeaking
    {
        get
        {
            if (!dialogueInstance.isValid()) { return false; }
            dialogueInstance.getPlaybackState(out FMOD.Studio.PLAYBACK_STATE state);
            return state == FMOD.Studio.PLAYBACK_STATE.PLAYING || state == FMOD.Studio.PLAYBACK_STATE.STARTING;
        }
    }

    public float CurrentTime
    {
        get
        {
            if (!dialogueInstance.isValid()) return 0f;
            dialogueInstance.getTimelinePosition(out int posMs);
            return posMs / 1000f; // Convert ms to seconds
        }
    }

    public SpeechPlayer(EventReference fmodDialogueEvent, LipSyncController lipSyncController)
    {
        this.fmodDialogueEvent = fmodDialogueEvent;
        this.lipSyncController = lipSyncController;
    }

    /// <param name="timeline">One entry per chunk, in the same order as <paramref name="audioChunks"/>.</param>
    /// <param name="audioChunks">Mono PCM16 per chunk; a leading WAV header is stripped.</param>
    public async Task PlayAsync(List<EmotionTimelineEvent> timeline, byte[][] audioChunks, int sampleRate)
    {
        string tempPath = Path.Combine(Application.temporaryCachePath, "stitched_voice.wav");
        byte[] stitchedPCM = null;

        // Offload the heavy array stitching and file writing to a background thread
        await Task.Run(() =>
        {
            byte[] silence = new byte[(int)(sampleRate * PAUSE_DURATION_SECONDS) * sizeof(short)];
            float accumulatedTime = 0f;

            using (MemoryStream stitched = new MemoryStream())
            {
                for (int i = 0; i < audioChunks.Length; i++)
                {
                    byte[] audio = audioChunks[i];
                    if (audio != null && audio.Length > 0)
                    {
                        // Inworld occasionally includes a 44-byte WAV header.
                        // We strip it off here to prevent loud 'pops' between chunks.
                        int startIndex = (audio.Length > 44 && audio[0] == 'R' && audio[1] == 'I' && audio[2] == 'F' && audio[3] == 'F') ? 44 : 0;

                        // Update the timeline with the exact start time of this chunk
                        if (i < timeline.Count)
                        {
                            var ev = timeline[i];
                            ev.startTime = accumulatedTime;
                            timeline[i] = ev;
                        }

                        // Calculate duration of this specific chunk to offset the next one
                        int dataLength = audio.Length - startIndex;
                        accumulatedTime += dataLength / (sampleRate * (float)sizeof(short));

                        stitched.Write(audio, startIndex, dataLength);

                        // Add a brief silence between chunks, but not after the very last one
                        if (i < audioChunks.Length - 1)
                        {
                            stitched.Write(silence, 0, silence.Length);
                            accumulatedTime += PAUSE_DURATION_SECONDS;
                        }
                    }
                }
                stitchedPCM = stitched.ToArray();
            }

            // Wrap a new WAV header around the combined raw PCM bytes and save to cache
            File.WriteAllBytes(tempPath, SaveWav.SaveFromPCM16(stitchedPCM, sampleRate, 1));
        });

        // Set the active timeline for the ExpressionController to read
        Timeline = timeline;

        // Play in FMOD (Must be called on the main thread after the background task finishes)
        PlayFMODProgrammerSound(tempPath);

        // Pass raw bytes and FMOD instance to the Lipsync Controller
        if (lipSyncController != null)
        {
            lipSyncController.PrepareAndPlayVisemes(stitchedPCM, sampleRate, dialogueInstance);
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
}
