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
        private BaseCursor[] cursors = new BaseCursor[0];
        private float nextCursorScan;

        private void Awake()
        {
            dot = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            dot.name = "VRGazeDot";
            Destroy(dot.GetComponent<Collider>());
            dot.localScale = Vector3.one * 0.008f;
            var renderer = dot.GetComponent<Renderer>();
            renderer.sharedMaterial = VRMaterials.Unlit(Color.white);
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dot.gameObject.layer = WorldSpaceUI.Layer;
            dot.SetParent(transform, false);
        }

        private void LateUpdate()
        {
            HideGameCursors();

            var rig = VRRig.Instance;
            var show = rig != null && rig.PlayerMode && GameCrosshairVisible();
            dot.gameObject.SetActive(show);
            if (!show)
                return;

            var head = rig.VRCamera.transform;
            dot.position = head.position + head.forward * Distance;
        }

        /// <summary>
        /// The game's mouse cursor sprite sits at the centre of the view (the virtual mouse is centred)
        /// and renders as a magenta square in VR. The laser does its job, so keep it hidden.
        /// </summary>
        private void HideGameCursors()
        {
            if (Time.unscaledTime >= nextCursorScan)
            {
                nextCursorScan = Time.unscaledTime + 1f;
                cursors = Object.FindObjectsByType<BaseCursor>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            }

            foreach (var cursor in cursors)
                if (cursor != null && cursor.uiCursorImage != null && cursor.uiCursorImage.enabled)
                    cursor.uiCursorImage.enabled = false;
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
