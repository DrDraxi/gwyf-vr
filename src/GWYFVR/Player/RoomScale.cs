using GWYFVR.Input;
using UnityEngine;

namespace GWYFVR.Player
{
    /// <summary>
    /// Physical movement: walking or leaning in your room moves the player's body by the same amount.
    /// The view always stays locked to the player's head (see VRRig), so without this, stepping around
    /// in the real world doesn't move you, which quickly feels wrong. Movement is swept against the
    /// world, so you can't walk through walls; the blocked part is simply dropped.
    /// </summary>
    public class RoomScale : MonoBehaviour
    {
        /// <summary>Ignore tracking jumps bigger than this in one frame (tracking loss, recentering).</summary>
        private const float MaxStep = 0.5f;

        private const float Skin = 0.02f;

        private Vector3? lastHeadPosition;
        private Vector3 pending;

        private void Update()
        {
            var rig = VRRig.Instance;
            var controller = LocalPlayer.Controller;
            if (!Plugin.Settings.RoomScale.Value || rig == null || !rig.PlayerMode || controller == null ||
                controller.IsLocked || controller.State != PlayerController.PlayerState.Free || !controller.hasBody)
            {
                lastHeadPosition = null;
                pending = Vector3.zero;
                return;
            }

            var head = VRInput.ReadPosition(VRInput.HeadPosition);
            head.y = 0f;

            if (lastHeadPosition.HasValue)
            {
                var delta = rig.transform.rotation * (head - lastHeadPosition.Value);
                delta.y = 0f;
                if (delta.magnitude < MaxStep)
                    pending += delta;
            }

            lastHeadPosition = head;
        }

        private void FixedUpdate()
        {
            if (pending.sqrMagnitude < 1e-8f)
                return;

            var controller = LocalPlayer.Controller;
            var body = controller != null ? controller._rb : null;
            if (body == null)
            {
                pending = Vector3.zero;
                return;
            }

            var distance = pending.magnitude;
            var direction = pending / distance;
            var move = pending;

            if (body.SweepTest(direction, out var hit, distance + Skin, QueryTriggerInteraction.Ignore))
                move = direction * Mathf.Max(0f, hit.distance - Skin);

            body.position += move;
            pending = Vector3.zero;
        }
    }
}
