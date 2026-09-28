using UnityEngine;
using UnityEngine.InputSystem;

namespace GWYFVR.Input
{
    /// <summary>
    /// Headset and controller poses plus the few controller inputs the mod handles itself
    /// (turning, recentering). Poses are in tracking space, i.e. local to the VR rig.
    /// </summary>
    internal static class VRInput
    {
        public static InputAction HeadPosition { get; private set; }
        public static InputAction HeadRotation { get; private set; }

        public static InputAction LeftPosition { get; private set; }
        public static InputAction LeftRotation { get; private set; }
        public static InputAction RightPosition { get; private set; }
        public static InputAction RightRotation { get; private set; }


        private static bool created;

        public static void Create()
        {
            if (created)
                return;
            created = true;

            AllowXRDevices();

            HeadPosition = Pose("HeadPosition", "<XRHMD>/centerEyePosition", "Vector3");
            HeadRotation = Pose("HeadRotation", "<XRHMD>/centerEyeRotation", "Quaternion");

            LeftPosition = Pose("LeftPosition", "<XRController>{LeftHand}/pointerPosition", "Vector3");
            LeftRotation = Pose("LeftRotation", "<XRController>{LeftHand}/pointerRotation", "Quaternion");
            RightPosition = Pose("RightPosition", "<XRController>{RightHand}/pointerPosition", "Vector3");
            RightRotation = Pose("RightRotation", "<XRController>{RightHand}/pointerRotation", "Quaternion");

        }

        private static InputAction Pose(string name, string binding, string type)
        {
            var action = new InputAction(name, InputActionType.Value, binding, expectedControlType: type);
            action.Enable();
            return action;
        }

        /// <summary>
        /// Games can restrict the Input System to certain device types, which hides XR devices.
        /// Allow them, and log what is connected to help diagnose tracking problems.
        /// </summary>
        public static void AllowXRDevices()
        {
            var supported = InputSystem.settings.supportedDevices;
            if (supported.Count > 0)
            {
                var list = new System.Collections.Generic.List<string>(supported);
                foreach (var layout in new[] { "XRHMD", "XRController", "TrackedDevice", "Gamepad", "Mouse" })
                    if (!list.Contains(layout))
                        list.Add(layout);
                InputSystem.settings.supportedDevices = new UnityEngine.InputSystem.Utilities.ReadOnlyArray<string>(list.ToArray());
                Plugin.Log.LogInfo($"Game limits input devices to [{string.Join(", ", supported)}], added XR devices");
            }
        }

        public static void LogDevices()
        {
            foreach (var device in InputSystem.devices)
                Plugin.Log.LogInfo($"Input device: {device.name} ({device.layout})");

            var xrDevices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
            UnityEngine.XR.InputDevices.GetDevices(xrDevices);
            foreach (var device in xrDevices)
                Plugin.Log.LogInfo($"XR device: {device.name} ({device.characteristics})");
        }

        public static Vector3 ReadPosition(InputAction action)
        {
            if (action.activeControl != null || action.controls.Count > 0)
                return action.ReadValue<Vector3>();

            // Fall back to Unity's XR device API when the Input System has no XR device.
            return FallbackDevice(action).TryGetFeatureValue(
                action == HeadPosition ? UnityEngine.XR.CommonUsages.centerEyePosition : UnityEngine.XR.CommonUsages.devicePosition,
                out var position) ? position : Vector3.zero;
        }

        public static Quaternion ReadRotation(InputAction action)
        {
            if (action.activeControl != null || action.controls.Count > 0)
            {
                var q = action.ReadValue<Quaternion>();
                // An unbound Quaternion action reads as all zeroes, which is not a valid rotation.
                return q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f ? Quaternion.identity : q;
            }

            return FallbackDevice(action).TryGetFeatureValue(
                action == HeadRotation ? UnityEngine.XR.CommonUsages.centerEyeRotation : UnityEngine.XR.CommonUsages.deviceRotation,
                out var rotation) ? rotation : Quaternion.identity;
        }

        private static UnityEngine.XR.InputDevice FallbackDevice(InputAction action)
        {
            var node = action == HeadPosition || action == HeadRotation ? UnityEngine.XR.XRNode.CenterEye
                : action == LeftPosition || action == LeftRotation ? UnityEngine.XR.XRNode.LeftHand
                : UnityEngine.XR.XRNode.RightHand;
            return UnityEngine.XR.InputDevices.GetDeviceAtXRNode(node);
        }
    }
}
