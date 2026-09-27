using System.Reflection;
using Extensions;
using GWYFVR.Input;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using UnityEngine.XR;

namespace GWYFVR.Player
{
    /// <summary>
    /// The VR camera. Instead of taking over the game's camera (which many game scripts move, animate,
    /// re-create or render into a texture) the rig follows whatever camera the game is currently using
    /// and renders from its position with the headset's orientation. The game's camera is disabled
    /// but left in place, so gameplay code that reads it keeps working.
    ///
    /// When the local player is in control, the headset drives the player's head instead: you walk
    /// where you look and interact with what you look at.
    /// </summary>
    public class VRRig : MonoBehaviour
    {
        public static VRRig Instance { get; private set; }

        /// <summary>The camera rendering to the headset.</summary>
        public Camera VRCamera { get; private set; }

        /// <summary>The game camera the rig is currently following.</summary>
        public Camera Source { get; private set; }

        /// <summary>True when the local player is in control and the headset drives their head.</summary>
        public bool PlayerMode { get; private set; }

        private float turnYaw;
        private bool snapLatched;
        private float nextSourceScan;

        private static readonly FieldInfo RendererIndexField =
            typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// With URP's XR system running the camera renders straight to the headset. Otherwise
        /// XRSubmitter renders the eyes and this camera only draws the desktop view.
        /// </summary>
        internal static StereoTargetEyeMask HeadsetEyes =>
            XR.URPXRSetup.Active ? StereoTargetEyeMask.Both : StereoTargetEyeMask.None;

        private void Awake()
        {
            Instance = this;

            var cameraObject = new GameObject("VRCamera");
            cameraObject.transform.SetParent(transform, false);
            VRCamera = cameraObject.AddComponent<Camera>();
            VRCamera.stereoTargetEye = HeadsetEyes;
            VRCamera.nearClipPlane = 0.02f;
            VRCamera.enabled = false;
            XRDevice.DisableAutoXRCameraTracking(VRCamera, true);

            Application.onBeforeRender += UpdatePose;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= UpdatePose;
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (Source == null || !Source.gameObject.activeInHierarchy || Time.unscaledTime >= nextSourceScan)
            {
                nextSourceScan = Time.unscaledTime + 0.5f;
                var candidate = FindSourceCamera();
                if (candidate != Source)
                    Adopt(candidate);
            }

            // Keep the game camera from rendering (flat) while we render it in VR.
            if (Source != null && Source.enabled)
                Source.enabled = false;

            PlayerMode = Source != null && LocalPlayer.Head != null && Source == LocalPlayer.Camera;

            HandleTurning();
        }

        private void LateUpdate()
        {
            UpdatePose();
        }

        private static Camera FindSourceCamera()
        {
            // The local player's camera, owned by the game's LocalManager.
            var local = MonoSingleton<LocalManager>.Instance;
            if (local != null && local.mainCamera != null && local.mainCamera.gameObject.activeInHierarchy)
                return local.mainCamera;

            // Otherwise the scene's main camera (menus, cutscenes, spectating). Disabled cameras are
            // skipped, except the one we disabled ourselves.
            Camera best = null;
            foreach (var cam in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None))
            {
                if (cam == Instance.VRCamera || !cam.gameObject.activeInHierarchy)
                    continue;
                if (!cam.enabled && cam != Instance.Source)
                    continue;
                if (cam.GetUniversalAdditionalCameraData().renderType == CameraRenderType.Overlay)
                    continue;

                if (best == null || cam.CompareTag("MainCamera") && !best.CompareTag("MainCamera") ||
                    cam.CompareTag("MainCamera") == best.CompareTag("MainCamera") && cam.depth > best.depth)
                    best = cam;
            }

            return best;
        }

        private void Adopt(Camera source)
        {
            if (Source != null && Source != source)
                Source.enabled = true;

            Source = source;

            if (source == null)
            {
                VRCamera.enabled = false;
                return;
            }

            Plugin.Log.LogInfo($"VR camera now follows '{source.name}'");

            if (source.targetTexture != null)
                HideTexturePreviews(source.targetTexture);

            CopyCameraSettings(source, VRCamera);
            VRCamera.enabled = true;
            source.enabled = false;

            if (LocalPlayer.Head != null && source == LocalPlayer.Camera)
                FaceYaw(LocalPlayer.Head.transform.eulerAngles.y);
        }

