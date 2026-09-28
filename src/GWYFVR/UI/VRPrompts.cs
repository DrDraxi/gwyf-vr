using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GWYFVR.UI
{
    /// <summary>
    /// Replaces keyboard/mouse prompts ("[E] Pick up", "Left Click: Use") with VR controller glyphs
    /// from Kenney's Input Prompts pack (CC0, www.kenney.nl), shipped in the mod's Prompts folder.
    /// </summary>
    internal static class VRPrompts
    {
        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static string folder;

        public static void Init(string pluginFolder)
        {
            folder = Path.Combine(pluginFolder, "Prompts");
            if (!Directory.Exists(folder))
                Plugin.Log.LogWarning("Prompts folder missing, keyboard prompts stay as they are");
        }

        /// <summary>The controller glyph for a keyboard/mouse key the game shows, or null to leave it alone.</summary>
        public static Sprite ForKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return null;

            switch (key.Trim().ToLowerInvariant())
            {
                case "e":
                case "interact":
                    // Locked in place (the spawn box) A interacts; items are grabbed with the grip,
                    // everything else is used with the trigger.
                    if (PlayerLocked())
                        return Load("quest_button_a");
                    return Load(InteractTargetIsItem() ? "quest_grip_right" : "quest_trigger_right");
                case "left click":
                case "lmb":
                case "mouse0":
                case "left mouse":
                    return Load("quest_trigger_right");
                case "q":
                    return Load("quest_grip_right");
                case "middle click":
                case "mmb":
                    return Load("quest_button_a");
                case "space":
                    return Load("quest_stick_r_press");
                case "ctrl":
                case "control":
                case "left ctrl":
                    return Load("quest_button_b");
                case "shift":
                case "left shift":
                    return Load("quest_stick_l_press");
                case "r":
                    return Load("quest_button_x");
                case "esc":
                case "escape":
                    return Load("quest_button_menu");
                default:
                    return null;
            }
        }

        private static bool PlayerLocked()
        {
            var controller = Player.LocalPlayer.Controller;
            return controller != null && controller.IsLocked;
        }

        private static bool InteractTargetIsItem()
        {
            var controller = Player.LocalPlayer.Controller;
            var interact = controller != null ? controller.GetComponent<PlayerInteract>() : null;
            return interact != null && interact.TargetInteractable is Item;
        }

        private static Sprite Load(string name)
        {
            if (Sprites.TryGetValue(name, out var sprite))
                return sprite;

            var path = folder != null ? Path.Combine(folder, name + ".png") : null;
            if (path == null || !File.Exists(path))
            {
                Sprites[name] = null;
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name };
            texture.LoadImage(File.ReadAllBytes(path));
            texture.hideFlags = HideFlags.HideAndDontSave;
            sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Sprites[name] = sprite;
            return sprite;
        }

        /// <summary>
        /// Show a glyph instead of a key: the key button's own image becomes the glyph (it keeps its size
        /// and layout), and the key's letter is hidden.
        /// </summary>
        public static void ShowGlyph(GameObject keyButton, Sprite glyph)
        {
            foreach (var text in keyButton.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (!text.gameObject.name.ToLowerInvariant().Contains("hold"))
                    text.enabled = false;

            var image = keyButton.GetComponent<Image>();
            if (image == null)
                return;

            image.enabled = true;
            image.gameObject.SetActive(true);
            image.sprite = glyph;
            image.color = Color.white;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }
    }

    [HarmonyPatch]
    internal static class PromptPatches
    {
        /// <summary>Interaction prompts like "[E] Pick up".</summary>
        [HarmonyPatch(typeof(KeyButtonManager), nameof(KeyButtonManager.CreateKeyButton))]
        [HarmonyPostfix]
        private static void InteractionKey(string keyText, GameObject __result)
        {
            if (__result == null)
                return;

            // The game shows the Interact binding's display name, which isn't always "E".
            var glyph = VRPrompts.ForKey(keyText) ?? VRPrompts.ForKey(IsInteractBinding(keyText) ? "e" : null);
            if (glyph != null)
                VRPrompts.ShowGlyph(__result, glyph);
        }

        /// <summary>
        /// The keyboard/mouse control hints in the corner of the screen ("Throw HOLD", "Ping") make no
        /// sense in VR; the prompts on the things you point at are enough.
        /// </summary>
        [HarmonyPatch(typeof(HeldItemActionPanel), nameof(HeldItemActionPanel.CreateActionElement))]
        [HarmonyPrefix]
        private static bool HideControlHints() => false;

        /// <summary>Held item action hints like "Left Click: Use".</summary>
        [HarmonyPatch(typeof(HeldItemActionPanel), nameof(HeldItemActionPanel.SetupKeyButton))]
        [HarmonyPostfix]
        private static void HeldItemKey(GameObject keyButton, string key)
        {
            var glyph = VRPrompts.ForKey(key);
            if (glyph == null)
                return;

            VRPrompts.ShowGlyph(keyButton, glyph);
        }

        private static bool IsInteractBinding(string keyText)
        {
            if (InputReader.Instance == null || string.IsNullOrWhiteSpace(keyText))
                return false;
            return string.Equals(InputReader.Instance.GetBindingDisplayName("Interact", 0), keyText, StringComparison.OrdinalIgnoreCase);
        }
    }
}
