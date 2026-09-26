using GWYFVR.Player;
using UnityEngine;

namespace GWYFVR.UI
{
    /// <summary>
    /// Replaces the game's screen centre crosshair. Interaction in the game raycasts from the camera,
    /// which follows your head, so the crosshair is a small dot locked to the centre of your view.
    /// </summary>
    public class GazeDot : MonoBehaviour
    {
        private const float Distance = 2f;
        private Transform dot;

        private void Awake()
        {
            dot = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            dot.name = "VRGazeDot";
            Destroy(dot.GetComponent<Collider>());
            dot.localScale = Vector3.one * 0.008f;
            var renderer = dot.GetComponent<Renderer>();
            renderer.sharedMaterial = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"))
            {
                color = new Color(1f, 1f, 1f, 0.7f)
            };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dot.gameObject.layer = WorldSpaceUI.Layer;
            dot.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            var rig = VRRig.Instance;
            var show = rig != null && rig.PlayerMode && GameCrosshairVisible();
            dot.gameObject.SetActive(show);
            if (!show)
                return;

            var head = rig.VRCamera.transform;
            dot.position = head.position + head.forward * Distance;
        }

        /// <summary>Checks whether the game wants its crosshair shown, and keeps the flat one invisible.</summary>
        private static bool GameCrosshairVisible()
        {
            var local = Extensions.MonoSingleton<LocalManager>.Instance;
            if (local == null || local.crosshair == null)
                return false;

            foreach (var graphic in local.crosshair.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                graphic.enabled = false;

            return local.crosshair.activeInHierarchy;
        }
    }
}
