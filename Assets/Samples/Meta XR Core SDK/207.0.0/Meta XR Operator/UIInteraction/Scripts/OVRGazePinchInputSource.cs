/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Drives Unity UI with eye gaze as the pointer and a hand pinch as the click.
/// </summary>
/// <remarks>
/// While either hand is tracked this replaces the two hand-ray pointers that <see cref="OVRHand"/>
/// registers, so exactly one pointer is ever hovering the UI. When a Touch controller is in hand this
/// source yields (see <see cref="IsActive"/>) so <see cref="OVRInputModule"/> drives its own
/// controller ray, leaving controller interaction unchanged.
///
/// Eye gaze only aims the pointer up until a pinch begins. Once pinched, the pointer is latched and
/// dragged by the pinching hand's movement while gaze is ignored, mirroring the Interaction SDK's
/// "gaze to target, pinch to grab, drag with the hand" model. This keeps a drag (e.g. a slider)
/// alive even if the user looks away mid-drag.
/// </remarks>
[HelpURL("https://developer.oculus.com/documentation/unity/unity-isdk-input-processing/")]
public class OVRGazePinchInputSource : MonoBehaviour, OVRInputModule.InputSource
{
    [SerializeField]
    [Tooltip("Left hand, used for pinch input and to decide whether gaze pointing is active.")]
    private OVRHand _leftHand;

    [SerializeField]
    [Tooltip("Right hand, used for pinch input and to decide whether gaze pointing is active.")]
    private OVRHand _rightHand;

    [SerializeField]
    [Tooltip("Space the eye gaze pose is expressed in. Usually OVRCameraRig's TrackingSpace.")]
    private Transform _trackingSpace;

    [SerializeField]
    [Tooltip("Pointer ray used when no eye gaze pose is available. Usually CenterEyeAnchor.")]
    private Transform _centerEyeAnchor;

    [SerializeField]
    [Tooltip("Optional reticle drawn where the gaze ray meets the UI. Leave its Renderer unset " +
             "so only the cursor is drawn, not a ray from the eye.")]
    private OVRRayHelper _rayHelper;

    [SerializeField]
    [Tooltip("Degrees the pinched pointer ray rotates per meter of hand movement while dragging. " +
             "Once a pinch starts the pointer stops following the eyes and is aimed by the hand.")]
    private float _dragHandDegreesPerMeter = 300f;

    private GameObject _gazePoseGO;
    private OVRPlugin.EyeGazeInteractionState _gazeState;
    private bool _isPinching;
    private bool _wasReleased;
    private OVRPlugin.Hand _pinchingHand = OVRPlugin.Hand.HandRight;

    // While a pinch is held the pointer ray is aimed by the hand from a pose anchored at pinch start.
    private bool _dragAnchored;
    private Vector3 _anchorRayPos;
    private Quaternion _anchorRayRot;
    private Vector3 _anchorHandPos;

    // Tracks the controller<->hand handoff edge so a stale UI highlight can be cleared on the switch.
    private bool _lastControllerActive;

    private void OnEnable()
    {
        ResetPinchState();
        OVRInputModule.TrackInputSource(this);
    }

    private void OnDisable()
    {
        OVRInputModule.UntrackInputSource(this);
        RestoreHandRays();
        ResetPinchState();
    }

    // Clear latched pinch/drag state so a pinch held across an enable/disable toggle (driven by
    // HandRayFallbackController) isn't reported as a stale press before Update runs again.
    private void ResetPinchState()
    {
        _isPinching = false;
        _wasReleased = false;
        _dragAnchored = false;
    }

    private void OnDestroy()
    {
        if (_gazePoseGO != null)
        {
            Destroy(_gazePoseGO);
        }
    }

    private void Update()
    {
        UpdateGazePose();
        UpdatePinchState();

        // OVRInputModule never sends a pointer-exit when an input source stops driving, so the ray's
        // last-hovered button stays highlighted after a controller<->hand handoff. Clear it on the
        // switch, mirroring how ISDK's PointableCanvasModule dispatches a pointer-exit on handoff.
        bool controllerActive = IsControllerActive();
        if (controllerActive != _lastControllerActive)
        {
            _lastControllerActive = controllerActive;
            ClearStaleHovers();
        }
    }

    private void LateUpdate()
    {
        // OVRHand re-registers itself in OnEnable and re-shows its ray in FixedUpdate, so the
        // suppression has to be reapplied rather than done once at startup. LateUpdate runs after
        // every OVRHand.Update, so the ray never renders.
        if (IsActive())
        {
            SuppressHandRays();
        }
        else
        {
            // OVRInputModule never hides a source's reticle on handoff, so a controller taking over
            // would otherwise leave a stale gaze reticle frozen where the user last looked.
            HideReticle();
        }
    }

