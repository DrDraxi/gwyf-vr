using HarmonyLib;

namespace GWYFVR.Patches
{
    [HarmonyPatch]
    internal static class RenderTexturePatches
    {
        /// <summary>
        /// The game renders its cameras into a texture sized to the window and re-renders the camera by
        /// hand when the window is resized. In VR those cameras are replaced by the VR rig, and forcing a
        /// render of a stereo camera into that texture crashes the game (e.g. when dragging the window).
        /// </summary>
        [HarmonyPatch(typeof(DynamicRenderTextureResolution), nameof(DynamicRenderTextureResolution.CheckAndApply))]
        [HarmonyPrefix]
        private static bool SkipResize() => false;
    }
}
