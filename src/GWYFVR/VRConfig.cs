using BepInEx.Configuration;
using UnityEngine;

namespace GWYFVR
{
    internal enum TurnMode
    {
        Snap,
        Smooth,
    }

    internal enum StereoMode
    {
        MultiPass,
        SinglePassInstanced,
    }

    internal class VRConfig
    {
        public readonly ConfigEntry<bool> EnableVR;
        public readonly ConfigEntry<string> OpenXRRuntimeFile;
        public readonly ConfigEntry<bool> SkipSplash;
        public readonly ConfigEntry<StereoMode> RenderMode;
        public readonly ConfigEntry<float> RenderScale;
        public readonly ConfigEntry<bool> PostProcessing;

        public readonly ConfigEntry<TurnMode> Turning;
        public readonly ConfigEntry<float> SnapTurnAngle;
        public readonly ConfigEntry<float> SmoothTurnSpeed;
        public readonly ConfigEntry<bool> LeftHandedPointer;

        public readonly ConfigEntry<Vector3> HandRotation;
        public readonly ConfigEntry<Vector3> HandPosition;
        public readonly ConfigEntry<Vector3> ItemOffset;

        public readonly ConfigEntry<float> UIDistance;
        public readonly ConfigEntry<float> UIWidth;

        public readonly ConfigEntry<bool> DevCommands;

        public VRConfig(ConfigFile config)
        {
            EnableVR = config.Bind("General", "EnableVR", true,
                "Start the game in VR. You can also launch with --disable-vr to play flat once.");
            OpenXRRuntimeFile = config.Bind("General", "OpenXRRuntimeFile", "",
                "Optional path to an OpenXR runtime json to use instead of the system default (for example SteamVR's steamxr_win64.json).");

            SkipSplash = config.Bind("General", "SkipSplash", true,
                "Skip the coin flip question at start-up and go straight to the main menu.");

            RenderMode = config.Bind("Rendering", "StereoMode", StereoMode.MultiPass,
                "MultiPass is the most compatible. SinglePassInstanced is faster but some game shaders may render in one eye only.");
            RenderScale = config.Bind("Rendering", "RenderScale", 1f,
                new ConfigDescription("Headset render resolution multiplier.", new AcceptableValueRange<float>(0.5f, 2f)));

            PostProcessing = config.Bind("Rendering", "PostProcessing", false,
                "Use the game's post-processing effects. They currently render black in VR, leave off.");

            Turning = config.Bind("Controls", "TurnMode", TurnMode.Snap, "How the right thumbstick turns you.");
            SnapTurnAngle = config.Bind("Controls", "SnapTurnAngle", 45f,
                new ConfigDescription("Degrees per snap turn.", new AcceptableValueRange<float>(10f, 90f)));
            SmoothTurnSpeed = config.Bind("Controls", "SmoothTurnSpeed", 120f,
                new ConfigDescription("Degrees per second for smooth turning.", new AcceptableValueRange<float>(30f, 360f)));
            LeftHandedPointer = config.Bind("Controls", "LeftHandedPointer", false,
                "Use the left controller as the menu pointer.");

            HandRotation = config.Bind("Hands", "Rotation", new Vector3(10f, 95f, 138f),
                "Rotation from the right controller to the right hand, in degrees (mirrored for the left hand). Tune in game with F8.");
            HandPosition = config.Bind("Hands", "Position", new Vector3(0.035f, 0.05f, 0.02f),
                "Offset from the right controller to the right hand, in meters (mirrored for the left hand).");
            ItemOffset = config.Bind("Hands", "ItemOffset", new Vector3(0.01f, 0f, 0.12f),
                "Offset from the right controller to held items, in meters.");

            UIDistance = config.Bind("UI", "Distance", 1.6f,
                new ConfigDescription("How far in front of you menus and the HUD float, in meters.", new AcceptableValueRange<float>(0.5f, 5f)));
            UIWidth = config.Bind("UI", "Width", 2.4f,
                new ConfigDescription("Width of menus and the HUD, in meters.", new AcceptableValueRange<float>(0.5f, 5f)));

            DevCommands = config.Bind("Debug", "DevCommands", false,
                "For mod development: run commands written to BepInEx/gwyfvr-command.txt (screenshot, dump).");
        }
    }
}
