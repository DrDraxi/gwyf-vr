using HarmonyLib;

namespace GWYFVR.Patches
{
    [HarmonyPatch]
    internal static class SplashPatches
    {
        /// <summary>
        /// The start-up splash asks "flip a coin?" with a coin toss scene. Answer "No" automatically so
        /// VR players go straight to the main menu.
        /// </summary>
        [HarmonyPatch(typeof(OfflineSplashCoinFlip), "Awake")]
        [HarmonyPostfix]
        private static void SkipSplash(OfflineSplashCoinFlip __instance)
        {
            if (Plugin.Settings.SkipSplash.Value)
                __instance.Invoke(nameof(OfflineSplashCoinFlip.OnChooseNo), 0.5f);
        }
    }
}
