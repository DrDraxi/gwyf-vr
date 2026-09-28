using GWYFVR.Patches;
using HarmonyLib;
using UnityEngine;

namespace GWYFVR.Player
{
    /// <summary>
    /// The bet slider is moved on the host, along the line your head points at. While you drag one in VR,
    /// your head (as other players and the host see it) is turned towards the spot on the slider your
    /// hand points at, so the knob follows your hand. Your own view keeps following the headset, and
    /// nothing new goes over the network, so it works with hosts that don't have the mod.
    /// </summary>
    [HarmonyPatch]
    internal static class VRBetSlider
    {
        /// <summary>The slider being dragged by the local player, if any.</summary>
        public static BetSlider Dragging { get; private set; }

        [HarmonyPatch(typeof(InputEvents), nameof(InputEvents.UpdateInteract))]
        [HarmonyPostfix]
        private static void TrackDrag(bool isPressed)
        {
            if (!isPressed)
            {
                Dragging = null;
                return;
            }

            var interact = LocalPlayer.Controller != null ? LocalPlayer.Controller.GetComponent<PlayerInteract>() : null;
            Dragging = interact != null ? FindSlider(interact.TargetInteractable as Component) : null;
        }

        private static BetSlider FindSlider(Component target)
        {
            if (target == null)
                return null;
            var slider = target.GetComponentInParent<BetSlider>();
            if (slider != null)
                return slider;

            // The handle may sit next to the slider rather than under it.
            BetSlider nearest = null;
            var best = 1f;
            foreach (var candidate in Object.FindObjectsByType<BetSlider>(FindObjectsSortMode.None))
            {
                var distance = Vector3.Distance(candidate.knob.transform.position, target.transform.position);
                if (distance < best)
                {
                    best = distance;
                    nearest = candidate;
                }
            }

            return nearest;
        }

        /// <summary>Runs after the head has been pointed where the headset looks (PlayerPatches).</summary>
        [HarmonyPatch(typeof(PlayerHead), nameof(PlayerHead.GetInput))]
        [HarmonyPostfix]
        private static void AimHeadAtSlider(PlayerHead __instance)
        {
            var slider = Dragging;
            if (slider == null || __instance != LocalPlayer.Head || __instance.isLocked)
                return;

            var hand = HandAim.PointingHand();
            if (hand == null)
                return;

            var ray = hand.transform;
            FathF.NearestPointToRayOnLine(slider.startPoint.position, slider.endPoint.position, ray.position, ray.forward, out var t, out _);
            var target = Vector3.Lerp(slider.startPoint.position, slider.endPoint.position, Mathf.Clamp01(t));
            var direction = target - __instance.transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                return;

            var parent = __instance.transform.parent;
            var world = Quaternion.LookRotation(direction);
            var local = parent != null ? Quaternion.Inverse(parent.rotation) * world : world;

            if (__instance.IsFree)
            {
                __instance.SetRotationFree(local);
            }
            else
            {
                var euler = local.eulerAngles;
                __instance.SetRotation(euler.y, Mathf.DeltaAngle(0f, euler.x));
            }
        }
    }
}
