using BepInEx.Configuration;

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
        public readonly ConfigEntry<StereoMode> RenderMode;
        public readonly ConfigEntry<float> RenderScale;

        public readonly ConfigEntry<TurnMode> Turning;
        public readonly ConfigEntry<float> SnapTurnAngle;
        public readonly ConfigEntry<float> SmoothTurnSpeed;
        public readonly ConfigEntry<bool> LeftHandedPointer;

        public readonly ConfigEntry<float> UIDistance;
        public readonly ConfigEntry<float> UIWidth;

        public readonly ConfigEntry<bool> DevCommands;

        public VRConfig(ConfigFile config)
        {
            EnableVR = config.Bind("General", "EnableVR", true,
                "Start the game in VR. You can also launch with --disable-vr to play flat once.");
            OpenXRRuntimeFile = config.Bind("General", "OpenXRRuntimeFile", "",
                "Optional path to an OpenXR runtime json to use instead of the system default (for example SteamVR's steamxr_win64.json).");

            RenderMode = config.Bind("Rendering", "StereoMode", StereoMode.MultiPass,
                "MultiPass is the most compatible. SinglePassInstanced is faster but some game shaders may render in one eye only.");
            RenderScale = config.Bind("Rendering", "RenderScale", 1f,
                new ConfigDescription("Headset render resolution multiplier.", new AcceptableValueRange<float>(0.5f, 2f)));

            Turning = config.Bind("Controls", "TurnMode", TurnMode.Snap, "How the right thumbstick turns you.");
            SnapTurnAngle = config.Bind("Controls", "SnapTurnAngle", 45f,
                new ConfigDescription("Degrees per snap turn.", new AcceptableValueRange<float>(10f, 90f)));
            SmoothTurnSpeed = config.Bind("Controls", "SmoothTurnSpeed", 120f,
                new ConfigDescription("Degrees per second for smooth turning.", new AcceptableValueRange<float>(30f, 360f)));
            LeftHandedPointer = config.Bind("Controls", "LeftHandedPointer", false,
                "Use the left controller as the menu pointer.");

            UIDistance = config.Bind("UI", "Distance", 1.6f,
                new ConfigDescription("How far in front of you menus and the HUD float, in meters.", new AcceptableValueRange<float>(0.5f, 5f)));
            UIWidth = config.Bind("UI", "Width", 1.8f,
                new ConfigDescription("Width of menus and the HUD, in meters.", new AcceptableValueRange<float>(0.5f, 5f)));

            DevCommands = config.Bind("Debug", "DevCommands", false,
                "For mod development: run commands written to BepInEx/gwyfvr-command.txt (screenshot, dump).");
        }
    }
}
