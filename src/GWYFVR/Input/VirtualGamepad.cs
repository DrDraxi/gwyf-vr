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
    ///   A = jump [south] (climbs out of the spawn box while locked in it [west]), B = crouch [east],
    ///   grip on an item = pick it up [west], trigger on a machine/button = interact [west],
    ///   trigger while holding = use item [right shoulder], right stick click = ping [right stick press],
    ///   left B / menu = pause [start]. Throwing is physical (see VRThrowing).
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

            HandRoles.Update();
            var active = HandRoles.Active;
            var holding = HandRoles.IsHoldingItem;

            // While a menu is pointed at, the trigger clicks the menu instead of doing anything in game.
            var pointingAtMenu = UI.VRPointer.Instance != null && UI.VRPointer.Instance.IsPointingAtMenu;

            var controller = Player.LocalPlayer.Controller;
            var interact = controller != null ? controller.GetComponent<PlayerInteract>() : null;
            var target = interact != null ? interact.TargetInteractable : null;
            var targetIsItem = target is Item;
            var locked = controller != null && controller.IsLocked;

            // Grip grabs items, trigger works machines, buttons and slots. Locked in the spawn box, A climbs out.
            var interactPressed = !pointingAtMenu &&
                                  (!holding && targetIsItem && active.Grip ||
                                   target != null && !targetIsItem && active.Trigger ||
                                   locked && r.Primary);

            // The trigger of the hand holding an item uses the item.
            var useItem = !pointingAtMenu && holding && HandRoles.Holding.Trigger && !(target != null && !targetIsItem);

            var state = new GamepadState
            {
                leftStick = l.Stick,
                leftTrigger = l.StickClick ? 1f : 0f,
            };

            state = state
                .WithButton(GamepadButton.South, r.Primary && !locked)
                .WithButton(GamepadButton.East, r.Secondary)
                .WithButton(GamepadButton.West, interactPressed)
                .WithButton(GamepadButton.RightShoulder, useItem)
                .WithButton(GamepadButton.RightStick, r.StickClick)
                .WithButton(GamepadButton.Start, l.Secondary || l.Menu);

            InputSystem.QueueStateEvent(pad, state);
        }
    }
}
