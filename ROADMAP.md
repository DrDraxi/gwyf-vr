# GWYFVR roadmap

Collected from play-testing on a Valve Index (2026-09-28). Roughly in priority order.

## Next
- Multiplayer: make sure VR works correctly online (other players see your head and hands move).
  - Cross-play: VR players must stay compatible with players who don't have the mod. Keep the network
    protocol untouched; anything extra (hand poses) only goes to players who also run the mod.
  - Throws as a client: the host's copy still launches items from in front of the face.
- Post-processing in VR: rebuild the XR-enabled render pipeline with the exact game Unity version
  (6000.3.6f1); the 6000.3.7 build is missing a pass in the game's UberPost shader.

- Emote wheel on the left hand: check in the headset that it sits well above the controller, that the
  dark full-screen backdrop is gone, and that stick directions line up with the emotes.
- Optional: a smoothed flat spectator camera for the game window instead of the raw eye view.
- Grip/trigger fresh-press detection is per active hand; pressing the other hand while one is held can be missed.

## Done since 0.2.0-alpha
- Main menu: home scene as static scenery behind a see-through menu.
- VR settings on the settings menu's Input tab.
- VR controller prompts (Kenney Input Prompts, CC0); corner control hints and bottom info text hidden.
- Body part machine: missing eye patched per eye; rolling head stays upright.
- Menu scrolling with the stick, grab only on a fresh grip press.
- Controls: jump on right stick click, sprint toggle on left stick click, ping on right A, emote wheel
  on left A with stick selection.
- Game window mirrors the left eye in game; VR logo on the main and pause menus.

## Done in 0.2.0-alpha
- VR rendering, head tracking, turning, room-scale, controller hands, grab/throw, laser pointer, menus/HUD.
