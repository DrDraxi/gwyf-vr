using HarmonyLib;
using UnityEngine;
using UnityEngine.XR;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace GWYFVR.Player
{
    /// <summary>
    /// Losing an eye at the body part machine darkens half of the flat screen with a vignette (a
    /// post-processing effect, which doesn't work in VR). In VR, a black patch sits right in front of the
    /// missing eye instead, so that eye really sees nothing, like wearing an eye patch.
    /// </summary>
    public class VREyePatches : MonoBehaviour
    {
        private const float Distance = 0.022f;
        private const float Size = 0.07f;

        public static bool LeftBlind;
        public static bool RightBlind;

        private Transform leftPatch;
        private Transform rightPatch;

        private void Awake()
        {
            // Drawn by the UI overlay camera, after the UI, so nothing (menus, outlines) shows through.
            var material = UI.VRMaterials.Unlit(Color.black);
            material.renderQueue = 5000;
            if (material.HasProperty("_ZTest"))
                material.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);
            leftPatch = CreatePatch("VRLeftEyePatch", material);
            rightPatch = CreatePatch("VRRightEyePatch", material);
        }

        private Transform CreatePatch(string name, Material material)
        {
            var patch = GameObject.CreatePrimitive(PrimitiveType.Quad).transform;
            patch.name = name;
            Destroy(patch.GetComponent<Collider>());
            patch.SetParent(transform, false);
            patch.localScale = Vector3.one * Size;
            var renderer = patch.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            patch.gameObject.layer = UI.WorldSpaceUI.Layer;
            patch.gameObject.SetActive(false);
            return patch;
        }

        private void LateUpdate()
        {
            var rig = VRRig.Instance;
            var active = rig != null && rig.PlayerMode;
            Place(leftPatch, active && LeftBlind, XRCommonUsages.leftEyePosition, -1f, rig);
            Place(rightPatch, active && RightBlind, XRCommonUsages.rightEyePosition, 1f, rig);
        }

        /// <summary>
        /// The patch is a child of the headset camera, so it moves exactly with your head in the same
        /// frame (placing it in world space made it lag behind turns and walking).
        /// </summary>
        private static void Place(Transform patch, bool show, InputFeatureUsage<Vector3> eyeUsage, float side, VRRig rig)
        {
            patch.gameObject.SetActive(show);
            if (!show)
                return;

            var head = rig.VRCamera.transform;
            if (patch.parent != head)
                patch.SetParent(head, false);

            // Eye offset from the centre of the head, in head space.
            var offset = new Vector3(0.032f * side, 0f, 0f);
            var hmd = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (hmd.isValid && hmd.TryGetFeatureValue(eyeUsage, out var eye) &&
                hmd.TryGetFeatureValue(XRCommonUsages.centerEyePosition, out var center) &&
                hmd.TryGetFeatureValue(XRCommonUsages.centerEyeRotation, out var rotation))
                offset = Quaternion.Inverse(rotation) * (eye - center);

            patch.localPosition = offset + Vector3.forward * Distance;
            patch.localRotation = Quaternion.identity;
            patch.localScale = Vector3.one * Size;
        }
    }

    [HarmonyPatch]
    internal static class EyeUIPatches
    {
        [HarmonyPatch(typeof(PlayerEyesUI), nameof(PlayerEyesUI.ToggleEye))]
        [HarmonyPostfix]
        private static void TrackEyes(bool isRightEye, bool isEnabled)
        {
            if (isRightEye)
                VREyePatches.RightBlind = !isEnabled;
            else
                VREyePatches.LeftBlind = !isEnabled;
        }
    }
}
