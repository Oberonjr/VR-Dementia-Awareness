using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Manages procedural eye tracking and realistic blinking behaviors for characters
/// </summary>
public class EyeGazeController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SkinnedMeshRenderer targetMesh;
    [SerializeField] private Transform vrCamera;
    [SerializeField] private Transform fpsCamera;

    [Header("Eye Transforms")]
    [SerializeField] private Transform leftEye;
    [SerializeField] private Transform rightEye;

    [Header("Gaze Settings")]
    [SerializeField] private float smoothSpeed = 20.0f;
    [SerializeField] private Vector2 verticalLimits = new Vector2(-100.0f, -70.0f);
    [SerializeField] private Vector2 horizontalLimits = new Vector2(-30.0f, 30.0f);

    [Header("Blink Settings")]
    [SerializeField] private int blinkIndex = 1;
    [SerializeField] private float minBlinkInterval = 3.0f;
    [SerializeField] private float maxBlinkInterval = 8.0f;
    [SerializeField] private float blinkCloseDurationMs = 50.0f;
    [SerializeField] private float blinkClosedDurationMs = 70.0f;
    [SerializeField] private float blinkOpenDurationMs = 60.0f;

    private Transform targetCamera;
    private Vector3 lookOffset;
    private bool isLookingAtPlayer = true;

    private enum BlinkState { Idle, Closing, Closed, Opening }
    private BlinkState blinkState = BlinkState.Idle;
    private float blinkTimer;
    private float blinkHoldTimer;
    private float currentBlinkWeight = 0.0f;

    private IEnumerator Start()
    {
        yield return new WaitForSeconds(0.2f);

        DetermineActiveCamera();
        ResetBlinkTimer();
        StartCoroutine(GazeBehaviorRoutine());
    }

    private void LateUpdate()
    {
        if (targetMesh != null) { HandleBlinking(); }
        if (targetCamera == null || leftEye == null || rightEye == null) { return; }

        // Central point between the eyes to guarantee parallel gaze (to prevent cross-eyed looks)
        Vector3 headCenter = (leftEye.position + rightEye.position) / 2.0f;
        Vector3 currentTargetPos;

        if (isLookingAtPlayer)
        {
            // Scale the random offset by distance to prevent the eyes from darting sideways when user stands close
            float distToCam = Vector3.Distance(headCenter, targetCamera.position);
            Vector3 scaledOffset = lookOffset * distToCam;
            currentTargetPos = targetCamera.position + scaledOffset;
        }
        else
        {
            currentTargetPos = headCenter + leftEye.parent.forward * 3.0f + leftEye.parent.up * -1.5f;
        }

        UpdateEye(leftEye, currentTargetPos, headCenter);
        UpdateEye(rightEye, currentTargetPos, headCenter);
    }

    private void DetermineActiveCamera()
    {
        List<InputDevice> hmdDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, hmdDevices);

        bool isVRPresent = hmdDevices.Count > 0 && hmdDevices[0].isValid;

        if (isVRPresent && vrCamera != null) { targetCamera = vrCamera; }
        else if (!isVRPresent && fpsCamera != null) { targetCamera = fpsCamera; }
        else if (Camera.main != null) { targetCamera = Camera.main.transform; }

        if (targetCamera == null) { Debug.LogError("EyeGazeController: No valid camera found!"); }
    }

    private void UpdateEye(Transform eye, Vector3 targetPos, Vector3 headCenter)
    {
        // Use headCenter instead of eye position so both eyes point exactly the same way
        Vector3 dirToTarget = targetPos - headCenter;

        Quaternion lookRot = Quaternion.LookRotation(dirToTarget);
        Quaternion localLookRot = Quaternion.Inverse(eye.parent.rotation) * lookRot;

        Vector3 cleanEuler = localLookRot.eulerAngles;
        float cleanPitch = NormalizeAngle(cleanEuler.x);
        float cleanYaw = NormalizeAngle(cleanEuler.y);

        float angleX = Mathf.Clamp(cleanPitch - 90.0f, verticalLimits.x, verticalLimits.y);
        float angleY = Mathf.Clamp(cleanYaw, horizontalLimits.x, horizontalLimits.y);

        eye.localRotation = Quaternion.Slerp(eye.localRotation, Quaternion.Euler(angleX, angleY, 0.0f), Time.deltaTime * smoothSpeed);
    }

    private void HandleBlinking()
    {
        switch (blinkState)
        {
            case BlinkState.Idle:
                blinkTimer -= Time.deltaTime;
                if (blinkTimer <= 0.0f)
                {
                    blinkState = BlinkState.Closing;
                    if (Random.value < 0.7f) { GenerateNewLookOffset(); }
                }
                break;

            case BlinkState.Closing:
                currentBlinkWeight = Mathf.MoveTowards(currentBlinkWeight, 100.0f, (100.0f / (blinkCloseDurationMs / 1000.0f)) * Time.deltaTime);
                if (currentBlinkWeight >= 100.0f)
                {
                    blinkState = BlinkState.Closed;
                    blinkHoldTimer = blinkClosedDurationMs / 1000.0f;
                }
                targetMesh.SetBlendShapeWeight(blinkIndex, currentBlinkWeight);
                break;

            case BlinkState.Closed:
                blinkHoldTimer -= Time.deltaTime;
                if (blinkHoldTimer <= 0.0f) { blinkState = BlinkState.Opening; }
                break;

            case BlinkState.Opening:
                currentBlinkWeight = Mathf.MoveTowards(currentBlinkWeight, 0.0f, (100.0f / (blinkOpenDurationMs / 1000.0f)) * Time.deltaTime);
                if (currentBlinkWeight <= 0.0f)
                {
                    blinkState = BlinkState.Idle;
                    ResetBlinkTimer();
                }
                targetMesh.SetBlendShapeWeight(blinkIndex, currentBlinkWeight);
                break;
        }
    }

    private void GenerateNewLookOffset()
    {
        lookOffset = new Vector3(Random.Range(-0.04f, 0.04f), Random.Range(-0.04f, 0.04f), Random.Range(-0.04f, 0.04f));
    }

    private void ResetBlinkTimer()
    {
        blinkTimer = Random.Range(minBlinkInterval, maxBlinkInterval);
    }

    private float NormalizeAngle(float angle)
    {
        while (angle > 180.0f) { angle -= 360.0f; }
        while (angle < -180.0f) { angle += 360.0f; }
        return angle;
    }

    private IEnumerator GazeBehaviorRoutine()
    {
        while (true)
        {
            isLookingAtPlayer = true;
            yield return new WaitForSeconds(Random.Range(3.0f, 8.0f));

            isLookingAtPlayer = false;
            yield return new WaitForSeconds(Random.Range(2.0f, 4.0f));
        }
    }
}