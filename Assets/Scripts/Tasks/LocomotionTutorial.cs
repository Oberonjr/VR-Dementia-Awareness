using FMODUnity;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

public class LocomotionTutorial : SimulationTask
{
    [Header("Feedback Settings")]
    [SerializeField] private EventReference successSound;
    [SerializeField] private Volume successVolume;
    [SerializeField] private float volumeTransitionTime = 1.0f;

    [Header("VR Character Setup")]
    [SerializeField] private OVRManager vrConfig;

    private int currentIndex;
    private Coroutine volumeRoutine;

    private void Start()
    {
        if (successVolume != null) { successVolume.weight = 0.0f; }
    }

    private void OnDestroy()
    {
        EventBus<OnMoved>.OnEvent -= MovingPerformed;
        EventBus<OnTurned>.OnEvent -= TurnPerformed;
        EventBus<OnPalmMenuVisibilityChanged>.OnEvent -= PalmMenuVisibilityChanged;
    }

    public override void StartTask()
    {
        base.StartTask();
        SetupCurrentLocomotion();
        EventBus<OnUpdateTask>.Publish(new OnUpdateTask());
        vrConfig.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.ConformingToController;
    }

    public override void FinishTask()
    {
        base.FinishTask();
        vrConfig.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.Natural;
    }

    private void MovingPerformed(OnMoved evt)
    {
        SetupCurrentLocomotion();
    }

    private void TurnPerformed(OnTurned evt)
    {
        SetupCurrentLocomotion();
    }

    private void SetupCurrentLocomotion()
    {
        switch (currentIndex)
        {
            case 0:
                {
                    EventBus<OnTurned>.OnEvent += TurnPerformed;
                    EventBus<OnShowTutorial>.Publish(new OnShowTutorial(true, TutorialType.Turning));
                    break;
                }
            case 1:
                {
                    EventBus<OnMoved>.OnEvent += MovingPerformed;
                    EventBus<OnTurned>.OnEvent -= TurnPerformed;
                    EventBus<OnShowTutorial>.Publish(new OnShowTutorial(true, TutorialType.Moving));
                    TriggerSuccessFeedback();
                    break;
                }
            case 2:
                {
                    EventBus<OnMoved>.OnEvent -= MovingPerformed;
                    EventBus<OnChangePalmMenuActive>.Publish(new OnChangePalmMenuActive(true));
                    EventBus<OnPalmMenuVisibilityChanged>.OnEvent += PalmMenuVisibilityChanged;
                    EventBus<OnShowTutorial>.Publish(new OnShowTutorial(true, TutorialType.MenuOpen));
                    TriggerSuccessFeedback();
                    break;
                }
        }

        currentIndex++;
    }

    private void PalmMenuVisibilityChanged(OnPalmMenuVisibilityChanged evt)
    {
        if (evt.isVisible)
        {
            EventBus<OnPalmMenuVisibilityChanged>.OnEvent -= PalmMenuVisibilityChanged;
            TriggerSuccessFeedback();
            EventBus<OnShowTutorial>.Publish(new OnShowTutorial(false));
            FinishTask();
        }
    }

    private void TriggerSuccessFeedback()
    {
        if (!successSound.IsNull) { RuntimeManager.PlayOneShot(successSound); }

        if (successVolume != null)
        {
            if (volumeRoutine != null) { StopCoroutine(volumeRoutine); }
            volumeRoutine = StartCoroutine(AnimateVolume());
        }
    }

    private IEnumerator AnimateVolume()
    {
        float speed = 1.0f / (volumeTransitionTime / 2.0f);

        while (successVolume.weight < 1.0f)
        {
            successVolume.weight = Mathf.MoveTowards(successVolume.weight, 1.0f, speed * Time.deltaTime);
            yield return null;
        }

        while (successVolume.weight > 0.0f)
        {
            successVolume.weight = Mathf.MoveTowards(successVolume.weight, 0.0f, speed * Time.deltaTime);
            yield return null;
        }
    }
}