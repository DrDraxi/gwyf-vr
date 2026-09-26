using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Builds an empty Windows player so we get release (non-editor) compiled copies of the
// XR packages, then copies the managed + native runtime dependencies the mod needs.
// Run: Unity.exe -batchmode -quit -projectPath UnityProject -executeMethod BuildRuntimeDeps.Build
public static class BuildRuntimeDeps
{
    private static readonly string[] ManagedDeps =
    {
        "Unity.XR.OpenXR.dll",
        "Unity.XR.Management.dll",
        "Unity.XR.CoreUtils.dll",
    };

    public static void Build()
    {
        var root = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        var buildDir = Path.Combine(Application.dataPath, "..", "Build");
        var outDir = Path.Combine(root, "lib", "RuntimeDeps");

        // The player build needs at least one scene, an empty one is enough.
        const string scenePath = "Assets/Empty.unity";
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(
            UnityEditor.SceneManagement.NewSceneSetup.EmptyScene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene, scenePath);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { scenePath },
            locationPathName = Path.Combine(buildDir, "Deps.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            Debug.LogError("Player build failed: " + report.summary.result);
            EditorApplication.Exit(1);
            return;
        }

        Directory.CreateDirectory(outDir);
        var data = Path.Combine(buildDir, "Deps_Data");

        foreach (var dll in ManagedDeps)
            Copy(Path.Combine(data, "Managed", dll), outDir);

        // Native plugins are only included in a player build when OpenXR is the active loader,
        // so take them straight from the package instead.
        var openxr = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.xr.openxr");
        Copy(Path.Combine(openxr.resolvedPath, "Runtime", "windows", "x64", "UnityOpenXR.dll"), outDir);
        Copy(Path.Combine(openxr.resolvedPath, "RuntimeLoaders", "windows", "x64", "openxr_loader.dll"), outDir);

        Debug.Log("Runtime deps written to " + outDir);
    }

    private static void Copy(string src, string outDir)
    {
        File.Copy(src, Path.Combine(outDir, Path.GetFileName(src)), true);
        Debug.Log("Copied " + src);
    }
}
