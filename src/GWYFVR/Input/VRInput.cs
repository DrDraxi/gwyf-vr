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

        public static InputAction Turn { get; private set; }
        public static InputAction UIClick { get; private set; }

        private static bool created;

        public static void Create()
        {
            if (created)
                return;
            created = true;

            HeadPosition = Pose("HeadPosition", "<XRHMD>/centerEyePosition", "Vector3");
            HeadRotation = Pose("HeadRotation", "<XRHMD>/centerEyeRotation", "Quaternion");

            LeftPosition = Pose("LeftPosition", "<XRController>{LeftHand}/pointerPosition", "Vector3");
            LeftRotation = Pose("LeftRotation", "<XRController>{LeftHand}/pointerRotation", "Quaternion");
            RightPosition = Pose("RightPosition", "<XRController>{RightHand}/pointerPosition", "Vector3");
            RightRotation = Pose("RightRotation", "<XRController>{RightHand}/pointerRotation", "Quaternion");

            Turn = new InputAction("Turn", InputActionType.Value, "<XRController>{RightHand}/{Primary2DAxis}", expectedControlType: "Vector2");
            Turn.Enable();

            UIClick = new InputAction("UIClick", InputActionType.Button, "<XRController>{RightHand}/{TriggerButton}");
            UIClick.AddBinding("<XRController>{LeftHand}/{TriggerButton}");
            UIClick.Enable();
        }

        private static InputAction Pose(string name, string binding, string type)
        {
            var action = new InputAction(name, InputActionType.Value, binding, expectedControlType: type);
            action.Enable();
            return action;
        }

        public static Vector3 ReadPosition(InputAction action) => action.ReadValue<Vector3>();

        public static Quaternion ReadRotation(InputAction action)
        {
            var q = action.ReadValue<Quaternion>();
            // An unbound Quaternion action reads as all zeroes, which is not a valid rotation.
            return q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f ? Quaternion.identity : q;
        }
    }
}
