using FMODUnity;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class AskGetSugar : SimulationTask
{
    [Header("Forgot Sugar Voicelines")]
    [SerializeField] private float forgotSugarDelay = 2.0f;
    [SerializeField] private EventReference enForgotSugar;
    [SerializeField] private EventReference nlForgotSugar;

    [Header("Bring Sugar Sam Settings")]
    [SerializeField] private float bringSugarSamDelay = 2.0f;
    [SerializeField] private EventReference enBringSugarSam;
    [SerializeField] private EventReference nlBringSugarSam;

    [Header("Sorry Robin Settings")]
    [SerializeField] private float sorryRobinDelay = 1.0f;
    [SerializeField] private EventReference enSorryRobin;
    [SerializeField] private EventReference nlSorryRobin;

    [Header("Reminder Indicator Settings")]
    [SerializeField] private float remindAfterSeconds = 60.0f;
    [SerializeField] private GrabSugarTrigger grabSugarTrigger;
    [SerializeField] private EventReference enSmallCabinet;
    [SerializeField] private EventReference nlSmallCabinet;

    private EventReference currentForgotSugar;
    private EventReference currentBringSugarSam;
    private EventReference currentSorryRobin;
    private EventReference currentSmallCabinet;
    private int sequenceIndex;
    private Timer timer;

    private void Start()
    {
        LocalizationSettings.SelectedLocaleChanged += HandleLocalizationChanged;

        if (LocalizationSettings.SelectedLocale != null)
        {
            HandleLocalizationChanged(LocalizationSettings.SelectedLocale);
        }

        timer = gameObject.AddComponent<Timer>();
        timer.OnTimerFinished += PlaySequence;

        EventBus<OnSugarPlacedDown>.OnEvent += SugarPlaced;
    }

    private void OnDestroy()
    {
        timer.OnTimerFinished -= PlaySequence;
        EventBus<OnJulietteFinishedTalk>.OnEvent -= JulietteFinishedTalk;
        EventBus<OnSugarPlacedDown>.OnEvent -= SugarPlaced;
    }

    public override void StartTask()
    {
        base.StartTask();
        timer.Setup(forgotSugarDelay, false, true);
        EventBus<OnJulietteFinishedTalk>.OnEvent += JulietteFinishedTalk;
        EventBus<OnUpdateTask>.Publish(new OnUpdateTask());
    }

    private void JulietteFinishedTalk(OnJulietteFinishedTalk evt)
    {
        switch (sequenceIndex)
        {
            case 1:
                {
                    timer.Setup(bringSugarSamDelay, false, true);
                    break;
                }
            case 2:
                {
                    if (grabSugarTrigger != null) { grabSugarTrigger.SetActive(true); }
                    timer.Setup(sorryRobinDelay, false, true);
                    break;
                }
            case 3:
                {
                    timer.Setup(remindAfterSeconds, true, true);
                    break;
                }
        }
    }

    private void PlaySequence()
    {
        switch (sequenceIndex)
        {
            case 0:
                {
                    if (!currentForgotSugar.IsNull) { EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentForgotSugar)); }
                    break;
                }
            case 1:
                {
                    if (!currentBringSugarSam.IsNull) { EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentBringSugarSam)); }
                    break;
                }
            case 2:
                {
                    if (!currentSorryRobin.IsNull) { EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentSorryRobin)); }
                    break;
                }
            case 3:
                {
                    if (!currentSmallCabinet.IsNull) { EventBus<OnJulietteTalk>.Publish(new OnJulietteTalk(currentSmallCabinet)); }
                    break;
                }
            case 4:
                {
                    EventBus<OnShowIndicator>.Publish(new OnShowIndicator(true, IndicatorHUDs.SugarPickup));
                    timer.StopTimer();
                    break;
                }
        }

        sequenceIndex++;
    }

    private void SugarPlaced(OnSugarPlacedDown evt)
    {
        EventBus<OnJulietteFinishedTalk>.OnEvent -= JulietteFinishedTalk;
        EventBus<OnShowIndicator>.Publish(new OnShowIndicator(false));
        timer.StopTimer();
        FinishTask();
    }

    private void HandleLocalizationChanged(Locale newLocale)
    {
        if (newLocale.Identifier.Code.StartsWith("en"))
        {
            currentForgotSugar = enForgotSugar;
            currentBringSugarSam = enBringSugarSam;
            currentSorryRobin = enSorryRobin;
            currentSmallCabinet = enSmallCabinet;
        }
        else if (newLocale.Identifier.Code.StartsWith("nl"))
        {
            currentForgotSugar = nlForgotSugar;
            currentBringSugarSam = nlForgotSugar; // Fixed to fit localized array mapping sequence
            currentSorryRobin = nlSorryRobin;
            currentSmallCabinet = nlSmallCabinet;
        }
    }
}