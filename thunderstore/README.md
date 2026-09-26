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
| Jump | A |
| Crouch | B |
| Interact / skip | Right grip |
| Use item | Right trigger |
| Throw item | Left grip |
| Zoom | Left trigger |
| Ping | Right stick click |
| Push to talk | X |
| Emote wheel | Y |
| Pause menu | Left menu button |
| Click menus | Point with the right controller, pull the trigger |

You walk where you look and interact with what you look at (a dot marks the centre of your view).

## Settings

Edit `BepInEx/config/io.github.drdraxi.gwyfvr.cfg` (or use your mod manager's config editor):

- `TurnMode`, `SnapTurnAngle`, `SmoothTurnSpeed`
- `StereoMode` (`MultiPass` is the most compatible, `SinglePassInstanced` is faster)
- `RenderScale`
- UI `Distance` and `Width`
- `LeftHandedPointer`
- `EnableVR` or launch option `--disable-vr` to play flat

## Troubleshooting

- **The game starts flat.** Check `BepInEx/LogOutput.log` for lines from `GWYFVR`. Make sure the headset is on and your OpenXR runtime is running. On the very first launch after installing, restart the game once.
- **Wrong runtime starts.** Set `OpenXRRuntimeFile` in the config to your runtime's json file.
