using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PressPhone : SimulationTask
{
    [Header("Phone Setup")]
    [SerializeField] private Transform phone;

    [Header("Hand Setup")]
    [SerializeField] private Transform[] leftHandComponents;

    [Header("Hint Setup")]
    [SerializeField] private float remindAfterSeconds = 60.0f;
    [SerializeField] private Image sendButtonImage;

    private Timer timer;
    private int reminderIndex;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);

        phone.gameObject.SetActive(false);

        timer = gameObject.AddComponent<Timer>();
        timer.OnTimerFinished += ShowReminder;
    }

    private void OnDestroy()
    {
        timer.OnTimerFinished -= ShowReminder;
    }

    public override void StartTask()
    {
        base.StartTask();
        phone.gameObject.SetActive(true);

        for (int i = 0; i < leftHandComponents.Length; i++)
        {
            leftHandComponents[i].gameObject.SetActive(false);
        }

        timer.Setup(remindAfterSeconds, true, true);
    }

    public void SendButtonPressed()
    {
        HidePhone();
    }

    private void HidePhone()
    {
        EventBus<OnPhoneSendPressed>.Publish(new OnPhoneSendPressed());

        timer.StopTimer();
        EventBus<OnShowIndicator>.Publish(new OnShowIndicator(false));
        phone.gameObject.SetActive(false);

        for (int i = 0; i < leftHandComponents.Length; i++)
        {
            leftHandComponents[i].gameObject.SetActive(true);
        }

        FinishTask();
    }

    private void ShowReminder()
    {
        switch (reminderIndex)
        {
            case 0:
                {
                    EventBus<OnShowIndicator>.Publish(new OnShowIndicator(true, IndicatorHUDs.PressSend));
                    break;
                }
            case 1:
                {
                    StartCoroutine(AnimateSendButtonImage());
                    timer.StopTimer();
                    break;
                }
        }

        reminderIndex++;
    }

    private IEnumerator AnimateSendButtonImage()
    {
        Color baseColor = sendButtonImage.color;
        float pulseSpeed = 5.0f;

        while (true)
        {
            baseColor.a = 0.7f + (Mathf.Sin(Time.time * pulseSpeed) * 0.3f);
            sendButtonImage.color = baseColor;
            yield return null;
        }
    }
}