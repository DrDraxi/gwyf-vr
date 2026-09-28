using UnityEngine;

namespace GWYFVR.UI
{
    /// <summary>Materials for the mod's own simple visuals, using shaders the game build includes.</summary>
    internal static class VRMaterials
    {
        private static readonly string[] Candidates =
        {
            "Universal Render Pipeline/Unlit",
            "Universal Render Pipeline/Particles/Unlit",
            "UI/Default",
            "Sprites/Default",
        };

        public static Material Unlit(Color color)
        {
            foreach (var name in Candidates)
            {
                var shader = Shader.Find(name);
                if (shader == null || !shader.isSupported)
                    continue;

                var material = new Material(shader) { color = color };
                if (material.HasProperty("_BaseColor"))
                    material.SetColor("_BaseColor", color);
                return material;
            }

            Plugin.Log.LogWarning("No unlit shader found for VR visuals");
            return new Material(Shader.Find("Hidden/InternalErrorShader"));
        }
    }
}
