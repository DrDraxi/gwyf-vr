using System.Collections.Generic;
using System.Reflection;
using Extensions;
using GWYFVR.Input;
using GWYFVR.Player;
using GWYFVR.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace GWYFVR.Patches
{
    /// <summary>
    /// Several items and actions aim by casting a ray from the game camera, i.e. where you look. In VR
    /// they aim with your hand instead, by briefly standing a camera on the controller in for the game
    /// camera. Everything here is client-side, so players without the mod see nothing different.
    /// </summary>
    internal static class HandAim
    {
        private static Camera holdingHandCamera;

        private static bool Enabled
        {
            get
            {
                var rig = VRRig.Instance;
                return Plugin.Settings.HandInteraction.Value && rig != null && rig.PlayerMode;
            }
        }

        /// <summary>A camera on the hand holding the item, pointing where that controller points.</summary>
        public static Camera HoldingHand()
        {
            if (!Enabled)
                return null;
            var rig = VRRig.Instance.transform;
            if (!XRControllers.TryGetPointerPose(HandRoles.HoldingHand, out var position, out var rotation))
                return null;

            if (holdingHandCamera == null)
            {
                // Never renders; the "VR" name keeps VRRig from treating it as a game camera.
                var go = new GameObject("VRItemAimCamera");
                go.transform.SetParent(rig, false);
                holdingHandCamera = go.AddComponent<Camera>();
                holdingHandCamera.enabled = false;
                holdingHandCamera.stereoTargetEye = StereoTargetEyeMask.None;
                holdingHandCamera.cullingMask = 0;
            }

            holdingHandCamera.transform.SetPositionAndRotation(rig.TransformPoint(position), rig.rotation * rotation);
            return holdingHandCamera;
        }

        /// <summary>The laser pointer's camera (the hand you last pressed something with).</summary>
        public static Camera PointingHand()
        {
            var pointer = VRPointer.Instance;
            if (!Enabled || pointer == null || !pointer.HandTracked || pointer.IsPointingAtMenu)
                return null;
            return pointer.PointerCamera;
        }

        /// <summary>Put <paramref name="aim"/> in place of the game camera; returns the camera to restore.</summary>
        public static Camera SwapMainCamera(Camera aim)
        {
            var local = MonoSingleton<LocalManager>.Instance;
            if (aim == null || local == null || local.mainCamera == null)
                return null;
            var original = local.mainCamera;
            local.mainCamera = aim;
            return original;
        }

        public static void RestoreMainCamera(Camera original)
        {
            var local = MonoSingleton<LocalManager>.Instance;
            if (original != null && local != null)
                local.mainCamera = original;
        }
    }

    /// <summary>Quota Gun shots, and choosing which game the Taser or Golden Chip is used on.</summary>
    [HarmonyPatch]
    internal static class HeldItemAimPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(QuotaGun), nameof(QuotaGun.Shoot));
            yield return AccessTools.Method(typeof(Taser), nameof(Taser.TryFindTargetGame));
            yield return AccessTools.Method(typeof(GoldenChip), nameof(GoldenChip.TryFindTargetGame));
        }

        private static void Prefix(out Camera __state)
        {
            __state = HandAim.SwapMainCamera(HandAim.HoldingHand());
        }

        private static void Finalizer(Camera __state)
        {
            HandAim.RestoreMainCamera(__state);
        }
    }

    /// <summary>Dragging the Hi-Lo slider follows the hand that grabbed it instead of your gaze.</summary>
    [HarmonyPatch(typeof(HiLoSlider), nameof(HiLoSlider.Update))]
    internal static class HiLoSliderAimPatch
    {
        private static void Prefix(HiLoSlider __instance, out Camera __state)
        {
            __state = __instance._localIsInteracting ? HandAim.SwapMainCamera(HandAim.PointingHand()) : null;
        }

        private static void Finalizer(Camera __state)
        {
            HandAim.RestoreMainCamera(__state);
        }
    }

    /// <summary>Knows when the local player holds a Hi-Lo slider, so VirtualGamepad can keep hold of it.</summary>
    [HarmonyPatch]
    internal static class HiLoSliderDrag
    {
        public static bool Dragging { get; private set; }

        [HarmonyPatch(typeof(HiLoSlider), nameof(HiLoSlider.OnPlayerInteract))]
        [HarmonyPostfix]
        private static void Start() => Dragging = true;

        [HarmonyPatch(typeof(HiLoSlider), nameof(HiLoSlider.HandlePlayerInteract))]
        [HarmonyPostfix]
        private static void End(bool isPressed)
        {
            if (!isPressed)
                Dragging = false;
        }
    }

    /// <summary>Pings land where the controller points.</summary>
    [HarmonyPatch(typeof(PlayerPingManager), nameof(PlayerPingManager.OnPing))]
    internal static class PingAimPatch
    {
        private static void Prefix(PlayerPingManager __instance, out Camera __state)
        {
            __state = null;
            var aim = HandAim.PointingHand();
            if (aim == null)
                return;
            __state = __instance._cam;
            __instance._cam = aim;
        }

        private static void Finalizer(PlayerPingManager __instance, Camera __state)
        {
            if (__state != null)
                __instance._cam = __state;
        }
    }
}
