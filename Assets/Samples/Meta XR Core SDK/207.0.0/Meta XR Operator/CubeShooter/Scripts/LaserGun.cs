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

public class LaserGun : MonoBehaviour
{
    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Transform muzzlePoint;
    public float fireRate = 0.15f;

    [Header("Grab")]
    public float grabRadius = 0.15f;

    [Header("Grip pose")]
    [SerializeField]
    [Tooltip("Grip point in the gun's local space for the CONTROLLER hold. Placed at the controller anchor so the gun is held by the handle, not the body/magazine.")]
    private Vector3 gripLocalPosition = new Vector3(0.11f, 0.015f, -0.05f);

    [SerializeField]
    [Tooltip("Grip point in the gun's local space for HAND holds; its lateral (X) value is the handle centerline. Placed at the wrist anchor so the gun is held by the handle.")]
    private Vector3 gripLocalPositionHand = new Vector3(0.10f, -0.02f, -0.15f);

    // The wrist-to-palm lateral offset is mirrored between hands, so shift the grip along gun-local X
    // per hand (+right, -left) to keep the palm centered on the handle for both.
    [SerializeField]
    [Tooltip("Lateral (gun-local X) shift applied to the hand grip point, added for the right hand and subtracted for the left, to center the palm on the handle for both hands.")]
    private float gripHandLateralOffset = 0.015f;

    [SerializeField]
    [Tooltip("Gun rotation relative to the CONTROLLER anchor when grabbed (Euler degrees). 0 keeps the gun's axes aligned to the controller.")]
    private Vector3 gripRotationController = Vector3.zero;

    // Left and right wrist frames are mirrored, so each hand needs its own grip rotation (mirror about Z).
    [SerializeField]
    [Tooltip("Gun rotation relative to the RIGHT HAND (wrist) anchor when grabbed (Euler degrees). Tuned so the muzzle points forward and the gun sits upright with the handle in the grip.")]
    private Vector3 gripRotationHandRight = new Vector3(0f, 0f, 90f);

    [SerializeField]
    [Tooltip("Gun rotation relative to the LEFT HAND (wrist) anchor when grabbed (Euler degrees). Mirror of the right offset because the left wrist frame is mirrored.")]
    private Vector3 gripRotationHandLeft = new Vector3(0f, 0f, -90f);

    [Header("Hands")]
    [SerializeField]
    [Tooltip("Left OVRHand. Auto-discovered from the scene in Start when left unset.")]
    private OVRHand _leftHand;

    [SerializeField]
    [Tooltip("Right OVRHand. Auto-discovered from the scene in Start when left unset.")]
    private OVRHand _rightHand;

    [SerializeField]
    [Tooltip("Optional left OVRSkeleton for finger-curl grab detection. Auto-discovered in Start when unset; falls back to middle-finger pinch strength when absent.")]
    private OVRSkeleton _leftSkeleton;

    [SerializeField]
    [Tooltip("Optional right OVRSkeleton for finger-curl grab detection. Auto-discovered in Start when unset; falls back to middle-finger pinch strength when absent.")]
    private OVRSkeleton _rightSkeleton;

    [SerializeField]
    [Tooltip("Middle-finger pinch strength (0..1) that counts as a grab when no skeleton is available. Tuned in a Play-mode spike.")]
    private float grabPinchThreshold = 0.7f;

    [SerializeField]
    [Tooltip("Average middle/ring/pinky tip-to-wrist distance (meters, normalized by hand scale) below which the hand counts as a fist. Tuned in a Play-mode spike.")]
    private float grabCurlThreshold = 0.12f;

    private enum HoldMode { None, Controller, Hand }

    private float nextFireTime;
    private HoldMode holdMode;
    private OVRInput.Controller grabbedByController;
    private OVRHand _heldHand;
    private OVRSkeleton _heldSkeleton;
    private OVRInput.Hand _heldHandSide;
    private Transform originalParent;
    private Rigidbody rb;
    private OVRCameraRig _cameraRig;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        originalParent = transform.parent;
        _cameraRig = FindAnyObjectByType<OVRCameraRig>();

