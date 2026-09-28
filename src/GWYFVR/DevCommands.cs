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

                case "cam":
                    // cam renderer N | cam pp 0/1 | cam hdr 0/1 | cam msaa 0/1
                    {
                        var cmd = args[1].Split(' ');
                        var vr = VRRig.Instance.VRCamera;
                        var data = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(vr);
                        var on = cmd.Length > 1 && cmd[1] != "0";
                        if (cmd[0] == "renderer") data.SetRenderer(int.Parse(cmd[1]));
                        if (cmd[0] == "pp") data.renderPostProcessing = on;
                        if (cmd[0] == "hdr") vr.allowHDR = on;
                        if (cmd[0] == "msaa") vr.allowMSAA = on;
                        if (cmd[0] == "aa") data.antialiasing = on ? UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing : UnityEngine.Rendering.Universal.AntialiasingMode.None;
                        Plugin.Log.LogInfo($"VR camera: renderer {VRRig.RendererIndexOf(data)} pp {data.renderPostProcessing} hdr {vr.allowHDR} msaa {vr.allowMSAA} aa {data.antialiasing} stack {data.cameraStack.Count}");
                    }
                    break;

                case "hands":
                    // hands rot x y z | hands pos x y z | hands item x y z
                    {
                        var h = args[1].Split(' ');
                        var v = new Vector3(float.Parse(h[1], System.Globalization.CultureInfo.InvariantCulture),
                            float.Parse(h[2], System.Globalization.CultureInfo.InvariantCulture),
                            float.Parse(h[3], System.Globalization.CultureInfo.InvariantCulture));
                        if (h[0] == "rot") VRHands.RotationOffset = v;
                        if (h[0] == "pos") VRHands.PositionOffset = v;
                        if (h[0] == "item") VRHands.ItemOffset = v;
                        Plugin.Log.LogInfo($"Hands rot {VRHands.RotationOffset} pos {VRHands.PositionOffset} item {VRHands.ItemOffset}");
                    }
                    break;

                case "find":
                    // find text: log paths of all objects whose name contains text
                    foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (t.name.IndexOf(args[1], StringComparison.OrdinalIgnoreCase) >= 0)
                            Plugin.Log.LogInfo($"{PathOf(t)} active {t.gameObject.activeInHierarchy} layer {t.gameObject.layer} components [{string.Join(", ", System.Linq.Enumerable.Select(t.GetComponents<Component>(), c => c.GetType().Name))}]");
                    break;

                case "go":
                    // go name [depth]: dump an object's hierarchy with components
                    {
                        var parts2 = args[1].Split(' ');
                        var depth = parts2.Length > 1 ? int.Parse(parts2[1]) : 4;
                        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (t.name == parts2[0])
                                LogObject(t, 0, depth);
                    }
                    break;

                case "tree":
                    foreach (var root in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        if (root.isRootCanvas && root.name == (args.Length > 1 ? args[1] : ""))
                            LogTree(root.transform, 0);
                    break;

                case "badshaders":
                    // Renderers whose shader can't run here (they draw magenta).
                    foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        foreach (var m in r.sharedMaterials)
                            if (m != null && (m.shader == null || !m.shader.isSupported || m.shader.name.Contains("InternalError")))
                                Plugin.Log.LogInfo($"Bad shader '{(m.shader != null ? m.shader.name : "null")}' on {PathOf(r.transform)} ({r.GetType().Name}) pos {r.transform.position} layer {r.gameObject.layer} material '{m.name}'");
                    break;

                case "renderers":
                    // renderers: count active renderers per layer; renderers N: list those on layer N
                    {
                        var counts = new System.Collections.Generic.SortedDictionary<int, int>();
                        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        {
                            if (!r.enabled)
                                continue;
                            counts.TryGetValue(r.gameObject.layer, out var n);
                            counts[r.gameObject.layer] = n + 1;
                            if (args.Length > 1 && r.gameObject.layer == int.Parse(args[1]))
                                Plugin.Log.LogInfo($"{PathOf(r.transform)} ({r.GetType().Name}) pos {r.transform.position} material '{(r.sharedMaterial != null ? r.sharedMaterial.name : "")}' shader '{(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "")}'");
                        }
                        foreach (var kv in counts)
                            Plugin.Log.LogInfo($"Layer {kv.Key} '{LayerMask.LayerToName(kv.Key)}': {kv.Value} renderers");
                        if (VRRig.Instance?.Source != null)
                            Plugin.Log.LogInfo($"Source mask {VRRig.Instance.Source.cullingMask:X8}, VR mask {VRRig.Instance.VRCamera.cullingMask:X8}");
                    }
                    break;

                case "inview":
                    // inview [degrees]: renderers near the middle of the headset view, nearest first
                    {
                        var cam = VRRig.Instance.VRCamera.transform;
                        var maxAngle = args.Length > 1 ? float.Parse(args[1], System.Globalization.CultureInfo.InvariantCulture) : 12f;
                        var hits = new System.Collections.Generic.List<(float, string)>();
                        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        {
                            if (!r.enabled || !r.gameObject.activeInHierarchy)
                                continue;
                            var to = r.bounds.center - cam.position;
                            if (to.magnitude > 15f || Vector3.Angle(cam.forward, to) > maxAngle)
                                continue;
                            var m = r.sharedMaterial;
                            hits.Add((to.magnitude, $"{to.magnitude:F2}m {PathOf(r.transform)} ({r.GetType().Name}) layer {r.gameObject.layer} size {r.bounds.size} material '{(m != null ? m.name : "")}' shader '{(m != null ? m.shader.name : "")}' tex '{(m != null && m.mainTexture != null ? m.mainTexture.name : "")}'"));
                        }
                        foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                        {
                            if (!g.isActiveAndEnabled || g.canvas == null || g.gameObject.layer == WorldSpaceUI.Layer)
                                continue;
                            var to = g.transform.position - cam.position;
                            if (to.magnitude > 15f || Vector3.Angle(cam.forward, to) > maxAngle)
                                continue;
                            hits.Add((to.magnitude, $"{to.magnitude:F2}m UI {PathOf(g.transform)} ({g.GetType().Name}) layer {g.gameObject.layer} canvas '{g.canvas.name}' material '{g.material.name}' shader '{g.material.shader.name}' tex '{(g.mainTexture != null ? g.mainTexture.name : "")}' color {g.color}"));
                        }
                        hits.Sort((a, b) => a.Item1.CompareTo(b.Item1));
                        foreach (var h in hits)
                            Plugin.Log.LogInfo(h.Item2);
                    }
                    break;

                case "features":
                    // features: list the headset camera's URP renderer features; features N 0/1: turn one off/on
                    {
                        var renderer = UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(VRRig.Instance.VRCamera).scriptableRenderer;
                        var list = (System.Collections.Generic.List<UnityEngine.Rendering.Universal.ScriptableRendererFeature>)typeof(UnityEngine.Rendering.Universal.ScriptableRenderer)
                            .GetProperty("rendererFeatures", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public)
                            .GetValue(renderer);
                        var parts2 = args.Length > 1 ? args[1].Split(' ') : new string[0];
                        if (parts2.Length == 2)
                            list[int.Parse(parts2[0])].SetActive(parts2[1] == "1");
                        for (var i = 0; i < list.Count; i++)
                            Plugin.Log.LogInfo($"Feature {i}: '{list[i]?.name}' {list[i]?.GetType().FullName} active {list[i]?.isActive}");
                    }
                    break;

                case "near":
                    // near x y z r: renderers, canvases and graphics within r metres of a point
                    {
                        var n = args[1].Split(' ');
                        var ci = System.Globalization.CultureInfo.InvariantCulture;
                        var point = new Vector3(float.Parse(n[0], ci), float.Parse(n[1], ci), float.Parse(n[2], ci));
                        var radius = float.Parse(n[3], ci);
                        foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                            if (r.enabled && (r.bounds.center - point).magnitude <= radius)
                                Plugin.Log.LogInfo($"Near: {PathOf(r.transform)} ({r.GetType().Name}) layer {r.gameObject.layer} pos {r.bounds.center} material '{(r.sharedMaterial != null ? r.sharedMaterial.name : "")}' shader '{(r.sharedMaterial != null ? r.sharedMaterial.shader.name : "")}'");
                        foreach (var g in FindObjectsByType<Graphic>(FindObjectsSortMode.None))
                            if (g.isActiveAndEnabled && (g.transform.position - point).magnitude <= radius)
                                Plugin.Log.LogInfo($"Near UI: {PathOf(g.transform)} ({g.GetType().Name}) layer {g.gameObject.layer} alpha {g.canvasRenderer.GetInheritedAlpha()} color {g.color} material '{g.material.name}' tex '{(g.mainTexture != null ? g.mainTexture.name : "")}'");
                    }
                    break;

                case "rt":
                    // rt name: save a render texture to BepInEx/gwyfvr-rt.png
                    foreach (var rt in Resources.FindObjectsOfTypeAll<RenderTexture>())
                        if (rt.name == args[1])
                        {
                            var previous = RenderTexture.active;
                            RenderTexture.active = rt;
                            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
                            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                            RenderTexture.active = previous;
                            File.WriteAllBytes(Path.Combine(Paths.BepInExRootPath, "gwyfvr-rt.png"), tex.EncodeToPNG());
                            Destroy(tex);
                            Plugin.Log.LogInfo($"Saved render texture '{rt.name}' {rt.width}x{rt.height}");
                            break;
                        }
                    break;

                case "active":
                    // active name 0/1: turn every object with this name off or on
                    {
                        var a = args[1].Split(' ');
                        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (t.name == a[0])
                            {
                                t.gameObject.SetActive(a[1] == "1");
                                Plugin.Log.LogInfo($"Set {PathOf(t)} active {a[1]}");
                            }
                    }
                    break;

                case "particles":
                    // particles: particle systems that currently have particles, with where they are
                    foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                        if (ps.particleCount > 0)
                        {
                            var pr = ps.GetComponent<ParticleSystemRenderer>();
                            var cam = VRRig.Instance.VRCamera.transform;
                            Plugin.Log.LogInfo($"Particles {PathOf(ps.transform)} count {ps.particleCount} dist {(ps.transform.position - cam.position).magnitude:F2}m angle {Vector3.Angle(cam.forward, ps.transform.position - cam.position):F0} space {ps.main.simulationSpace} layer {ps.gameObject.layer} material '{(pr != null && pr.sharedMaterial != null ? pr.sharedMaterial.name : "")}' shader '{(pr != null && pr.sharedMaterial != null ? pr.sharedMaterial.shader.name : "")}'");
                        }
                    break;

                case "hide":
                    // hide text: turn off renderers whose path contains text
                    foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                        if (PathOf(r.transform).IndexOf(args[1], StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            r.enabled = false;
                            Plugin.Log.LogInfo($"Hid {PathOf(r.transform)}");
                        }
                    break;

                case "loading":
                    // loading 1/0: show or hide the game's loading screen
                    Extensions.MonoSingleton<SceneTransitioner>.Instance?.ForceSet(args.Length > 1 && args[1] == "1");
                    break;

                case "dump":
                    GWYFVR.Input.VRInput.LogDevices();
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

        private static string PathOf(Transform t) => t.parent == null ? t.name : PathOf(t.parent) + "/" + t.name;

        private static void LogObject(Transform t, int depth, int maxDepth)
        {
            Plugin.Log.LogInfo($"{new string(' ', depth * 2)}{t.name}{(t.gameObject.activeSelf ? "" : " [inactive]")} layer {t.gameObject.layer} " +
                               $"lpos {t.localPosition} lrot {t.localEulerAngles} lscale {t.localScale} " +
                               $"[{string.Join(", ", System.Linq.Enumerable.Select(t.GetComponents<Component>(), c => c.GetType().Name))}]");
            if (depth < maxDepth)
                for (var i = 0; i < t.childCount; i++)
                    LogObject(t.GetChild(i), depth + 1, maxDepth);
        }

        private static void LogTree(Transform t, int depth)
        {
            var info = new StringBuilder();
            info.Append(new string(' ', depth * 2)).Append(t.name)
                .Append(t.gameObject.activeSelf ? "" : " [inactive]")
                .Append($" layer {t.gameObject.layer} pos {t.localPosition} scale {t.localScale}");
            var group = t.GetComponent<CanvasGroup>();
            if (group != null)
                info.Append($" group alpha {group.alpha}");
            var graphic = t.GetComponent<Graphic>();
            if (graphic != null)
            {
                info.Append($" {graphic.GetType().Name} enabled {graphic.enabled} color {graphic.color} size {graphic.rectTransform.rect.size}");
                var material = graphic.material;
                if (material != null)
                    info.Append($" material '{material.name}' shader '{material.shader.name}'");
                if (graphic.mainTexture != null)
                    info.Append($" texture '{graphic.mainTexture.name}' {graphic.mainTexture.width}x{graphic.mainTexture.height} wrap {graphic.mainTexture.wrapMode}");
            }
            var canvas = t.GetComponent<Canvas>();
            if (canvas != null)
                info.Append($" canvas mode {canvas.renderMode} order {canvas.sortingOrder} override {canvas.overrideSorting}");
            Plugin.Log.LogInfo(info.ToString());

            if (depth < 6)
                for (var i = 0; i < t.childCount; i++)
                    LogTree(t.GetChild(i), depth + 1);
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
