using UnityEngine;

/// <summary>
/// Used to transition into a mood quickly, by simply entering an area.
/// Mainly used for quick iterations
/// </summary>
public class MoodTransitionTrigger : MonoBehaviour
{

    [Header("Mood Configuration")]
    [SerializeField] private VolumeConfiguration volumeConfiguration;
    [SerializeField] private float transitionTime = 2;

    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            GameManager.Instance.TransitionMood(volumeConfiguration, transitionTime);
        }
    }
}
