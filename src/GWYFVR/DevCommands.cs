using System;
using System.IO;
using System.Text;
using BepInEx;
using GWYFVR.Player;
using GWYFVR.UI;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using System.Collections;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
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
    ///   key Name [seconds] holds a keyboard key (Input System Key name)
    ///   turn degrees       turns the VR rig to face a world yaw
    ///   click text         clicks the first active button whose name or label contains text (=name for exact)
    /// </summary>
    internal class DevCommands : MonoBehaviour
    {
        private static string CommandFile => Path.Combine(Paths.BepInExRootPath, "gwyfvr-command.txt");

        private static DevCommands Instance;
        private float nextCheck;

        private void Awake() => Instance = this;

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

                case "xrcapture":
                    XRSubmitter.CaptureFile = args.Length > 1 ? args[1] : Path.Combine(Paths.BepInExRootPath, "gwyfvr-xrcapture.png");
                    if (XR.URPXRSetup.Active)
                        Instance.StartCoroutine(CaptureHeadsetAtEndOfFrame());
                    break;

                case "buttons":
                    foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                        if (button.isActiveAndEnabled)
                            Plugin.Log.LogInfo($"Button '{button.name}' label '{Label(button)}' interactable {button.interactable}");
                    break;

                case "click":
                    Click(args.Length > 1 ? args[1] : "");
                    break;

                case "key":
                    var parts = args.Length > 1 ? args[1].Split(' ') : new string[0];
                    var key = (Key)Enum.Parse(typeof(Key), parts[0], true);
                    var seconds = parts.Length > 1 ? float.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture) : 0.15f;
                    Instance.StartCoroutine(HoldKey(key, seconds));
                    break;

                case "turn":
                    VRRig.Instance?.FaceYaw(float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture));
                    break;

                case "dump":
                    Plugin.Log.LogInfo(Dump());
                    break;

                default:
                    Plugin.Log.LogWarning($"Unknown dev command '{args[0]}'");
                    break;
            }
        }

        private static IEnumerator HoldKey(Key key, float seconds)
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                yield break;

            Plugin.Log.LogInfo($"Holding {key} for {seconds}s");
            var end = Time.unscaledTime + seconds;
            while (Time.unscaledTime < end)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null;
            }

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
        }

        /// <summary>With URP rendering to the headset, read its first eye target back after the frame rendered.</summary>
        private static IEnumerator CaptureHeadsetAtEndOfFrame()
        {
            yield return new WaitForEndOfFrame();
            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            if (displays.Count == 0 || displays[0].GetRenderPassCount() == 0)
            {
                Plugin.Log.LogWarning("No XR render pass to capture");
                yield break;
            }

            displays[0].GetRenderPass(0, out var pass);
            XRSubmitter.CaptureTarget(pass, 0);
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

            sb.AppendLine($"XR device active {UnityEngine.XR.XRSettings.isDeviceActive} eye {UnityEngine.XR.XRSettings.eyeTextureWidth}x{UnityEngine.XR.XRSettings.eyeTextureHeight} mode {UnityEngine.XR.XRSettings.stereoRenderingMode}");
            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            foreach (var d in displays)
            {
                d.TryGetDroppedFrameCount(out var dropped);
                d.TryGetFramePresentCount(out var presented);
                sb.AppendLine($"XR display running {d.running} passes {d.GetRenderPassCount()} presented {presented} dropped {dropped}");
            }

            var rig = VRRig.Instance;
            if (rig != null)
            {
                sb.AppendLine($"Frames submitted {rig.GetComponent<XRSubmitter>()?.FramesSubmitted}");
                sb.AppendLine($"VR camera stereo {rig.VRCamera.stereoEnabled} active {rig.VRCamera.isActiveAndEnabled}");
                if (LocalPlayer.Head != null)
                    sb.AppendLine($"Local head rot {LocalPlayer.Head.transform.eulerAngles} state {LocalPlayer.Controller.State} locked {LocalPlayer.Controller.IsLocked} body pos {LocalPlayer.Controller.transform.position}");
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
