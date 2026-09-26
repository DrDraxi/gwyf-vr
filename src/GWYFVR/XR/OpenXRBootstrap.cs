using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace GWYFVR.XR
{
    /// <summary>
    /// Starts Unity's OpenXR provider inside a game that was built without XR. The game has no XR
    /// settings assets, so the loader, the settings and every interaction profile are created at runtime.
    /// </summary>
    internal static class OpenXRBootstrap
    {
        private static XRGeneralSettings generalSettings;
        private static XRManagerSettings managerSettings;
        private static OpenXRLoader loader;

        public static bool Start()
        {
            var descriptors = new List<ISubsystemDescriptor>();
            SubsystemManager.GetAllSubsystemDescriptors(descriptors);

            if (!descriptors.Any(d => d.id == "OpenXR Display") || !descriptors.Any(d => d.id == "OpenXR Input"))
            {
                Plugin.Log.LogError(
                    "The OpenXR subsystem is not registered. If this is the first launch after installing, restart the game once; otherwise check that GWYFVR.Preload is in BepInEx/patchers.");
                return false;
            }

            var runtimeFile = Plugin.Settings.OpenXRRuntimeFile.Value;
            if (!string.IsNullOrWhiteSpace(runtimeFile))
            {
                Plugin.Log.LogInfo($"Using OpenXR runtime override {runtimeFile}");
                Environment.SetEnvironmentVariable("XR_RUNTIME_JSON", runtimeFile);
            }

            generalSettings = Keep(ScriptableObject.CreateInstance<XRGeneralSettings>());
            managerSettings = Keep(ScriptableObject.CreateInstance<XRManagerSettings>());
            loader = Keep(ScriptableObject.CreateInstance<OpenXRLoader>());

            generalSettings.Manager = managerSettings;
            managerSettings.automaticLoading = false;
            managerSettings.automaticRunning = false;
            // TryAddLoader only accepts loaders registered in the editor, so add it to the list directly.
            ((List<XRLoader>)managerSettings.activeLoaders).Add(loader);

            var settings = OpenXRSettings.Instance;
            Keep(settings);
            settings.renderMode = Plugin.Settings.RenderMode.Value == StereoMode.SinglePassInstanced
                ? OpenXRSettings.RenderMode.SinglePassInstanced
                : OpenXRSettings.RenderMode.MultiPass;
            settings.depthSubmissionMode = OpenXRSettings.DepthSubmissionMode.None;
            SetFeatures(settings, CreateInteractionProfiles());

            Plugin.Log.LogInfo($"Initializing OpenXR ({SystemInfo.graphicsDeviceType})");

            managerSettings.InitializeLoaderSync();
            if (managerSettings.activeLoader == null)
            {
                Plugin.Log.LogError("OpenXR loader failed to initialize");
                LogDiagnosticReport();
                return false;
            }

            managerSettings.StartSubsystems();

            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            // The display only reports running once the OpenXR session begins, a frame or two later.
            if (displays.Count == 0)
            {
                Plugin.Log.LogError("OpenXR started but no headset display was created");
                managerSettings.DeinitializeLoader();
                return false;
            }

            XRSettings.eyeTextureResolutionScale = Plugin.Settings.RenderScale.Value;

            Plugin.Log.LogInfo($"OpenXR running on {OpenXRRuntime.name} {OpenXRRuntime.version}");
            return true;
        }

        [DllImport("UnityOpenXR", EntryPoint = "DiagnosticReport_GenerateReport")]
        private static extern IntPtr GenerateReport();

        [DllImport("UnityOpenXR", EntryPoint = "DiagnosticReport_ReleaseReport")]
        private static extern void ReleaseReport(IntPtr report);

        /// <summary>Unity's OpenXR plugin collects why startup failed in a report that is not logged by default.</summary>
        private static void LogDiagnosticReport()
        {
            try
            {
                var report = GenerateReport();
                if (report == IntPtr.Zero)
                    return;

                Plugin.Log.LogWarning("OpenXR diagnostic report:\n" + Marshal.PtrToStringAnsi(report));
                ReleaseReport(report);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Could not read the OpenXR diagnostic report: {ex.Message}");
            }
        }

        private static T Keep<T>(T obj) where T : UnityEngine.Object
        {
            obj.hideFlags = HideFlags.HideAndDontSave;
            return obj;
        }

        /// <summary>
        /// Controller profiles normally come from the OpenXR project settings. Their metadata is filled
        /// in by the editor from [OpenXRFeature] attributes, so we fill it in ourselves.
        /// </summary>
        private static OpenXRFeature[] CreateInteractionProfiles()
        {
            return new[]
            {
                Profile<OculusTouchControllerProfile>(OculusTouchControllerProfile.featureId, "Oculus Touch Controller Profile"),
                Profile<MetaQuestTouchPlusControllerProfile>(MetaQuestTouchPlusControllerProfile.featureId, "Meta Quest Touch Plus Controller Profile", "XR_META_touch_controller_plus"),
                Profile<MetaQuestTouchProControllerProfile>(MetaQuestTouchProControllerProfile.featureId, "Meta Quest Touch Pro Controller Profile", "XR_FB_touch_controller_pro"),
                Profile<ValveIndexControllerProfile>(ValveIndexControllerProfile.featureId, "Valve Index Controller Profile"),
                Profile<HTCViveControllerProfile>(HTCViveControllerProfile.featureId, "HTC Vive Controller Profile"),
                Profile<MicrosoftMotionControllerProfile>(MicrosoftMotionControllerProfile.featureId, "Microsoft Motion Controller Profile"),
                Profile<HPReverbG2ControllerProfile>(HPReverbG2ControllerProfile.featureId, "HP Reverb G2 Controller Profile", "XR_EXT_hp_mixed_reality_controller"),
                Profile<KHRSimpleControllerProfile>(KHRSimpleControllerProfile.featureId, "Khronos Simple Controller Profile"),
            };
        }

        private static OpenXRFeature Profile<T>(string id, string name, string extensions = "") where T : OpenXRFeature
        {
            var feature = Keep(ScriptableObject.CreateInstance<T>());

            SetField(feature, "nameUi", name);
            SetField(feature, "version", "0.0.1");
            SetField(feature, "featureIdInternal", id);
            SetField(feature, "openxrExtensionStrings", extensions);
            SetField(feature, "company", "Unity");
            SetField(feature, "priority", 0);
            SetField(feature, "required", false);
            feature.enabled = true;

            return feature;
        }

        private static void SetFeatures(OpenXRSettings settings, OpenXRFeature[] features)
        {
            var field = typeof(OpenXRSettings).GetField("features", BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null)
                throw new MissingFieldException(nameof(OpenXRSettings), "features");

            field.SetValue(settings, features);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = typeof(OpenXRFeature).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
                throw new MissingFieldException(nameof(OpenXRFeature), name);

            field.SetValue(target, value);
        }
    }
}
