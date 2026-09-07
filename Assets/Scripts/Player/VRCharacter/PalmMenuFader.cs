using UnityEngine;
using Oculus.Interaction;
using Oculus.Interaction.HandGrab;
using System;

/// <summary>
/// Fades a handheld palm menu in or out by mapping the angle between the player's head and palm transform
/// </summary>
public class PalmMenuFader : MonoBehaviour
{
    [Header("Tracking References")]
    [SerializeField] private Transform headTransform;
    [SerializeField] private Transform palmTransform;

    [Header("UI References")]
    [SerializeField] private CanvasGroup menuCanvasGroup;

    [Header("Interaction References")]
    [SerializeField] private HandGrabInteractor leftHandGrabInteractor;
    [SerializeField] private HandGrabInteractor leftControllerGrabInteractor;

    [Header("Fade Settings")]
    [SerializeField] private float fullyVisibleAngle = 20.0f;
    [SerializeField] private float startFadingAngle = 45.0f;
    [SerializeField] private float fadeSpeed = 12.0f;

    private bool isActive;
    private bool angledAfterActivation;

    private void Start()
    {
        EventBus<OnChangePalmMenuActive>.OnEvent += SetActiveness;
    }

    private void Update()
    {
        if (headTransform == null || palmTransform == null || menuCanvasGroup == null) { return; }

        float targetAlpha = 0.0f;

        if (!IsHoldingObject() && isActive)
        {
            Vector3 directionToFace = (headTransform.position - palmTransform.position).normalized;

            // Depending on the Palm Transform orientation, this axis might need adjustment
            float angleToFace = Vector3.Angle(-palmTransform.up, directionToFace);

            if (angleToFace <= startFadingAngle && !angledAfterActivation)
            {
                targetAlpha = 1.0f - Mathf.Clamp01((angleToFace - fullyVisibleAngle) / (startFadingAngle - fullyVisibleAngle));
            }
            else if (angleToFace > startFadingAngle && angledAfterActivation) { angledAfterActivation = false; }
        }

        menuCanvasGroup.alpha = Mathf.Lerp(menuCanvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

        bool isVisible = menuCanvasGroup.alpha > 0.05f;

        if (menuCanvasGroup.gameObject.activeSelf != isVisible)
        {
            menuCanvasGroup.gameObject.SetActive(isVisible);
        }

        if (isVisible)
        {
            menuCanvasGroup.interactable = true;
            menuCanvasGroup.blocksRaycasts = true;
            EventBus<OnPalmMenuVisibilityChanged>.Publish(new OnPalmMenuVisibilityChanged(true));
        }
        else
        {
            menuCanvasGroup.interactable = false;
            menuCanvasGroup.blocksRaycasts = false;
            EventBus<OnPalmMenuVisibilityChanged>.Publish(new OnPalmMenuVisibilityChanged(false));
        }
    }

    private void OnDestroy()
    {
        EventBus<OnChangePalmMenuActive>.OnEvent -= SetActiveness;
    }

    private bool IsHoldingObject()
    {
        if (leftHandGrabInteractor != null && leftHandGrabInteractor.State == InteractorState.Select) { return true; }
        if (leftControllerGrabInteractor != null && leftControllerGrabInteractor.State == InteractorState.Select) { return true; }

        return false;
    }

    private void SetActiveness(OnChangePalmMenuActive newActive)
    {
        Vector3 directionToFace = (headTransform.position - palmTransform.position).normalized;
        float angleToFace = Vector3.Angle(-palmTransform.up, directionToFace);

        if (angleToFace <= startFadingAngle) { angledAfterActivation = true; }
        else { angledAfterActivation = false; }

        isActive = newActive.isActive;
    }
}