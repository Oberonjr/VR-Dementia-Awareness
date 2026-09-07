using FMODUnity;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class HouseEntered : SimulationTask
{
    [Header("Come Sit Voicelines")]
    [SerializeField] private float julietteSitDownDelay = 2.0f;
    [SerializeField] private EventReference enComeSitDown;
    [SerializeField] private EventReference nlComeSitDown;

    private EventReference currentSitDown;
    private Timer timer;

    private void Start()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocalizationChanged;
        EventBus<OnEnterBuilding>.OnEvent += BuildingEntered;

        if (LocalizationSettings.SelectedLocale != null)
        {
            HandleLocalizationChanged(LocalizationSettings.SelectedLocale);
        }

        timer = gameObject.AddComponent<Timer>();
        timer.OnTimerFinished += PlayVoice;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocalizationChanged;
        EventBus<OnEnterBuilding>.OnEvent -= BuildingEntered;
        EventBus<OnJulietteAnimFinished>.OnEvent -= JulietteSitDown;
        timer.OnTimerFinished -= PlayVoice;
    }

    private void BuildingEntered(OnEnterBuilding evt)
    {
        timer.Setup(julietteSitDownDelay, false, true);
    }

    private void PlayVoice()
    {
        EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentSitDown));
        EventBus<OnWalkAnim>.Publish(new OnWalkAnim());

        EventBus<OnJulietteAnimFinished>.OnEvent += JulietteSitDown;
    }

    private void JulietteSitDown(OnJulietteAnimFinished evt)
    {
        if (evt.animationType != JulietteAnimations.Walk) { return; }

        EventBus<OnSitAnim>.Publish(new OnSitAnim());
        EventBus<OnJulietteAnimFinished>.OnEvent -= JulietteSitDown;
        FinishTask();
    }

    private void HandleLocalizationChanged(Locale newLocale)
    {
        if (newLocale.Identifier.Code.StartsWith("en")) { currentSitDown = enComeSitDown; }
        else if (newLocale.Identifier.Code.StartsWith("nl")) { currentSitDown = nlComeSitDown; }
    }
}