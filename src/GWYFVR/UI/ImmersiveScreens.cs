using System.Collections.Generic;
using GWYFVR.Player;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GWYFVR.UI
{
    /// <summary>
    /// Full-screen screens (fades, loading, round end, game over) cover the whole flat screen with a
    /// background. In VR that would only be a floating panel with the world visible around it, so each
    /// such background is drawn on a sphere around your head instead, and the panel keeps only its
    /// content (text, buttons) floating in front.
    /// </summary>
    public class ImmersiveScreens : MonoBehaviour
    {
        private const float Radius = 5f;
        // How much more of the view the sphere covers than the flat panel, used to keep patterns at a
        // similar size: the panel spans roughly 75 by 45 degrees, the sphere 360 by 180.
        private static readonly Vector2 PanelToSphere = new Vector2(360f / 75f, 180f / 45f);

        private class Backdrop
        {
            public Graphic Source;
            public Canvas Canvas;
            public int Index;
            public Renderer Renderer;
            public Material Material;
        }

        private readonly Dictionary<Graphic, Backdrop> backdrops = new Dictionary<Graphic, Backdrop>();
        private readonly List<Graphic> stale = new List<Graphic>();
        private Mesh sphere;
        private float nextScan;

        private void Awake()
        {
            // A sphere turned inside out, so it is seen from inside even with shaders that cull back faces.
            var primitive = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            Destroy(primitive);
            var triangles = sphere.triangles;
            for (var i = 0; i < triangles.Length; i += 3)
                (triangles[i], triangles[i + 1]) = (triangles[i + 1], triangles[i]);
            sphere.triangles = triangles;
            var uv = sphere.uv;
            for (var i = 0; i < uv.Length; i++)
                uv[i].x = 1f - uv[i].x;
            sphere.uv = uv;
        }

        private void LateUpdate()
        {
            var rig = VRRig.Instance;
            if (rig == null)
                return;

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 0.25f;
                Scan();
            }

            var head = rig.VRCamera.transform.position;
            stale.Clear();
            foreach (var pair in backdrops)
            {
                var b = pair.Value;
                if (b.Source == null || b.Canvas == null)
                {
                    stale.Add(pair.Key);
                    continue;
                }

                var alpha = b.Source.isActiveAndEnabled
                    ? b.Source.color.a * b.Source.canvasRenderer.GetInheritedAlpha()
                    : 0f;
                var show = alpha > 0.001f && b.Canvas.isActiveAndEnabled;
                b.Renderer.gameObject.SetActive(show);
                // The flat copy stays hidden; the game keeps animating its colour and alpha, which the sphere copies.
                b.Source.canvasRenderer.cull = true;
                if (!show)
                    continue;

                b.Renderer.transform.position = head;
                var color = b.Source.color;
                color.a = alpha;
                b.Material.color = color;
                var texture = b.Source.mainTexture;
                if (b.Material.mainTexture != texture)
                    b.Material.mainTexture = texture;
                // Drawn just under the canvas it came from, so the screen's own text and other screens on top still show.
                b.Renderer.sortingLayerID = b.Canvas.sortingLayerID;
                b.Renderer.sortingOrder = b.Canvas.sortingOrder - 1;
            }

            foreach (var key in stale)
                Remove(key);
        }

        private void Scan()
        {
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.gameObject.layer != WorldSpaceUI.Layer || canvas.renderMode != RenderMode.WorldSpace)
                    continue;
                // The main menu's backgrounds make way for the menu scenery instead (MenuBackdrop).
                if (canvas.gameObject.scene.name == MenuBackdrop.MenuScene)
                    continue;

                var root = (RectTransform)canvas.transform;
                var screen = root.rect.size;
                var graphics = canvas.GetComponentsInChildren<Graphic>(false);
                for (var i = 0; i < graphics.Length; i++)
                {
                    var graphic = graphics[i];
                    if (backdrops.ContainsKey(graphic) || !IsBackdrop(graphic, root, screen))
                        continue;
                    Add(graphic, canvas, i);
                }
            }
        }

        /// <summary>An opaque picture filling the whole screen.</summary>
        private static bool IsBackdrop(Graphic graphic, RectTransform root, Vector2 screen)
        {
            if (graphic is TMP_Text || graphic is Text || graphic.color.a < 0.95f || !graphic.enabled)
                return false;
            // The emote wheel moves onto the hand (EmoteWheelMount) and drops its backdrop.
            if (graphic.GetComponentInParent<EmoteWheelController>(true) != null)
                return false;
            // Full-screen render textures show other cameras (HUD, minimaps), not a background.
            if (graphic is RawImage raw && raw.texture is RenderTexture)
                return false;

            var rect = graphic.rectTransform;
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var min = root.InverseTransformPoint(corners[0]);
            var max = root.InverseTransformPoint(corners[2]);
            var size = new Vector2(Mathf.Abs(max.x - min.x), Mathf.Abs(max.y - min.y));
            return size.x >= screen.x * 0.9f && size.y >= screen.y * 0.9f;
        }

        private void Add(Graphic graphic, Canvas canvas, int index)
        {
            var go = new GameObject($"VRBackdrop {graphic.name}", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * (Radius * 2f);
            go.layer = WorldSpaceUI.Layer;
            go.GetComponent<MeshFilter>().sharedMesh = sphere;

            var material = new Material(graphic.material) { name = $"VRBackdrop {graphic.name}" };
            material.renderQueue = 3000 + Mathf.Min(index, 99);
            // UI shaders: draw over everything behind, ignore UI masks.
            if (material.HasProperty("unity_GUIZTestMode"))
                material.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            if (material.HasProperty("_StencilComp"))
                material.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.Always);
            if (material.HasProperty("_Stencil"))
                material.SetInt("_Stencil", 0);
            material.mainTextureScale = TextureScale(graphic);

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            go.SetActive(false);

            backdrops[graphic] = new Backdrop { Source = graphic, Canvas = canvas, Index = index, Renderer = renderer, Material = material };
            Plugin.Log.LogInfo($"Surrounding backdrop for '{graphic.name}' on '{canvas.name}' (material '{graphic.material.name}', shader '{graphic.material.shader.name}')");
        }

        /// <summary>Repeat patterns about as often around you as they repeat across the flat screen.</summary>
        private static Vector2 TextureScale(Graphic graphic)
        {
            var tiles = Vector2.one;
            if (graphic is Image image && image.type == Image.Type.Tiled && image.sprite != null)
            {
                var tile = image.sprite.rect.size / image.pixelsPerUnit;
                var size = image.rectTransform.rect.size;
                if (tile.x > 0f && tile.y > 0f)
                    tiles = new Vector2(size.x / tile.x, size.y / tile.y);
            }
            else if (graphic.mainTexture == null || graphic.mainTexture.wrapMode != TextureWrapMode.Repeat)
            {
                // A stretched picture (gradient, photo): show it once around you.
                return Vector2.one;
            }

            return Vector2.Scale(tiles, PanelToSphere);
        }

        private void Remove(Graphic key)
        {
            if (backdrops.TryGetValue(key, out var b))
            {
                if (b.Renderer != null)
                    Destroy(b.Renderer.gameObject);
                if (b.Material != null)
                    Destroy(b.Material);
            }
            backdrops.Remove(key);
        }
    }
}
