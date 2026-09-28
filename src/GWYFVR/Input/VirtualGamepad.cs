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
    ///   left stick = move [left stick], right stick click = jump [south], left stick click = sprint toggle
    ///   [left trigger] (off when you stop moving), right A = ping [right stick press] (climbs out of the
    ///   spawn box while locked in it, holds to skip screens like game over [west]), right B = voice (push-to-talk or mute,
    ///   see VRVoice; crouch is not mapped), left A = emote wheel,
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
        private bool lastLeftStickClick;
        private float lastMovingTime;


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
            // Day summary, game over and credits: hold right A to skip (the game's skip is on the same button).
            var cutscene = InputEvents.ActiveLayer == InputLayer.Cutscene;

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
                                   locked && r.Primary) ||
                                  cutscene && r.Primary;
            // Dragging a slider keeps hold of it while the trigger stays down, even if the hand leaves it.
            if (!pointingAtMenu && active.Trigger && (Player.VRBetSlider.Dragging != null || Patches.HiLoSliderDrag.Dragging))
                interactPressed = true;

            // The trigger of the hand holding an item uses the item.
            var useItem = !pointingAtMenu && holding && HandRoles.Holding.Trigger && !(target != null && !targetIsItem);

            VREmoteWheel.Update(l);
            // Crouch (east) is dropped: right B is the voice button.
            VRVoice.Update(r.Secondary);

            // Sprint toggles on with a left stick click and switches itself off when you stop moving. Clicking
            // the stick can briefly centre it, so it has to stay released for a moment to count as stopping.
            if (l.Stick.magnitude >= 0.2f || l.StickClick)
                lastMovingTime = Time.unscaledTime;
            if (l.StickClick && !lastLeftStickClick && !VREmoteWheel.Open)
            {
                sprinting = !sprinting;
                lastMovingTime = Time.unscaledTime;
            }
            lastLeftStickClick = l.StickClick;
            if (Time.unscaledTime - lastMovingTime > 0.3f)
                sprinting = false;

            var state = new GamepadState
            {
                // While the emote wheel is open the left stick picks an emote instead of moving.
                leftStick = VREmoteWheel.Open ? Vector2.zero : l.Stick,
                leftTrigger = sprinting && !locked ? 1f : 0f,
            };

            state = state
                .WithButton(GamepadButton.South, r.StickClick)
                .WithButton(GamepadButton.West, interactPressed)
                .WithButton(GamepadButton.RightShoulder, useItem)
                .WithButton(GamepadButton.RightStick, r.Primary && !locked && !cutscene)
                .WithButton(GamepadButton.Start, l.Secondary || l.Menu);

            InputSystem.QueueStateEvent(pad, state);
        }
    }
}
