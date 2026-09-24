using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Story Beat")]
public class StoryBeat: ScriptableObject
{
    [SerializeField, Tooltip("To keep track of each individual beat")]
    private string beatId;
    [SerializeField, TextArea(3, 10), Tooltip("The custom instruction for this beat that will be sent to the AI besides the context and the rules for it to act in a certain way.")]
    private string directiveText = string.Empty;
    [SerializeField, TextArea(3, 5), Tooltip("The instructions for success of this beat - if the AI decrees this condition is met, a success tag will be sent with the AI's response.")]
    private string successCondition = "Consider this beat as a success if the user ";
    [SerializeField, TextArea(3, 5), Tooltip("The instructions for failure of this beat - if the AI decrees this condition is met, a failure tag will be sent with the AI's response.")]
    private string failureCondition = "Consider this beat as a failure if the user ";
    [SerializeField]
    private StoryBeat successBeat;
    [SerializeField]
    private StoryBeat failureBeat;
    [SerializeField] 
    private int maxMessages = 0;

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