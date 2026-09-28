using UnityEngine;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;

namespace GWYFVR.Input
{
    /// <summary>
    /// Controller buttons and sticks read through Unity's XR device API. The game's Input System was
    /// built without XR support, so XR controllers never show up there.
    /// </summary>
    internal struct ControllerState
    {
        public bool Tracked;
        public Vector2 Stick;
        public bool StickClick;
        public bool Primary;
        public bool Secondary;
        public bool Grip;
        public bool Trigger;
        public bool Menu;

        public static ControllerState Read(XRNode node)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var state = new ControllerState { Tracked = device.isValid };
            if (!device.isValid)
                return state;

            device.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out state.Stick);
            device.TryGetFeatureValue(XRCommonUsages.primary2DAxisClick, out state.StickClick);
            device.TryGetFeatureValue(XRCommonUsages.primaryButton, out state.Primary);
            device.TryGetFeatureValue(XRCommonUsages.secondaryButton, out state.Secondary);
            device.TryGetFeatureValue(XRCommonUsages.gripButton, out state.Grip);
            device.TryGetFeatureValue(XRCommonUsages.triggerButton, out state.Trigger);
            device.TryGetFeatureValue(XRCommonUsages.menuButton, out state.Menu);
            return state;
        }
    }

    internal static class XRControllers
    {
        public static ControllerState Left;
        public static ControllerState Right;

        private static int lastFrame = -1;

        /// <summary>Read both controllers once per frame.</summary>
        public static void Poll()
        {
            if (lastFrame == Time.frameCount)
                return;
            lastFrame = Time.frameCount;

            Left = ControllerState.Read(XRNode.LeftHand);
            Right = ControllerState.Read(XRNode.RightHand);
        }

        private static readonly InputFeatureUsage<Vector3> PointerPosition = new InputFeatureUsage<Vector3>("PointerPosition");
        private static readonly InputFeatureUsage<Quaternion> PointerRotation = new InputFeatureUsage<Quaternion>("PointerRotation");
        private static bool loggedUsages;

        /// <summary>
        /// The controller's pointing pose (OpenXR "aim" pose, or the grip pose tilted down when the runtime
        /// has none), adjusted by the configured laser rotation.
        /// </summary>
        public static bool TryGetPointerPose(XRNode node, out Vector3 position, out Quaternion rotation)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            position = default;
            rotation = Quaternion.identity;
            if (!device.isValid)
                return false;

            if (!loggedUsages)
            {
                loggedUsages = true;
                var usages = new System.Collections.Generic.List<InputFeatureUsage>();
                if (device.TryGetFeatureUsages(usages))
                    Plugin.Log.LogInfo($"Controller features: {string.Join(", ", System.Linq.Enumerable.Select(usages, u => u.name))}");
            }

            var adjust = Plugin.Settings.LaserRotation.Value;
            if (node == XRNode.LeftHand)
                adjust = new Vector3(adjust.x, -adjust.y, -adjust.z);

            if (device.TryGetFeatureValue(PointerPosition, out position) && device.TryGetFeatureValue(PointerRotation, out rotation))
            {
                rotation *= Quaternion.Euler(adjust);
                return true;
            }

            if (!TryGetAimPose(node, out position, out rotation))
                return false;
            rotation *= Quaternion.Euler(35f, 0f, 0f) * Quaternion.Euler(adjust);
            return true;
        }

        public static bool TryGetAimPose(XRNode node, out Vector3 position, out Quaternion rotation)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            position = default;
            rotation = Quaternion.identity;
            return device.isValid &&
                   device.TryGetFeatureValue(XRCommonUsages.devicePosition, out position) &&
                   device.TryGetFeatureValue(XRCommonUsages.deviceRotation, out rotation);
        }
    }
}
