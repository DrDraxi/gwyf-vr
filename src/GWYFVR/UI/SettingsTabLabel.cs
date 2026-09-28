using TMPro;
using UnityEngine;

namespace GWYFVR.UI
{
    /// <summary>Labels the settings tab that holds the VR settings "VR" instead of "Input".</summary>
    public class SettingsTabLabel : MonoBehaviour
    {
        private float nextScan;

        private void Update()
        {
            if (Time.unscaledTime < nextScan)
                return;
            nextScan = Time.unscaledTime + 0.5f;

            var tabName = VRSettingsMenu.VRTabName;
            if (string.IsNullOrEmpty(tabName))
                return;

            foreach (var settings in Object.FindObjectsByType<SettingsLayoutRuntimeUI>(FindObjectsSortMode.None))
            {
                // The tab buttons sit next to the settings content, under the same screen.
                var screen = settings.transform.parent != null ? settings.transform.parent : settings.transform;
                foreach (var text in screen.GetComponentsInChildren<TMP_Text>(true))
                {
                    var value = text.text.Trim();
                    if (value == "VR" || !(string.Equals(value, tabName, System.StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(value, "Input", System.StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Keep the game's localization from putting the old name back.
                    foreach (var component in text.GetComponents<MonoBehaviour>())
                        if (component != text && component.GetType().Name.Contains("Locali"))
                            component.enabled = false;
                    text.text = "VR";
                }
            }
        }
    }
}
