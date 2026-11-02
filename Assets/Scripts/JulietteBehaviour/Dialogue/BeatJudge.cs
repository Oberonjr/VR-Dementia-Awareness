using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;
using VRD_AIProviders;
using ChatMessage = VRD_AIProviders.ChatMessage;

/// <summary>
/// Decides whether the user's reply moves the current StoryBeat forward, using the small classifier LLM.
/// </summary>
public class BeatJudge
{
    private readonly ILLMService classifierLLM;
    private readonly CoreRules coreRules;
    private readonly StoryBeatManager storyBeatManager;
    private readonly ConversationHistory history;

    public BeatJudge(ILLMService classifierLLM, CoreRules coreRules, StoryBeatManager storyBeatManager, ConversationHistory history)
    {
        this.classifierLLM = classifierLLM;
        this.coreRules = coreRules;
        this.storyBeatManager = storyBeatManager;
        this.history = history;
    }

    /// <returns>True if the current StoryBeat changed, meaning the system prompt needs rebuilding.</returns>
    public async Task<bool> ResolveAsync(string userText)
    {
        if (storyBeatManager == null) return false;
        StoryBeat previousBeat = storyBeatManager.CurrentStoryBeat;
        if (!storyBeatManager.AdvanceOnTimeout() && storyBeatManager.NeedsClassification)
        {
            storyBeatManager.ReportOutcome(await ClassifyUserReply(userText));
        }
        return storyBeatManager.CurrentStoryBeat != previousBeat;
    }

    private async Task<StoryProgressResult> ClassifyUserReply(string userText)
    {
        StoryBeat beat = storyBeatManager.CurrentStoryBeat;
        string success = string.IsNullOrEmpty(beat.SuccessCondition) ? "(none)" : beat.SuccessCondition;
        string failure = string.IsNullOrEmpty(beat.FailureCondition) ? "(none)" : beat.FailureCondition;

        ChatResponse res = await classifierLLM.CompleteAsync(new ChatRequest
        {
            Messages = new List<ChatMessage>
            {
                new ChatMessage("system", coreRules.OutcomeClassifierPrompt),
                new ChatMessage("user",
                    $"Success condition: {success}\nFailure condition: {failure}\n\nNPC's last line: {history.GetLastNpcLine(stripTags: true)}\nUser's reply: {userText}")
            },
            Temperature = 0f,
            MaxTokens = 1024
        });
        string raw = res.Success ? res.Text?.Trim() : string.Empty;
        Match digit = string.IsNullOrEmpty(raw) ? Match.Empty : Regex.Match(raw, "[0-2]");
        if (digit.Success && Enum.IsDefined(typeof(StoryProgressResult), int.Parse(digit.Value)))
        {
            StoryProgressResult outcome = (StoryProgressResult)int.Parse(digit.Value);
            Debug.Log($"[StoryBeat]'{beat.BeatId}' judged {outcome} (raw '{raw}') | user : {userText}");
            return outcome;
        }

        Debug.LogWarning($"[StoryBeat] Classifier returned '{raw}' on '{beat.BeatId}', defaulting to Continue, finish: {res.FinishReason}, error: {res.Error}");
        return StoryProgressResult.Continue;
    }
}
