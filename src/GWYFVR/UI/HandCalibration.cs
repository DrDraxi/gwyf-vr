using GWYFVR.Player;
using UnityEngine;

namespace GWYFVR.UI
{
    /// <summary>
    /// On-screen sliders (on the desktop window) for lining the game's hands up with the controllers.
    /// Toggle with F8. Values are saved to the mod's config as they change.
    /// </summary>
    public class HandCalibration : MonoBehaviour
    {
        public static bool Visible => instance != null && instance.visible;

        private static HandCalibration instance;
        private bool visible;

        private void Awake() => instance = this;
        private Rect window = new Rect(20, 20, 420, 520);

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
                visible = !visible;
        }

        /// <summary>The game locks the cursor while playing; free it while the sliders are open.</summary>
        private void LateUpdate()
        {
            if (!visible)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!visible)
                return;

            window = GUILayout.Window(0x6E7F, window, Draw, "GWYFVR hand calibration (F8)");
        }

        private void Draw(int id)
        {
            VRHands.RotationOffset = Vector3Sliders("Hand rotation", VRHands.RotationOffset, -180f, 180f, 1f);
            VRHands.PositionOffset = Vector3Sliders("Hand position (m)", VRHands.PositionOffset, -0.3f, 0.3f, 0.005f);
            VRHands.ItemOffset = Vector3Sliders("Item offset (m)", VRHands.ItemOffset, -0.3f, 0.3f, 0.005f);

            Plugin.Settings.LaserRotation.Value = Vector3Sliders("Laser rotation (pitch, yaw, roll)", Plugin.Settings.LaserRotation.Value, -90f, 90f, 1f);

            if (GUILayout.Button("Reset to defaults"))
            {
                Plugin.Settings.HandRotation.Value = (Vector3)Plugin.Settings.HandRotation.DefaultValue;
                Plugin.Settings.HandPosition.Value = (Vector3)Plugin.Settings.HandPosition.DefaultValue;
                Plugin.Settings.ItemOffset.Value = (Vector3)Plugin.Settings.ItemOffset.DefaultValue;
                Plugin.Settings.LaserRotation.Value = (Vector3)Plugin.Settings.LaserRotation.DefaultValue;
            }

            GUI.DragWindow();
        }

        private static Vector3 Vector3Sliders(string label, Vector3 value, float min, float max, float step)
        {
            GUILayout.Label($"{label}: {value.x:0.###}, {value.y:0.###}, {value.z:0.###}");
            value.x = Slider("X", value.x, min, max, step);
            value.y = Slider("Y", value.y, min, max, step);
            value.z = Slider("Z", value.z, min, max, step);
            return value;
        }

        private static float Slider(string axis, float value, float min, float max, float step)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(axis, GUILayout.Width(15));
            var result = GUILayout.HorizontalSlider(value, min, max);
            GUILayout.EndHorizontal();
            return Mathf.Round(result / step) * step;
        }
    }
}
