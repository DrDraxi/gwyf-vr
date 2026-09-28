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
