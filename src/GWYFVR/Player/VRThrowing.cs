using System.Collections.Generic;
using GWYFVR.Input;
using UnityEngine;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace GWYFVR.Player
{
    /// <summary>
    /// Physical throwing: while holding an item, keep the right grip held; swing your arm and let go of
    /// the grip to throw the item with your hand's speed and direction. Replaces the game's
    /// "hold a button to charge, release to throw" (which still works on the left grip).
    /// </summary>
    public class VRThrowing : MonoBehaviour
    {
        private const int VelocitySamples = 5;

        /// <summary>Below this hand speed (m/s) letting go just drops the item in front of you.</summary>
        private const float DropSpeed = 0.6f;

        private readonly Queue<Vector3> velocities = new Queue<Vector3>();
        private bool gripWasHeld;
        private float heldSince;

        private void Update()
        {
            if (!Plugin.Settings.PhysicalThrowing.Value)
                return;

            var rig = VRRig.Instance;
            var inventory = LocalPlayer.Controller != null ? LocalPlayer.Controller.GetComponent<PlayerInventory>() : null;
            if (rig == null || !rig.PlayerMode || inventory == null)
            {
                velocities.Clear();
                return;
            }

            SampleHandVelocity(rig.transform);

            XRControllers.Poll();
            var grip = XRControllers.Right.Grip;
            var holding = inventory.NetworkholdingItem != null && !inventory._localAlreadyThrown;

            if (grip && !gripWasHeld)
                heldSince = Time.time;

            // Let go of the grip while holding an item: throw it. The short delay avoids throwing the item
            // on the same grip press that picked it up if the grip is released straight away.
            if (!grip && gripWasHeld && holding && Time.time - heldSince > 0.15f)
                Throw(inventory);

            gripWasHeld = grip;
        }

        private void SampleHandVelocity(Transform origin)
        {
            var device = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            if (!device.isValid || !device.TryGetFeatureValue(XRCommonUsages.deviceVelocity, out var velocity))
                return;

            velocities.Enqueue(origin.rotation * velocity);
            while (velocities.Count > VelocitySamples)
                velocities.Dequeue();
        }

        private Vector3 HandVelocity()
        {
            if (velocities.Count == 0)
                return Vector3.zero;

            // Use the fastest recent sample: the release frame itself is often already slowing down.
            var best = Vector3.zero;
            foreach (var v in velocities)
                if (v.sqrMagnitude > best.sqrMagnitude)
                    best = v;
            return best;
        }

        private static void Throw(PlayerInventory inventory)
        {
            var item = inventory.NetworkholdingItem;
            var settings = inventory._ps;
            var handVelocity = new Vector3();
            var thrower = VRRig.Instance.GetComponent<VRThrowing>();
            if (thrower != null)
                handVelocity = thrower.HandVelocity();

            var speed = handVelocity.magnitude * Plugin.Settings.ThrowStrength.Value;
            var direction = speed > DropSpeed ? handVelocity.normalized : Vector3.down;
            if (speed <= DropSpeed)
                speed = 0.5f;

            // The game computes velocity = playerVelocity + direction * force / (mass + constantMass).
            var massFactor = item.Mass + settings.constantMass;
            var force = speed * massFactor;
            var torque = Mathf.Lerp(settings.minItemThrowTorque, settings.maxItemThrowTorque, Mathf.Clamp01(speed / 10f));

            // Launch from the hand rather than in front of the face (applies when we are the host).
            var hand = VRHands.Instance != null ? VRHands.Instance.RightHandPosition : (Vector3?)null;
            if (hand.HasValue && inventory.throwPosition != null)
                inventory.throwPosition.position = hand.Value;

            inventory._localAlreadyThrown = true;
            inventory.StopThrowRoutine();
            item.OnLocalDrop();
            inventory.CmdThrowItem(inventory._rigidbody.linearVelocity, direction, force, torque);
            inventory.OnItemThrown(force, item);
            inventory.CmdOnItemThrown(force, item);

            Plugin.Log.LogDebug($"VR throw at {speed:0.0} m/s");
        }
    }
}
