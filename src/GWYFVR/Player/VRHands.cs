using GWYFVR.Input;
using UnityEngine;
using UnityEngine.XR;

namespace GWYFVR.Player
{
    /// <summary>
    /// Makes the local player's first person hands follow the VR controllers.
    ///
    /// The game's hands are one animated rig under the player's head
    /// (Hands/HandBobbing/Armature/Bone.005/Bone.L and Bone.R, skinned by PlayerHands). The Animator
    /// still animates the fingers; after it ran, each hand bone is moved onto its controller. Held
    /// items live on Armature/HandTransform, which is moved onto the right hand.
    /// </summary>
    public class VRHands : MonoBehaviour
    {
        public static VRHands Instance { get; private set; }

        /// <summary>Rotation from the controller grip pose to a hand bone (fingers are the bone's +Y).</summary>
        public static Vector3 RotationOffset
        {
            get => Plugin.Settings.HandRotation.Value;
            set => Plugin.Settings.HandRotation.Value = value;
        }

        /// <summary>Hand bone position relative to the controller grip, in the controller's space (meters).</summary>
        public static Vector3 PositionOffset
        {
            get => Plugin.Settings.HandPosition.Value;
            set => Plugin.Settings.HandPosition.Value = value;
        }

        /// <summary>Held item position relative to the right controller.</summary>
        public static Vector3 ItemOffset
        {
            get => Plugin.Settings.ItemOffset.Value;
            set => Plugin.Settings.ItemOffset.Value = value;
        }

        public bool Active { get; private set; }

        /// <summary>World position of the hand holding the item, while the hands are active.</summary>
        public Vector3? HoldingHandPosition
        {
            get
            {
                var bone = HandRoles.HoldingLeft ? leftBone : rightBone;
                return Active && bone != null ? bone.position : (Vector3?)null;
            }
        }

        private PlayerHead boundHead;
        private Transform leftBone;
        private Transform rightBone;
        private Transform itemHolder;
        private Transform throwPosition;
        private PlayerInventory inventory;
        private readonly System.Collections.Generic.List<(Transform bone, Quaternion open)> openFingers =
            new System.Collections.Generic.List<(Transform, Quaternion)>();
        private bool fingersCaptured;
        private Item scaledItem;
        private Vector3 scaledItemOriginalScale;

        private void Awake()
        {
            Instance = this;
            Application.onBeforeRender += Apply;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= Apply;
        }

        private void LateUpdate() => Apply();

