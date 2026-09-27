using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering.Universal;

namespace GWYFVR.XR
{
    /// <summary>
    /// Starts URP's XR system. The game's build strips the XR shaders URP needs, so URP never
    /// initializes XR by itself even with the XR-enabled pipeline assemblies the preloader swaps in.
    /// The shaders come from an asset bundle built with the game's exact Unity version.
    /// </summary>
    internal static class URPXRSetup
    {
        private const string ShaderBundle = "gwyfvr_xrshaders";

        /// <summary>True when the render pipeline can render to the headset.</summary>
        public static bool Active { get; private set; }

        public static bool TryInitialize(string runtimeDeps)
        {
            // The stripped XRSystem has no display state at all.
            if (typeof(XRSystem).GetField("s_Display", BindingFlags.Static | BindingFlags.NonPublic) == null)
            {
                Plugin.Log.LogError("The game's render pipeline has no XR support and the XR-enabled one was not loaded. " +
                                    "If this is the first launch after installing, restart the game once.");
                return false;
            }

            var bundle = AssetBundle.LoadFromFile(Path.Combine(runtimeDeps, ShaderBundle));
            if (bundle == null)
            {
                Plugin.Log.LogError($"Could not load {ShaderBundle}");
                return false;
            }

            var shaders = bundle.LoadAllAssets<Shader>();
            var occlusion = shaders.FirstOrDefault(s => s.name.EndsWith("XROcclusionMesh"));
            var mirror = shaders.FirstOrDefault(s => s.name.EndsWith("XRMirrorView"));
            var motion = shaders.FirstOrDefault(s => s.name.EndsWith("XRMotionVector"));
            if (occlusion == null || mirror == null)
            {
                Plugin.Log.LogError($"XR shaders missing from {ShaderBundle} ({string.Join(", ", shaders.Select(s => s.name))})");
                return false;
            }

            var urp = typeof(UniversalRenderPipeline).Assembly;
            var create = urp.GetType("UnityEngine.Rendering.Universal.XRPassUniversal")
                ?.GetMethod("Create", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (create == null)
            {
                Plugin.Log.LogError("XRPassUniversal.Create not found");
                return false;
            }

            var allocator = (Func<XRPassCreateInfo, XRPass>)Delegate.CreateDelegate(typeof(Func<XRPassCreateInfo, XRPass>), create);
            XRSystem.Initialize(allocator, occlusion, mirror);

            // URP's XR motion vector pass (optional).
            var universalXR = urp.GetType("UnityEngine.Rendering.Universal.XRSystemUniversal");
            var initMotion = universalXR?.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(m => m.Name == "Initialize" && m.GetParameters().Length == 1 && m.GetParameters()[0].ParameterType == typeof(Shader));
            if (initMotion != null && motion != null)
                initMotion.Invoke(null, new object[] { motion });

            Active = true;
            Plugin.Log.LogInfo("URP XR rendering initialized");
            return true;
        }
    }
}
