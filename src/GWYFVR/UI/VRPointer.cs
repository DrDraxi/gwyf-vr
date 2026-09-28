using GWYFVR.Input;
using GWYFVR.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.XR;

namespace GWYFVR.UI
{
    /// <summary>
    /// Lets a controller point at and click the world space menus, and draws simple controllers plus a
    /// laser while a menu is pointed at.
    ///
    /// The game's Input System has no XR support, so tracked-device UI input isn't available. Instead a
    /// (never rendering) camera sits on the controller and is used as every menu canvas's event camera,
    /// and a virtual mouse permanently "points" at the centre of that camera's screen. The game's normal
    /// mouse UI handling then hits whatever the controller points at; the trigger is the mouse button.
    /// </summary>
    public class VRPointer : MonoBehaviour
    {
        public static VRPointer Instance { get; private set; }

        /// <summary>Event camera placed on the pointing controller.</summary>
        public Camera PointerCamera { get; private set; }

        public bool IsPointingAtMenu { get; private set; }

        /// <summary>Mouse wheel units per second at full stick.</summary>
        private const float MenuScrollSpeed = 1500f;

        /// <summary>True while the pointing controller is tracked and the pointer camera follows it.</summary>
        public bool HandTracked { get; private set; }

        private Mouse mouse;
        private LineRenderer laser;
        private Transform leftHand;
        private Transform rightHand;
        private Transform hitDot;

        private static bool LeftHanded => Plugin.Settings.LeftHandedPointer.Value;

        private void Awake()
        {
            Instance = this;

            var cameraObject = new GameObject("VRPointerCamera");
            cameraObject.transform.SetParent(transform, false);
            PointerCamera = cameraObject.AddComponent<Camera>();
            PointerCamera.enabled = false;
            PointerCamera.stereoTargetEye = StereoTargetEyeMask.None;
            PointerCamera.fieldOfView = 10f;
            PointerCamera.nearClipPlane = 0.01f;
            PointerCamera.cullingMask = 0;

            var material = VRMaterials.Unlit(new Color(0.85f, 0.85f, 0.9f));

            laser = new GameObject("VRLaser").AddComponent<LineRenderer>();
            laser.transform.SetParent(transform, false);
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = 0.004f;
            laser.endWidth = 0.002f;
            laser.material = material;
            laser.startColor = new Color(1f, 1f, 1f, 0.8f);
            laser.endColor = new Color(1f, 1f, 1f, 0.1f);
            laser.gameObject.layer = WorldSpaceUI.Layer;

            leftHand = CreateHand("VRLeftHand", material);
            rightHand = CreateHand("VRRightHand", material);

            hitDot = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform;
            hitDot.name = "VRLaserDot";
            Destroy(hitDot.GetComponent<Collider>());
            hitDot.SetParent(transform, false);
            hitDot.localScale = Vector3.one * 0.012f;
            hitDot.GetComponent<Renderer>().sharedMaterial = material;
            hitDot.gameObject.layer = WorldSpaceUI.Layer;
        }

        private void OnDestroy()
        {
            if (mouse != null)
                InputSystem.RemoveDevice(mouse);
        }

        private Transform CreateHand(string name, Material material)
        {
            var hand = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            hand.name = name;
            Destroy(hand.GetComponent<Collider>());
            hand.SetParent(transform, false);
            hand.localScale = new Vector3(0.03f, 0.03f, 0.1f);
            var renderer = hand.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return hand;
        }

        private void Update()
        {
            XRControllers.Poll();
            HandRoles.Update();
            var controller = HandRoles.Active;

            // Created lazily: the Input System isn't ready yet while plugins load.
            if (mouse == null)
            {
                try
                {
                    mouse = InputSystem.AddDevice<Mouse>("GWYFVR Pointer");
                }
                catch (System.Exception ex)
                {
                    Plugin.Log.LogWarning($"Could not add the VR pointer mouse yet: {ex.Message}");
                    enabled = false;
                    return;
                }
            }

            // Leave the real mouse alone while the calibration sliders are open.
            if (HandCalibration.Visible)
                return;

            // Keep the virtual mouse on the centre of the pointer camera, the trigger is its left button.
            // The pointing hand's stick scrolls whatever menu it points at (settings lists etc.).
            var scroll = IsPointingAtMenu && Mathf.Abs(controller.Stick.y) > 0.2f ? controller.Stick.y * MenuScrollSpeed * Time.unscaledDeltaTime : 0f;

            var state = new MouseState
                {
                    position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                    scroll = new Vector2(0f, scroll),
                }
                .WithButton(MouseButton.Left, controller.Trigger && IsPointingAtMenu);
            InputSystem.QueueStateEvent(mouse, state);
        }

        private void LateUpdate()
        {
            var rig = VRRig.Instance;
            var ui = WorldSpaceUI.Instance;
            var origin = rig != null ? rig.transform : null;

            var leftTracked = origin != null && Place(leftHand, origin, XRNode.LeftHand);
            var rightTracked = origin != null && Place(rightHand, origin, XRNode.RightHand);
            // The game's own hands follow the controllers in game (VRHands), the cubes are for menus.
            // Menu renderers can't draw these (magenta), so they're only a fallback in game.
            var playing = rig != null && rig.PlayerMode;
            var gameHands = VRHands.Instance != null && VRHands.Instance.Active;
            leftHand.gameObject.SetActive(leftTracked && playing && !gameHands);
            rightHand.gameObject.SetActive(rightTracked && playing && !gameHands);

            IsPointingAtMenu = false;
            HandTracked = false;

            if (origin == null || !XRControllers.TryGetPointerPose(HandRoles.ActiveHand, out var aimPos, out var aimRot))
            {
                laser.enabled = false;
                hitDot.gameObject.SetActive(false);
                return;
            }

            HandTracked = true;
            // The pointer camera is also what in-game interaction aims with (see InteractionPatches).
            PointerCamera.transform.SetPositionAndRotation(origin.TransformPoint(aimPos), origin.rotation * aimRot);
            var ray = new Ray(PointerCamera.transform.position, PointerCamera.transform.forward);
            var hit = Vector3.zero;
            IsPointingAtMenu = ui != null && ui.Raycast(ray, out hit);

            // In game, show the laser only while it points at something you can interact with.
            var inGame = !IsPointingAtMenu && rig.PlayerMode && Plugin.Settings.HandInteraction.Value && HasInteractTarget();
            if (inGame)
                hit = Physics.Raycast(ray, out var worldHit, 3f, ~(1 << WorldSpaceUI.Layer), QueryTriggerInteraction.Ignore)
                    ? worldHit.point
                    : ray.GetPoint(3f);

            laser.enabled = IsPointingAtMenu || inGame;
            hitDot.gameObject.SetActive(IsPointingAtMenu);
            if (!laser.enabled)
                return;

            laser.SetPosition(0, ray.origin);
            laser.SetPosition(1, hit);
            hitDot.position = hit;
        }

        private static bool HasInteractTarget()
        {
            var controller = LocalPlayer.Controller;
            var interact = controller != null ? controller.GetComponent<PlayerInteract>() : null;
            return interact != null && interact.TargetInteractable != null;
        }

        private static bool Place(Transform hand, Transform origin, XRNode node)
        {
            if (!XRControllers.TryGetAimPose(node, out var position, out var rotation))
                return false;

            hand.SetPositionAndRotation(origin.TransformPoint(position), origin.rotation * rotation);
            return true;
        }
    }
}
