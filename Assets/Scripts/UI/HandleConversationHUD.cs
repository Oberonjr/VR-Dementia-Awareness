using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Makes the HUDs relevant during the Juliette conversation appear / disappear
/// Animations are handled within their respective animators (except for the progress bar of course)
/// </summary>
public class HandleConversationHUD : MonoBehaviour
{
    [Header("UI Windows")]
    [SerializeField] private GameObject pressXUI;
    [SerializeField] private GameObject microphoneUI;
    [SerializeField] private GameObject processingUI;

    [Header("Progress Bar Settings")]
    [SerializeField] private Image progressBar;

    private bool playerSitting;

    private void Start()
    {
        pressXUI.SetActive(false);
        microphoneUI.SetActive(false);
        processingUI.SetActive(false);

        EventBus<OnUpdateTalkTimer>.OnEvent += UpdateProgress;
        EventBus<OnShowMicrophonePickup>.OnEvent += ShowMicrophone;
        EventBus<OnShowProcessing>.OnEvent += ShowProcessing;
        EventBus<OnShowAfterDiscard>.OnEvent += ShowAfterDiscard;
        EventBus<OnHideTalk>.OnEvent += HideAll;

        EventBus<OnPlayerSitDown>.OnEvent += PlayerSatDown;
    }

    private void OnDestroy()
    {
        EventBus<OnUpdateTalkTimer>.OnEvent -= UpdateProgress;
        EventBus<OnShowMicrophonePickup>.OnEvent -= ShowMicrophone;
        EventBus<OnShowProcessing>.OnEvent -= ShowProcessing;
        EventBus<OnShowAfterDiscard>.OnEvent -= ShowAfterDiscard;
        EventBus<OnHideTalk>.OnEvent -= HideAll;

        EventBus<OnPlayerSitDown>.OnEvent -= PlayerSatDown;
    }

    private void UpdateProgress(OnUpdateTalkTimer evt)
    {
        progressBar.fillAmount = evt.currentProgress;
    }

    private void ShowMicrophone(OnShowMicrophonePickup evt)
    {
        pressXUI.SetActive(false);
        microphoneUI.SetActive(true);
    }

    private void ShowProcessing(OnShowProcessing evt)
    {
        pressXUI.SetActive(false);
        microphoneUI.SetActive(false);
        processingUI.SetActive(true);
    }

    private void PlayerSatDown(OnPlayerSitDown evt)
    {
        playerSitting = evt.isSitting;

        if (!GameManager.Instance.conversationActive)
        {
            pressXUI.SetActive(false);
            return;
        }
        pressXUI.SetActive(evt.isSitting);
    }

    private void ShowAfterDiscard(OnShowAfterDiscard evt)
    {
        pressXUI.SetActive(playerSitting);
        microphoneUI.SetActive(false);
        processingUI.SetActive(false);
    }

    private void HideAll(OnHideTalk evt)
    {
        pressXUI.SetActive(false);
        microphoneUI.SetActive(false);
        processingUI.SetActive(false);
    }
}