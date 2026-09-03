using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using UnityEngine;

/// <summary>
/// Controls character facial expressions, breathing animations, and post-processing mood states based on voice interaction timelines
/// Might be an idea to make it more pretty, and separate the breathing logic to another script
/// </summary>
public class ExpressionController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinnedMeshRenderer targetMesh;

    [Header("Blendshape Indices")]
    [SerializeField] private int happyFullFaceIndex = 0;
    [SerializeField] private int happyIndex = 16;
    [SerializeField] private int sadIndex = 17;
    [SerializeField] private int confusedIndex = 18;

    [Header("Emotion Settings")]
    [SerializeField] private float emotionTransitionTime = 0.5f;
    [SerializeField] private float moodTransitionTime = 1.0f;

    [Header("Breathing Mesh")]
    [SerializeField] private SkinnedMeshRenderer breathingMesh;
    [SerializeField] private int breathingIndex = 0;

    [Header("Breathing Intensities")]
    [SerializeField] private float minIntensity = 65.0f;
    [SerializeField] private float maxIntensity = 80.0f;

    [Header("Breathing Durations")]
    [SerializeField] private float minBreathDuration = 1.4f;
    [SerializeField] private float maxBreathDuration = 2.2f;
    [SerializeField] private float minRestDuration = 0.3f;
    [SerializeField] private float maxRestDuration = 0.7f;

    private VoiceInteractionManager voiceManager;
    private string currentEmotion = "neutral";
    private string lastEmotion = "neutral";
    private bool isCurrentlyNostalgic;

    private float currentHappyFullFaceWeight;
    private float currentHappyWeight;
    private float currentSadWeight;
    private float currentConfusedWeight;

    private float moodNeutral = 100.0f;
    private float moodHappy;
    private float moodSad;
    private float moodNostalgic;
    private float moodFurious;
    private float moodAnxious;

    private float breathingTimer;
    private float currentBreathingDuration = 1.5f;
    private float currentBreathingWeight;
    private float startBreathingWeight;
    private float targetBreathingWeight;
    private int breathingState;

    private void Start()
    {
        voiceManager = GameManager.Instance.VoiceInterManager;

        currentBreathingDuration = Random.Range(minBreathDuration, maxBreathDuration);
        targetBreathingWeight = Random.Range(minIntensity, maxIntensity);

        EventBus<OnJulietteFinishedTalk>.OnEvent += EndMoodOnFinishTalking;
    }

    private void Update()
    {
        HandleBreathing();

        if (targetMesh == null) { return; }

        HandleEmotions();
        UpdatePostProcessingMoods();
    }

    private void HandleEmotions()
    {
        if (voiceManager == null) { return; }

        bool isSpeaking = voiceManager.IsSpeaking;

        if (isSpeaking)
        {
            float currentTime = voiceManager.GetCurrentDialogueTime();
            var timeline = voiceManager.CurrentEmotionTimeline;

            string evaluatedEmotion = "neutral";
            bool evaluatedNostalgic = false;

            // Evaluate the active emotion from timeline based on current playback time
            for (int i = timeline.Count - 1; i >= 0; i--)
            {
                if (currentTime >= timeline[i].startTime)
                {
                    evaluatedEmotion = timeline[i].emotion;
                    evaluatedNostalgic = timeline[i].isNostalgic;
                    break;
                }
            }

            if (evaluatedEmotion != currentEmotion || evaluatedNostalgic != isCurrentlyNostalgic)
            {
                currentEmotion = evaluatedEmotion;
                lastEmotion = currentEmotion;
                isCurrentlyNostalgic = evaluatedNostalgic;
            }
        }
        else
        {
            currentEmotion = lastEmotion;
        }

        float targetHappyFull = 0.0f;
        float targetHappy = 0.0f;
        float targetSad = 0.0f;
        float targetConfused = 0.0f;

        switch (currentEmotion)
        {
            case "happy":
                if (isSpeaking) { targetHappy = 100.0f; }
                else { targetHappyFull = 100.0f; }
                break;
            case "sad":
                targetSad = 100.0f;
                break;
            case "fearful":
            case "angry":
                targetConfused = 100.0f;
                break;
        }

        float emotionSpeed = 100.0f / emotionTransitionTime;

        currentHappyFullFaceWeight = Mathf.MoveTowards(currentHappyFullFaceWeight, targetHappyFull, emotionSpeed * Time.deltaTime);
        currentHappyWeight = Mathf.MoveTowards(currentHappyWeight, targetHappy, emotionSpeed * Time.deltaTime);
        currentSadWeight = Mathf.MoveTowards(currentSadWeight, targetSad, emotionSpeed * Time.deltaTime);
        currentConfusedWeight = Mathf.MoveTowards(currentConfusedWeight, targetConfused, emotionSpeed * Time.deltaTime);

        targetMesh.SetBlendShapeWeight(happyFullFaceIndex, currentHappyFullFaceWeight);
        targetMesh.SetBlendShapeWeight(happyIndex, currentHappyWeight);
        targetMesh.SetBlendShapeWeight(sadIndex, currentSadWeight);
        targetMesh.SetBlendShapeWeight(confusedIndex, currentConfusedWeight);
    }

    private void UpdatePostProcessingMoods()
    {
        float targetNeutral = 0.0f;
        float targetHappy = 0.0f;
        float targetSad = 0.0f;
        float targetNostalgic = 0.0f;
        float targetFurious = 0.0f;
        float targetAnxious = 0.0f;

        if (isCurrentlyNostalgic)
        {
            targetNostalgic = 100.0f;
        }
        else
        {
            switch (currentEmotion)
            {
                case "happy": targetHappy = 100.0f; break;
                case "sad": targetSad = 100.0f; break;
                case "angry": targetFurious = 100.0f; break;
                case "fearful": targetAnxious = 100.0f; break;
                default: targetNeutral = 100.0f; break;
            }
        }

        float moodSpeed = 100.0f / moodTransitionTime;

        UpdateSingleMood(Mood.Neutral, ref moodNeutral, targetNeutral, moodSpeed);
        UpdateSingleMood(Mood.Happy, ref moodHappy, targetHappy, moodSpeed);
        UpdateSingleMood(Mood.Sad, ref moodSad, targetSad, moodSpeed);
        UpdateSingleMood(Mood.Nostalgic, ref moodNostalgic, targetNostalgic, moodSpeed);
        UpdateSingleMood(Mood.Furious, ref moodFurious, targetFurious, moodSpeed);
        UpdateSingleMood(Mood.Anxious, ref moodAnxious, targetAnxious, moodSpeed);
    }

    private void UpdateSingleMood(Mood mood, ref float currentValue, float targetValue, float speed)
    {
        if (Mathf.Approximately(currentValue, targetValue)) { return; }

        int oldInt = Mathf.RoundToInt(currentValue);
        currentValue = Mathf.MoveTowards(currentValue, targetValue, speed * Time.deltaTime);
        int newInt = Mathf.RoundToInt(currentValue);

        if (oldInt != newInt)
        {
            GameManager.Instance.SetMoodPercentage(mood, newInt);
        }
    }

    private void EndMoodOnFinishTalking(OnJulietteFinishedTalk e)
    {
        GameManager.Instance.TransitionMood(GameManager.Instance.volumeconfig, moodTransitionTime);
    }

    private void HandleBreathing()
    {
        if (breathingMesh == null) { return; }

        breathingTimer += Time.deltaTime;

        // Switch states: 0 = Inhale, 1 = Exhale, 2 = Rest
        if (breathingTimer >= currentBreathingDuration)
        {
            breathingTimer = 0.0f;
            startBreathingWeight = currentBreathingWeight;
            breathingState = (breathingState + 1) % 3;

            switch (breathingState)
            {
                case 0:
                    currentBreathingDuration = Random.Range(minBreathDuration, maxBreathDuration);
                    targetBreathingWeight = Random.Range(minIntensity, maxIntensity);
                    break;
                case 1:
                    currentBreathingDuration = Random.Range(minBreathDuration, maxBreathDuration);
                    targetBreathingWeight = 0.0f;
                    break;
                case 2:
                    currentBreathingDuration = Random.Range(minRestDuration, maxRestDuration);
                    targetBreathingWeight = 0.0f;
                    break;
            }
        }

        float progress = breathingTimer / currentBreathingDuration;
        currentBreathingWeight = Mathf.Lerp(startBreathingWeight, targetBreathingWeight, Mathf.SmoothStep(0.0f, 1.0f, progress));

        breathingMesh.SetBlendShapeWeight(breathingIndex, currentBreathingWeight);
    }
}