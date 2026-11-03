using System.Collections.Generic;
using System.Text.RegularExpressions;
using ChatMessage = VRD_AIProviders.ChatMessage;

/// <summary>
/// The chat history sent to the dialogue LLM. The system prompt is always kept as the first message.
/// </summary>
public class ConversationHistory
{
    private readonly List<ChatMessage> messages = new List<ChatMessage>();

    public List<ChatMessage> Messages => messages;

    /// <summary>Inserts the system prompt, or replaces it if it already exists (language or beat changed mid-conversation).</summary>
    public void SetSystem(string systemPrompt)
    {
        if (messages.Count > 0 && messages[0].Role == "system")
        {
            messages[0].Content = systemPrompt;
        }
        else
        {
            messages.Insert(0, new ChatMessage("system", systemPrompt));
        }
    }

    public void AddUser(string content) => messages.Add(new ChatMessage("user", content));

    public void AddAssistant(string content) => messages.Add(new ChatMessage("assistant", content));

    public string GetLastNpcLine(bool stripTags)
    {
        for (int i = messages.Count - 1; i >= 0; i--)
        {
            if (messages[i].Role != "assistant") continue;
            return stripTags ? Regex.Replace(messages[i].Content, @"\[[^\]]*\]", "").Trim() : messages[i].Content;
        }

        return string.Empty;
    }
}
