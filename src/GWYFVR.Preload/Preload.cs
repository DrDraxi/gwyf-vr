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

        /// <summary>
        /// The game's render pipeline assemblies are compiled without XR. XR-enabled builds of the exact
        /// same URP version live in RuntimeDeps/RenderPipelines and replace them.
        /// </summary>
        private static readonly string[] RenderPipelineAssemblies =
        {
            "Unity.RenderPipelines.Core.Runtime.dll",
            "Unity.RenderPipelines.Universal.Runtime.dll",
        };

        public static IEnumerable<string> TargetDLLs { get; } = Array.Empty<string>();

        public static void Initialize()
        {
            try
            {
                SetupRuntimeAssets();
                InstallRenderPipeline();
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to set up the OpenXR runtime, VR will not be available: {ex}");
            }
        }

        public static void Patch(AssemblyDefinition assembly)
        {
        }

        /// <summary>
        /// Unity loads the render pipeline assemblies from the Managed folder before BepInEx can patch
        /// them in memory, so the XR-enabled builds are written over the game's files. The originals are
        /// backed up to BepInEx/GWYFVR-backup, and Steam's "verify files" also restores them. A file in
        /// use can still be renamed on Windows, so this works while the game is starting; the new files
        /// are picked up from the next launch.
        /// </summary>
        private static void InstallRenderPipeline()
        {
            var deps = FindRuntimeDeps();
            if (deps == null)
                return;

            var managed = Path.Combine(Paths.GameRootPath, DataFolder, "Managed");
            var backup = Path.Combine(Paths.BepInExRootPath, "GWYFVR-backup");
            var cache = Path.Combine(Paths.CachePath, "GWYFVR");
            Directory.CreateDirectory(backup);
            Directory.CreateDirectory(cache);
            var changed = false;

            foreach (var name in RenderPipelineAssemblies)
            {
                var xrBuild = Path.Combine(deps, "RenderPipelines", name);
                var installed = Path.Combine(managed, name);
                var original = Path.Combine(backup, name);
                var aligned = Path.Combine(cache, name);

                if (!File.Exists(xrBuild))
                {
                    Log.LogWarning($"XR-enabled {name} is missing from the mod, VR will show a black screen");
                    continue;
                }

                if (!File.Exists(original))
                    File.Copy(installed, original);

                if (!File.Exists(aligned) || File.GetLastWriteTimeUtc(aligned) < File.GetLastWriteTimeUtc(xrBuild))
                    AlignSerializedLayout(xrBuild, original, aligned);

                if (SameFile(aligned, installed))
                    continue;

                try
                {
                    var old = installed + ".gwyfvr-old";
                    if (File.Exists(old))
                        File.Delete(old);
                    File.Move(installed, old);
                    File.Copy(aligned, installed);
                    changed = true;
                    Log.LogInfo($"Installed XR-enabled {name}");
                }
                catch (IOException)
                {
                    // Unity already has the file open; the dll search path override picks it up instead.
                    Log.LogDebug($"{name} is in use, relying on the doorstop search path override");
                }
            }

            changed |= EnsureDoorstopSearchPath();

            if (changed)
                Log.LogWarning("The XR-enabled render pipeline was just installed. Restart the game once for VR to show in the headset.");
        }

        /// <summary>
        /// Unity reads the game's saved settings by field layout. XR builds add serialized fields
        /// (e.g. UniversalRendererData.xrSystemData) the game's data doesn't have, which makes Unity
        /// reject its settings as corrupted. Make every type's fields match the game's original order,
        /// and stop serializing fields the original doesn't have.
        /// </summary>
        private static void AlignSerializedLayout(string xrBuild, string original, string output)
        {
            var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory(Path.GetDirectoryName(original));
            resolver.AddSearchDirectory(Path.Combine(Paths.GameRootPath, DataFolder, "Managed"));
            var parameters = new ReaderParameters { AssemblyResolver = resolver };

            using var target = AssemblyDefinition.ReadAssembly(xrBuild, parameters);
            using var reference = AssemblyDefinition.ReadAssembly(original, parameters);

            var referenceTypes = reference.MainModule.GetTypes().ToDictionary(t => t.FullName);
            var hidden = 0;

            foreach (var type in target.MainModule.GetTypes())
            {
                if (!referenceTypes.TryGetValue(type.FullName, out var refType))
                    continue;

                var order = refType.Fields.Select((f, i) => (f.Name, i)).ToDictionary(x => x.Name, x => x.i);
                var fields = type.Fields.ToList();

                foreach (var field in fields)
                {
                    if (field.IsStatic || order.ContainsKey(field.Name) || field.IsNotSerialized)
                        continue;
                    field.IsNotSerialized = true;
                    hidden++;
                    Log.LogDebug($"Not serializing {type.FullName}.{field.Name}");
                }

                // Struct field order is also memory layout, leave structs alone.
                if (type.IsValueType)
                    continue;

                var sorted = fields
                    .OrderBy(f => order.TryGetValue(f.Name, out var i) ? i : int.MaxValue)
                    .ToList();
                type.Fields.Clear();
                foreach (var field in sorted)
                    type.Fields.Add(field);
            }

            target.Write(output);
            Log.LogInfo($"Prepared {Path.GetFileName(output)} ({hidden} XR-only fields excluded from serialization)");
        }

        /// <summary>
        /// Point Mono at the prepared assemblies before the Managed folder, for when the game's copies
        /// are in use and can't be replaced. Doorstop reads this setting at launch, so it applies from
        /// the next start.
        /// </summary>
        private static bool EnsureDoorstopSearchPath()
        {
            const string key = "dll_search_path_override";
            var ini = Path.Combine(Paths.GameRootPath, "doorstop_config.ini");
            if (!File.Exists(ini))
                return false;

            var value = Path.Combine("BepInEx", "cache", "GWYFVR");
            var lines = File.ReadAllLines(ini);
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].TrimStart().StartsWith(key))
                    continue;

                var current = lines[i].Substring(lines[i].IndexOf('=') + 1).Trim();
                if (current.Split(';').Any(p => p.Trim() == value))
                    return false;

                lines[i] = $"{key} = {(current.Length > 0 ? value + ";" + current : value)}";
                File.WriteAllLines(ini, lines);
                Log.LogInfo("Added the XR render pipeline folder to doorstop's dll search path");
                return true;
            }

            return false;
        }

        private static bool SameFile(string a, string b)
        {
            if (!File.Exists(b))
                return false;
            var fa = File.ReadAllBytes(a);
            var fb = File.ReadAllBytes(b);
            return fa.Length == fb.Length && fa.SequenceEqual(fb);
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
