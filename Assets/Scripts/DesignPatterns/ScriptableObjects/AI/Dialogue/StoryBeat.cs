using UnityEngine;

[CreateAssetMenu(menuName = "Scriptable Objects/Dialogue/Story Beat")]
public class StoryBeat: ScriptableObject
{
    [SerializeField, Tooltip("To keep track of each individual beat")]
    private string beatId;
    [SerializeField, TextArea(3, 10), Tooltip("The custom instruction for this beat that will be sent to the AI besides the context and the rules for it to act in a certain way. Write them in the same person as the scenario is written.")]
    private string directiveText = string.Empty;
    [SerializeField, TextArea(3, 5), Tooltip("The instructions for success of this beat - if the AI decrees this condition is met, a success tag will be sent with the AI's response.")]
    private string successCondition = string.Empty;
    [SerializeField, TextArea(3, 5), Tooltip("The instructions for failure of this beat - if the AI decrees this condition is met, a failure tag will be sent with the AI's response.")]
    private string failureCondition = string.Empty;
    [SerializeField]
    private StoryBeat successBeat;
    [SerializeField]
    private StoryBeat failureBeat;
    [SerializeField] 
    private int maxMessages = 0;
    [SerializeField, Min(1), Tooltip("How many 'Failure' verdicts it takes to move to the Failure Beat. 1 is on the first one (default).\nFailures below this stay on the beat and fire the 'On Failed Attempt' hook.")]
    private int failureThreshold = 1;
    [SerializeField, Tooltip("Can the player talk to the NPC during this beat? Turn off for the beats that wait on gameplay (a task, a trigger).\nTalking comes back on the next beat that allows it.")]
    private bool allowTalking = true;

    public bool AllowTalking => allowTalking;

    public int  FailureThreshold => Mathf.Max(1, failureThreshold);
    public string BeatId => string.IsNullOrEmpty(beatId) ? name : beatId;

    private void OnValidate()
    {
        if (string.IsNullOrEmpty(beatId)) beatId = name;
    }

    public string DirectiveText => directiveText;
    public string SuccessCondition => successCondition;
    public string FailureCondition => failureCondition;
    public StoryBeat SuccessBeat => successBeat;
    public StoryBeat FailureBeat => failureBeat;
    public int  MaxMessages => maxMessages;
}