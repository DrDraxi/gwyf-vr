using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR;

namespace GWYFVR.Player
{
    /// <summary>
    /// Shows the left eye in the game window. The runtime's own mirror only reaches the window in
    /// menus: in game it ends up black with the HUD on top. So at the end of every frame the left eye
    /// image is copied to the window, cropped to the window's shape.
    /// </summary>
    public class DesktopMirror : MonoBehaviour
    {
        private readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>();
        private readonly WaitForEndOfFrame endOfFrame = new WaitForEndOfFrame();
        private CommandBuffer cmd;

        private void OnEnable()
        {
            cmd = new CommandBuffer { name = "GWYFVR desktop mirror" };
            StartCoroutine(MirrorLoop());
        }

        private void OnDisable()
        {
            cmd?.Release();
            cmd = null;
        }

        private IEnumerator MirrorLoop()
        {
            while (true)
            {
                yield return endOfFrame;
                if (!Plugin.Settings.DesktopMirror.Value)
                    continue;

                displays.Clear();
                SubsystemManager.GetSubsystems(displays);
                if (displays.Count == 0 || !displays[0].running || displays[0].GetRenderPassCount() == 0)
                    continue;

                displays[0].GetRenderPass(0, out var pass);
                var desc = pass.renderTargetDesc;
                if (desc.width <= 0 || desc.height <= 0 || Screen.width <= 0 || Screen.height <= 0)
                    continue;

                // Fill the window: crop the eye image to the window's aspect ratio around its centre.
                var eyeAspect = (float)desc.width / desc.height;
                var screenAspect = (float)Screen.width / Screen.height;
                var scale = Vector2.one;
                if (screenAspect > eyeAspect)
                    scale.y = eyeAspect / screenAspect;
                else
                    scale.x = screenAspect / eyeAspect;
                var offset = (Vector2.one - scale) * 0.5f;

                // The eye image is stored upside down compared to what a blit to the window expects.
                offset.y += scale.y;
                scale.y = -scale.y;

                cmd.Clear();
                cmd.Blit(pass.renderTarget, BuiltinRenderTextureType.CameraTarget, scale, offset, 0, 0);
                Graphics.ExecuteCommandBuffer(cmd);
            }
        }
    }
}
