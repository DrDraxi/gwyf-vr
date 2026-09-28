using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GWYFVR.Patches
{
    /// <summary>
    /// Screen effects that are fine on a monitor but make people sick in a headset. They only show when
    /// post-processing is on in VR.
    /// </summary>
    [HarmonyPatch]
    internal static class ComfortEffectPatches
    {
        /// <summary>Bloom intensity the immunity buff glows at (the game uses 5).</summary>
        private const float ImmunityBloom = 1.5f;

        /// <summary>
        /// Motion blur and panini projection smear or warp the whole view, so every volume has them turned
        /// off. Lens distortion (the drunk wobble and feedback effects) too, unless the drunk effect is on.
        /// </summary>
        [HarmonyPatch(typeof(Volume), "OnEnable")]
        [HarmonyPostfix]
        private static void StripVolume(Volume __instance)
        {
            var profile = __instance.HasInstantiatedProfile() ? __instance.profile : __instance.sharedProfile;
            if (profile == null)
                return;
            if (!Plugin.Settings.DrunkEffect.Value && profile.TryGet<LensDistortion>(out var lens))
                lens.active = false;
            if (profile.TryGet<MotionBlur>(out var blur))
                blur.active = false;
            if (profile.TryGet<PaniniProjection>(out var panini))
                panini.active = false;
        }

        /// <summary>
        /// The drunk buff wobbles the view with lens distortion. With the "Drunk screen wobble" setting off
        /// it keeps its sound but not the wobble. The setting applies the next time you get drunk.
        /// </summary>
        [HarmonyPatch(typeof(PlayerBuffUI), nameof(PlayerBuffUI.StartPingPong))]
        [HarmonyPrefix]
        private static bool DrunkWobble(PlayerBuffUI __instance)
        {
            var lens = __instance._lensDistortion;
            if (Plugin.Settings.DrunkEffect.Value)
            {
                if (lens != null)
                    lens.active = true;
                return true;
            }

            if (__instance.drunkVolume != null)
                __instance.drunkVolume.weight = 0f;
            return false;
        }

        /// <summary>The immunity buff still glows, just not blindingly: its bloom target of 5 becomes 1.5.</summary>
        [HarmonyPatch(typeof(PlayerBuffUI), nameof(PlayerBuffUI.OnImmunityChanged))]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> SofterImmunityGlow(IEnumerable<CodeInstruction> instructions)
        {
            foreach (var instruction in instructions)
            {
                if (instruction.opcode == OpCodes.Ldc_R4 && instruction.operand is float f && f == 5f)
                    instruction.operand = ImmunityBloom;
                yield return instruction;
            }
        }
    }
}
