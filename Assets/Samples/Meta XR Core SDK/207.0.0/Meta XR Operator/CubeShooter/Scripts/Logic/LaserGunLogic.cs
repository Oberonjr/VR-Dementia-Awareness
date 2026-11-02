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

/// <summary>Pure, OVR-free helpers for LaserGun.</summary>
public static class LaserGunLogic
{
    public enum GrabDevice { None, LeftController, RightController, LeftHand, RightHand }

    // Controller-in-hand always wins over a tracked hand; right is chosen before left.
    public static GrabDevice SelectGrabDevice(
        bool rightControllerInHand, bool leftControllerInHand,
        bool rightHandTracked, bool leftHandTracked)
    {
        if (rightControllerInHand) return GrabDevice.RightController;
        if (leftControllerInHand) return GrabDevice.LeftController;
        if (rightHandTracked) return GrabDevice.RightHand;
        if (leftHandTracked) return GrabDevice.LeftHand;
        return GrabDevice.None;
    }

    public static bool ShouldFire(bool fireHeld, float now, float nextFireTime, float fireRate,
        out float newNextFireTime)
    {
        newNextFireTime = nextFireTime;
        if (!fireHeld || now < nextFireTime) return false;
        newNextFireTime = now + fireRate;
        return true;
    }

    public static bool IsWithinGrab(Vector3 devicePos, Vector3 gunPos, float grabRadius)
        => Vector3.Distance(devicePos, gunPos) < grabRadius;
}
