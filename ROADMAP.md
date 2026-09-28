# GWYFVR roadmap

Collected from play-testing on a Valve Index (2026-09-28). Roughly in priority order.

## Next
- Multiplayer: make sure VR works correctly online (other players see your head and hands move).
  - Cross-play: VR players must stay compatible with players who don't have the mod. Keep the network
    protocol untouched; anything extra (hand poses) only goes to players who also run the mod.
  - Throws as a client: the host's copy still launches items from in front of the face.

- Ragdoll, knockback, being carried and the spawn-box launch: add a comfort fade or vignette.
- Number boxes (custom lobby, settings) need a VR keypad; zoom and pocket slots need VR buttons.
- Still to test in the headset: hand-aimed Quota Gun/Taser/Golden Chip/Hi-Lo, bet slider, drunk toggle.
- Release 0.3.0 on Thunderstore (only with the go-ahead).
- Hand sync between modded players (optional, mod-only layer; cross-play test first).
- Cutscene camera (Cinemachine) moves the view: leave until it looks wrong in the headset.
- Optional: a smoothed flat spectator camera for the game window instead of the raw eye view.
- Grip/trigger fresh-press detection is per active hand; pressing the other hand while one is held can be missed.

## Done since 0.2.0-alpha
- Main menu: home scene as static scenery behind a see-through menu.
- VR settings on the settings menu's Input tab.
- VR controller prompts (Kenney Input Prompts, CC0); corner control hints and bottom info text hidden.
- Body part machine: missing eye patched per eye; rolling head stays upright.
- Menu scrolling with the stick, grab only on a fresh grip press.
- Controls: jump on right stick click, sprint toggle on left stick click, ping on right A, emote wheel
  on left A with stick selection; right B is voice (push-to-talk, or mute toggle with open mic),
  crouch is no longer mapped.
- Emote wheel on the left hand, sized to taste.
- Hand-aimed Quota Gun, Taser, Golden Chip, ping and Hi-Lo slider.
- Drunk wobble and motion blur off in VR, immunity bloom toned down from 5 to 1.5.
- Physical bat swing: hand speed turns on the bat's hit area.
- Grab items by touching them with either hand (laser as fallback); bet slider follows the hand.
- Drunk wobble kept by default, with a VR setting to turn it off.
- Post-processing works in VR (URP's XR visibility mesh off, so UberPost's missing XR pass isn't used).
- Lost eye: 92.5% dark vignette with a stitched plus instead of solid black.
- Fixed: touch grab targeted your own body (blocked all interaction); desktop mouse pulled menu clicks off
  the laser; round end screens stand still instead of following the head.
- Full-screen screens (loading, round end, game over) surround you on a sphere; hold right A to skip.
- No more magenta lens outline at the world origin (XR occlusion mesh off).
- Game window mirrors the left eye in game; VR logo on the main and pause menus.

## Done in 0.2.0-alpha
- VR rendering, head tracking, turning, room-scale, controller hands, grab/throw, laser pointer, menus/HUD.
