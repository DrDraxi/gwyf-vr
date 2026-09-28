using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace GWYFVR.Input
{
    /// <summary>
    /// Feeds the VR controllers into the game as a gamepad. The game already supports gamepads for
    /// every action and for menu navigation, so this is all it takes to play with VR controllers.
    ///
    /// Mapping (game's gamepad binding in brackets):
    ///   left stick = move [left stick], left stick click = sprint [left trigger],
    ///   A = jump [south], B = crouch [east], right grip = interact [west],
    ///   right trigger = use item [right shoulder], left grip = throw item [right trigger],
    ///   right stick click = ping [right stick press], left B / menu = pause [start].
    /// The right stick is not forwarded: it turns the VR rig instead of aiming.
    /// </summary>
    public class VirtualGamepad : MonoBehaviour
    {
        private Gamepad pad;


        private void OnDestroy()
        {
            if (pad != null)
                InputSystem.RemoveDevice(pad);
        }

        private void Update()
        {
            XRControllers.Poll();
            var l = XRControllers.Left;
            var r = XRControllers.Right;
            if (!l.Tracked && !r.Tracked)
                return;

            // Created lazily: the Input System isn't ready yet while plugins load.
            if (pad == null)
            {
                pad = InputSystem.AddDevice<Gamepad>("GWYFVR Controllers");
                Plugin.Log.LogInfo("Added virtual gamepad for the VR controllers");
            }

            var state = new GamepadState
            {
                leftStick = l.Stick,
                leftTrigger = l.StickClick ? 1f : 0f,
                rightTrigger = l.Grip ? 1f : 0f,
            };

            // While a menu is being pointed at, the trigger clicks the menu instead of using the item.
            var pointingAtMenu = UI.VRPointer.Instance != null && UI.VRPointer.Instance.IsPointingAtMenu;

            state = state
                .WithButton(GamepadButton.South, r.Primary)
                .WithButton(GamepadButton.East, r.Secondary)
                .WithButton(GamepadButton.West, r.Grip)
                .WithButton(GamepadButton.RightShoulder, r.Trigger && !pointingAtMenu)
                .WithButton(GamepadButton.RightStick, r.StickClick)
                .WithButton(GamepadButton.Start, l.Secondary || l.Menu);

            InputSystem.QueueStateEvent(pad, state);
        }
    }
}
