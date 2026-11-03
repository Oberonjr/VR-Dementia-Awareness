using UnityEngine;

/// <summary>
/// Concatenates the inspector-authored sections and the current StoryBeat directive into Juliette's system prompt.
/// </summary>
public static class PromptBuilder
{
    public static string Build(string scenario, string character, string systemRules, CoreRules coreRules, CurrentLanguage language, string directive)
    {
        // Guarded so an unassigned/empty CoreRules asset can't NRE from OnValidate() while editing in the Inspector
        // (Start() already hard-fails on this at Play time; this is just the Edit-mode safety net).
        string currentRules = systemRules;
        if (coreRules != null && !string.IsNullOrEmpty(coreRules.Rules))
        {
            currentRules += " \n\n " + coreRules.Rules;
        }
        else
        {
            Debug.LogWarning("Core rules are missing or empty — system prompt is being built without them.");
        }

        string languageSelection = $"Format all responses in: {language}";

        return $"# Scenario\n{scenario}\n\n# Character\n{character}\n\n# Rules\n{currentRules}\n\n# Language \n{languageSelection}\n\n# Current Directive \n{directive}\n";
    }
}
