using UnityEngine;

/// <summary>
/// Manages the visuals for the sugar grabbing tutorial, toggling UI elements based on player proximity and interaction state
/// </summary>
public class GrabSugarTrigger : MonoBehaviour
{
    [Header("Tutorial References")]
    [SerializeField] private GameObject grabTutorialWindow;
    [SerializeField] private PhysicsProxyFollower sugarGrabbable;
    [SerializeField] private GameObject placeSugarHere;

    private MeshRenderer triggerArea;
    private bool isActive;
    private bool userInside;
    private bool showIndicator;
    private bool wasGrabbed;

    private void Start()
    {
        triggerArea = GetComponent<MeshRenderer>();
        SetActive(false);

        EventBus<OnSugarPlacedDown>.OnEvent += SugarPlaced;
    }

    private void Update()
    {
        CheckCurrentlyVisible();
    }

    private void OnDestroy()
    {
        EventBus<OnSugarPlacedDown>.OnEvent -= SugarPlaced;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) { userInside = true; }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) { userInside = false; }
    }

    public void SetActive(bool isActive)
    {
        if (triggerArea == null || grabTutorialWindow == null) { return; }
        this.isActive = isActive;
        showIndicator = isActive;

        if (!isActive)
        {
            triggerArea.enabled = false;
            grabTutorialWindow.SetActive(false);
            placeSugarHere.SetActive(false);
        }
    }

    private void CheckCurrentlyVisible()
    {
        if (!isActive) { return; }

        // Toggle UI visibility based on whether the player is inside the area
        if (showIndicator)
        {
            triggerArea.enabled = !userInside;
            grabTutorialWindow.SetActive(userInside);
        }

        // Manage target indicator when item is actively held
        if (sugarGrabbable.IsGrabbed())
        {
            placeSugarHere.SetActive(true);
            showIndicator = false;
            triggerArea.enabled = false;
            grabTutorialWindow.SetActive(false);
            wasGrabbed = true;
        }
        else if (!sugarGrabbable.IsGrabbed())
        {
            placeSugarHere.SetActive(false);

            if (wasGrabbed && userInside)
            {
                showIndicator = true;
                wasGrabbed = false;
            }
        }
    }

    private void SugarPlaced(OnSugarPlacedDown evt)
    {
        triggerArea.enabled = false;
        grabTutorialWindow.SetActive(false);
        isActive = false;
    }
}