using UnityEngine;

/// <summary>
/// Used to trigger capturing or sending off audio from microphone
/// The function is assigned to one of the gesture events, found within the rotation locomotor of the VR character
/// </summary>
public class TriggerDialogueGesture : MonoBehaviour
{
    public void RequestTalk()
    {
        EventBus<OnRequestTalk>.Publish(new OnRequestTalk());
    }
}
