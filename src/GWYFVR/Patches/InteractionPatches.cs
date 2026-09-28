using GWYFVR.UI;
using HarmonyLib;
using UnityEngine;

namespace GWYFVR.Patches
{
    [HarmonyPatch]
    internal static class InteractionPatches
    {
        /// <summary>
        /// The game finds what you can interact with by casting a ray from the camera. In VR, cast it
        /// from the right controller instead, so you pick things up by pointing at them.
        /// </summary>
        [HarmonyPatch(typeof(PlayerInteract), nameof(PlayerInteract.RaycastInteractable))]
        [HarmonyPrefix]
        private static void AimWithHand(PlayerInteract __instance, out Camera __state)
        {
            __state = __instance._cam;
            var pointer = VRPointer.Instance;
            // Locked in place (e.g. waking up in the spawn box) the game expects you to look at things.
            var locked = __instance._pc != null && __instance._pc.IsLocked;
            if (Plugin.Settings.HandInteraction.Value && pointer != null && pointer.HandTracked && !pointer.IsPointingAtMenu && !locked)
                __instance._cam = pointer.PointerCamera;
        }

        [HarmonyPatch(typeof(PlayerInteract), nameof(PlayerInteract.RaycastInteractable))]
        [HarmonyPostfix]
        private static void Restore(PlayerInteract __instance, Camera __state)
        {
            __instance._cam = __state;
        }
    }
}
