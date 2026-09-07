using UnityEngine;
using Oculus.Interaction.Locomotion;

/// <summary>
/// Moves grabbable VR objects in a physics-based way by tracking a kinematic proxy target to allow realistic collisions and weight simulation
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PhysicsProxyFollower : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform proxyTarget;
    [SerializeField] private GrabCollisionHandler grabCollisionHandler;

    [Header("Locomotion Sync")]
    [SerializeField] private FirstPersonLocomotor playerLocomotor;

    [Header("Physics Settings")]
    [SerializeField] private float positionMultiplier = 20.0f;
    [SerializeField] private float rotationMultiplier = 20.0f;
    [SerializeField] private float maxVelocity = 15.0f;
    [SerializeField] private float maxAngularVelocity = 30.0f;
    [SerializeField] private float maxDistanceBeforeTeleport = 1.0f;

    private Rigidbody rb;
    private float currentTeleportCooldown;
    private const float TeleportCooldown = 0.05f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        TeleportObject();
    }

    private void FixedUpdate()
    {
        if (proxyTarget == null || grabCollisionHandler == null) { return; }

        if (currentTeleportCooldown > 0.0f)
        {
            currentTeleportCooldown -= Time.fixedDeltaTime;

            // Keeps the object locked to the target transform during teleport cooldown
            TeleportObject();
            return;
        }

        if (grabCollisionHandler.IsGrabbed)
        {
            PhysicsMoveToTarget();
            PhysicsRotateToTarget();
        }
        else
        {
            rb.useGravity = true;
            proxyTarget.position = transform.position;
            proxyTarget.rotation = transform.rotation;
        }
    }

    private void OnEnable()
    {
        if (playerLocomotor != null)
        {
            playerLocomotor.WhenLocomotionEventHandled += HandleLocomotionEvent;
        }
    }

    private void OnDisable()
    {
        if (playerLocomotor != null)
        {
            playerLocomotor.WhenLocomotionEventHandled -= HandleLocomotionEvent;
        }
    }

    private void PhysicsMoveToTarget()
    {
        rb.useGravity = false;

        Vector3 positionDifference = proxyTarget.position - transform.position;

        if (positionDifference.magnitude > maxDistanceBeforeTeleport)
        {
            TeleportObject();
            return;
        }

        Vector3 targetVelocity = positionDifference * positionMultiplier;
        rb.linearVelocity = Vector3.ClampMagnitude(targetVelocity, maxVelocity);
    }

    private void PhysicsRotateToTarget()
    {
        Quaternion rotationDifference = proxyTarget.rotation * Quaternion.Inverse(transform.rotation);
        rotationDifference.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

        if (angleInDegrees > 180.0f) { angleInDegrees -= 360.0f; }

        if (Mathf.Abs(angleInDegrees) > 0.1f && !float.IsInfinity(rotationAxis.x))
        {
            Vector3 targetAngularVelocity = angleInDegrees * rotationAxis * Mathf.Deg2Rad * rotationMultiplier;
            rb.angularVelocity = Vector3.ClampMagnitude(targetAngularVelocity, maxAngularVelocity);
        }
    }

    private void HandleLocomotionEvent(LocomotionEvent locomotionEvent, Pose delta)
    {
        bool isTranslationJump = locomotionEvent.Translation == LocomotionEvent.TranslationType.Absolute ||
                                 locomotionEvent.Translation == LocomotionEvent.TranslationType.Relative;

        bool isRotationJump = locomotionEvent.Rotation == LocomotionEvent.RotationType.Absolute ||
                              locomotionEvent.Rotation == LocomotionEvent.RotationType.Relative;

        if (isTranslationJump || isRotationJump)
        {
            TeleportObject();
            currentTeleportCooldown = TeleportCooldown;
        }
    }

    private void TeleportObject()
    {
        transform.position = proxyTarget.position;
        transform.rotation = proxyTarget.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    public bool IsGrabbed()
    {
        return grabCollisionHandler.IsGrabbed;
    }
}