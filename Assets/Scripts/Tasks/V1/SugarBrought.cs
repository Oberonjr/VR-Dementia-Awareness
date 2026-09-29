using FMODUnity;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SugarBrought : SimulationTask
{
    [Header("Go Ahead Voicelines")]
    [SerializeField] private float julietteGoAheadDelay = 0.0f;
    [SerializeField] private EventReference enGoAhead;
    [SerializeField] private EventReference nlGoAhead;

    private EventReference currentGoAhead;
    private Timer timer;

    private void Start()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocalizationChanged;

        if (LocalizationSettings.SelectedLocale != null)
        {
            HandleLocalizationChanged(LocalizationSettings.SelectedLocale);
        }

        timer = gameObject.AddComponent<Timer>();
        timer.OnTimerFinished += PlayPhrase;
    }

    private void OnDestroy()
    {
        timer.OnTimerFinished -= PlayPhrase;
        LocalizationSettings.SelectedLocaleChanged -= HandleLocalizationChanged;
        EventBus<OnJulietteFinishedTalk>.OnEvent -= PhraseFinished;
    }

    public override void StartTask()
    {
        base.StartTask();
        timer.Setup(julietteGoAheadDelay, false, true);
    }

    public void PlayPhrase()
    {
        EventBus<OnJulietteFinishedTalk>.OnEvent += PhraseFinished;
        EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentGoAhead));
    }

    private void PhraseFinished(OnJulietteFinishedTalk evt)
    {
        FinishTask();
    }

    private void HandleLocalizationChanged(Locale newLocale)
    {
        if (newLocale.Identifier.Code.StartsWith("en")) { currentGoAhead = enGoAhead; }
        else if (newLocale.Identifier.Code.StartsWith("nl")) { currentGoAhead = nlGoAhead; }
    }
}