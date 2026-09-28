using GWYFVR.Input;
using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace GWYFVR.Player
{
    /// <summary>
    /// Swing the bat for real: while you hold it, swinging the hand fast enough turns on the bat's hit
    /// area for as long as the swing lasts, so whatever the bat passes through gets hit. The game's own
    /// trigger swing still works. Hits go through the game's normal commands, so cross-play is unaffected.
    /// </summary>
    public class VRBatSwing : MonoBehaviour
    {
        /// <summary>Estimated speed (m/s) of the bat's head that starts a swing.</summary>
        private const float StartSpeed = 3.5f;

        /// <summary>Below this the swing is over.</summary>
        private const float StopSpeed = 2f;

        /// <summary>Distance from the hand to the bat's head, for turning wrist rotation into speed.</summary>
        private const float BatLength = 0.6f;

        /// <summary>Pause between swings, so a shaky hand doesn't count as many swings.</summary>
        private const float Cooldown = 0.25f;

        private Bat bat;
        private bool swinging;
        private float nextSwing;

        private void Awake()
        {
            bat = GetComponent<Bat>();
        }

        private void Update()
        {
            var holder = bat != null ? bat.NetworkHolder : null;
            var rig = VRRig.Instance;
            if (holder == null || !holder.isLocalPlayer || bat.isInPocket || rig == null || !rig.PlayerMode)
            {
                EndSwing();
                return;
            }

            // The game's trigger swing drives the hit area itself.
            if (bat._isUsing)
            {
                swinging = false;
                return;
            }

            var speed = HeadSpeed();
            if (!swinging && speed >= StartSpeed && Time.time >= nextSwing)
            {
                swinging = true;
                bat.hitPoint.enabled = true;
                bat.CmdPlayBatSFX(0);
            }
            else if (swinging && speed < StopSpeed)
            {
                EndSwing();
            }
        }

        private void OnDisable()
        {
            EndSwing();
        }

        private void EndSwing()
        {
            if (!swinging)
                return;
            swinging = false;
            nextSwing = Time.time + Cooldown;
            if (bat != null && !bat._isUsing)
                bat.hitPoint.enabled = false;
        }

        /// <summary>Hand speed plus what the wrist's rotation adds at the bat's head.</summary>
        private static float HeadSpeed()
        {
            var device = InputDevices.GetDeviceAtXRNode(HandRoles.HoldingHand);
            if (!device.isValid || !device.TryGetFeatureValue(XRCommonUsages.deviceVelocity, out var velocity))
                return 0f;
            device.TryGetFeatureValue(XRCommonUsages.deviceAngularVelocity, out var angular);
            return velocity.magnitude + angular.magnitude * BatLength;
        }
    }

    [HarmonyPatch]
    internal static class VRBatSwingPatches
    {
        /// <summary>Runs on every client when someone picks a bat up; the component only acts for your own.</summary>
        [HarmonyPatch(typeof(Bat), nameof(Bat.UserCode_RpcOnPickedUp))]
        [HarmonyPostfix]
        private static void AddSwing(Bat __instance)
        {
            if (__instance.GetComponent<VRBatSwing>() == null)
                __instance.gameObject.AddComponent<VRBatSwing>();
        }
    }
}
