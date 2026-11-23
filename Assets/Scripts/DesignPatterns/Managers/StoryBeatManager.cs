using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class StoryBeatManager : MonoBehaviour
{
    [SerializeField] private List<StoryBeat> storyBeats = new List<StoryBeat>();
    
    [Header("Scene Hooks - optional")]
    [SerializeField] private List<BeatHook> beatHooks = new List<BeatHook>();
    [SerializeField] private UnityEvent onStoryEnded;

    public event Action<StoryBeat, StoryBeat, BeatTransitionReason> OnBeatChanged;
    public event Action OnStoryEnded;
    
    private StoryBeat _currentStoryBeat;
    public StoryBeat CurrentStoryBeat => _currentStoryBeat;
    private int _messagesOnCurrentBeat = 0;
    
    private static StoryBeatManager _instance;
    public static StoryBeatManager Instance => _instance;
    


    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
        else
        {  
            Debug.Log("An instance of StoryBeatManager already exists. Destroying extra copy on object: " + gameObject.name);
            Destroy(this);
            return; 
        }
        if (storyBeats.Count > 0)
        {
            _currentStoryBeat = storyBeats[0];
            Debug.Log($"[StoryBeat] Starting on '{_currentStoryBeat.BeatId}'");
        }
        else
        {
            Debug.LogWarning("[StoryBeat] No story beats assigned.");
        }
    }

    private void Start()
    {
        if (_instance != this || _currentStoryBeat == null) return;
        RaiseTransition(null, _currentStoryBeat, BeatTransitionReason.Start);
    }

    public string GetDirective()
    {
        if (_currentStoryBeat == null) { return string.Empty; }

        List<string> parts = new List<string>();

        if (!string.IsNullOrEmpty(_currentStoryBeat.DirectiveText))
            parts.Add(_currentStoryBeat.DirectiveText);

        return string.Join("\n", parts);
    }

    public void ReportOutcome(StoryProgressResult result, StoryBeat expected = null)
    {
        if (_currentStoryBeat == null) return;
        if (expected != null && expected != _currentStoryBeat)
        {
            Debug.Log($"[StoryBeat] Ignoring stale {result} for '{expected.BeatId}', current beat is '{_currentStoryBeat.BeatId}'.'");
        }
        
        switch (result)
        {
            case StoryProgressResult.Success:
                SetCurrentStoryBeat(ResolveSuccessTransition(), BeatTransitionReason.Success);
                break;
            case StoryProgressResult.Failure:
                if (_currentStoryBeat.FailureBeat != null)
                {
                    SetCurrentStoryBeat(_currentStoryBeat.FailureBeat, BeatTransitionReason.Failure);
                }
                else
                {
                    // No failure beat authored: stay on the current beat. Left as-is on purpose,
                    // so the message counter (used by MaxMessages beats) isn't reset for no reason.
                    Debug.LogAssertion($"[StoryBeat] Failure on '{_currentStoryBeat.BeatId}' with no failure beat, staying.");
                }
                break;
            case StoryProgressResult.Continue:
            default:
                // Nothing to do: stay on the current beat.
                break;
        }
    }

    public void ReportSuccess() => ReportOutcome(StoryProgressResult.Success);
    public void ReportFailure() => ReportOutcome(StoryProgressResult.Failure);

    public void GoToBeat(StoryBeat beat)
    {
        if (beat == null)
        {
            Debug.LogWarning("[StoryBeat] GoToBeat called without a beat. Use EndStory() to end the story.");
            return;
        }
        SetCurrentStoryBeat(beat, BeatTransitionReason.Jump);
    }

    public void EndStory()
    {
        if (_currentStoryBeat == null) return;
        SetCurrentStoryBeat(null, BeatTransitionReason.Jump);
    }

    public void RegisterNpcTurn() => _messagesOnCurrentBeat++;

    public bool AdvanceOnTimeout()
    {
        if(_currentStoryBeat == null || _currentStoryBeat.MaxMessages <= 0) return false;

        if (_messagesOnCurrentBeat < _currentStoryBeat.MaxMessages) return false;
        
        SetCurrentStoryBeat(ResolveSuccessTransition(), BeatTransitionReason.Timeout);
        return true;
    }

    public bool NeedsClassification => _currentStoryBeat != null && _currentStoryBeat.MaxMessages == 0 &&
                                       _messagesOnCurrentBeat > 0 &&
                                       (!string.IsNullOrWhiteSpace(_currentStoryBeat.SuccessCondition) ||
                                        !string.IsNullOrWhiteSpace(_currentStoryBeat.FailureCondition));


    // Shared by ReportOutcome(Success) and the MaxMessages timeout in NotifyTurnCompleted:
    // follow the linked success beat if one is set, otherwise fall back to the next entry
    // in storyBeats, otherwise the story has ended (null - a normal, expected outcome).
    private StoryBeat ResolveSuccessTransition()
    {
        if (_currentStoryBeat.SuccessBeat != null)
        {
            return _currentStoryBeat.SuccessBeat;
        }

        int index = storyBeats.IndexOf(_currentStoryBeat);
        if (index >=0 && index + 1 < storyBeats.Count)
        {
            return storyBeats[index + 1];
        }
        else if (index < 0)
        {
            Debug.LogWarning($"[StoryBeat] '{_currentStoryBeat.BeatId}' is not in the list and has no successBeat.");
        }

        Debug.Log("Reached the end of the current storyline.");
        return null;
    }

    private void SetCurrentStoryBeat(StoryBeat next, BeatTransitionReason reason)
    {
        StoryBeat previous = _currentStoryBeat;
        Debug.Log($"[StoryBeat] {(_currentStoryBeat ? _currentStoryBeat.BeatId : "<none>")} -> {(next ? next.BeatId : "<end>")} ({reason})");
        _messagesOnCurrentBeat = 0;
        _currentStoryBeat = next;
        RaiseTransition(previous, next, reason);
    }

    private void RaiseTransition(StoryBeat previous, StoryBeat next, BeatTransitionReason reason)
    {
        OnBeatChanged?.Invoke(previous, next, reason);
        if (_currentStoryBeat != next) return;

        if (previous != null)
        {
            bool succeeded = reason == BeatTransitionReason.Success || reason == BeatTransitionReason.Timeout;
            bool failed = reason == BeatTransitionReason.Failure;
            for (int i = 0; i < beatHooks.Count; i++)
            {
                BeatHook hook = beatHooks[i];
                if(hook.beat != previous) continue;
                if(succeeded) hook.onSuccess?.Invoke();
                else if(failed) hook.onFailure?.Invoke();
                hook.onExit?.Invoke();

                if (_currentStoryBeat != next) return;

            }
        }

        if (next != null)
        {
            for (int i = 0; i < beatHooks.Count; i++)
            {
                BeatHook hook = beatHooks[i];
                if (hook.beat != next) continue;
                hook.onEnter?.Invoke();
                if (_currentStoryBeat != next) return;
            }
        }
        else
        {
            OnStoryEnded?.Invoke();
            onStoryEnded?.Invoke();
        }
    }
}
