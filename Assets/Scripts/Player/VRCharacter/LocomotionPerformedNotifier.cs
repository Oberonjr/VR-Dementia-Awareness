using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Listens to various Oculus locomotion events and forwards them to the global EventBus to notify other systems when the player moves or turns
/// </summary>
public class LocomotionPerformedNotifier : MonoBehaviour
{
    [Header("Move Locomotion")]
    [SerializeField] private TeleportInteractor[] teleportLocomotion;
    [SerializeField] private SlideLocomotionBroadcaster[] slideLocomotion;

    [Header("Turn Locomotion")]
    [SerializeField] private TurnerEventBroadcaster[] controllerTurnLocomotion;
    [SerializeField] private TurnLocomotionBroadcaster[] handTurnLocomotion;

    private void Start()
    {
        SetupEventListeners();
    }

    private void OnDestroy()
    {
        UnsubscribeEventListeners();
    }

    private void SetupEventListeners()
    {
        for (int i = 0; i < teleportLocomotion.Length; i++)
        {
            teleportLocomotion[i].WhenLocomotionPerformed += PlayerMoved;
        }

        for (int i = 0; i < slideLocomotion.Length; i++)
        {
            slideLocomotion[i].WhenLocomotionPerformed += PlayerMoved;
        }

        for (int i = 0; i < controllerTurnLocomotion.Length; i++)
        {
            controllerTurnLocomotion[i].WhenLocomotionPerformed += PlayerRotated;
        }

        for (int i = 0; i < handTurnLocomotion.Length; i++)
        {
            handTurnLocomotion[i].WhenLocomotionPerformed += PlayerRotated;
        }
    }

    private void UnsubscribeEventListeners()
    {
        for (int i = 0; i < teleportLocomotion.Length; i++)
        {
            teleportLocomotion[i].WhenLocomotionPerformed -= PlayerMoved;
        }

        for (int i = 0; i < slideLocomotion.Length; i++)
        {
            slideLocomotion[i].WhenLocomotionPerformed -= PlayerMoved;
        }

        for (int i = 0; i < controllerTurnLocomotion.Length; i++)
        {
            controllerTurnLocomotion[i].WhenLocomotionPerformed -= PlayerRotated;
        }

        for (int i = 0; i < handTurnLocomotion.Length; i++)
        {
            handTurnLocomotion[i].WhenLocomotionPerformed -= PlayerRotated;
        }
    }

    private void PlayerMoved(LocomotionEvent evt)
    {
        EventBus<OnMoved>.Publish(new OnMoved());
    }

    private void PlayerRotated(LocomotionEvent evt)
    {
        EventBus<OnTurned>.Publish(new OnTurned());
    }
}