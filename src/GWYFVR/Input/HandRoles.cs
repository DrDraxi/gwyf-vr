using GWYFVR.Player;
using UnityEngine;
using UnityEngine.XR;

namespace GWYFVR.Input
{
    /// <summary>
    /// Which hand is doing what. The game has a single "aim" and a single held item, so the mod decides
    /// which controller they belong to: the hand you last pressed a grip or trigger on aims, and the hand
    /// that picked an item up holds it until it is thrown or dropped.
    /// </summary>
    internal static class HandRoles
    {
        /// <summary>Hand that aims interactions and the laser.</summary>
        public static XRNode ActiveHand { get; private set; } = XRNode.RightHand;

        /// <summary>Hand holding the current item (only meaningful while an item is held).</summary>
        public static XRNode HoldingHand { get; private set; } = XRNode.RightHand;

        public static bool HoldingLeft => HoldingHand == XRNode.LeftHand;

        private static ControllerState lastLeft;
        private static ControllerState lastRight;
        private static bool wasHolding;
        private static int lastFrame = -1;

        public static ControllerState Active => ActiveHand == XRNode.LeftHand ? XRControllers.Left : XRControllers.Right;
        public static ControllerState Holding => HoldingHand == XRNode.LeftHand ? XRControllers.Left : XRControllers.Right;

        /// <summary>Update once per frame, after XRControllers.Poll.</summary>
        public static void Update()
        {
            if (lastFrame == Time.frameCount)
                return;
            lastFrame = Time.frameCount;

            XRControllers.Poll();
            var l = XRControllers.Left;
            var r = XRControllers.Right;

            if (Pressed(l, lastLeft))
                ActiveHand = XRNode.LeftHand;
            else if (Pressed(r, lastRight))
                ActiveHand = XRNode.RightHand;

            // Whoever picked the item up (the aiming hand at that moment) holds it.
            var holding = IsHoldingItem;
            if (holding && !wasHolding)
                HoldingHand = ActiveHand;
            wasHolding = holding;

            lastLeft = l;
            lastRight = r;
        }

        public static bool IsHoldingItem
        {
            get
            {
                var inventory = LocalPlayer.Controller != null ? LocalPlayer.Controller.GetComponent<PlayerInventory>() : null;
                return inventory != null && inventory.NetworkholdingItem != null;
            }
        }

        private static bool Pressed(ControllerState now, ControllerState before) =>
            now.Grip && !before.Grip || now.Trigger && !before.Trigger;
    }
}
