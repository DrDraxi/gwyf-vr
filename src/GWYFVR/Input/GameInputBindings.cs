using UnityEngine.InputSystem;

namespace GWYFVR.Input
{
    /// <summary>
    /// Adds VR controller bindings to the game's own "Player" action map, so every game system that
    /// listens to InputReader (movement, jumping, interacting, items, menus) works with controllers.
    /// Turning is not bound here: the mod turns the VR rig itself.
    /// </summary>
    internal static class GameInputBindings
    {
        private const string Left = "<XRController>{LeftHand}";
        private const string Right = "<XRController>{RightHand}";

        public static bool Applied { get; private set; }

        public static bool TryApply()
        {
            if (Applied)
                return true;

            var reader = InputReader.Instance;
            if (reader == null || reader._inputActions == null)
                return false;

            var map = reader._inputActions.asset.FindActionMap("Player");
            if (map == null)
                return false;

            var wasEnabled = map.enabled;
            map.Disable();

            Bind(map, "Move", Left + "/{Primary2DAxis}");
            Bind(map, "Sprint", Left + "/{Primary2DAxisClick}");
            Bind(map, "Jump", Right + "/{PrimaryButton}");
            Bind(map, "Crouch", Right + "/{SecondaryButton}");
            Bind(map, "Interact", Right + "/{GripButton}");
            Bind(map, "SkipUI", Right + "/{GripButton}");
            Bind(map, "UseItem", Right + "/{TriggerButton}");
            Bind(map, "ThrowItem", Left + "/{GripButton}");
            Bind(map, "Zoom", Left + "/{TriggerButton}");
            Bind(map, "Ping", Right + "/{Primary2DAxisClick}");
            Bind(map, "EmoteWheel", Left + "/{SecondaryButton}");
            Bind(map, "PushToTalk", Left + "/{PrimaryButton}");
            Bind(map, "EscapeMenu", Left + "/{MenuButton}");

            if (wasEnabled)
                map.Enable();

            Applied = true;
            Plugin.Log.LogInfo("VR controller bindings added to the game's input actions");
            return true;
        }

        private static void Bind(InputActionMap map, string actionName, string path)
        {
            var action = map.FindAction(actionName);
            if (action == null)
            {
                Plugin.Log.LogWarning($"Game input action '{actionName}' not found, skipping VR binding");
                return;
            }

            action.AddBinding(path);
        }
    }
}
