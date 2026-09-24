using System;
using System.Collections.Generic;
using UnityEngine;

public class StoryBeatManager : MonoBehaviour
{
    [SerializeField] private List<StoryBeat> storyBeats = new List<StoryBeat>();
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
            //DontDestroyOnLoad(gameObject);
        }
        else
        {  
            Debug.Log("An instance of StoryBeatManager already exists. Destroying extra copy on object: " + gameObject.name);
            Destroy(this);
            return; //Ensure the below code doesn't trigger on a dupe manager
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


    public string GetDirective()
    {
        if (_currentStoryBeat == null) { return string.Empty; }

        List<string> parts = new List<string>();

        if (!string.IsNullOrEmpty(_currentStoryBeat.DirectiveText))
            parts.Add(_currentStoryBeat.DirectiveText);

        return string.Join("\n", parts);
    }

    public void ReportOutcome(StoryProgressResult result)
    {
        switch (result)
        {
            case StoryProgressResult.Success:
                SetCurrentStoryBeat(ResolveSuccessTransition(), "Success");
                break;
            case StoryProgressResult.Failure:
                if (_currentStoryBeat.FailureBeat != null)
                {
                    SetCurrentStoryBeat(_currentStoryBeat.FailureBeat, "Failure");
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

    public void RegisterNpcTurn() => _messagesOnCurrentBeat++;

    public bool AdvanceOnTimeout()
    {
        if(_currentStoryBeat == null || _currentStoryBeat.MaxMessages <= 0) return false;

        if (_messagesOnCurrentBeat < _currentStoryBeat.MaxMessages) return false;
        
        SetCurrentStoryBeat(ResolveSuccessTransition(), "MaxMessages reached");
        return true;
    }

    public bool NeedsClassification => _currentStoryBeat != null && _currentStoryBeat.MaxMessages == 0 &&
                                       _messagesOnCurrentBeat > 0 &&
                                       (!string.IsNullOrEmpty(_currentStoryBeat.SuccessCondition) ||
                                        !string.IsNullOrEmpty(_currentStoryBeat.FailureCondition));


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

    private void SetCurrentStoryBeat(StoryBeat next, string reason)
    {
        Debug.Log($"[StoryBeat] {(_currentStoryBeat ? _currentStoryBeat.BeatId : "<none>")} -> {(next ? next.BeatId : "<end>")} ({reason})");
        _messagesOnCurrentBeat = 0;
        _currentStoryBeat = next;
    }
}
