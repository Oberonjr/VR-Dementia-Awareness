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
/// Enables the hand-pointing ray as a fallback far-field UI driver for this
/// sample. Each <see cref="OVRHand"/> is registered as an
/// <see cref="OVRInputModule"/> input source (so its pointer pose drives the
/// <c>OVRRaycaster</c> canvases and a pinch selects) only while no Touch
/// controller is active and eye-gaze interaction is unavailable. When a
/// controller is present, or eye gaze is available, the hand ray is withdrawn and
/// <see cref="OVRGazePinchInputSource"/> stays the primary hand driver.
/// </summary>
[HelpURL("https://developer.oculus.com/documentation/unity/unity-isdk-input-processing/")]
public class HandRayFallbackController : MonoBehaviour
{
    [Tooltip("Hands registered as UI ray sources while the fallback is active. Auto-discovered when empty.")]
    [SerializeField]
    private OVRHand[] hands;

    [Tooltip("Gaze-pinch source, disabled while the hand ray drives. Auto-discovered when empty.")]
    [SerializeField]
    private OVRGazePinchInputSource gazePinch;

    private bool _handRayActive;
    private bool _applied;

    private void Awake()
    {
#pragma warning disable 618
        if (hands == null || hands.Length == 0)
        {
            hands = FindObjectsOfType<OVRHand>();
        }

        if (gazePinch == null)
        {
            gazePinch = FindObjectOfType<OVRGazePinchInputSource>();
        }
#pragma warning restore 618
    }

    private void OnDisable()
    {
        // Restore the scene default: hand ray withdrawn, gaze-pinch driving.
        SetHandRaySources(false);
        if (gazePinch != null)
        {
            gazePinch.enabled = true;
        }

        _applied = false;
    }

    private void Update()
    {
        Apply(ShouldHandRayDrive());
    }

    private bool ShouldHandRayDrive()
    {
        // Mirror OVRGazePinchInputSource's predicates so the two arbiters can't disagree:
        // a controller counts only when held, and gaze counts only when enabled (not merely
        // supported, which is all the gaze source's pose actually gates on).
        bool controllerActive =
            OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandLeft) ==
                OVRInput.ControllerInHandState.ControllerInHand ||
            OVRInput.GetControllerIsInHandState(OVRInput.Hand.HandRight) ==
                OVRInput.ControllerInHandState.ControllerInHand;
        bool gazeAvailable = OVRPlugin.eyeGazeInteractionsEnabled;
        return !controllerActive && !gazeAvailable;
    }

    private void Apply(bool handRayShouldDrive)
    {
        if (_applied && handRayShouldDrive == _handRayActive)
        {
            return;
        }

        _applied = true;
        _handRayActive = handRayShouldDrive;

        // Gaze-pinch and the hand ray are mutually exclusive so only one reticle drives.
        if (gazePinch != null)
        {
            gazePinch.enabled = !handRayShouldDrive;
        }

        SetHandRaySources(handRayShouldDrive);
    }

    private void SetHandRaySources(bool register)
    {
        if (hands == null)
        {
            return;
        }

        foreach (OVRHand hand in hands)
        {
            if (hand == null)
            {
                continue;
            }

            if (register)
            {
                OVRInputModule.TrackInputSource(hand);
            }
            else
            {
                OVRInputModule.UntrackInputSource(hand);
            }
        }
    }
}
