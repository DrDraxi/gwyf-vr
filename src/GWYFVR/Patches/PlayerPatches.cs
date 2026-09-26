using System.Collections.Generic;
using System.Reflection;
using GWYFVR.Player;
using HarmonyLib;
using UnityEngine;

namespace GWYFVR.Patches
{
    [HarmonyPatch]
    internal static class PlayerPatches
    {
        [HarmonyPatch(typeof(PlayerHead), nameof(PlayerHead.OnStartClient))]
        [HarmonyPostfix]
        private static void RegisterLocalHead(PlayerHead __instance)
        {
            if (__instance.isLocalPlayer)
                LocalPlayer.Head = __instance;
        }

        /// <summary>
        /// The headset replaces mouse/stick look: the player's head is pointed where the headset looks,
        /// so movement, interaction raycasts and what other players see all follow your real head.
        /// </summary>
        [HarmonyPatch(typeof(PlayerHead), nameof(PlayerHead.GetInput))]
        [HarmonyPrefix]
        private static bool HeadFollowsHeadset(PlayerHead __instance)
        {
            var rig = VRRig.Instance;
            if (rig == null || !rig.PlayerMode || __instance != LocalPlayer.Head)
                return true;

            if (__instance.isLocked)
                return false;

            var parent = __instance.transform.parent;
            var world = rig.VRCamera.transform.rotation;
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

            return false;
        }

        /// <summary>Snap the head to the headset instead of smoothing towards it, smoothing lags your view.</summary>
        [HarmonyPatch(typeof(PlayerHead), nameof(PlayerHead.RotateHead))]
        [HarmonyPrefix]
        private static bool RotateHeadInstantly(PlayerHead __instance)
        {
            var rig = VRRig.Instance;
            if (rig == null || !rig.PlayerMode || __instance != LocalPlayer.Head)
                return true;

            __instance.transform.localRotation = __instance._targetRotation;
            return false;
        }

        /// <summary>No head bob, sway or FOV kicks in VR.</summary>
        [HarmonyPatch(typeof(PlayerCameraController), nameof(PlayerCameraController.LateUpdate))]
        [HarmonyPrefix]
        private static bool DisableCameraEffects(PlayerCameraController __instance)
        {
            __instance.transform.localPosition = Vector3.zero;
            __instance.transform.localRotation = Quaternion.identity;
            return false;
        }

        /// <summary>When the game turns the player (spawning, teleports) turn the VR rig with it.</summary>
        [HarmonyPatch(typeof(PlayerController), nameof(PlayerController.LocalRotate))]
        [HarmonyPostfix]
        private static void FollowGameRotation(PlayerController __instance, Vector2 rotation)
        {
            if (VRRig.Instance == null || LocalPlayer.Head == null || __instance.head != LocalPlayer.Head)
                return;

            var parent = __instance.head.transform.parent;
            var parentYaw = parent != null ? parent.eulerAngles.y : 0f;
            VRRig.Instance.FaceYaw(parentYaw + rotation.x);
        }
    }

    /// <summary>The game's software mouse cursor makes no sense in VR, menus use the controller laser.</summary>
    [HarmonyPatch]
    internal static class CursorPatches
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(BaseCursor), nameof(BaseCursor.Update));
            yield return AccessTools.Method(typeof(UICursor), nameof(UICursor.Update));
            yield return AccessTools.Method(typeof(UICursorSimple), nameof(UICursorSimple.Update));
        }

        private static bool Prefix(BaseCursor __instance)
        {
            if (__instance.uiCursorImage != null)
                __instance.uiCursorImage.enabled = false;
            return false;
        }
    }
}