        private static void CopyCameraSettings(Camera from, Camera to)
        {
            to.clearFlags = from.clearFlags;
            to.backgroundColor = from.backgroundColor;
            to.cullingMask = from.cullingMask & ~(1 << UI.WorldSpaceUI.Layer);
            to.nearClipPlane = Mathf.Min(from.nearClipPlane, 0.05f);
            to.farClipPlane = from.farClipPlane;
            to.depth = from.depth;
            to.allowHDR = from.allowHDR;
            to.allowMSAA = from.allowMSAA;
            to.useOcclusionCulling = from.useOcclusionCulling;
            to.targetTexture = null;
            to.fieldOfView = from.fieldOfView;
            to.stereoTargetEye = HeadsetEyes;

            var src = from.GetUniversalAdditionalCameraData();
            var dst = to.GetUniversalAdditionalCameraData();
            dst.renderType = CameraRenderType.Base;
            dst.renderPostProcessing = src.renderPostProcessing;
            dst.antialiasing = src.antialiasing;
            dst.antialiasingQuality = src.antialiasingQuality;
            dst.renderShadows = src.renderShadows;
            dst.requiresColorOption = src.requiresColorOption;
            dst.requiresDepthOption = src.requiresDepthOption;
            dst.volumeLayerMask = src.volumeLayerMask;
            dst.volumeTrigger = to.transform;
            dst.stopNaN = src.stopNaN;
            dst.dithering = src.dithering;
            dst.allowXRRendering = true;

            CopyRenderer(src, dst);

            dst.cameraStack.Clear();
            foreach (var overlay in src.cameraStack)
                if (overlay != null)
                    dst.cameraStack.Add(overlay);

            UI.WorldSpaceUI.Instance?.AttachOverlayCamera(to);
        }

        internal static void CopyRenderer(UniversalAdditionalCameraData from, UniversalAdditionalCameraData to)
        {
            if (RendererIndexField != null)
                RendererIndexField.SetValue(to, RendererIndexField.GetValue(from));
        }

        /// <summary>
        /// The game renders its camera into a texture shown on a full screen RawImage. In VR that image
        /// would float in front of you with a stale frame, so hide it.
        /// </summary>
        private static void HideTexturePreviews(Texture texture)
        {
            foreach (var image in Object.FindObjectsByType<RawImage>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (image.texture != texture)
                    continue;

                image.enabled = false;
                Plugin.Log.LogInfo($"Hid flat screen preview '{image.name}'");
            }
        }

        private void HandleTurning()
        {
            if (VRInput.Turn == null)
                return;

            var x = VRInput.Turn.ReadValue<Vector2>().x;

            if (Plugin.Settings.Turning.Value == TurnMode.Smooth)
            {
                if (Mathf.Abs(x) > 0.2f)
                    turnYaw += x * Plugin.Settings.SmoothTurnSpeed.Value * Time.unscaledDeltaTime;
                return;
            }

            if (!snapLatched && Mathf.Abs(x) > 0.75f)
            {
                snapLatched = true;
                turnYaw += Mathf.Sign(x) * Plugin.Settings.SnapTurnAngle.Value;
            }
            else if (snapLatched && Mathf.Abs(x) < 0.3f)
            {
                snapLatched = false;
            }
        }

        /// <summary>
        /// Turn the rig so the headset faces the given world yaw, used when the game rotates the player.
        /// </summary>
        public void FaceYaw(float worldYaw)
        {
            var headYaw = VRInput.HeadRotation != null ? VRInput.ReadRotation(VRInput.HeadRotation).eulerAngles.y : 0f;
            turnYaw = worldYaw - headYaw;
        }

        private void UpdatePose()
        {
            if (Source == null || VRInput.HeadPosition == null)
                return;

            var headPos = VRInput.ReadPosition(VRInput.HeadPosition);
            var headRot = VRInput.ReadRotation(VRInput.HeadRotation);

            // With the player in control only the turn offset applies, the headset drives the head.
            // Otherwise (menus, cutscenes) the game camera's view becomes "straight ahead", including
            // its pitch, so top-down menu cameras still show what they point at. Roll is dropped.
            var rigRotation = Quaternion.Euler(0f, turnYaw, 0f);
            if (!PlayerMode)
            {
                var source = Source.transform.eulerAngles;
                rigRotation = Quaternion.Euler(source.x, source.y + turnYaw, 0f);
            }

            // Put the headset's eye exactly where the game camera is. Physical movement is not added on
            // top, which keeps the view and the player's body in sync.
            transform.SetPositionAndRotation(Source.transform.position - rigRotation * headPos, rigRotation);
            VRCamera.transform.SetLocalPositionAndRotation(headPos, headRot);
        }
    }
}
