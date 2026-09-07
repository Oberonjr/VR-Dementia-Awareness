using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attached to the FadeCamera GameObject
/// Handles the fading from black to transparent, and the other way around
/// </summary>
public class FadeBlackScreen : MonoBehaviour
{
    [Header("Fader Elements")]
    [SerializeField] private Image screenImage;

    private Timer timer;
    private Color fadeStartColor;
    private Color fadeTargetColor;
    private float currentFadeDuration;

    private void Start()
    {
        EventBus<OnTransitionScreen>.OnEvent += ActivateFade;

        timer = gameObject.AddComponent<Timer>();
        timer.OnTimerRunning += UpdateFade;
        timer.OnTimerFinished += FinishFade;

        EventBus<OnTransitionScreen>.Publish(new OnTransitionScreen(0.0f, 1.5f));
    }

    private void OnDestroy()
    {
        EventBus<OnTransitionScreen>.OnEvent -= ActivateFade;

        if (timer != null)
        {
            timer.OnTimerRunning -= UpdateFade;
            timer.OnTimerFinished -= FinishFade;
        }
    }

    private void ActivateFade(OnTransitionScreen evt)
    {
        StartFade(evt.targetPercent, evt.duration);
    }

    private void StartFade(float percent, float duration)
    {
        if (screenImage == null) { return; }

        currentFadeDuration = duration;
        fadeStartColor = screenImage.color;

        float targetPercent = Mathf.Clamp01(percent);
        fadeTargetColor = new Color(fadeStartColor.r, fadeStartColor.g, fadeStartColor.b, targetPercent);

        timer.ResetTimer();
        timer.Setup(duration, false, true);
    }

    private void UpdateFade()
    {
        if (screenImage == null || currentFadeDuration <= 0.0f) { return; }

        float elapsedTime = currentFadeDuration - timer.GetTimeLeft();
        screenImage.color = Color.Lerp(fadeStartColor, fadeTargetColor, elapsedTime / currentFadeDuration);
    }

    private void FinishFade()
    {
        if (screenImage != null) { screenImage.color = fadeTargetColor; }
    }
}