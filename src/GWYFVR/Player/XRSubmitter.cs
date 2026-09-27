using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR;

namespace GWYFVR.Player
{
    /// <summary>
    /// Renders the eyes and hands them to the headset ourselves.
    ///
    /// The game ships its render pipeline compiled without XR (URP's XRSystem is a stub), so the
    /// pipeline never draws into the headset's swapchain. Every frame this renders an eye camera once
    /// per XR render parameter, with the view and projection the runtime asks for, into a texture and
    /// blits that into the display subsystem's render target.
    /// </summary>
    public class XRSubmitter : MonoBehaviour
    {
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private readonly Dictionary<int, RenderTexture> eyeTextures = new Dictionary<int, RenderTexture>();
        private Camera eyeCamera;
        private CommandBuffer blit;
        private bool loggedLayout;

        public int FramesSubmitted { get; private set; }

        /// <summary>Dev: file to save the next frame's first swapchain image to (read back from the headset target).</summary>
        public static string CaptureFile;

        private void Awake()
        {
            var go = new GameObject("VREyeCamera");
            go.transform.SetParent(transform, false);
            eyeCamera = go.AddComponent<Camera>();
            eyeCamera.enabled = false;
            eyeCamera.stereoTargetEye = StereoTargetEyeMask.None;
            XRDevice.DisableAutoXRCameraTracking(eyeCamera, true);

            blit = new CommandBuffer { name = "GWYFVR eye blit" };

            // After the rig has placed itself for this frame (VRRig registers first).
            Application.onBeforeRender += Submit;
        }

        private void OnDestroy()
        {
            Application.onBeforeRender -= Submit;
            foreach (var rt in eyeTextures.Values)
                if (rt != null)
                    rt.Release();
        }

        private void Submit()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.Source == null)
                return;

            displays.Clear();
            SubsystemManager.GetSubsystems(displays);
            var display = displays.Count > 0 ? displays[0] : null;
            if (display == null || !display.running)
                return;

            var template = rig.VRCamera;
            CopySettings(template, eyeCamera);
            var overlays = eyeCamera.GetUniversalAdditionalCameraData().cameraStack;

            var passCount = display.GetRenderPassCount();
            for (var p = 0; p < passCount; p++)
            {
                display.GetRenderPass(p, out var pass);

                for (var i = 0; i < pass.GetRenderParameterCount(); i++)
                {
                    pass.GetRenderParameter(eyeCamera, i, out var param);

                    if (!loggedLayout)
                        Plugin.Log.LogInfo($"XR pass {p} param {i}: {pass.renderTargetDesc.width}x{pass.renderTargetDesc.height} " +
                                           $"dim {pass.renderTargetDesc.dimension} slice {param.textureArraySlice} viewport {param.viewport}");

                    var target = EyeTexture(p * 4 + i, pass.renderTargetDesc);
                    PlaceEye(rig.transform, param);

                    eyeCamera.targetTexture = target;
                    foreach (var overlay in overlays)
                    {
                        if (overlay == null)
                            continue;
                        overlay.transform.SetPositionAndRotation(eyeCamera.transform.position, eyeCamera.transform.rotation);
                        overlay.projectionMatrix = param.projection;
                    }

                    eyeCamera.Render();

                    blit.Clear();
                    var slice = param.textureArraySlice >= 0 ? param.textureArraySlice : 0;
                    blit.Blit(target, pass.renderTarget, 0, slice);
                    Graphics.ExecuteCommandBuffer(blit);

                    if (CaptureFile != null && p == 0 && i == 0)
                        CaptureTarget(pass, slice);
                }
            }

            loggedLayout = true;
            eyeCamera.targetTexture = null;

            // The overlay cameras are shared with the desktop view, put them back.
            foreach (var overlay in overlays)
            {
                if (overlay == null)
                    continue;
                overlay.ResetProjectionMatrix();
                overlay.transform.SetPositionAndRotation(template.transform.position, template.transform.rotation);
            }

            FramesSubmitted++;
        }

        private static void CaptureTarget(XRDisplaySubsystem.XRRenderPass pass, int slice)
        {
            var file = CaptureFile;
            CaptureFile = null;

            var rt = RenderTexture.GetTemporary(pass.renderTargetDesc.width, pass.renderTargetDesc.height, 0, RenderTextureFormat.ARGB32);
            var cmd = new CommandBuffer();
            cmd.Blit(pass.renderTarget, rt, slice, 0);
            Graphics.ExecuteCommandBuffer(cmd);

            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = previous;
            System.IO.File.WriteAllBytes(file, tex.EncodeToPNG());
            Destroy(tex);
            RenderTexture.ReleaseTemporary(rt);
            Plugin.Log.LogInfo($"Headset target saved to {file}");
        }

        /// <summary>The runtime's view matrix is relative to the tracking origin, which is the rig.</summary>
        private void PlaceEye(Transform origin, XRDisplaySubsystem.XRRenderParameter param)
        {
            var eyeToWorld = origin.localToWorldMatrix * param.view.inverse;
            var position = (Vector3)eyeToWorld.GetColumn(3);
            var forward = -(Vector3)eyeToWorld.GetColumn(2);
            var up = (Vector3)eyeToWorld.GetColumn(1);

            eyeCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(forward, up));
            eyeCamera.worldToCameraMatrix = param.view * origin.worldToLocalMatrix;
            eyeCamera.projectionMatrix = param.projection;
        }

        private RenderTexture EyeTexture(int key, RenderTextureDescriptor targetDesc)
        {
            eyeTextures.TryGetValue(key, out var rt);
            if (rt != null && rt.width == targetDesc.width && rt.height == targetDesc.height)
                return rt;

            if (rt != null)
                rt.Release();

            var desc = new RenderTextureDescriptor(targetDesc.width, targetDesc.height, RenderTextureFormat.ARGB32, 24)
            {
                sRGB = targetDesc.sRGB,
                msaaSamples = 1,
            };
            rt = new RenderTexture(desc) { name = $"GWYFVR Eye {key}" };
            rt.Create();
            eyeTextures[key] = rt;
            return rt;
        }

        private static void CopySettings(Camera from, Camera to)
        {
            to.clearFlags = from.clearFlags;
            to.backgroundColor = from.backgroundColor;
            // Camera.Render() doesn't draw URP overlay stacks reliably, so draw the VR UI layer directly.
            to.cullingMask = from.cullingMask | (1 << UI.WorldSpaceUI.Layer);
            to.nearClipPlane = from.nearClipPlane;
            to.farClipPlane = from.farClipPlane;
            to.allowHDR = from.allowHDR;
            to.allowMSAA = false;
            to.useOcclusionCulling = from.useOcclusionCulling;

            var src = from.GetUniversalAdditionalCameraData();
            var dst = to.GetUniversalAdditionalCameraData();
            dst.renderType = CameraRenderType.Base;
            dst.renderPostProcessing = src.renderPostProcessing;
            dst.antialiasing = src.antialiasing;
            dst.renderShadows = src.renderShadows;
            dst.volumeLayerMask = src.volumeLayerMask;
            dst.volumeTrigger = to.transform;
            VRRig.CopyRenderer(src, dst);
            dst.cameraStack.Clear();
            dst.cameraStack.AddRange(src.cameraStack);
        }
    }
}
