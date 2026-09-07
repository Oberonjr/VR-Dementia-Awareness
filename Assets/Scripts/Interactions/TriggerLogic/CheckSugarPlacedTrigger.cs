using UnityEngine;

public class CheckSugarPlacedTrigger : MonoBehaviour
{
    [SerializeField] GameObject sugarObject;

    private MeshRenderer placeArea;

    private void Start()
    {
        placeArea = GetComponent<MeshRenderer>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject == sugarObject)
        {
            EventBus<OnSugarPlacedDown>.Publish(new OnSugarPlacedDown());
            if (placeArea != null) { placeArea.enabled = false; }
        }
    }
}