    private void EnsureGazePose()
    {
        if (_gazePoseGO != null)
        {
            return;
        }

        // OVRCameraRig populates its anchors in Awake, so this fallback is resolved on first use
        // rather than in our own Awake, where the ordering between the two is undefined.
        if ((_trackingSpace == null || _centerEyeAnchor == null) &&
            TryGetComponent(out OVRCameraRig rig))
        {
            _trackingSpace = _trackingSpace != null ? _trackingSpace : rig.trackingSpace;
            _centerEyeAnchor = _centerEyeAnchor != null ? _centerEyeAnchor : rig.centerEyeAnchor;
        }

        _gazePoseGO = new GameObject($"{nameof(OVRGazePinchInputSource)} Pose")
        {
            hideFlags = HideFlags.DontSave
        };

        if (_trackingSpace != null)
        {
            _gazePoseGO.transform.SetParent(_trackingSpace, false);
        }
    }

    private void UpdateGazePose()
    {
        EnsureGazePose();
        Transform pose = _gazePoseGO.transform;

        // While a pinch is held the pointer is aimed by the hand, not the eyes: rotate the ray
        // anchored at pinch start by how far the pinching hand has moved. Rotating (rather than only
        // translating) both keeps the ray hitting the canvas and produces the angular change
        // OVRInputModule needs to sustain a drag, so a drag survives the gaze leaving the target.
        if (_isPinching && _dragAnchored)
        {
            Transform hand = GetPinchingHandTransform();
            if (hand != null)
            {
                Vector3 handDelta = hand.position - _anchorHandPos;
                float yaw = handDelta.x * _dragHandDegreesPerMeter;
                float pitch = -handDelta.y * _dragHandDegreesPerMeter;
                pose.SetPositionAndRotation(
                    _anchorRayPos,
                    _anchorRayRot * Quaternion.Euler(pitch, yaw, 0f));
                return;
            }
        }

        // eyeGazeInteractionsEnabled is checked first because GetEyeGazeInteractionState logs a
        // warning on every failure, which would be one per frame on hardware without eye tracking.
        if (OVRPlugin.eyeGazeInteractionsEnabled &&
            OVRPlugin.GetEyeGazeInteractionState(OVRPlugin.Step.Render, -1, ref _gazeState) &&
            _gazeState.IsValid &&
            _trackingSpace != null)
        {
            pose.localPosition = _gazeState.Pose.Position.FromFlippedZVector3f();
            pose.localRotation = _gazeState.Pose.Orientation.FromFlippedZQuatf();
        }
        else if (_centerEyeAnchor != null)
        {
            pose.SetPositionAndRotation(_centerEyeAnchor.position, _centerEyeAnchor.rotation);
        }
    }

    private void UpdatePinchState()
    {
        bool rightPinching = IsPinching(_rightHand);
        bool leftPinching = IsPinching(_leftHand);
        bool pinching = rightPinching || leftPinching;

        bool pinchStarted = pinching && !_isPinching;
        _wasReleased = !pinching && _isPinching;

        if (rightPinching)
        {
            _pinchingHand = OVRPlugin.Hand.HandRight;
        }
        else if (leftPinching)
        {
            _pinchingHand = OVRPlugin.Hand.HandLeft;
        }

        if (pinchStarted)
        {
            AnchorDrag();
        }
        else if (!pinching)
        {
            _dragAnchored = false;
        }

        _isPinching = pinching;
    }

    // Capture the gaze pointer pose and hand position at the moment a pinch begins so the pointer can
    // be aimed from that anchor by the hand for the rest of the pinch (gaze is ignored until release).
    private void AnchorDrag()
    {
        Transform hand = GetPinchingHandTransform();
        if (hand == null)
        {
            _dragAnchored = false;
            return;
        }

        Transform pose = _gazePoseGO.transform;
        _anchorRayPos = pose.position;
        _anchorRayRot = pose.rotation;
        _anchorHandPos = hand.position;
        _dragAnchored = true;
    }

    private Transform GetPinchingHandTransform()
    {
        OVRHand hand = _pinchingHand == OVRPlugin.Hand.HandLeft ? _leftHand : _rightHand;
        return hand != null ? hand.transform : null;
    }

    private void SuppressHandRays()
    {
        SuppressHandRay(_leftHand);
        SuppressHandRay(_rightHand);
    }

