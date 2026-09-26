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

            var rig = new GameObject("VRRig");
            rig.transform.SetParent(transform, false);

            // WorldSpaceUI first, the rig adds its overlay camera to the headset camera.
            gameObject.AddComponent<WorldSpaceUI>();
            rig.AddComponent<VRRig>();
            gameObject.AddComponent<VRPointer>();
            gameObject.AddComponent<GazeDot>();

            if (Plugin.Settings.DevCommands.Value)
                gameObject.AddComponent<DevCommands>();
        }

        private void Update()
        {
            if (!GameInputBindings.Applied)
                GameInputBindings.TryApply();
        }
    }
}
