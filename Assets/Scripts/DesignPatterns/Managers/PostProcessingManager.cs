using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using FMODUnity;
using FMOD.Studio;

/// <summary>
/// Dynamic post-processing volume and FMOD audio mixer based on mood settings
/// Used to transition between moods
/// </summary>
public class PostProcessingManager : MonoBehaviour
{
    [Header("FMOD Music Setup")]
    [Tooltip("The background music event that responds to the mood parameters.")]
    public EventReference musicEvent;
    private EventInstance musicInstance;

    [Header("Volumes")]
    public VolumeCollection volumeCollection;

    [HideInInspector]
    public List<VolumeConfig> activeConfigs = new List<VolumeConfig>();

    private Dictionary<Mood, Volume> volumeInstances = new Dictionary<Mood, Volume>();
    private Coroutine currentTransition;

    private bool requiresUpdate = true;

    private void Start()
    {
        if (!musicEvent.IsNull)
        {
            musicInstance = RuntimeManager.CreateInstance(musicEvent);
            musicInstance.start();
        }

        if (volumeCollection == null)
        {
            Debug.LogWarning("PostProcessingManager: Missing VolumeCollection Asset.");
            return;
        }

        // Instantiate and configure post-processing volumes from entries
        for (int i = 0; i < volumeCollection.entries.Count; i++)
        {
            VolumeEntry entry = volumeCollection.entries[i];
            if (entry.volume == null) { continue; }

            GameObject gameObject = Instantiate(entry.volume, Vector3.zero, Quaternion.identity, this.transform);
            gameObject.name = $"Volume_{entry.mood}";

            Volume volComponent = gameObject.GetComponent<Volume>();
            volComponent.priority = i;

            volumeInstances.Add(entry.mood, volComponent);
        }

        InitializeActiveConfigs();
    }

    private void OnDestroy()
    {
        if (musicInstance.isValid())
        {
            musicInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            musicInstance.release();
        }
    }

    private void OnValidate() { SetDirty(); }

    private void Update()
    {
        if (requiresUpdate)
        {
            EvaluateWeights();
            requiresUpdate = false;
        }
    }

    public void SetDirty() { requiresUpdate = true; }

    public void InitializeActiveConfigs()
    {
        foreach (KeyValuePair<Mood, Volume> kvp in volumeInstances)
        {
            Mood m = kvp.Key;
            if (!activeConfigs.Any(x => x.mood == m))
            {
                activeConfigs.Add(new VolumeConfig { mood = m, volumePercentage = 0 });
            }
        }
        activeConfigs.Sort((a, b) => a.mood.CompareTo(b.mood));

        SetDirty();
    }

    public void SetMoodPercentage(Mood mood, int percentage)
    {
        if (currentTransition != null)
        {
            StopCoroutine(currentTransition);
            currentTransition = null;
        }

        VolumeConfig config = activeConfigs.Find(x => x.mood == mood);
        if (config != null)
        {
            config.volumePercentage = Mathf.Clamp(percentage, 0, 100);
            SetDirty();
        }
    }

    private void EvaluateWeights()
    {
        float totalUserPercent = 0;
        foreach (VolumeConfig cfg in activeConfigs) { totalUserPercent += cfg.volumePercentage; }

        // Calculate normalization factor if sum exceeds 100 percent
        float normalizationFactor = 1;
        if (totalUserPercent > 100) { normalizationFactor = 100 / totalUserPercent; }

        float currentCoverage = 0;

        // Process from highest priority to lowest
        for (int i = volumeCollection.entries.Count - 1; i >= 0; i--)
        {
            VolumeEntry entry = volumeCollection.entries[i];
            Mood mood = entry.mood;

            if (!volumeInstances.TryGetValue(mood, out Volume vol)) { continue; }

            VolumeConfig config = activeConfigs.Find(x => x.mood == mood);
            float userPercent = 0;
            if (config != null) { userPercent = config.volumePercentage; }

            // Update associated FMOD parameter
            if (musicInstance.isValid() && !string.IsNullOrEmpty(entry.paramFMOD))
            {
                musicInstance.setParameterByName(entry.paramFMOD, userPercent / 100.0f);
            }

            if (userPercent <= 0.01f)
            {
                vol.weight = 0;
                continue;
            }

            float targetShare = (userPercent * normalizationFactor) / 100;
            float remainingSpace = 1 - currentCoverage;

            if (remainingSpace <= 0.001f)
            {
                vol.weight = 0;
                continue;
            }

            // Calculate relative weight based on leftover blend space
            float calculatedWeight = targetShare / remainingSpace;
            vol.weight = Mathf.Clamp01(calculatedWeight);

            currentCoverage += targetShare;
        }
    }

    public void SwitchMood(VolumeConfiguration targetConfig, float transitionTime)
    {
        if (currentTransition != null) { StopCoroutine(currentTransition); }
        currentTransition = StartCoroutine(TransitionMoodRoutine(targetConfig, transitionTime));
    }

    private IEnumerator TransitionMoodRoutine(VolumeConfiguration targetConfig, float duration)
    {
        // Cache initial values before starting transition
        List<VolumeConfig> startValues = new List<VolumeConfig>();
        foreach (VolumeConfig current in activeConfigs)
        {
            startValues.Add(new VolumeConfig { mood = current.mood, volumePercentage = current.volumePercentage });
        }

        float timer = 0f;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / duration);

            foreach (VolumeConfig activeCfg in activeConfigs)
            {
                VolumeConfig startItem = startValues.Find(x => x.mood == activeCfg.mood);
                int startVal = 0;
                if (startItem != null) { startVal = startItem.volumePercentage; }

                VolumeConfig targetItem = targetConfig.configs.Find(x => x.mood == activeCfg.mood);
                int targetVal = 0;
                if (targetItem != null) { targetVal = targetItem.volumePercentage; }

                activeCfg.volumePercentage = (int)Mathf.Lerp(startVal, targetVal, t);
            }

            EvaluateWeights();
            yield return null;
        }

        // Ensure final target configurations are perfectly set
        foreach (VolumeConfig activeCfg in activeConfigs)
        {
            VolumeConfig targetItem = targetConfig.configs.Find(x => x.mood == activeCfg.mood);

            int finalTargetVal = 0;
            if (targetItem != null) { finalTargetVal = targetItem.volumePercentage; }

            activeCfg.volumePercentage = finalTargetVal;
        }

        EvaluateWeights();
        currentTransition = null;
    }
}