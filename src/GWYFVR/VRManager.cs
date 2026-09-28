using GWYFVR.Input;
using GWYFVR.Player;
using GWYFVR.UI;
using UnityEngine;

namespace GWYFVR
{
    /// <summary>Owns the persistent VR objects and applies game hooks that need the game to be loaded.</summary>
    internal class VRManager : MonoBehaviour
    {
        private void Awake()
        {
            VRInput.Create();
            Invoke(nameof(LogDevices), 5f);

            // The headset compositor usually has focus, not the game window. Keep reading input and
            // running at full speed anyway.
            UnityEngine.InputSystem.InputSystem.settings.backgroundBehavior =
                UnityEngine.InputSystem.InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;

            var rig = new GameObject("VRRig");
            rig.transform.SetParent(transform, false);

            // WorldSpaceUI first, the rig adds its overlay camera to the headset camera.
            gameObject.AddComponent<WorldSpaceUI>();
            rig.AddComponent<VRRig>();
            if (!XR.URPXRSetup.Active)
                rig.AddComponent<XRSubmitter>();
            gameObject.AddComponent<VRPointer>();
            rig.AddComponent<VRHands>();
            rig.AddComponent<VRThrowing>();
            rig.AddComponent<RoomScale>();
            rig.AddComponent<VREyePatches>();
            gameObject.AddComponent<HandCalibration>();
            gameObject.AddComponent<MenuBackdrop>();
            VRSettingsMenu.Install();
            gameObject.AddComponent<VirtualGamepad>();
            gameObject.AddComponent<GazeDot>();

            if (Plugin.Settings.DevCommands.Value)
                gameObject.AddComponent<DevCommands>();
        }

        private void LogDevices() => VRInput.LogDevices();

    }
}
