# GWYFVR

A VR mod for [Gamble With Your Friends](https://store.steampowered.com/app/3892270/) built on BepInEx 5.
Player-facing docs (controls, settings, troubleshooting) live in [thunderstore/README.md](thunderstore/README.md).

## How it works

The game is a Unity 6 (6000.3), Mono, URP build with its XR support stripped, so the mod brings its own:

- **Preloader patcher** (`src/GWYFVR.Preload`) registers Unity's OpenXR subsystem before the engine scans for it
  (subsystem manifest plus `UnityOpenXR.dll`/`openxr_loader.dll` in `Plugins/x86_64`). It also swaps in XR-enabled
  builds of URP and Core RP: their serialized layout is aligned to the game's copies with Mono.Cecil, written to
  `BepInEx/cache/GWYFVR`, and loaded through Doorstop's search path.
- **OpenXR bootstrap** (`XR/OpenXRBootstrap.cs`) creates the XR management settings, loader and interaction
  profiles at runtime. `XR/URPXRSetup.cs` starts URP's XR system with shaders from the `gwyfvr_xrshaders` bundle;
  the occlusion and visibility meshes stay off because the game's shaders can't draw them.
- **VR rig** (`Player/`) renders from its own stereo camera that follows whichever camera the game is using. While
  you play, the headset drives the player's head, room-scale moves the body, the game's hands follow the
  controllers, and releasing a grip throws with the hand's velocity. `DesktopMirror` copies the left eye to the
  game window.
- **Input** (`Input/`): the game's Input System has no XR either, so poses and buttons come from
  `UnityEngine.XR.InputDevices` and are fed to the game as a virtual gamepad. Harmony patches (`Patches/`) make
  interaction, items, pings and sliders aim with the hand.
- **UI** (`UI/`) moves screen space canvases onto world space panels drawn by an overlay camera. A laser drives a
  virtual mouse through a hand-mounted pointer camera. Full-screen backgrounds are drawn on a sphere around you,
  the main menu gets the home scene as scenery, and the settings menu gets a VR tab.

Nothing the game sends over the network changes, so VR players stay compatible with players without the mod.
This follows the approach of DaXcess's [LCVR](https://github.com/DaXcess/LCVR) and [RepoXR](https://github.com/DaXcess/RepoXR).

## Building

Requirements: .NET SDK 8+, the game installed, and Unity **6000.3.x** (only to produce the OpenXR runtime libraries).

1. Build the OpenXR runtime dependencies once. This creates `lib/RuntimeDeps`:
   ```
   "C:\Program Files\Unity\Hub\Editor\6000.3.7f1\Editor\Unity.exe" -batchmode -quit -projectPath UnityProject -executeMethod BuildRuntimeDeps.Build -logFile -
   ```
2. If the game is not in the default Steam folder, create `GWYFVR.props.user` next to `Directory.Build.props`:
   ```xml
   <Project><PropertyGroup><GameDir>D:\Games\Gamble With Your Friends</GameDir></PropertyGroup></Project>
   ```
3. `scripts/deploy.ps1` builds and copies the mod into a game install that has BepInEx 5 installed.
4. `scripts/package.ps1` produces a Thunderstore zip in `dist/`.

### Testing without a headset

Enable SteamVR's null driver (in `Steam/config/steamvr.vrsettings` set `"steamvr": { "forcedDriver": "null" }` and
`"driver_null": { "enable": true }`). With `DevCommands = true` in the mod config, `scripts/dev.ps1` can drive the
game and save what the headset sees:

```
./scripts/dev.ps1 -Wait 8 -Commands "click Deny","click =Host Button","click =Confirm","click =SaveSlot","click SELECT"
./scripts/dev.ps1 -Commands "key E","key W 1.5","capture","dump"
```

Launching `Gamble With Your Friends.exe` directly needs a `steam_appid.txt` containing `3892270` in the game folder.

## License

MIT for the mod's source. The OpenXR runtime libraries shipped in the package come from Unity's
`com.unity.xr.openxr` package and are covered by the Unity Companion License.
