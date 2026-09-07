using UnityEngine;

/// <summary>
/// Shows appropriate indicators, in case the User takes a lot of time for a specific task
/// </summary>
public class HandleIndicationHUD : MonoBehaviour
{
    [Header("HUD Setup")]
    [SerializeField] private GameObject indicatorHUDs;
    [SerializeField] private IndicatorInstance[] indicatorInstances;

    private void Start()
    {
        for (int i = 0; i < indicatorInstances.Length; i++)
        {
            indicatorInstances[i].instance.SetActive(false);
        }

        EventBus<OnShowIndicator>.OnEvent += HandleIndicator;
        HandleIndicator(new OnShowIndicator(false));
    }

    private void OnDestroy()
    {
        EventBus<OnShowIndicator>.OnEvent -= HandleIndicator;
    }

    private void HandleIndicator(OnShowIndicator evt)
    {
        indicatorHUDs.SetActive(evt.isActive);

        for (int i = 0; i < indicatorInstances.Length; i++)
        {
            bool active = indicatorInstances[i].indicatorType == evt.indicator;
            indicatorInstances[i].instance.SetActive(active);
        }
    }
}