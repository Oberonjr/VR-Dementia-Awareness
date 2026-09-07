using UnityEngine;

/// <summary>
/// Main Menu options buttons.
/// Can be extended, the more buttons or menus are added to the main menu.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Header("Menu Windows")]
    [SerializeField] private GameObject exitConfirm;
    [SerializeField] private GameObject settingsWindow;

    public void OnExitClicked()
    {
        gameObject.SetActive(false);
        exitConfirm.SetActive(true);
    }

    public void OnSettingsClicked()
    {
        gameObject.SetActive(false);
        settingsWindow.SetActive(true);
    }

    public void OnStartClicked()
    {
        gameObject.SetActive(false);
        EventBus<OnStartSimulation>.Publish(new OnStartSimulation());
    }

    public void OnRestartSimulation()
    {
        GameManager.Instance.RestartCurrentScene();
    }
}