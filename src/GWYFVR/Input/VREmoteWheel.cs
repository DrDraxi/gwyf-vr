using HarmonyLib;
using UnityEngine;

namespace GWYFVR.Input
{
    /// <summary>
    /// Emote wheel on the controllers: left A opens it, push the left stick toward an emote and let the
    /// stick go to play it (or press left A again to close it). The game opens the wheel on press and plays
    /// the selected emote when it closes, so this just drives those events and the wheel's selection.
    /// </summary>
    internal static class VREmoteWheel
    {
        private const float SelectThreshold = 0.7f;
        private const float ReleaseThreshold = 0.3f;

        public static bool Open { get; private set; }

        /// <summary>Stick direction chosen while the wheel is open (null until pushed far enough).</summary>
        public static Vector2? Selection { get; private set; }

        private static bool lastButton;
        private static bool stickPushed;

        public static void Update(ControllerState left)
        {
            var pressed = left.Primary && !lastButton;
            lastButton = left.Primary;

            if (!Open)
            {
                if (pressed && Player.LocalPlayer.Head != null)
                    SetOpen(true);
                return;
            }

            if (pressed)
            {
                // Closing with A cancels: clear the wheel's selection so the game plays nothing.
                var wheel = Object.FindAnyObjectByType<EmoteWheelController>();
                if (wheel != null && wheel.radialMenu != null)
                    wheel.radialMenu.index = -1;
                SetOpen(false);
                return;
            }

            var stick = left.Stick;
            if (stick.magnitude > SelectThreshold)
            {
                stickPushed = true;
                Selection = stick.normalized;
            }
            else if (stickPushed && stick.magnitude < ReleaseThreshold)
            {
                SetOpen(false);
            }
        }

        private static void SetOpen(bool open)
        {
            Open = open;
            stickPushed = false;
            if (open)
                Selection = null;
            InputEvents.OnEmoteWheelEvent?.Invoke(open);
        }
    }

    [HarmonyPatch]
    internal static class EmoteWheelPatches
    {
        /// <summary>While the VR wheel is open, point the wheel's selection where the left stick points.</summary>
        [HarmonyPatch(typeof(RMF_RadialMenu), "Update")]
        [HarmonyPrefix]
        private static bool StickSelection(RMF_RadialMenu __instance)
        {
            if (!VREmoteWheel.Open)
                return true;

            var selection = VREmoteWheel.Selection;
            if (selection == null || __instance.elements == null || __instance.elements.Count == 0)
                return false;

            var angleOffset = 360f / __instance.elements.Count;
            var angle = Mathf.Atan2(selection.Value.y, selection.Value.x) * Mathf.Rad2Deg;
            __instance.currentAngle = __instance.normalizeAngle(-angle + 90f - __instance.globalOffset + angleOffset / 2f);
            var index = Mathf.Clamp((int)(__instance.currentAngle / angleOffset), 0, __instance.elements.Count - 1);
            __instance.index = index;
            if (__instance.elements[index] != null)
                __instance.selectButton(index);

            if (__instance.useSelectionFollower && __instance.selectionFollowerContainer != null)
                __instance.selectionFollowerContainer.rotation = Quaternion.Euler(0f, 0f, angle + 270f);

            return false;
        }
    }
}
