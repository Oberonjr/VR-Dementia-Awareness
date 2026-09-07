using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

/// <summary>
/// Keeps an object at a fixed distance, horizontal level, and lets it smoothly follow the camera's yaw when a threshold is exceeded
/// </summary>
public class HeadFollow : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform vrCamera;
    [SerializeField] private Transform fpsCamera;

    [Header("Positioning Settings")]
    [SerializeField] private float distance = 2.0f;
    [SerializeField] private float heightOffset = -0.2f;

    [Header("Movement Settings")]
    [SerializeField] private float yawThreshold = 25.0f;
    [SerializeField] private float followSpeed = 5.0f;

    private float targetYaw;
    private Transform targetCamera;

    private IEnumerator Start()
    {
        // Wait a brief moment for the VR system to register the headset
        yield return new WaitForSeconds(0.2f);

        DetermineActiveTarget();

        if (targetCamera != null)
        {
            targetYaw = targetCamera.eulerAngles.y;

            Vector3 targetDirection = Quaternion.Euler(0, targetYaw, 0) * Vector3.forward;
            Vector3 targetPosition = targetCamera.position + new Vector3(0, heightOffset, 0) + (targetDirection * distance);
            Quaternion targetRotation = Quaternion.Euler(gameObject.transform.rotation.eulerAngles.x, targetYaw, gameObject.transform.rotation.eulerAngles.z);

            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        else
        {
            Debug.LogError("VRHUDController: No VR Camera found! Please assign it in the inspector.");
        }
    }

    private void LateUpdate()
    {
        if (targetCamera == null) { return; }

        float cameraYaw = targetCamera.eulerAngles.y;
        float yawDifference = Mathf.DeltaAngle(targetYaw, cameraYaw);

        // Drag the target yaw along if the threshold is exceeded
        if (Mathf.Abs(yawDifference) > yawThreshold)
        {
            targetYaw = cameraYaw - (Mathf.Sign(yawDifference) * yawThreshold);
        }

        Vector3 targetDirection = Quaternion.Euler(0, targetYaw, 0) * Vector3.forward;
        Vector3 targetPosition = targetCamera.position + new Vector3(0, heightOffset, 0) + (targetDirection * distance);
        Quaternion targetRotation = Quaternion.Euler(gameObject.transform.rotation.eulerAngles.x, targetYaw, gameObject.transform.rotation.eulerAngles.z);

        // Smoothly interpolate position and rotation towards target
        transform.position = Vector3.Lerp(transform.position, targetPosition, followSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, followSpeed * Time.deltaTime);
    }

    public void DetermineActiveTarget()
    {
        List<InputDevice> hmdDevices = new List<InputDevice>();
        InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.HeadMounted, hmdDevices);

        bool isVRPresent = hmdDevices.Count > 0 && hmdDevices[0].isValid;

        if (isVRPresent && vrCamera != null) { targetCamera = vrCamera; }
        else if (!isVRPresent && fpsCamera != null) { targetCamera = fpsCamera; }
        else if (Camera.main != null) { targetCamera = Camera.main.transform; }
    }
}