using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace GWYFVR.UI
{
    /// <summary>Shows the game logo with "VR" added (main menu, pause menu) instead of the original.</summary>
    public class VRLogo : MonoBehaviour
    {
        private Sprite logo;
        private readonly HashSet<Image> replaced = new HashSet<Image>();
        private float nextScan;

        private void Awake()
        {
            var path = Path.Combine(Path.GetDirectoryName(typeof(Plugin).Assembly.Location), "logo-vr.png");
            if (!File.Exists(path))
            {
                Plugin.Log.LogWarning("logo-vr.png missing, keeping the game's logo");
                enabled = false;
                return;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true) { name = "GWYFVR logo" };
            texture.LoadImage(File.ReadAllBytes(path));
            texture.hideFlags = HideFlags.HideAndDontSave;
            logo = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f), 100f);
            logo.name = "GWYFVR logo";
            logo.hideFlags = HideFlags.HideAndDontSave;
        }

        private void LateUpdate()
        {
            // The logo's localizer can put the original back (language change), so keep checking.
            foreach (var image in replaced)
                if (image != null && image.sprite != logo)
                    image.sprite = logo;
            replaced.RemoveWhere(i => i == null);

            if (Time.unscaledTime < nextScan)
                return;
            nextScan = Time.unscaledTime + 1f;

            foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (replaced.Contains(image) || !IsGameLogo(image))
                    continue;

                Plugin.Log.LogInfo($"Replaced logo '{image.name}' (sprite '{image.sprite.name}')");
                image.sprite = logo;
                image.preserveAspect = true;
                replaced.Add(image);
            }
        }

        private static bool IsGameLogo(Image image)
        {
            var sprite = image.sprite;
            if (sprite == null)
                return false;
            var name = image.name.ToLowerInvariant();
            var spriteName = sprite.name.ToLowerInvariant();
            if (!name.Contains("logo") && !spriteName.Contains("logo"))
                return false;
            // The wide title logo, not small icons that happen to be called logo.
            return sprite.rect.width > sprite.rect.height * 1.5f;
        }
    }
}
