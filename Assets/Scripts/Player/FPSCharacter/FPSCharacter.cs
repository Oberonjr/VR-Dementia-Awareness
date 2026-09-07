using UnityEngine;
using UnityEngine.InputSystem;
using Oculus.Interaction;

/// <summary>
/// First-person character controller handling keyboard movement, mouse looking, physics-based object interaction, and Meta UI integration
/// Though it was made to enable easier testing, it isn't able to pass through the tutorial, which makes it difficult to test with
/// It is a good base for a desktop-integration, so worth keeping
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FPSCharacter : MonoBehaviour, IActiveState
{
    public bool Active => Keyboard.current != null && Keyboard.current.eKey.isPressed;

    [Header("Movement")]
    public float moveSpeed = 5.0f;
    private Rigidbody rb;

    [Header("Camera")]
    public Camera playerCamera;
    public float mouseSensitivity = 0.2f;
    private float xRot;
    private float yRot;

    [Header("Object Grab")]
    public float grabDistance = 3.0f;
    public LayerMask grabbableLayer;
    public Transform holdPosition;
    public float grabForce = 15.0f;

    [Header("Meta UI Integration")]
    [Tooltip("Ray Interactor here")]
    public RayInteractor desktopUIRay;

    private Rigidbody grabbedRb;
    private float originalLinearDamping;
    private float originalAngularDamping;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Vector3 currentRot = playerCamera.transform.rotation.eulerAngles;
        xRot = currentRot.x;
        yRot = transform.rotation.eulerAngles.y;
    }

    private void Update()
    {
        HandleLook();
        HandleInteractionInput();
    }

    private void FixedUpdate()
    {
        HandleMovement();
        HandleGrabbedObject();
    }

    private void HandleLook()
    {
        if (Mouse.current == null) { return; }

        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        xRot -= mouseDelta.y * mouseSensitivity;
        xRot = Mathf.Clamp(xRot, -85.0f, 85.0f);

        yRot += mouseDelta.x * mouseSensitivity;

        playerCamera.transform.localRotation = Quaternion.Euler(xRot, 0.0f, 0.0f);
        transform.rotation = Quaternion.Euler(0.0f, yRot, 0.0f);
    }

    private void HandleMovement()
    {
        if (Keyboard.current == null) { return; }

        float x = 0.0f;
        float z = 0.0f;

        if (Keyboard.current.wKey.isPressed) { z += 1.0f; }
        if (Keyboard.current.sKey.isPressed) { z -= 1.0f; }
        if (Keyboard.current.aKey.isPressed) { x -= 1.0f; }
        if (Keyboard.current.dKey.isPressed) { x += 1.0f; }

        Vector3 moveDir = (transform.forward * z + transform.right * x).normalized;
        Vector3 targetVelocity = moveDir * moveSpeed;

        targetVelocity.y = rb.linearVelocity.y;
        rb.linearVelocity = targetVelocity;
    }

    private void HandleInteractionInput()
    {
        if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
        {
            if (grabbedRb != null)
            {
                Drop();
            }
            else
            {
                bool isHoveringUI = desktopUIRay != null && desktopUIRay.State == InteractorState.Hover;
                if (!isHoveringUI) { TryGrab(); }
            }
        }
    }

    private void TryGrab()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, grabDistance, grabbableLayer))
        {
            if (hit.rigidbody != null)
            {
                grabbedRb = hit.rigidbody;
                grabbedRb.useGravity = false;

                originalLinearDamping = grabbedRb.linearDamping;
                originalAngularDamping = grabbedRb.angularDamping;

                grabbedRb.linearDamping = 10.0f;
                grabbedRb.angularDamping = 10.0f;
            }
        }
    }

    private void Drop()
    {
        if (grabbedRb != null)
        {
            grabbedRb.useGravity = true;
            grabbedRb.linearDamping = originalLinearDamping;
            grabbedRb.angularDamping = originalAngularDamping;
            grabbedRb = null;
        }
    }

    private void HandleGrabbedObject()
    {
        if (grabbedRb != null)
        {
            Vector3 directionToPoint = holdPosition.position - grabbedRb.position;
            grabbedRb.linearVelocity = directionToPoint * grabForce;
        }
    }
}