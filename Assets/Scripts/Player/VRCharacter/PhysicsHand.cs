using UnityEngine;
using Oculus.Interaction.Locomotion;
using System.Collections;

/// <summary>
/// Moves the physics hands in a physics-based way, predicting future positions based on locomotor velocity to sync Update and FixedUpdate
/// This solution might look slightly jittery still, but based on testing, not many people noticed it
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class VRPhysicsHand : MonoBehaviour
{
    [Header("Setup")]
    [SerializeField] private Transform targetTransform;
    [SerializeField] private FirstPersonLocomotor playerLocomotor;
    [SerializeField] private GameObject ghostHand;

    [Header("Show Ghost Hand")]
    [SerializeField] private float distanceToShowGhost = 0.1f;

    [Header("Physics Settings")]
    [SerializeField] private float maxDistanceBeforeTeleport = 1.0f;
    [SerializeField] private float positionMultiplier = 20.0f;
    [SerializeField] private float rotationMultiplier = 20.0f;
    [SerializeField] private float maxVelocity = 15.0f;
    [SerializeField] private float maxAngularVelocity = 30.0f;

    [Header("Locomotion Sync Tuning")]
    [Range(0.0f, 1.0f)]
    [Tooltip("Controls how far ahead the target position is predicted. Lower to reduce forward overshoot.")]
    [SerializeField] private float predictionDamping = 0.5f;

    [Range(0.0f, 1.0f)]
    [Tooltip("Controls how much raw player velocity is injected into the hand. Lower to reduce forward pushing.")]
    [SerializeField] private float feedForwardDamping = 0.5f;

    private Rigidbody rb;
    private float currentTeleportCooldown;
    private const float TeleportCooldown = 0.05f;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.maxAngularVelocity = 150.0f;
        rb.maxDepenetrationVelocity = 3.0f;
        rb.solverIterations = 25;
        rb.solverVelocityIterations = 15;

        if (ghostHand != null) { ghostHand.SetActive(false); }

        DelayedTeleport();
    }

    private void FixedUpdate()
    {
        if (targetTransform == null) { return; }

        if (currentTeleportCooldown > 0.0f)
        {
            currentTeleportCooldown -= Time.fixedDeltaTime;

            // Forces the physics hand to stick directly to the controller during cooldown
            TeleportHand();
            CheckGhostHandVisibility();
            return;
        }

        PhysicsMoveToTarget();
        PhysicsRotateToTarget();
        CheckGhostHandVisibility();
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

    private void HandleLocomotionEvent(LocomotionEvent locomotionEvent, Pose delta)
    {
        bool isTranslationJump = locomotionEvent.Translation == LocomotionEvent.TranslationType.Absolute ||
                                 locomotionEvent.Translation == LocomotionEvent.TranslationType.Relative;

        bool isRotationJump = locomotionEvent.Rotation == LocomotionEvent.RotationType.Absolute ||
                              locomotionEvent.Rotation == LocomotionEvent.RotationType.Relative;

        if (isTranslationJump || isRotationJump)
        {
            TeleportHand();
            currentTeleportCooldown = TeleportCooldown;
        }
    }

    private void PhysicsMoveToTarget()
    {
        Vector3 predictedTargetPosition = targetTransform.position;
        Vector3 locomotorVelocity = Vector3.zero;

        if (playerLocomotor != null && !playerLocomotor.IgnoringVelocity)
        {
            locomotorVelocity = playerLocomotor.Velocity;

            // Predict future position, damping it slightly to fit the actual hand position
            predictedTargetPosition += locomotorVelocity * Time.fixedDeltaTime * predictionDamping;
        }

        Vector3 positionDifference = predictedTargetPosition - transform.position;

        if (positionDifference.magnitude > maxDistanceBeforeTeleport)
        {
            TeleportHand();
            return;
        }

        Vector3 targetVelocity = positionDifference * positionMultiplier;

        // Calculate velocity to push the hand forward, based on the movement velocity
        targetVelocity += locomotorVelocity * feedForwardDamping;

        rb.linearVelocity = Vector3.ClampMagnitude(targetVelocity, maxVelocity);
    }

    private void PhysicsRotateToTarget()
    {
        Quaternion rotationDifference = targetTransform.rotation * Quaternion.Inverse(transform.rotation);
        rotationDifference.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

        if (angleInDegrees > 180.0f) { angleInDegrees -= 360.0f; }

        if (Mathf.Abs(angleInDegrees) > 0.1f && !float.IsInfinity(rotationAxis.x))
        {
            Vector3 targetAngularVelocity = angleInDegrees * rotationAxis * Mathf.Deg2Rad * rotationMultiplier;
            rb.angularVelocity = Vector3.ClampMagnitude(targetAngularVelocity, maxAngularVelocity);
        }
    }

    private void CheckGhostHandVisibility()
    {
        if (ghostHand == null) { return; }

        float distance = Vector3.Distance(transform.position, targetTransform.position);
        ghostHand.SetActive(distance > distanceToShowGhost);
    }

    private void TeleportHand()
    {
        transform.position = targetTransform.position;
        transform.rotation = targetTransform.rotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }

    private IEnumerator DelayedTeleport()
    {
        yield return 1.0f;
        TeleportHand();
    }
}