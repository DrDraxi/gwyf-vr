using System;
using System.IO;
using System.Text;
using BepInEx;
using GWYFVR.Player;
using GWYFVR.UI;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace GWYFVR
{
    /// <summary>
    /// Development helper, off by default. When enabled, write a command to
    /// BepInEx/gwyfvr-command.txt and the mod runs it on the next frame:
    ///   screenshot [file]  saves the desktop mirror to BepInEx/gwyfvr-screenshot.png (or file)
    ///   dump               logs the VR camera, source camera and converted canvases
    ///   capture [file]     renders what the headset sees (one eye, with UI) to BepInEx/gwyfvr-capture.png
    ///   buttons            logs every clickable button
    ///   click text         clicks the first active button whose name or label contains text (=name for exact)
    /// </summary>
    internal class DevCommands : MonoBehaviour
    {
        private static string CommandFile => Path.Combine(Paths.BepInExRootPath, "gwyfvr-command.txt");

        private float nextCheck;

        private void Update()
        {
            if (Time.unscaledTime < nextCheck)
                return;
            nextCheck = Time.unscaledTime + 0.5f;

            if (!File.Exists(CommandFile))
                return;

            string[] lines;
            try
            {
                lines = File.ReadAllLines(CommandFile);
                File.Delete(CommandFile);
            }
            catch (IOException)
            {
                return;
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                try
                {
                    Run(line.Trim().Split(new[] { ' ' }, 2));
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogError($"Dev command '{line}' failed: {ex}");
                }
            }
        }

        private static void Run(string[] args)
        {
            switch (args[0])
            {
                case "screenshot":
                    var file = args.Length > 1 ? args[1] : Path.Combine(Paths.BepInExRootPath, "gwyfvr-screenshot.png");
                    ScreenCapture.CaptureScreenshot(file);
                    Plugin.Log.LogInfo($"Screenshot saved to {file}");
                    break;

                case "capture":
                    Capture(args.Length > 1 ? args[1] : Path.Combine(Paths.BepInExRootPath, "gwyfvr-capture.png"));
                    break;

                case "buttons":
                    foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                        if (button.isActiveAndEnabled)
                            Plugin.Log.LogInfo($"Button '{button.name}' label '{Label(button)}' interactable {button.interactable}");
                    break;

                case "click":
                    Click(args.Length > 1 ? args[1] : "");
                    break;

                case "dump":
                    Plugin.Log.LogInfo(Dump());
                    break;

                default:
                    Plugin.Log.LogWarning($"Unknown dev command '{args[0]}'");
                    break;
            }
        }

        private static string Label(Component button)
        {
            var tmp = button.GetComponentInChildren<TMP_Text>();
            if (tmp != null)
                return tmp.text;
            var text = button.GetComponentInChildren<Text>();
            return text != null ? text.text : "";
        }

        private static void Click(string query)
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
            {
                if (!button.isActiveAndEnabled || !button.interactable)
                    continue;
                var exact = query.StartsWith("=");
                var matches = exact
                    ? button.name == query.Substring(1)
                    : button.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                      Label(button).IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
                if (!matches)
                    continue;

                Plugin.Log.LogInfo($"Clicking '{button.name}'");
                var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerEnterHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerDownHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerUpHandler);
                ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
                return;
            }

            Plugin.Log.LogWarning($"No button matching '{query}'");
        }

        /// <summary>Render the headset view from the VR camera pose into a PNG, without touching the XR output.</summary>
        private static void Capture(string file)
        {
            var rig = VRRig.Instance;
            if (rig == null)
                return;

            var rt = RenderTexture.GetTemporary(1280, 720, 24);
            var go = new GameObject("GWYFVR Capture");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.CopyFrom(rig.VRCamera);
                cam.stereoTargetEye = StereoTargetEyeMask.None;
                cam.fieldOfView = 90f;
                cam.cullingMask |= 1 << WorldSpaceUI.Layer;
                cam.targetTexture = rt;
                cam.transform.SetPositionAndRotation(rig.VRCamera.transform.position, rig.VRCamera.transform.rotation);
                cam.Render();

                var previous = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = previous;

                File.WriteAllBytes(file, tex.EncodeToPNG());
                Destroy(tex);
                Plugin.Log.LogInfo($"Capture saved to {file}");
            }
            finally
            {
                Destroy(go);
                RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static string Dump()
        {
            var sb = new StringBuilder("State dump\n");
            sb.AppendLine($"Scene: {SceneManager.GetActiveScene().name}");

            var rig = VRRig.Instance;
            if (rig != null)
            {
                sb.AppendLine($"Rig pos {rig.transform.position} rot {rig.transform.eulerAngles}, player mode {rig.PlayerMode}");
                sb.AppendLine($"VR camera pos {rig.VRCamera.transform.position} rot {rig.VRCamera.transform.eulerAngles} mask {rig.VRCamera.cullingMask:X}");
                if (rig.Source != null)
                    sb.AppendLine($"Source '{rig.Source.name}' pos {rig.Source.transform.position} rot {rig.Source.transform.eulerAngles} rt {rig.Source.targetTexture}");
            }

            foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
                sb.AppendLine($"Camera '{cam.name}' enabled {cam.enabled} depth {cam.depth} target {cam.targetTexture} eye {cam.stereoTargetEye}");

            if (WorldSpaceUI.Instance != null)
                foreach (var canvas in WorldSpaceUI.Instance.Canvases)
                    if (canvas != null)
                        sb.AppendLine($"Canvas '{canvas.name}' active {canvas.isActiveAndEnabled} order {canvas.sortingOrder} pos {canvas.transform.position} size {((RectTransform)canvas.transform).rect.size} scale {canvas.transform.lossyScale} mode {canvas.renderMode}");

            return sb.ToString();
        }
    }
}
