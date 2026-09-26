using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;
using Mono.Cecil;

namespace GWYFVR.Preload
{
    /// <summary>
    /// BepInEx preloader patcher. It runs before the engine enumerates XR subsystems, which is the
    /// only point where we can still register Unity's OpenXR provider: it writes the subsystem
    /// manifest and copies the native OpenXR plugin + loader into the game's Plugins folder.
    /// It does not patch any assembly.
    /// </summary>
    public static class Preload
    {
        private const string DataFolder = "Gamble With Your Friends_Data";

        private const string SubsystemManifest = @"{
  ""name"": ""OpenXR XR Plugin"",
  ""version"": ""1.18.0"",
  ""libraryName"": ""UnityOpenXR"",
  ""displays"": [
    {
      ""id"": ""OpenXR Display""
    }
  ],
  ""inputs"": [
    {
      ""id"": ""OpenXR Input""
    }
  ]
}";

        private static readonly string[] NativeLibraries = { "UnityOpenXR.dll", "openxr_loader.dll" };

        private static readonly ManualLogSource Log = Logger.CreateLogSource("GWYFVR.Preload");

        public static IEnumerable<string> TargetDLLs { get; } = Array.Empty<string>();

        public static void Initialize()
        {
            try
            {
                SetupRuntimeAssets();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to set up the OpenXR runtime, VR will not be available: {ex}");
            }
        }

        public static void Patch(AssemblyDefinition assembly)
        {
        }

        private static void SetupRuntimeAssets()
        {
            var deps = FindRuntimeDeps();
            if (deps == null)
            {
                Log.LogError("Could not find the GWYFVR RuntimeDeps folder next to GWYFVR.dll, is the mod installed correctly?");
                return;
            }

            var data = Path.Combine(Paths.GameRootPath, DataFolder);

            var manifestDir = Path.Combine(data, "UnitySubsystems", "UnityOpenXR");
            Directory.CreateDirectory(manifestDir);
            File.WriteAllText(Path.Combine(manifestDir, "UnitySubsystemsManifest.json"), SubsystemManifest);

            var plugins = Path.Combine(data, "Plugins", "x86_64");
            Directory.CreateDirectory(plugins);

            foreach (var lib in NativeLibraries)
            {
                var src = Path.Combine(deps, lib);
                var dst = Path.Combine(plugins, lib);

                if (File.Exists(dst) && new FileInfo(dst).Length == new FileInfo(src).Length &&
                    File.GetLastWriteTimeUtc(dst) >= File.GetLastWriteTimeUtc(src))
                    continue;

                File.Copy(src, dst, true);
                Log.LogInfo($"Installed {lib}");
            }

            Log.LogInfo("OpenXR runtime assets are in place");
        }

        private static string FindRuntimeDeps()
        {
            return Directory.GetFiles(Paths.PluginPath, "GWYFVR.dll", SearchOption.AllDirectories)
                .Select(dll => Path.Combine(Path.GetDirectoryName(dll)!, "RuntimeDeps"))
                .FirstOrDefault(dir => NativeLibraries.All(lib => File.Exists(Path.Combine(dir, lib))));
        }
    }
}
