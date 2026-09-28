using System.Collections.Generic;
using GWYFVR.Input;
using GWYFVR.Player;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace GWYFVR.UI
{
    /// <summary>
    /// Screen space canvases can't be seen in a headset. This moves every screen space canvas onto a
    /// panel floating in front of the player. The panel follows your head position and turns to face
    /// you when you look far enough away. Converted canvases are drawn by an overlay camera so walls
    /// never cover them, and they get a tracked-device raycaster so controllers can point at them.
    /// </summary>
    public class WorldSpaceUI : MonoBehaviour
    {
        /// <summary>Layer used for converted canvases, rendered only by the UI overlay camera.</summary>
        public static int Layer { get; } = FindFreeLayer();

        private static int FindFreeLayer()
        {
            for (var layer = 31; layer >= 8; layer--)
                if (string.IsNullOrEmpty(LayerMask.LayerToName(layer)))
                    return layer;

            Plugin.Log.LogWarning("No unused layer found for VR UI, menus may be hidden behind walls");
            return 5;
        }

        /// <summary>Canvases are laid out on this virtual screen, then scaled to the configured width.</summary>
        private static readonly Vector2 VirtualScreen = new Vector2(1920f, 1080f);

        private const float FollowAngle = 40f;

        public static WorldSpaceUI Instance { get; private set; }

        /// <summary>Panel in front of the player all converted canvases are placed on.</summary>
        public Transform Anchor { get; private set; }

        public IReadOnlyList<Canvas> Canvases => canvases;

        private readonly List<Canvas> canvases = new List<Canvas>();
        private readonly HashSet<Canvas> converted = new HashSet<Canvas>();
        private readonly Dictionary<Canvas, float> drawOrder = new Dictionary<Canvas, float>();
        private readonly List<Canvas> stacked = new List<Canvas>();
        private Camera overlayCamera;
        private float anchorYaw;
        private bool anchorPlaced;
        private float nextScan;

        private void Awake()
        {
            Instance = this;

            Plugin.Log.LogInfo($"VR UI uses layer {Layer}");

            Anchor = new GameObject("VRUIAnchor").transform;
            Anchor.SetParent(transform, false);

            var overlayObject = new GameObject("VRUICamera");
            overlayObject.transform.SetParent(transform, false);
            overlayCamera = overlayObject.AddComponent<Camera>();
            overlayCamera.cullingMask = 1 << Layer;
            overlayCamera.clearFlags = CameraClearFlags.Depth;
            overlayCamera.nearClipPlane = 0.02f;
            overlayCamera.stereoTargetEye = VRRig.HeadsetEyes;
            var data = overlayCamera.GetUniversalAdditionalCameraData();
            data.renderType = CameraRenderType.Overlay;
            data.renderPostProcessing = false;
            data.renderShadows = false;
        }

        /// <summary>Add the UI overlay camera to the VR camera's stack.</summary>
        public void AttachOverlayCamera(Camera baseCamera)
        {
            var stack = baseCamera.GetUniversalAdditionalCameraData().cameraStack;
            if (!stack.Contains(overlayCamera))
                stack.Add(overlayCamera);
        }

        private void Update()
        {
            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.25f;
                ScanCanvases();
            }
        }

        private void LateUpdate()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.Source == null)
                return;

            // The overlay camera must render from exactly the same pose as the headset camera.
            overlayCamera.transform.SetPositionAndRotation(rig.VRCamera.transform.position, rig.VRCamera.transform.rotation);

            UpdateAnchor(rig.transform, rig.VRCamera.transform);
            PlaceCanvases();
        }

        /// <summary>Place the panel in front of the head, in the rig's frame so it follows a tilted menu view.</summary>
        private void UpdateAnchor(Transform rig, Transform head)
        {
            var headYaw = head.localEulerAngles.y;
            if (!anchorPlaced || Mathf.Abs(Mathf.DeltaAngle(anchorYaw, headYaw)) > FollowAngle || AnyNewlyOpened())
            {
                anchorYaw = headYaw;
                anchorPlaced = true;
            }

            var rotation = rig.rotation * Quaternion.Euler(0f, anchorYaw, 0f);
            var position = head.position + rotation * Vector3.forward * Plugin.Settings.UIDistance.Value;
            Anchor.SetPositionAndRotation(position, rotation);
        }

        private bool anyVisibleLastFrame;

        /// <summary>Recenter the panel in front of you when a menu opens while none was open.</summary>
        private bool AnyNewlyOpened()
        {
            var anyVisible = false;
            foreach (var canvas in canvases)
                if (canvas != null && canvas.isActiveAndEnabled && HasRaycaster(canvas))
                    anyVisible = true;

            var opened = anyVisible && !anyVisibleLastFrame;
            anyVisibleLastFrame = anyVisible;
            return opened;
        }

        private static bool HasRaycaster(Canvas canvas)
        {
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            return raycaster != null && raycaster.enabled && canvas.GetComponentInChildren<Selectable>() != null;
        }

        private void ScanCanvases()
        {
            canvases.RemoveAll(c => c == null);

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || converted.Contains(canvas))
                    continue;
                if (canvas.renderMode == RenderMode.WorldSpace)
                    continue;

                Convert(canvas);
            }

            // Canvases instantiate children at runtime which keep their prefab layer.
            foreach (var canvas in canvases)
                SetLayer(canvas.transform);
        }

        private void Convert(Canvas canvas)
        {
            converted.Add(canvas);
            canvases.Add(canvas);
            // The order screen space canvases were drawn in, used to stack them the same way in VR.
            // Overlay canvases are drawn after everything a camera renders, so they always sit on top
            // of screen-space-camera canvases; among camera canvases, nearer planes are on top.
            var layer = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? 1_000_000f : -canvas.planeDistance * 100f;
            drawOrder[canvas] = layer + canvas.sortingOrder + drawOrder.Count * 0.001f;

            canvas.renderMode = RenderMode.WorldSpace;
            // Menus are hit-tested from the controller, see VRPointer.
            canvas.worldCamera = VRPointer.Instance != null ? VRPointer.Instance.PointerCamera : null;

            var rect = (RectTransform)canvas.transform;
            rect.sizeDelta = VirtualScreen;
            rect.pivot = new Vector2(0.5f, 0.5f);

            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null)
                scaler.dynamicPixelsPerUnit = 1f;

            SetLayer(canvas.transform);

            Plugin.Log.LogInfo($"Moved canvas '{canvas.name}' into world space");
        }

        private void PlaceCanvases()
        {
            var scale = Plugin.Settings.UIWidth.Value / VirtualScreen.x;

            // Stack canvases like the flat game draws them: by sorting order, then by draw order. Each
            // later canvas sits a little closer to the viewer so it is drawn on top.
            stacked.Clear();
            foreach (var canvas in canvases)
                if (canvas != null && canvas.gameObject.activeInHierarchy)
                    stacked.Add(canvas);
            stacked.Sort((a, b) => DrawOrder(a).CompareTo(DrawOrder(b)));

            for (var i = 0; i < stacked.Count; i++)
            {
                var canvas = stacked[i];
                var offset = -Anchor.forward * (i * 0.002f);
                if (VRPointer.Instance != null && canvas.worldCamera != VRPointer.Instance.PointerCamera)
                    canvas.worldCamera = VRPointer.Instance.PointerCamera;

                var t = canvas.transform;
                t.SetPositionAndRotation(Anchor.position + offset, Anchor.rotation);

                var parentScale = t.parent != null ? t.parent.lossyScale : Vector3.one;
                t.localScale = new Vector3(Div(scale, parentScale.x), Div(scale, parentScale.y), Div(scale, parentScale.z));
            }
        }

        private float DrawOrder(Canvas canvas) => drawOrder.TryGetValue(canvas, out var order) ? order : float.MaxValue;

        private static float Div(float a, float b) => Mathf.Approximately(b, 0f) ? a : a / b;

        private static void SetLayer(Transform root)
        {
            if (root.gameObject.layer != Layer)
                root.gameObject.layer = Layer;

            for (var i = 0; i < root.childCount; i++)
                SetLayer(root.GetChild(i));
        }

        /// <summary>Intersect a ray with the visible, pointable canvases. Used to draw the laser.</summary>
        private static bool HitsSelectable(Canvas canvas, Vector3 worldPoint)
        {
            foreach (var selectable in canvas.GetComponentsInChildren<Selectable>())
            {
                if (!selectable.IsInteractable())
                    continue;
                var rt = (RectTransform)selectable.transform;
                if (rt.rect.Contains(rt.InverseTransformPoint(worldPoint)))
                    return true;
            }

            return false;
        }

        public bool Raycast(Ray ray, out Vector3 point)
        {
            point = default;
            var best = float.MaxValue;

            // In game the HUD is always up; only count hits on actual buttons there, so the laser doesn't
            // stick to the HUD. In menus any part of a menu panel counts.
            var buttonsOnly = VRRig.Instance != null && VRRig.Instance.PlayerMode;

            foreach (var canvas in canvases)
            {
                if (canvas == null || !canvas.isActiveAndEnabled || !HasRaycaster(canvas))
                    continue;

                var rect = (RectTransform)canvas.transform;
                var plane = new Plane(rect.forward, rect.position);
                if (!plane.Raycast(ray, out var distance) || distance > best)
                    continue;

                var hit = ray.GetPoint(distance);
                var local = rect.InverseTransformPoint(hit);
                if (!rect.rect.Contains(local))
                    continue;
                if (buttonsOnly && !HitsSelectable(canvas, hit))
                    continue;

                best = distance;
                point = hit;
            }

            return best < float.MaxValue;
        }
    }
}
