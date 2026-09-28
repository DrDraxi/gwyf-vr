using TMPro;
using UnityEngine;

namespace GWYFVR.UI
{
    /// <summary>Labels the settings tab that holds the VR settings "VR" instead of "Input".</summary>
    public class SettingsTabLabel : MonoBehaviour
    {
        private float nextScan;
        private readonly System.Collections.Generic.HashSet<Transform> logged = new System.Collections.Generic.HashSet<Transform>();

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
                // The tab buttons can sit anywhere on the settings screen's canvas.
                var canvas = settings.GetComponentInParent<Canvas>();
                var screen = canvas != null ? canvas.rootCanvas.transform : settings.transform.root;
                var found = false;
                foreach (var text in screen.GetComponentsInChildren<TMP_Text>(true))
                {
                    // Labels come wrapped in rich text tags ("<noparse></noparse>Input").
                    var value = System.Text.RegularExpressions.Regex.Replace(text.text, "<[^>]*>", "").Trim();
                    if (value == "VR" || !(string.Equals(value, tabName, System.StringComparison.OrdinalIgnoreCase) ||
                                           string.Equals(value, "Input", System.StringComparison.OrdinalIgnoreCase)))
                        continue;

                    // Keep the game's localization from putting the old name back.
                    foreach (var component in text.GetComponents<MonoBehaviour>())
                        if (component != text && component.GetType().Name.Contains("Locali"))
                            component.enabled = false;
                    text.text = "VR";
                    found = true;
                    Plugin.Log.LogInfo($"Relabelled settings tab '{text.name}' to VR");
                }

                if (!found && logged.Add(screen))
                    foreach (var button in screen.GetComponentsInChildren<UnityEngine.UI.Button>(true))
                    {
                        var label = button.GetComponentInChildren<TMP_Text>(true);
                        if (label != null && label.text.Length < 40)
                            Plugin.Log.LogInfo($"Settings tab candidate '{button.name}' text '{label.text}'");
                    }
            }
        }
    }
}
