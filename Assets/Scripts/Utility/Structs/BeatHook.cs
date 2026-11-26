using System;
using UnityEngine.Events;

[Serializable]
public class BeatHook
{
    public StoryBeat beat;
    public UnityEvent onEnter;
    public UnityEvent onExit;
    public UnityEvent onSuccess;
    public UnityEvent onFailure;
    public UnityEvent onFailedAttempt;
}
