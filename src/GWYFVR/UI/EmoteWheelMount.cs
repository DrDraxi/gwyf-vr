using GWYFVR.Input;
using GWYFVR.Player;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR;

namespace GWYFVR.UI
{
    /// <summary>
    /// Puts the emote wheel on a small disc just above the left controller, lined up with the controller
    /// so pushing the stick toward an emote points at it on the disc. The wheel's full screen dark
    /// backdrop (made to dim a flat screen) is turned off.
    /// </summary>
    public class EmoteWheelMount : MonoBehaviour
    {
        // Width of the wheel's layout rect, which is much larger than the visible circle.
        private const float Diameter = 1.28f;
        private static readonly Vector3 Offset = new Vector3(0f, 0.07f, 0.02f);
        private static readonly Vector2 VirtualScreen = new Vector2(1920f, 1080f);

        private EmoteWheelController wheel;
        private Canvas canvas;
        private float nextSearch;

        private void LateUpdate()
        {
            if (wheel == null)
            {
                if (Time.unscaledTime < nextSearch)
                    return;
                nextSearch = Time.unscaledTime + 1f;
                wheel = Object.FindAnyObjectByType<EmoteWheelController>();
                if (wheel == null || wheel.radialMenu == null)
                {
                    wheel = null;
                    return;
                }
                Mount();
            }

            if (!wheel.IsEmoteWheelActive || canvas == null)
                return;

            var rig = VRRig.Instance;
            if (rig == null || !XRControllers.TryGetAimPose(XRNode.LeftHand, out var position, out var rotation))
                return;

            var handRotation = rig.transform.rotation * rotation;
            var target = rig.transform.TransformPoint(position) + handRotation * Offset;

            // Lying flat on top of the controller: seen from above, the wheel's up is the controller's forward.
            var t = canvas.transform;
            t.rotation = handRotation * Quaternion.LookRotation(Vector3.down, Vector3.forward);

            // Scale and move so the circle itself (not the whole screen it was laid out on) sits on the hand.
            var menu = wheel.radialMenu.rt != null ? wheel.radialMenu.rt : (RectTransform)wheel.radialMenu.transform;
            var width = menu.rect.width * menu.lossyScale.x;
            if (width > 0.0001f)
                t.localScale *= Diameter / width;
            t.position += target - menu.TransformPoint(menu.rect.center);

            WorldSpaceUI.SetLayer(t);
        }

        private void Mount()
        {
            var own = wheel.GetComponent<Canvas>();
            if (own != null && own.isRootCanvas)
            {
                // The wheel is its own canvas: take it off the VR panels and place it directly.
                canvas = own;
                WorldSpaceUI.Instance?.Release(own);
                canvas.renderMode = RenderMode.WorldSpace;
                ((RectTransform)canvas.transform).sizeDelta = VirtualScreen;
            }
            else
            {
                var go = new GameObject("VREmoteWheelCanvas", typeof(RectTransform));
                // Lives and dies with the scene the wheel belongs to.
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, wheel.gameObject.scene);
                canvas = go.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.sortingOrder = 30000;
                ((RectTransform)go.transform).sizeDelta = VirtualScreen;
                go.transform.localScale = Vector3.one * (Diameter / 600f);
                // Keeps its layout: the new canvas is the same size as the virtual screen it came from.
                wheel.transform.SetParent(go.transform, false);
            }

            canvas.worldCamera = VRPointer.Instance != null ? VRPointer.Instance.PointerCamera : null;
            HideBackdrop();
            WorldSpaceUI.SetLayer(canvas.transform);
            Plugin.Log.LogInfo($"Mounted the emote wheel '{wheel.name}' on the left hand");
        }

        /// <summary>Anything that fills the whole screen is the dimming backdrop, not part of the wheel.</summary>
        private void HideBackdrop()
        {
            Canvas.ForceUpdateCanvases();
            var screen = (RectTransform)canvas.transform;
            var screenSize = Vector2.Scale(screen.rect.size, screen.lossyScale);

            foreach (var graphic in wheel.GetComponentsInChildren<Graphic>(true))
            {
                if (graphic.GetComponentInParent<RMF_RadialMenuElement>(true) != null)
                    continue;
                var rect = graphic.rectTransform;
                var size = Vector2.Scale(rect.rect.size, rect.lossyScale);
                if (size.x >= screenSize.x * 0.9f && size.y >= screenSize.y * 0.9f)
                {
                    graphic.enabled = false;
                    Plugin.Log.LogInfo($"Hid emote wheel backdrop '{graphic.name}'");
                }
            }

            foreach (var line in wheel.GetComponentsInChildren<LineRenderer>(true))
                line.enabled = false;
        }
    }
}