    private static void SuppressHandRay(OVRHand hand)
    {
        if (hand == null)
        {
            return;
        }

        OVRInputModule.UntrackInputSource(hand);

        if (hand.RayHelper != null && hand.RayHelper.gameObject.activeSelf)
        {
            hand.RayHelper.gameObject.SetActive(false);
        }
    }

    private void RestoreHandRays()
    {
        RestoreHandRay(_leftHand);
        RestoreHandRay(_rightHand);
    }

    private static void RestoreHandRay(OVRHand hand)
    {
        if (hand != null && hand.isActiveAndEnabled)
        {
            // The ray object itself is re-shown by OVRHand.FixedUpdate once it is tracked again.
            OVRInputModule.TrackInputSource(hand);
        }
    }

    private void HideReticle()
    {
        if (_rayHelper != null && _rayHelper.gameObject.activeSelf)
        {
            _rayHelper.gameObject.SetActive(false);
        }
    }

    // Return any UI left highlighted/selected by the ray that just stopped driving back to Normal, so
    // a controller<->hand switch doesn't leave a button stuck highlighted.
    private static void ClearStaleHovers()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            return;
        }

        var pointer = new PointerEventData(eventSystem);
#pragma warning disable 618
        foreach (UnityEngine.UI.Selectable selectable in FindObjectsOfType<UnityEngine.UI.Selectable>())
#pragma warning restore 618
        {
            if (selectable != null)
            {
                selectable.OnPointerExit(pointer);
            }
        }

        eventSystem.SetSelectedGameObject(null);
    }

    private static bool IsPinching(OVRHand hand)
    {
        return hand != null && hand.GetFingerIsPinching(OVRHand.HandFinger.Index);
    }

    private static float PinchStrength(OVRHand hand)
    {
        return hand == null ? 0f : hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
    }

    // Gaze+pinch must yield to a Touch controller whenever one is actually in the user's hand.
    // GetControllerIsInHandState is the capsense "held" signal exposed by multimodal / controller-
    // driven-hand-poses: it flips the moment a controller is picked up or set down, is stable for an
    // idle powered-on controller (unlike IsControllerConnected, which flickers), and covers the
    // single-controller case (GetActiveController returns a combined "LTouch, RHand" value there that
    // an equality check would miss).
    private static bool IsControllerActive()
    {
        return OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandLeft) ==
                   OVRInput.ControllerInHandState.ControllerInHand ||
               OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandRight) ==
                   OVRInput.ControllerInHandState.ControllerInHand;
    }

    /// <summary>
    /// True while either hand's index finger is pinching.
    /// </summary>
    public bool IsPressed()
    {
        return _isPinching;
    }

    /// <summary>
    /// True on the frame a pinch ends.
    /// </summary>
    public bool IsReleased()
    {
        return _wasReleased;
    }

    /// <summary>
    /// Returns the transform tracking the pointer pose, which points along its Z axis.
    /// </summary>
    public Transform GetPointerRayTransform()
    {
        EnsureGazePose();
        return _gazePoseGO.transform;
    }

    /// <summary>
    /// True when this object is not null.
    /// </summary>
    public bool IsValid()
    {
        return this != null;
    }

    /// <summary>
    /// True when at least one hand is tracked and no Touch controller is in hand, which is when gaze
    /// pointing should drive the UI. A held controller takes precedence, so this yields and
    /// <see cref="OVRInputModule"/> drives the controller ray instead.
    /// </summary>
    public bool IsActive()
    {
        if (IsControllerActive())
        {
            return false;
        }

        return (_leftHand != null && _leftHand.IsActive()) ||
               (_rightHand != null && _rightHand.IsActive());
    }

    /// <summary>
    /// Returns the hand that is pinching, defaulting to the right hand.
    /// </summary>
    public OVRPlugin.Hand GetHand()
    {
        return _pinchingHand;
    }

    /// <summary>
    /// Updates the reticle marking where the pointer ray meets the UI.
    /// </summary>
    /// <param name="rayData">Where the pointer ray intersects the UI this frame.</param>
    public void UpdatePointerRay(OVRInputRayData rayData)
    {
        if (_rayHelper == null)
        {
            return;
        }

        // Re-show the reticle if it was hidden while this source was inactive (see HideReticle).
        if (!_rayHelper.gameObject.activeSelf)
        {
            _rayHelper.gameObject.SetActive(true);
        }

        rayData.ActivationStrength = Mathf.Max(PinchStrength(_leftHand), PinchStrength(_rightHand));
        _rayHelper.UpdatePointerRay(rayData);
    }
}
