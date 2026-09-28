using GWYFVR.Player;
using GWYFVR.UI;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GWYFVR.Input
{
    /// <summary>
    /// Right B is the voice button, following the game's voice chat mode setting: in push-to-talk mode you
    /// talk while holding it, in open-mic (voice activation) mode each press mutes or unmutes your
    /// microphone. While muted, a small "Mic muted" label stays at the bottom of your view.
    /// Muting only stops sending your voice, so players without the mod are unaffected.
    /// </summary>
    internal static class VRVoice
    {
        public static bool Muted { get; private set; }

        private static bool lastButton;
        private static GameObject indicator;

        private static bool PushToTalk => InputEvents.ProximityVoiceChatMode == VoiceChatInputMode.PushToTalk;

        /// <summary>Call once per frame with the voice button's state.</summary>
        public static void Update(bool button)
        {
            if (PushToTalk)
            {
                // Mute is an open-mic thing; push-to-talk already keeps the mic closed until you hold the button.
                if (Muted)
                    SetMuted(false);
                if (button != lastButton)
                    InputEvents.UpdatePushToTalk(button);
            }
            else
            {
                if (button && !lastButton)
                    SetMuted(!Muted);
                else if (Muted)
                    // The game re-applies trigger settings when voice settings change; keep our mute on top.
                    ApplyMute(true);
            }

            lastButton = button;
            UpdateIndicator();
        }

        private static void SetMuted(bool muted)
        {
            Muted = muted;
            ApplyMute(muted);
            Plugin.Log.LogInfo(muted ? "Microphone muted" : "Microphone unmuted");
        }

        // Dissonance isn't referenced at build time, so its triggers are reached by name.
        private static readonly System.Type CommsType = AccessTools.TypeByName("Dissonance.DissonanceComms");
        private static readonly System.Type[] TriggerTypes =
        {
            AccessTools.TypeByName("Dissonance.VoiceBroadcastTrigger"),
            AccessTools.TypeByName("Dissonance.VoiceProximityBroadcastTrigger"),
        };

        private static void ApplyMute(bool muted)
        {
            var comms = CommsType != null ? AccessTools.Method(CommsType, "GetSingleton")?.Invoke(null, null) as Component : null;
            if (comms == null)
                return;
            foreach (var type in TriggerTypes)
            {
                var trigger = type != null ? comms.GetComponent(type) : null;
                var property = trigger != null ? AccessTools.Property(type, "IsMuted") : null;
                if (property != null && (bool)property.GetValue(trigger) != muted)
                    property.SetValue(trigger, muted);
            }
        }

        private static void UpdateIndicator()
        {
            var rig = VRRig.Instance;
            if (rig == null || rig.VRCamera == null)
                return;

            if (indicator == null)
                indicator = CreateIndicator(rig.VRCamera.transform);
            if (indicator.activeSelf != Muted)
                indicator.SetActive(Muted);
        }

        /// <summary>A small red "Mic muted" label fixed low in the view, drawn over the world by the UI camera.</summary>
        private static GameObject CreateIndicator(Transform head)
        {
            var root = new GameObject("VRMicMuted");
            root.transform.SetParent(head, false);
            root.transform.localPosition = new Vector3(0f, -0.28f, 0.9f);
            root.transform.localRotation = Quaternion.Euler(15f, 0f, 0f);
            root.transform.localScale = Vector3.one * 0.001f;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 1000;
            ((RectTransform)root.transform).sizeDelta = new Vector2(220f, 56f);

            var background = new GameObject("Background").AddComponent<Image>();
            background.transform.SetParent(root.transform, false);
            background.color = new Color(0.75f, 0.1f, 0.1f, 0.85f);
            Stretch(background.rectTransform);

            var label = new GameObject("Label").AddComponent<TextMeshProUGUI>();
            label.transform.SetParent(root.transform, false);
            if (TMP_Settings.defaultFontAsset != null)
                label.font = TMP_Settings.defaultFontAsset;
            label.text = "MIC MUTED";
            label.fontSize = 30f;
            label.fontStyle = FontStyles.Bold;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            background.raycastTarget = false;
            Stretch(label.rectTransform);

            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = WorldSpaceUI.Layer;

            root.SetActive(false);
            return root;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
