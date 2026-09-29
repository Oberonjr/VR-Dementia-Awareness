using FMODUnity;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class WrapUp : SimulationTask
{
    [Header("Record Voicelines")]
    [SerializeField] private float recordDelay = 2.0f;
    [SerializeField] private EventReference enRecord;
    [SerializeField] private EventReference nlRecord;

    [Header("Humming Voiceline")]
    [SerializeField] private float hummingDelay = 1.0f;
    [SerializeField] private EventReference humming;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 8.0f;

    [Header("Restart after Hum Timer")]
    [SerializeField] private float restartDelay = 1.0f;

    private EventReference currentRecord;
    private int sequenceIndex;
    private Timer timer;

    private void Start()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocalizationChanged;

        if (LocalizationSettings.SelectedLocale != null)
        {
            HandleLocalizationChanged(LocalizationSettings.SelectedLocale);
        }
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= HandleLocalizationChanged;

        if (timer != null) { timer.OnTimerFinished -= PlaySequences; }

        EventBus<OnJulietteFinishedTalk>.OnEvent -= JulietteFinishedTalk;

        RuntimeManager.GetEventDescription(humming.Guid).unloadSampleData();
    }

    public override void StartTask()
    {
        base.StartTask();

        timer = gameObject.AddComponent<Timer>();
        timer.Setup(recordDelay, false, true);
        timer.OnTimerFinished += PlaySequences;

        EventBus<OnJulietteFinishedTalk>.OnEvent += JulietteFinishedTalk;
    }

    private void JulietteFinishedTalk(OnJulietteFinishedTalk evt)
    {
        switch (sequenceIndex)
        {
            case 1:
                {
                    timer.Setup(hummingDelay, false, true);
                    break;
                }
            case 2:
                {
                    timer.Setup(restartDelay, false, true);
                    break;
                }
        }
    }

    private void PlaySequences()
    {
        switch (sequenceIndex)
        {
            case 0:
                {
                    EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentRecord));
                    break;
                }
            case 1:
                {
                    EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(humming));
                    EventBus<OnTransitionScreen>.Publish(new OnTransitionScreen(1.0f, fadeDuration));
                    break;
                }
            case 2:
                {
                    GameManager.Instance.RestartCurrentScene();
                    break;
                }
        }

        sequenceIndex++;
    }

    private void HandleLocalizationChanged(Locale newLocale)
    {
        if (newLocale.Identifier.Code.StartsWith("en")) { currentRecord = enRecord; }
        else if (newLocale.Identifier.Code.StartsWith("nl")) { currentRecord = nlRecord; }
    }
}