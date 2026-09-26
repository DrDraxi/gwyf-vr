using GWYFVR.Input;
using GWYFVR.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace GWYFVR.UI
{
    /// <summary>
    /// Lets a controller point at and click the world space menus through the game's own
    /// InputSystemUIInputModule, and draws simple controllers plus a laser while a menu is pointable.
    /// </summary>
    public class VRPointer : MonoBehaviour
    {
        private InputAction pointerPosition;
        private InputAction pointerRotation;
        private InputSystemUIInputModule configuredModule;

        private LineRenderer laser;
        private Transform leftHand;
        private Transform rightHand;
        private Transform hitDot;

        private bool LeftHanded => Plugin.Settings.LeftHandedPointer.Value;

        private void Awake()
        {
            var hand = LeftHanded ? "{LeftHand}" : "{RightHand}";
            pointerPosition = new InputAction("VRPointerPosition", InputActionType.PassThrough,
                $"<XRController>{hand}/pointerPosition", expectedControlType: "Vector3");
            pointerRotation = new InputAction("VRPointerRotation", InputActionType.PassThrough,
                $"<XRController>{hand}/pointerRotation", expectedControlType: "Quaternion");
            pointerPosition.Enable();
            pointerRotation.Enable();

            var material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("UI/Default"));

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
            ConfigureInputModule();
        }

        /// <summary>
        /// Point the game's UI input module at the controller aim pose (its defaults use the grip pose)
        /// and tell it tracked positions are relative to the VR rig.
        /// </summary>
        private void ConfigureInputModule()
        {
            var module = EventSystem.current != null ? EventSystem.current.currentInputModule as InputSystemUIInputModule : null;
            if (module == null || VRRig.Instance == null)
                return;

            module.xrTrackingOrigin = VRRig.Instance.transform;

            if (module == configuredModule)
                return;

            module.trackedDevicePosition = InputActionReference.Create(pointerPosition);
            module.trackedDeviceOrientation = InputActionReference.Create(pointerRotation);
            configuredModule = module;
            Plugin.Log.LogInfo("Configured UI input module for VR pointers");
        }

        private void LateUpdate()
        {
            var rig = VRRig.Instance;
            var ui = WorldSpaceUI.Instance;
            var visible = rig != null && rig.Source != null;

            leftHand.gameObject.SetActive(visible);
            rightHand.gameObject.SetActive(visible);
            if (!visible)
            {
                laser.enabled = false;
                hitDot.gameObject.SetActive(false);
                return;
            }

            var origin = rig.transform;
            Place(leftHand, origin, VRInput.LeftPosition, VRInput.LeftRotation);
            Place(rightHand, origin, VRInput.RightPosition, VRInput.RightRotation);

            var pointer = LeftHanded ? leftHand : rightHand;
            var ray = new Ray(pointer.position, pointer.forward);

            if (ui != null && ui.Raycast(ray, out var hit))
            {
                laser.enabled = true;
                laser.SetPosition(0, ray.origin);
                laser.SetPosition(1, hit);
                hitDot.gameObject.SetActive(true);
                hitDot.position = hit;
            }
            else
            {
                laser.enabled = false;
                hitDot.gameObject.SetActive(false);
            }
        }

        private static void Place(Transform hand, Transform origin, InputAction position, InputAction rotation)
        {
            hand.SetPositionAndRotation(
                origin.TransformPoint(VRInput.ReadPosition(position)),
                origin.rotation * VRInput.ReadRotation(rotation));
        }
    }
}
