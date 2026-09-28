# GWYFVR roadmap

Collected from play-testing on a Valve Index (2026-09-28). Roughly in priority order.

## In progress
- Main menu: home scene as static scenery behind the menu, see-through menu panel, better viewpoint.

## Next
- Pause menu / settings: replace the desktop input-rebinding settings with VR settings
  (snap/smooth turn, snap angle, turn speed, room-scale, physical throwing, hand/item size, HUD distance...).
- Input prompts: show VR controller prompts instead of keyboard/gamepad prompts, using Kenney's
  input prompts pack (CC0) for the controller glyphs.
- Multiplayer: make sure VR works correctly online (other players see your head and hands move).
  - Cross-play: VR players must stay compatible with players who don't have the mod. Keep the network
    protocol untouched; anything extra (hand poses) only goes to players who also run the mod.
  - Throws as a client: the host's copy still launches items from in front of the face.
- Post-processing in VR: rebuild the XR-enabled render pipeline with the exact game Unity version
  (6000.3.6f1); the 6000.3.7 build is missing a pass in the game's UberPost shader.

## Done in 0.2.0-alpha
- VR rendering, head tracking, turning, room-scale, controller hands, grab/throw, laser pointer, menus/HUD.
