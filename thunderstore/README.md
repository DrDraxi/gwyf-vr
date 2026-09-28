# GWYFVR

Play **Gamble With Your Friends** in VR. Your friends can stay on flat screens, only the player using VR needs the mod.

> Early build. Expect rough edges, please report issues on GitHub.

## Requirements

- A PC VR headset with an OpenXR runtime: SteamVR, Meta Quest Link / Air Link, or Virtual Desktop.
- Turn the headset on and start your OpenXR runtime before launching the game.

## Controls

| Action | Controller |
| --- | --- |
| Move | Left stick |
| Sprint | Left stick click |
| Turn (snap or smooth) | Right stick left/right |
| Walk around | Just walk, room-scale moves your character |
| Jump / climb out of the spawn box | A |
| Crouch | B |
| Pick up and hold an item | Point at it, hold grip (either hand) |
| Throw | Swing and let go of the grip |
| Drop | Let go of the grip slowly |
| Use a machine, button, slot | Point at it, press or hold trigger |
| Use the held item | Trigger of the holding hand |
| Ping | Right stick click |
| Pause menu | Left B / menu |
| Click menus | Point with the laser, pull the trigger |

The laser shows when you point at something you can interact with or at a menu.
Press **F8** on the desktop window for calibration sliders (hand and laser angle, hand and item size).

## Settings

Edit `BepInEx/config/io.github.drdraxi.gwyfvr.cfg` (or use your mod manager's config editor):

- `TurnMode`, `SnapTurnAngle`, `SmoothTurnSpeed`, `RoomScale`, `PhysicalThrowing`, `ThrowStrength`
- `StereoMode` (`MultiPass` is the most compatible, `SinglePassInstanced` is faster)
- `RenderScale`
- Menu `Distance`/`Width`, HUD `HudDistance`/`HudWidth`/`HudTilt`
- `SkipSplash`
- `LeftHandedPointer`
- `EnableVR` or launch option `--disable-vr` to play flat

## Troubleshooting

- **The game starts flat.** Check `BepInEx/LogOutput.log` for lines from `GWYFVR`. Make sure the headset is on and your OpenXR runtime is running. On the very first launch after installing, restart the game once.
- **Wrong runtime starts.** Set `OpenXRRuntimeFile` in the config to your runtime's json file.