        if (_leftHand == null || _rightHand == null)
        {
            var hands = FindObjectsByType<OVRHand>(FindObjectsSortMode.None);
            for (int i = 0; i < hands.Length; i++)
            {
                OVRPlugin.Hand side = hands[i].GetHand();
                if (side == OVRPlugin.Hand.HandLeft && _leftHand == null) _leftHand = hands[i];
                else if (side == OVRPlugin.Hand.HandRight && _rightHand == null) _rightHand = hands[i];
            }
        }

        if (_leftSkeleton == null || _rightSkeleton == null)
        {
            var skeletons = FindObjectsByType<OVRSkeleton>(FindObjectsSortMode.None);
            for (int i = 0; i < skeletons.Length; i++)
            {
                switch (skeletons[i].GetSkeletonType())
                {
                    case OVRSkeleton.SkeletonType.HandLeft:
                    case OVRSkeleton.SkeletonType.XRHandLeft:
                        if (_leftSkeleton == null) _leftSkeleton = skeletons[i];
                        break;
                    case OVRSkeleton.SkeletonType.HandRight:
                    case OVRSkeleton.SkeletonType.XRHandRight:
                        if (_rightSkeleton == null) _rightSkeleton = skeletons[i];
                        break;
                }
            }
        }
    }

    void Update()
    {
        switch (holdMode)
        {
            case HoldMode.None:
                TryAcquire();
                break;
            case HoldMode.Controller:
                UpdateControllerHold();
                break;
            case HoldMode.Hand:
                UpdateHandHold();
                break;
        }
    }

    void TryAcquire()
    {
        bool rightCtrlInHand = OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandRight)
            == OVRInput.ControllerInHandState.ControllerInHand;
        bool leftCtrlInHand = OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandLeft)
            == OVRInput.ControllerInHandState.ControllerInHand;
        bool rightHandTracked = _rightHand != null && _rightHand.IsActive();
        bool leftHandTracked = _leftHand != null && _leftHand.IsActive();

        // SelectGrabDevice picks the category (controllers beat hands) and the right-before-left order.
        // Within the winning category, fall through to the secondary side so a closer left device can
        // still grab when the right one is out of range.
        switch (LaserGunLogic.SelectGrabDevice(rightCtrlInHand, leftCtrlInHand, rightHandTracked, leftHandTracked))
        {
            case LaserGunLogic.GrabDevice.RightController:
            case LaserGunLogic.GrabDevice.LeftController:
                if (rightCtrlInHand) TryGrabController(OVRInput.Controller.RTouch);
                if (holdMode == HoldMode.None && leftCtrlInHand) TryGrabController(OVRInput.Controller.LTouch);
                break;
            case LaserGunLogic.GrabDevice.RightHand:
            case LaserGunLogic.GrabDevice.LeftHand:
                if (rightHandTracked) TryGrabHand(_rightHand, _rightSkeleton, OVRInput.Hand.HandRight);
                if (holdMode == HoldMode.None && leftHandTracked) TryGrabHand(_leftHand, _leftSkeleton, OVRInput.Hand.HandLeft);
                break;
        }
    }

    void TryGrabController(OVRInput.Controller controller)
    {
        float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, controller);
        if (grip < 0.7f) return;

        Vector3 handPos = OVRInput.GetLocalControllerPosition(controller);
        // Convert from local tracking space to world space
        if (_cameraRig != null)
        {
            handPos = _cameraRig.trackingSpace.TransformPoint(handPos);
        }

        if (LaserGunLogic.IsWithinGrab(handPos, transform.position, grabRadius))
        {
            GrabController(controller);
        }
    }

    void GrabController(OVRInput.Controller controller)
    {
        holdMode = HoldMode.Controller;
        grabbedByController = controller;
        nextFireTime = 0f;

        Transform anchor = null;
        if (_cameraRig != null)
        {
            anchor = controller == OVRInput.Controller.RTouch
                ? _cameraRig.rightControllerAnchor
                : _cameraRig.leftControllerAnchor;
        }

        GrabToAnchor(anchor, gripRotationController, gripLocalPosition);
    }

    void TryGrabHand(OVRHand hand, OVRSkeleton skeleton, OVRInput.Hand side)
    {
        if (hand == null || _cameraRig == null) return;

        Transform handAnchor = side == OVRInput.Hand.HandRight
            ? _cameraRig.rightHandAnchor
            : _cameraRig.leftHandAnchor;
        if (handAnchor == null) return;

        if (!LaserGunLogic.IsWithinGrab(handAnchor.position, transform.position, grabRadius)) return;
        if (!IsHandGrabbing(hand, skeleton)) return;

        GrabHand(hand, skeleton, side, handAnchor);
    }

    void GrabHand(OVRHand hand, OVRSkeleton skeleton, OVRInput.Hand side, Transform handAnchor)
    {
        holdMode = HoldMode.Hand;
        _heldHand = hand;
        _heldSkeleton = skeleton;
        _heldHandSide = side;
        nextFireTime = 0f;

        bool right = side == OVRInput.Hand.HandRight;
        Vector3 gripRotation = right ? gripRotationHandRight : gripRotationHandLeft;
        // Mirror the lateral grip offset between hands so the palm centers on the handle for both.
        Vector3 gripPosition = gripLocalPositionHand
            + new Vector3(right ? gripHandLateralOffset : -gripHandLateralOffset, 0f, 0f);
        GrabToAnchor(handAnchor, gripRotation, gripPosition);
    }

    void GrabToAnchor(Transform anchor, Vector3 gripRotationEuler, Vector3 gripLocalPos)
    {
        if (rb != null)
        {
            rb.isKinematic = true;
        }

        if (anchor == null) return;

        transform.SetParent(anchor);

        // Hold the gun at a fixed pose relative to the anchor: rotate by the per-device grip offset,
        // then translate so the handle grip point (gripLocalPos, in gun-local space) sits on the
        // anchor. This grips the handle rather than the body and keeps the gun's roll consistent.
        transform.rotation = anchor.rotation * Quaternion.Euler(gripRotationEuler);
        Vector3 gripWorld = transform.TransformPoint(gripLocalPos);
        transform.position += anchor.position - gripWorld;
    }

    void UpdateControllerHold()
    {
        // Check for release (grip button up)
        float grip = OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, grabbedByController);
        if (grip < 0.3f)
        {
            ReleaseController();
            return;
        }

        // Check for fire (index trigger)
        float trigger = OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, grabbedByController);
        if (LaserGunLogic.ShouldFire(trigger > 0.7f, Time.time, nextFireTime, fireRate, out nextFireTime))
        {
            Fire();
        }
    }

    void UpdateHandHold()
    {
        // A destroyed hand never recovers, so release unconditionally rather than latching forever.
        if (_heldHand == null)
        {
            ReleaseHand();
            return;
        }

        // Latch through momentary tracking loss: keep holding without sampling or releasing. The one
        // exception is the user closing that hand onto a controller, which would otherwise strand the gun.
        if (!_heldHand.IsTracked || !_heldHand.IsDataValid)
        {
            if (OVRInput.GetControllerIsInHandState(_heldHandSide) == OVRInput.ControllerInHandState.ControllerInHand)
            {
                ReleaseHand();
            }
            return;
        }

        bool firePinch = _heldHand.GetFingerIsPinching(OVRHand.HandFinger.Index);
        if (LaserGunLogic.ShouldFire(firePinch, Time.time, nextFireTime, fireRate, out nextFireTime))
        {
            Fire();
        }

        // Release only when the hand is fully open: neither a fist nor an index (fire) pinch.
        if (!IsHandGrabbing(_heldHand, _heldSkeleton) && !firePinch)
        {
            ReleaseHand();
        }
    }

    // Grab detection deliberately excludes the index finger so a fire pinch is never read as a grab.
    bool IsHandGrabbing(OVRHand hand, OVRSkeleton skeleton)
    {
        if (hand == null) return false;

        if (skeleton != null && skeleton.Bones != null && skeleton.Bones.Count > 0)
        {
            // OVR ("Hand_*") and OpenXR ("XRHand_*") skeletons use different bone-id sets; pick the
            // wrist + middle/ring/pinky(little) tips that match the format this skeleton actually uses.
            OVRSkeleton.BoneId wristId, middleTipId, ringTipId, pinkyTipId;
            switch (skeleton.GetSkeletonType())
            {
                case OVRSkeleton.SkeletonType.XRHandLeft:
                case OVRSkeleton.SkeletonType.XRHandRight:
                    wristId = OVRSkeleton.BoneId.XRHand_Wrist;
                    middleTipId = OVRSkeleton.BoneId.XRHand_MiddleTip;
                    ringTipId = OVRSkeleton.BoneId.XRHand_RingTip;
                    pinkyTipId = OVRSkeleton.BoneId.XRHand_LittleTip;
                    break;
                default:
                    wristId = OVRSkeleton.BoneId.Hand_WristRoot;
                    middleTipId = OVRSkeleton.BoneId.Hand_MiddleTip;
                    ringTipId = OVRSkeleton.BoneId.Hand_RingTip;
                    pinkyTipId = OVRSkeleton.BoneId.Hand_PinkyTip;
                    break;
            }

            Transform wrist = null, middleTip = null, ringTip = null, pinkyTip = null;
            var bones = skeleton.Bones;
            for (int i = 0; i < bones.Count; i++)
            {
                OVRSkeleton.BoneId id = bones[i].Id;
                if (id == wristId) wrist = bones[i].Transform;
                else if (id == middleTipId) middleTip = bones[i].Transform;
                else if (id == ringTipId) ringTip = bones[i].Transform;
                else if (id == pinkyTipId) pinkyTip = bones[i].Transform;
            }

            if (wrist != null && middleTip != null && ringTip != null && pinkyTip != null)
            {
                float avg = (Vector3.Distance(middleTip.position, wrist.position)
                    + Vector3.Distance(ringTip.position, wrist.position)
                    + Vector3.Distance(pinkyTip.position, wrist.position)) / 3f;
                float scale = hand.HandScale > 0f ? hand.HandScale : 1f;
                return (avg / scale) < grabCurlThreshold;
            }
        }

        return hand.GetFingerPinchStrength(OVRHand.HandFinger.Middle) >= grabPinchThreshold;
    }

    void ReleaseController()
    {
        holdMode = HoldMode.None;
        transform.SetParent(originalParent);
        FreezeInPlace();
    }

    void ReleaseHand()
    {
        holdMode = HoldMode.None;
        transform.SetParent(originalParent);
        FreezeInPlace();

        _heldHand = null;
        _heldSkeleton = null;
    }

    // The gun has no gravity and the scene has no floor, so any residual velocity on release would send
    // it drifting out of bounds. Restore its rest state (kinematic, zero velocity) so it stays put and
    // cannot be thrown.
    void FreezeInPlace()
    {
        if (rb == null) return;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.isKinematic = true;
    }

    void Fire()
    {
        if (projectilePrefab == null || muzzlePoint == null) return;

        GameObject proj = Instantiate(projectilePrefab, muzzlePoint.position, muzzlePoint.rotation);
        // Ignore collision between projectile and gun
        var projCollider = proj.GetComponent<Collider>();
        var gunCollider = GetComponent<Collider>();
        if (projCollider != null && gunCollider != null)
        {
            Physics.IgnoreCollision(projCollider, gunCollider);
        }
    }
}
