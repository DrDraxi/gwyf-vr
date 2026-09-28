using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine;

namespace GWYFVR.UI
{
    /// <summary>
    /// Puts the mod's VR options into the game's own settings menu. The desktop key rebinding entries
    /// (useless with VR controllers) are replaced by VR entries built from the game's own setting item
    /// types, so they look and behave like every other setting. Values live in the mod's config.
    /// </summary>
    internal static class VRSettingsMenu
    {
        private const string KeyPrefix = "gwyfvr.";

        private static readonly Dictionary<SettingItemBase, Action<SettingItemBase>> Appliers =
            new Dictionary<SettingItemBase, Action<SettingItemBase>>();

        private static readonly List<SettingItemBase> Entries = new List<SettingItemBase>();

        public static void Install()
        {
            var layout = Resources.Load<SettingsLayout>("SettingsLayout");
            if (layout == null)
            {
                Plugin.Log.LogWarning("Game settings layout not found, VR settings are only in the config file");
                return;
            }

            // The tab holding the key rebinding entries becomes the VR tab.
            var tab = layout.tabs.FirstOrDefault(t => t != null && t.entries.Any(e => e is RebindSettingItem));
            if (tab == null)
            {
                Plugin.Log.LogWarning("No input settings tab found, VR settings are only in the config file");
                return;
            }

            if (tab.entries.Any(e => e != null && e.key != null && e.key.StartsWith(KeyPrefix)))
                return;

            var s = Plugin.Settings;
            BuildEntries(s);

            // Keep the tab's other entries (e.g. mouse sensitivity is harmless), drop the rebinds.
            tab.entries.RemoveAll(e => e is RebindSettingItem);
            tab.entries.InsertRange(0, Entries);

            SettingItemBase.SettingsChanged += OnSettingChanged;
            Plugin.Log.LogInfo($"Added VR settings to the '{tab.tabName}' settings tab");
        }

        private static void BuildEntries(VRConfig s)
        {
            Entries.Clear();
            Appliers.Clear();

            Add(Title("VR"));

            var turn = Dropdown("turnmode", "Turning", new[] { "Snap", "Smooth" }, (int)s.Turning.Value, (int)(TurnMode)s.Turning.DefaultValue);
            Add(turn, e => s.Turning.Value = (TurnMode)((DropdownSettingItem)e).index);

            Add(Slider("snapangle", "Snap turn angle", 10f, 90f, true, s.SnapTurnAngle), e => s.SnapTurnAngle.Value = ((SliderSettingItem)e).value);
            Add(Slider("turnspeed", "Smooth turn speed", 30f, 360f, true, s.SmoothTurnSpeed), e => s.SmoothTurnSpeed.Value = ((SliderSettingItem)e).value);
            Add(Toggle("roomscale", "Room-scale movement", s.RoomScale), e => s.RoomScale.Value = ((ToggleSettingItem)e).value);
            Add(Toggle("throwing", "Physical throwing", s.PhysicalThrowing), e => s.PhysicalThrowing.Value = ((ToggleSettingItem)e).value);
            Add(Slider("throwstrength", "Throw strength", 0.5f, 5f, false, s.ThrowStrength), e => s.ThrowStrength.Value = ((SliderSettingItem)e).value);
            Add(Toggle("handaim", "Aim with your hand", s.HandInteraction), e => s.HandInteraction.Value = ((ToggleSettingItem)e).value);
            Add(Toggle("proximitygrab", "Grab by touch", s.ProximityGrab), e => s.ProximityGrab.Value = ((ToggleSettingItem)e).value);
            Add(Toggle("lefthanded", "Left handed menu pointer", s.LeftHandedPointer), e => s.LeftHandedPointer.Value = ((ToggleSettingItem)e).value);

            Add(Title("VR display"));
            Add(Slider("handscale", "Hand size", 0.2f, 1.5f, false, s.HandScale), e => s.HandScale.Value = ((SliderSettingItem)e).value);
            Add(Slider("itemscale", "Held item size", 0.2f, 1.5f, false, s.HeldItemScale), e => s.HeldItemScale.Value = ((SliderSettingItem)e).value);
            Add(Slider("huddistance", "HUD distance", 0.5f, 3f, false, s.HudDistance), e => s.HudDistance.Value = ((SliderSettingItem)e).value);
            Add(Slider("hudwidth", "HUD size", 0.5f, 3f, false, s.HudWidth), e => s.HudWidth.Value = ((SliderSettingItem)e).value);
            Add(Slider("menudistance", "Menu distance", 0.5f, 5f, false, s.UIDistance), e => s.UIDistance.Value = ((SliderSettingItem)e).value);
            Add(Toggle("drunkeffect", "Drunk screen wobble", s.DrunkEffect), e => s.DrunkEffect.Value = ((ToggleSettingItem)e).value);
            Add(Toggle("skipsplash", "Skip the start-up coin flip", s.SkipSplash), e => s.SkipSplash.Value = ((ToggleSettingItem)e).value);
        }

        private static void Add(SettingItemBase entry, Action<SettingItemBase> apply = null)
        {
            Entries.Add(entry);
            if (apply != null)
                Appliers[entry] = apply;
        }

        private static void OnSettingChanged(SettingItemBase entry)
        {
            if (entry != null && Appliers.TryGetValue(entry, out var apply))
                apply(entry);
        }

        private static T Create<T>(string key, string label) where T : SettingItemBase
        {
            var item = ScriptableObject.CreateInstance<T>();
            item.hideFlags = HideFlags.HideAndDontSave;
            item.name = KeyPrefix + key;
            item.key = KeyPrefix + key;
            item.label = label;
            // A key that doesn't exist in the game's localization table, so the label is used as is.
            item.localizationKey = KeyPrefix + key;
            return item;
        }

        private static TitleSettingItem Title(string label) => Create<TitleSettingItem>("title." + label.ToLowerInvariant(), label);

        private static ToggleSettingItem Toggle(string key, string label, ConfigEntry<bool> config)
        {
            var item = Create<ToggleSettingItem>(key, label);
            item.value = config.Value;
            item.defaultValue = (bool)config.DefaultValue;
            return item;
        }

        private static SliderSettingItem Slider(string key, string label, float min, float max, bool whole, ConfigEntry<float> config)
        {
            var item = Create<SliderSettingItem>(key, label);
            item.min = min;
            item.max = max;
            item.wholeNumbers = whole;
            item.value = Mathf.Clamp(config.Value, min, max);
            item.defaultValue = Mathf.Clamp((float)config.DefaultValue, min, max);
            return item;
        }

        private static DropdownSettingItem Dropdown(string key, string label, string[] options, int index, int defaultIndex)
        {
            var item = Create<DropdownSettingItem>(key, label);
            item.options = new List<string>(options);
            item.index = Mathf.Clamp(index, 0, options.Length - 1);
            return item;
        }
    }
}
