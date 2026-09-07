using Oculus.Interaction.Locomotion;
using UnityEngine;

/// <summary>
/// Used for the settings menu (Handheld and Main Menu)
/// More functions can be added, depending on the amount of options.
/// </summary>
public class SettingsMenu : MonoBehaviour
{
    [Header("Locomotion Components")]
    [SerializeField] private GameObject slideComponent;
    [SerializeField] private GameObject teleportComponent;
    [SerializeField] private GameObject stepComponent;
    [SerializeField] private TurnerEventBroadcaster turnComponent;

    [Header("Main Menu")]
    [SerializeField] private GameObject mainMenu;

    public void OnGoBack()
    {
        gameObject.SetActive(false);
        mainMenu.SetActive(true);
    }

    public void OnSetDutchLocal()
    {
        GameManager.Instance.ChangeLocalization("nl-NL");
    }

    public void OnSetEnglishLocal()
    {
        GameManager.Instance.ChangeLocalization("en-GB");
    }

    public void OnSetTeleportMovement()
    {
        SetLocomotion(true);
    }

    public void OnSetSlideMovement()
    {
        SetLocomotion(false);
    }

    private void SetLocomotion(bool setTeleport)
    {
        slideComponent.SetActive(!setTeleport);
        teleportComponent.SetActive(setTeleport);
        stepComponent.SetActive(setTeleport);

        if (setTeleport) { turnComponent.TurnMethod = TurnerEventBroadcaster.TurnMode.Snap; }
        else { turnComponent.TurnMethod = TurnerEventBroadcaster.TurnMode.Smooth; }
    }
}