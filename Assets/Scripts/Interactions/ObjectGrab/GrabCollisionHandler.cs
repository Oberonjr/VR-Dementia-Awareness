using UnityEngine;

/// <summary>
/// Manages physics layers for grabbable VR objects and tracks the interaction state using built-in Meta interaction events
/// </summary>
public class GrabCollisionHandler : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private GameObject targetPhysicsObject;
    [SerializeField] private string ignoreHandLayerName = "Ignore Physics Hands";

    public bool IsGrabbed { get; private set; }

    private int ignoreHandLayer;
    private int originalLayer;

    private void Awake()
    {
        ignoreHandLayer = LayerMask.NameToLayer(ignoreHandLayerName);
        if (targetPhysicsObject != null)
        {
            originalLayer = targetPhysicsObject.layer;
        }
    }

    public void HandleGrab()
    {
        if (targetPhysicsObject == null) { return; }
        IsGrabbed = true;
        SetLayerRecursively(targetPhysicsObject, ignoreHandLayer);
    }

    public void HandleRelease()
    {
        if (targetPhysicsObject == null) { return; }
        IsGrabbed = false;
        SetLayerRecursively(targetPhysicsObject, originalLayer);
    }

    private void SetLayerRecursively(GameObject obj, int newLayer)
    {
        obj.layer = newLayer;

        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, newLayer);
        }
    }
}