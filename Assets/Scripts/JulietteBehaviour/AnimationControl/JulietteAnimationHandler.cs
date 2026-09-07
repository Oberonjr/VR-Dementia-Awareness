using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles queuing and execution of character animations, synchronized with position adjustments and event-driven triggers
/// Would be better to have the animations work smoothly (with no manual position adjustments), but this will do for the moment
/// </summary>
public class JulietteAnimationHandler : MonoBehaviour
{
    [Header("Animation Setup")]
    [SerializeField] private JulietteAnimationInfo[] julietteAnimInfo;
    [SerializeField] private Animator doorAnim;

    private Dictionary<JulietteAnimations, GameObject> animInfo = new Dictionary<JulietteAnimations, GameObject>();
    private Animator julietteAnim;
    private Queue<AnimationQueueItem> animationQueue = new Queue<AnimationQueueItem>();
    private bool isPlayingQueue;

    private void Start()
    {
        julietteAnim = GetComponent<Animator>();

        EventBus<OnOpenDoorAnim>.OnEvent += OpenDoorAnim;
        EventBus<OnWalkAnim>.OnEvent += WalkAnim;
        EventBus<OnSitAnim>.OnEvent += SitAnim;
        EventBus<OnIdleAnim>.OnEvent += IdleAnim;

        // Map animation types to their respective start positions
        if (julietteAnimInfo != null)
        {
            for (int i = 0; i < julietteAnimInfo.Length; i++)
            {
                if (julietteAnimInfo[i].startPosition != null)
                {
                    animInfo.Add(julietteAnimInfo[i].animationType, julietteAnimInfo[i].startPosition);
                }
            }
        }
    }

    private void OnDestroy()
    {
        EventBus<OnOpenDoorAnim>.OnEvent -= OpenDoorAnim;
        EventBus<OnWalkAnim>.OnEvent -= WalkAnim;
        EventBus<OnSitAnim>.OnEvent -= SitAnim;
        EventBus<OnIdleAnim>.OnEvent -= IdleAnim;
    }

    private void OpenDoorAnim(OnOpenDoorAnim evt)
    {
        QueueAnimation("OpenDoor", JulietteAnimations.OpenDoor);
    }

    private void WalkAnim(OnWalkAnim evt)
    {
        QueueAnimation("Walk", JulietteAnimations.Walk);
    }

    private void SitAnim(OnSitAnim evt)
    {
        QueueAnimation("SitDown", JulietteAnimations.Sit);
    }

    private void IdleAnim(OnIdleAnim evt)
    {
        QueueAnimation("Idle", JulietteAnimations.IdleStand);
    }

    private void QueueAnimation(string triggerName, JulietteAnimations animType)
    {
        animationQueue.Enqueue(new AnimationQueueItem(triggerName, animType));

        if (!isPlayingQueue) { StartCoroutine(ExecuteQueue()); }
    }

    private IEnumerator ExecuteQueue()
    {
        isPlayingQueue = true;

        while (animationQueue.Count > 0)
        {
            AnimationQueueItem current = animationQueue.Dequeue();

            // Snap character to the predefined start position if available
            if (animInfo.ContainsKey(current.animType) && animInfo[current.animType] != null)
            {
                transform.position = animInfo[current.animType].transform.position;
                transform.rotation = animInfo[current.animType].transform.rotation;
            }

            if (julietteAnim != null)
            {
                yield return StartCoroutine(WaitForCurrentAnimation(current.animType, current.triggerName));
            }
        }

        isPlayingQueue = false;
    }

    private IEnumerator WaitForCurrentAnimation(JulietteAnimations animType, string triggerName)
    {
        if (julietteAnim == null) { yield break; }

        AnimatorStateInfo initialState = julietteAnim.GetCurrentAnimatorStateInfo(0);
        julietteAnim.SetTrigger(triggerName);

        if (triggerName == "OpenDoor") { doorAnim.SetTrigger(triggerName); }

        // Wait until the transition to the new animation state begins
        while (julietteAnim.GetCurrentAnimatorStateInfo(0).fullPathHash == initialState.fullPathHash && !julietteAnim.IsInTransition(0))
        {
            yield return null;
        }

        // Wait for the transition to complete
        while (julietteAnim.IsInTransition(0))
        {
            yield return null;
        }

        // Wait until the animation reaches its final phase
        while (julietteAnim.GetCurrentAnimatorStateInfo(0).normalizedTime < 0.95f)
        {
            yield return null;
        }

        EventBus<OnJulietteAnimFinished>.Publish(new OnJulietteAnimFinished(animType));
    }
}