using GWYFVR.Input;
using GWYFVR.Player;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;

namespace GWYFVR.Patches
{
    /// <summary>
    /// Grab by touch: an item within reach of either hand becomes the target, so squeezing the grip on
    /// it picks it up without pointing the laser at it. Nothing in reach falls back to the laser.
    /// Only the local choice of target changes, so the pickup itself is the game's own.
    /// </summary>
    [HarmonyPatch(typeof(PlayerInteract), nameof(PlayerInteract.RaycastInteractable))]
    internal static class ProximityGrabPatch
    {
        /// <summary>How far (m) from the controller an item can be to count as touched.</summary>
        private const float Reach = 0.15f;

        private static readonly Collider[] Hits = new Collider[32];

        [HarmonyPriority(Priority.First)]
        private static bool Prefix(PlayerInteract __instance)
        {
            var rig = VRRig.Instance;
            if (!Plugin.Settings.ProximityGrab.Value || rig == null || !rig.PlayerMode || __instance._pc == null || __instance._pc != LocalPlayer.Controller)
                return true;

            Item best = null;
            var bestDistance = Reach;
            Nearest(rig.transform, XRNode.LeftHand, __instance.raycastLayer, ref best, ref bestDistance);
            Nearest(rig.transform, XRNode.RightHand, __instance.raycastLayer, ref best, ref bestDistance);
            if (best == null)
                return true;

            __instance.SetTargetInteractable(best);
            return false;
        }

        private static void Nearest(Transform rig, XRNode hand, LayerMask layers, ref Item best, ref float bestDistance)
        {
            if (!XRControllers.TryGetAimPose(hand, out var position, out _))
                return;
            var point = rig.TransformPoint(position);

            var count = Physics.OverlapSphereNonAlloc(point, Reach, Hits, layers, QueryTriggerInteraction.Collide);
            for (var i = 0; i < count; i++)
            {
                var collider = Hits[i];
                var item = collider.GetComponentInParent<Item>();
                if (item == null || item.NetworkHolder != null || item.isInPocket)
                    continue;

                var distance = Vector3.Distance(point, collider.bounds.ClosestPoint(point));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = item;
                }
            }
        }
    }
}
