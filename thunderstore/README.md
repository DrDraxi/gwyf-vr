# GWYFVR

Play **Gamble With Your Friends** in VR with a PC VR headset.

Only the VR player needs the mod. It doesn't change anything the game sends over the network, so friends
on flat screens without the mod should be able to play with you (not tested with an unmodded friend yet).

> Early build. Expect rough edges, please report issues on GitHub.

## Features

- Full stereo VR with head tracking, snap or smooth turning, and room-scale (walking in your room moves you).
- Your game hands follow your controllers. Grab items with either hand by touching them or pointing the
  laser at them, swing and let go to throw.
- The Quota Gun, Taser, Golden Chip, pings, sliders and the bat all work with your hands; the bat hits when
  you swing it.
- Menus and the HUD float in front of you, with a laser pointer. Loading, round end and game over screens
  surround you.
- Controller button prompts instead of keyboard keys, and the emote wheel on your left hand.
- The game's post-processing (colour grading, bloom, drunk wobble) works in VR.
- The game window on your monitor shows what you see.

## Requirements

- A PC VR headset with an OpenXR runtime: SteamVR, Meta Quest Link / Air Link, or Virtual Desktop.
- Turn the headset on and start your OpenXR runtime before launching the game.

## Controls

| Action | Controller |
| --- | --- |
| Move | Left stick |
| Sprint | Left stick click (toggles, turns off when you stop) |
| Turn (snap or smooth) | Right stick left/right |
| Walk around | Just walk, room-scale moves your character |
| Jump | Right stick click |
| Pick up and hold an item | Touch it or point at it, hold grip (either hand) |
| Throw | Swing and let go of the grip |
| Drop | Let go of the grip slowly |
| Use a machine, button, slot | Point at it, press or hold trigger |
| Use the held item | Trigger of the holding hand (aims where that hand points) |
| Ping | Right A |
| Climb out of the spawn box | Right A |
| Skip (day summary, game over, credits) | Hold right A |
| Voice: push-to-talk (or mute toggle with open mic) | Right B |
| Emote wheel | Left A, push the left stick toward an emote and let go (left A again cancels) |
| Pause menu | Left B / menu |
| Click menus | Point with the laser, pull the trigger |
| Scroll menus | Point at a list, push the stick up or down |

The laser shows when you point at something you can interact with or at a menu.

## Settings

The **VR** tab of the game's settings menu has turning, room-scale, throwing, hand aiming, grab by touch,
hand and item size, HUD and menu placement, post-processing and the drunk wobble.

Everything else is in `BepInEx/config/io.github.drdraxi.gwyfvr.cfg` (or your mod manager's config editor):

- `StereoMode` (`MultiPass` is the most compatible, `SinglePassInstanced` is faster)
- `RenderScale`, `DesktopMirror` (show the left eye in the game window)
- `SkipSplash`, `LeftHandedPointer`
- `EnableVR` or launch option `--disable-vr` to play flat

Press **F8** on the desktop window for calibration sliders (hand and laser angle, hand and item size). The
desktop mouse is switched off in VR so it can't move the menu pointer; it comes back while F8 is open.

## Troubleshooting

- **The game starts flat.** Check `BepInEx/LogOutput.log` for lines from `GWYFVR`. Make sure the headset is on and your OpenXR runtime is running. On the very first launch after installing, restart the game once.
- **Wrong runtime starts.** Set `OpenXRRuntimeFile` in the config to your runtime's json file.
- **Everything looks black or broken.** Turn off Post-processing on the VR settings tab.

## Credits

Controller prompts from [Kenney's Input Prompts](https://kenney.nl/assets/input-prompts) (CC0).
