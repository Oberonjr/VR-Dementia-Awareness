using UnityEngine;

[CreateAssetMenu(menuName = "AI Rules/Core Rules")]
public class CoreRules : ScriptableObject
{
    /// <summary>
    /// This is put in its own separate SO so that it can be separate
    /// from the rest of the prose of how the AI should treat everything in the scene,
    /// in order to highlight it as a more important part
    /// but still keep it easily editable/iterative
    /// </summary>
    [SerializeField, TextArea(5, 40),
     Tooltip(
         "Only modify this if you know exactly what you are doing.\nThe core set of rules for the response format the AI agent must use in order for the text parsing to function properly.")]
    private string coreRules = "CORE PROTOCOL — DO NOT OMIT, REORDER, OR REWORD ANY TAG BELOW.\n " +
                               "These are read by code, not by a human. Everything else in this character's instructions is negotiable; this section is not.\n" +
                               "\nEmotion/action tags:" +
                               "\n- Every response is fed to a text-to-speech engine that reads bracketed tags to choose vocal delivery." +
                               "\n- The ONLY valid tags are: [neutral] [happy] [sad] [angry] [fearful] [surprised] [disgusted] [nostalgic] [laugh] [sigh] [cough] [breathe]" +
                               "\n- Never invent a tag outside this list (no [pausing], [smiles], [searching for a word], etc.) — an unrecognized tag is silently discarded, so anything not on this list simply does not play." +
                               "\n- [nostalgic] is a modifier, not a delivery on its own: it must be immediately followed by one of the other emotion tags, e.g. \"[nostalgic] [happy] Oh, I remember those tulips...\"." +
                               "\n- Place a tag at the very start of the response, and insert a new one mid-response whenever the delivery should change." +
                               "\n- Tags stay in English and in brackets even when the spoken language is not English." +
                               "\n- Use ellipses (...) rather than a tag to indicate a pause or a search for a word.";
    public string Rules => coreRules;
    
    [SerializeField, TextArea(5, 20), Tooltip("Only modify this if you know exactly what you are doing.\nPrompt for the fast model that judges the user's reply against the current beat. Must output a single StoryProgressResult digit.")]
    private string outcomeClassifierPrompt =
        "You judge one moment in a conversation between an NPC and a user.\n" +
        "You receive the current beat's success and failure conditions, the NPC's last line, and the user's reply to it.\n" +
        "Judge ONLY the user's reply to that line.\n" +
        "Output exactly one digit and nothing else:\n" +
        "0 = the success condition is met\n1 = the failure condition is met\n2 = neither is clearly met\n" +
        "When in doubt, output 2.";
    
    public string OutcomeClassifierPrompt => outcomeClassifierPrompt;
}
