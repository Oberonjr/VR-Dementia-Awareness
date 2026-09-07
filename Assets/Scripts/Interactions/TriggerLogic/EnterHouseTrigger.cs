using UnityEngine;

public class EnterHouseTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (other.tag == "Player")
        {
            EventBus<OnEnterBuilding>.Publish(new OnEnterBuilding());
            gameObject.SetActive(false);
        }
    }
}