        private void Bind()
        {
            boundHead = LocalPlayer.Head;
            leftBone = rightBone = itemHolder = throwPosition = null;
            inventory = null;
            openFingers.Clear();
            fingersCaptured = false;
            if (boundHead == null)
                return;

            var armature = boundHead.transform.Find("Hands/HandBobbing/Armature");
            if (armature == null)
            {
                Plugin.Log.LogWarning("Could not find the player's hand rig, VR hands disabled");
                return;
            }

            leftBone = armature.Find("Bone.005/Bone.L");
            rightBone = armature.Find("Bone.005/Bone.R");
            itemHolder = armature.Find("HandTransform");
            inventory = boundHead.GetComponentInParent<PlayerInventory>();
            throwPosition = inventory != null ? inventory.throwPosition : null;

            // The hands now move well away from where the mesh's bounds expect them; don't cull them.
            foreach (var skin in armature.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;
            Plugin.Log.LogInfo($"Bound VR hands (left {leftBone != null}, right {rightBone != null}, items {itemHolder != null})");
        }

        private void Apply()
        {
            if (LocalPlayer.Head != boundHead)
                Bind();

            var rig = VRRig.Instance;
            Active = rig != null && rig.PlayerMode && leftBone != null && rightBone != null;
            if (!Active)
                return;

            var leftTracked = PlaceHand(leftBone, rig.transform, XRNode.LeftHand, true);
            var rightTracked = PlaceHand(rightBone, rig.transform, XRNode.RightHand, false);

            // The held item and its launch point follow the hand that picked it up.
            var holdingLeft = HandRoles.HoldingLeft;
            var holdingNode = holdingLeft ? XRNode.LeftHand : XRNode.RightHand;
            var holdingBone = holdingLeft ? leftBone : rightBone;
            var holdingTracked = holdingLeft ? leftTracked : rightTracked;

            if (itemHolder != null && holdingTracked &&
                XRControllers.TryGetAimPose(holdingNode, out var position, out var rotation))
            {
                var worldRotation = rig.transform.rotation * rotation;
                var offset = holdingLeft ? new Vector3(-ItemOffset.x, ItemOffset.y, ItemOffset.z) : ItemOffset;
                itemHolder.SetPositionAndRotation(
                    rig.transform.TransformPoint(position) + worldRotation * offset,
                    worldRotation * Quaternion.Euler(90f, 0f, 0f));
            }

            // Items launch from the throw position: keep it on the holding hand so throws leave the hand.
            if (throwPosition != null && holdingTracked)
                throwPosition.SetPositionAndRotation(holdingBone.position, itemHolder != null ? itemHolder.rotation : holdingBone.rotation);

            KeepFreeHandOpen();
            ScaleHeldItem();

            Active = leftTracked || rightTracked;
        }

        /// <summary>
        /// The game animates both hands gripping a held item. In VR one hand holds it, so while holding,
        /// put the other hand's fingers back into their empty-handed pose.
        /// </summary>
        private void KeepFreeHandOpen()
        {
            var holding = inventory != null && inventory.NetworkholdingItem != null;
            if (!holding)
            {
                // Remember what both hands' fingers look like when empty.
                openFingers.Clear();
                foreach (var hand in new[] { leftBone, rightBone })
                    foreach (var bone in hand.GetComponentsInChildren<Transform>())
                        if (bone != hand)
                            openFingers.Add((bone, bone.localRotation));
                fingersCaptured = true;
                return;
            }

            if (!fingersCaptured)
                return;

            var freeHand = HandRoles.HoldingLeft ? rightBone : leftBone;
            foreach (var (bone, open) in openFingers)
                if (bone != null && bone.IsChildOf(freeHand))
                    bone.localRotation = open;
        }

        /// <summary>Shrink the item model while it is held; restore it as soon as it isn't.</summary>
        private void ScaleHeldItem()
        {
            var held = inventory != null ? inventory.NetworkholdingItem : null;
            if (held == scaledItem)
            {
                if (held != null && held.modelTransform != null)
                    held.modelTransform.localScale = scaledItemOriginalScale * Plugin.Settings.HeldItemScale.Value;
                return;
            }

            if (scaledItem != null && scaledItem.modelTransform != null)
                scaledItem.modelTransform.localScale = scaledItemOriginalScale;

            scaledItem = held;
            if (held != null && held.modelTransform != null)
            {
                scaledItemOriginalScale = held.modelTransform.localScale;
                held.modelTransform.localScale = scaledItemOriginalScale * Plugin.Settings.HeldItemScale.Value;
            }
        }

        private static bool PlaceHand(Transform bone, Transform origin, XRNode node, bool left)
        {
            if (!XRControllers.TryGetAimPose(node, out var position, out var rotation))
                return false;

            var worldRotation = origin.rotation * rotation;
            var offset = left ? new Vector3(-PositionOffset.x, PositionOffset.y, PositionOffset.z) : PositionOffset;
            var euler = left ? new Vector3(RotationOffset.x, -RotationOffset.y, -RotationOffset.z) : RotationOffset;

            bone.SetPositionAndRotation(
                origin.TransformPoint(position) + worldRotation * offset,
                worldRotation * Quaternion.Euler(euler));
            bone.localScale = Vector3.one * Plugin.Settings.HandScale.Value;
            return true;
        }
    }
}
