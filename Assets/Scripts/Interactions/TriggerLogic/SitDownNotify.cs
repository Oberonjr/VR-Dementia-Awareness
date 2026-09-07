using UnityEngine;

public class SitDownNotify : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            EventBus<OnPlayerSitDown>.Publish(new OnPlayerSitDown(true));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.tag == "Player")
        {
            EventBus<OnPlayerSitDown>.Publish(new OnPlayerSitDown(false));
        }
    }
}
