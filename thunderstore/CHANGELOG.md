# Changelog

## 0.2.0 (alpha)

First version tested on a real headset (Valve Index).

- Renders to the headset: the game ships its render pipeline without VR support, so the mod swaps in a
  VR-enabled build and starts URP's XR system (game post-processing is off in VR for now).
- Head tracking, snap/smooth turning, room-scale movement (walking in your room moves you).
- Controllers: move/jump/crouch/sprint via a virtual gamepad; grip picks up and holds items in either hand,
  swing and let go to throw; trigger works machines and uses items; A climbs out of the spawn box.
- The game's hands follow your controllers (half size), held items shrink to half size.
- Laser pointer for menus and interactables; menus on a floating panel, HUD follows your head.
- Start-up coin flip splash is skipped.
- F8 on the desktop opens sliders for hand/laser calibration.

## 0.1.0

- First playable build: OpenXR startup, stereo first person view, head tracked look, snap/smooth turning,
  controller bindings for the game's actions, menus and HUD on floating panels with a controller laser.
