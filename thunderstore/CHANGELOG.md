# Changelog

## 0.3.2 (alpha)

- New icon and an updated readme.

## 0.3.1 (alpha)

- The VR settings tab is labelled "VR".
- The stick scrolls menus when pointing anywhere on a list, at a gentler speed.

## 0.3.0 (alpha)

- Main menu: the home scene as scenery behind a see-through menu, with a GWYF VR logo.
- VR settings (turning, room-scale, throwing, grab by touch, drunk wobble) on the settings menu's Input tab.
- Controller button prompts instead of keyboard keys (Kenney Input Prompts, CC0).
- New controls: jump on right stick click, sprint toggle on left stick click, ping on right A, emote
  wheel on the left hand (left A, pick with the stick), voice on right B, hold right A to skip screens.
- Grab items by touching them with either hand, or with the laser.
- Hand-aimed Quota Gun, Taser, Golden Chip, ping, Hi-Lo and bet sliders; the bat hits when you swing it.
- Loading, round end and game over screens surround you; fades cover your whole view.
- Body part machine: a lost eye gets a dark stitched patch; a rolling head stays upright.
- The game window shows the left eye.
- Post-processing (colour grading, bloom, drunk wobble) now works in VR, with an on/off setting.
- Fixes: a pink lens outline in the world, round end blocking interaction, the desktop mouse pulling
  menu clicks away from the laser.

## 0.2.0 (alpha)

First version tested on a real headset (Valve Index).

- Renders to the headset: the game ships its render pipeline without VR support, so the mod swaps in a
  VR-enabled build and starts URP's XR system (game post-processing was off in VR).
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
