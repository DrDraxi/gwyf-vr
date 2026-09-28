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
    ///   left stick = move [left stick], left stick click = jump [south], right stick click = sprint toggle
    ///   [left trigger] (off when you stop moving), right A = ping [right stick press] (climbs out of the
    ///   spawn box while locked in it [west]), B = crouch [east], left A = emote wheel,
    ///   grip on an item = pick it up [west], trigger on a machine/button = interact [west],
    ///   trigger while holding = use item [right shoulder],
    ///   left B / menu = pause [start]. Throwing is physical (see VRThrowing).
    /// The right stick is not forwarded: it turns the VR rig instead of aiming.
    /// </summary>
    public class VirtualGamepad : MonoBehaviour
    {
        private Gamepad pad;
        private bool gripArmed;
        private bool triggerArmed;
        private bool lastActiveGrip;
        private bool lastActiveTrigger;
        private bool sprinting;
        private bool lastRightStickClick;


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
            // Only a fresh press counts: sweeping an already-held grip or trigger over something does nothing.
            if (active.Grip && !lastActiveGrip)
                gripArmed = !pointingAtMenu && !holding && targetIsItem;
            if (!active.Grip)
                gripArmed = false;
            if (active.Trigger && !lastActiveTrigger)
                triggerArmed = !pointingAtMenu && target != null && !targetIsItem;
            if (!active.Trigger)
                triggerArmed = false;
            lastActiveGrip = active.Grip;
            lastActiveTrigger = active.Trigger;

            var interactPressed = !pointingAtMenu &&
                                  (gripArmed && !holding ||
                                   triggerArmed && target != null && !targetIsItem ||
                                   locked && r.Primary);

            // The trigger of the hand holding an item uses the item.
            var useItem = !pointingAtMenu && holding && HandRoles.Holding.Trigger && !(target != null && !targetIsItem);

            VREmoteWheel.Update(l);

            // Sprint toggles on with a right stick click and switches itself off when you stop moving.
            if (r.StickClick && !lastRightStickClick)
                sprinting = !sprinting;
            lastRightStickClick = r.StickClick;
            if (l.Stick.magnitude < 0.2f)
                sprinting = false;

            var state = new GamepadState
            {
                // While the emote wheel is open the left stick picks an emote instead of moving.
                leftStick = VREmoteWheel.Open ? Vector2.zero : l.Stick,
                leftTrigger = sprinting && !locked ? 1f : 0f,
            };

            state = state
                .WithButton(GamepadButton.South, l.StickClick && !VREmoteWheel.Open)
                .WithButton(GamepadButton.East, r.Secondary)
                .WithButton(GamepadButton.West, interactPressed)
                .WithButton(GamepadButton.RightShoulder, useItem)
                .WithButton(GamepadButton.RightStick, r.Primary && !locked)
                .WithButton(GamepadButton.Start, l.Secondary || l.Menu);

            InputSystem.QueueStateEvent(pad, state);
        }
    }
}
