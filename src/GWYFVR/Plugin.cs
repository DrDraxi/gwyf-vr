using System;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace GWYFVR
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "io.github.drdraxi.gwyfvr";
        public const string Name = "GWYFVR";
        public const string Version = "0.1.0";

        internal static ManualLogSource Log { get; private set; }
        internal static VRConfig Settings { get; private set; }

        /// <summary>True once OpenXR started and a headset display is running.</summary>
        public static bool VREnabled { get; private set; }

        private void Awake()
        {
            Log = Logger;
            Settings = new VRConfig(Config);

            Log.LogInfo($"{Name} {Version} starting");

            var disabled = !Settings.EnableVR.Value ||
                           Environment.GetCommandLineArgs().Contains("--disable-vr", StringComparer.OrdinalIgnoreCase);
            if (disabled)
            {
                Log.LogWarning("VR is disabled by config or the --disable-vr launch option");
                return;
            }

            if (!PreloadRuntimeDependencies())
                return;

            // Kept in a separate method so the XR assemblies are only resolved after they were preloaded.
            StartVR();
        }

        private bool PreloadRuntimeDependencies()
        {
            var deps = Path.Combine(Path.GetDirectoryName(Info.Location)!, "RuntimeDeps");
            if (!Directory.Exists(deps))
            {
                Log.LogError($"RuntimeDeps folder is missing ({deps}), the mod is not installed correctly");
                return false;
            }

            foreach (var file in Directory.GetFiles(deps, "Unity.*.dll"))
            {
                try
                {
                    Assembly.LoadFile(file);
                }
                catch (Exception ex)
                {
                    Log.LogError($"Failed to load {Path.GetFileName(file)}: {ex.Message}");
                    return false;
                }
            }

            return true;
        }

        private void StartVR()
        {
            if (!XR.OpenXRBootstrap.Start())
            {
                Log.LogError("Could not start VR. Is the headset connected and your OpenXR runtime (SteamVR, Oculus, Virtual Desktop) running?");
                return;
            }

            VREnabled = true;

            if (!XR.URPXRSetup.TryInitialize(Path.Combine(Path.GetDirectoryName(Info.Location)!, "RuntimeDeps")))
                Log.LogWarning("Falling back to rendering the eyes manually (experimental)");

            new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);

            var manager = new GameObject("GWYFVR");
            DontDestroyOnLoad(manager);
            manager.hideFlags = HideFlags.HideAndDontSave;
            manager.AddComponent<VRManager>();

            Log.LogInfo("VR started");
        }
    }
}
