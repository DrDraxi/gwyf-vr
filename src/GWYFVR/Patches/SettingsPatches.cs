using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TMPro;

namespace GWYFVR.Patches
{
    /// <summary>
    /// The VR settings (see VRSettingsMenu) aren't in the game's localization table, which would show
    /// "No translation found" instead of their labels. Use the English labels directly.
    /// </summary>
    [HarmonyPatch]
    internal static class SettingsPatches
    {
        private static bool IsVRSetting(SettingItemBase entry) => entry != null && entry.key != null && entry.key.StartsWith("gwyfvr.");

        private static IEnumerator Nothing()
        {
            yield break;
        }

        [HarmonyPatch(typeof(SettingsLocalization), nameof(SettingsLocalization.ApplyLabel))]
        [HarmonyPrefix]
        private static bool Label(TMP_Text label, SettingItemBase entry, ref IEnumerator __result)
        {
            if (!IsVRSetting(entry))
                return true;

            if (label != null)
                label.text = entry.DisplayLabel;
            __result = Nothing();
            return false;
        }

        [HarmonyPatch(typeof(SettingsLocalization), nameof(SettingsLocalization.ApplyDropdownLabels))]
        [HarmonyPrefix]
        private static bool Options(SettingItemBase entry, IList<string> sourceOptions, List<string> targetLabels, ref IEnumerator __result)
        {
            if (!IsVRSetting(entry) || entry is ToggleSettingItem)
                return true;

            targetLabels.Clear();
            if (sourceOptions != null)
                targetLabels.AddRange(sourceOptions);
            __result = Nothing();
            return false;
        }
    }
}
