using System.Collections.Generic;
using GWYFVR.Player;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GWYFVR.UI
{
    /// <summary>
    /// The flat main menu is a 2D screen, which in VR is a panel floating in a black void. This loads a
    /// game scene (the home plaza by default) additively behind the menu as static scenery: every game
    /// script, collider, rigidbody, camera, audio source and canvas in it is switched off, and anything it
    /// tries to keep alive across scenes is destroyed. The scenery is unloaded when you leave the menu.
    /// </summary>
    public class MenuBackdrop : MonoBehaviour
    {
        public const string MenuScene = "MainMenuScene";

        public static MenuBackdrop Instance { get; private set; }

        /// <summary>Where you stand in the scenery (set once it's loaded).</summary>
        public Transform Viewpoint { get; private set; }

        public bool Active => Viewpoint != null && backdrop.IsValid() && backdrop.isLoaded;

        private Scene backdrop;
        private bool loading;
        private HashSet<GameObject> persistentBefore;
        private GameObject persistentProbe;

        private void Awake()
        {
            Instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == MenuScene && !loading && !backdrop.IsValid())
                LoadBackdrop();
            else if (loading && scene.name == Plugin.Settings.MenuBackdropScene.Value && mode == LoadSceneMode.Additive)
                OnBackdropLoaded(scene);
            else if (mode == LoadSceneMode.Single && scene.name != MenuScene)
                Unload();
        }

        private void OnSceneUnloaded(Scene scene)
        {
            if (scene.name == MenuScene)
                Unload();
        }

        private void LoadBackdrop()
        {
            var name = Plugin.Settings.MenuBackdropScene.Value;
            if (string.IsNullOrWhiteSpace(name) || !Application.CanStreamedLevelBeLoaded(name))
                return;

            persistentBefore = PersistentObjects();
            loading = true;
            SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            Plugin.Log.LogInfo($"Loading {name} as the menu backdrop");
        }

        private void OnBackdropLoaded(Scene scene)
        {
            loading = false;
            backdrop = scene;

            // Anything the scene moved to DontDestroyOnLoad (managers, singletons) must not outlive it.
            foreach (var go in PersistentObjects())
                if (!persistentBefore.Contains(go) && go != persistentProbe && go != gameObject && go.transform.root != transform.root)
                {
                    Plugin.Log.LogInfo($"Backdrop: destroying persistent object '{go.name}'");
                    Destroy(go);
                }

            foreach (var root in scene.GetRootGameObjects())
                Neutralize(root);

            // Use the scenery's sky and lighting. RenderSettings belong to the active scene.
            var menu = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(scene);
            var skybox = RenderSettings.skybox;
            var ambientMode = RenderSettings.ambientMode;
            var ambientLight = RenderSettings.ambientLight;
            var ambientSky = RenderSettings.ambientSkyColor;
            var ambientEquator = RenderSettings.ambientEquatorColor;
            var ambientGround = RenderSettings.ambientGroundColor;
            var fog = RenderSettings.fog;
            var fogColor = RenderSettings.fogColor;
            var fogMode = RenderSettings.fogMode;
            var fogDensity = RenderSettings.fogDensity;
            var fogStart = RenderSettings.fogStartDistance;
            var fogEnd = RenderSettings.fogEndDistance;
            var sun = RenderSettings.sun;
            SceneManager.SetActiveScene(menu);
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientSkyColor = ambientSky;
            RenderSettings.ambientEquatorColor = ambientEquator;
            RenderSettings.ambientGroundColor = ambientGround;
            RenderSettings.fog = fog;
            RenderSettings.fogColor = fogColor;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogStartDistance = fogStart;
            RenderSettings.fogEndDistance = fogEnd;
            RenderSettings.sun = sun;

            var view = new GameObject("GWYFVR Menu Viewpoint");
            SceneManager.MoveGameObjectToScene(view, scene);
            view.transform.SetPositionAndRotation(Plugin.Settings.MenuBackdropPosition.Value,
                Quaternion.Euler(0f, Plugin.Settings.MenuBackdropYaw.Value, 0f));
            Viewpoint = view.transform;

            VRRig.Instance?.RefreshCamera();
            HideMenuBackgrounds();
            Plugin.Log.LogInfo("Menu backdrop ready");
        }

        private void Unload()
        {
            loading = false;
            Viewpoint = null;
            if (backdrop.IsValid() && backdrop.isLoaded)
            {
                SceneManager.UnloadSceneAsync(backdrop);
                Plugin.Log.LogInfo("Unloaded the menu backdrop");
            }

            backdrop = default;
            VRRig.Instance?.RefreshCamera();
        }

        /// <summary>
        /// The flat menu fills the screen with an opaque patterned background, which would hide the
        /// scenery. Hide full-screen graphics without anything clickable in them on the menu's canvases.
        /// </summary>
        private static void HideMenuBackgrounds()
        {
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isRootCanvas || canvas.gameObject.scene.name != MenuScene)
                    continue;

                var canvasRect = ((RectTransform)canvas.transform).rect;
                for (var i = 0; i < canvas.transform.childCount; i++)
                {
                    var child = canvas.transform.GetChild(i);
                    var graphic = child.GetComponent<UnityEngine.UI.Graphic>();
                    if (graphic == null || !graphic.enabled || !child.gameObject.activeInHierarchy)
                        continue;
                    if (child.GetComponentInChildren<UnityEngine.UI.Selectable>() != null)
                        continue;

                    var rect = ((RectTransform)child).rect;
                    if (rect.width < canvasRect.width * 0.9f || rect.height < canvasRect.height * 0.9f)
                        continue;

                    graphic.enabled = false;
                    Plugin.Log.LogInfo($"Backdrop: hid menu background '{child.name}'");
                }
            }
        }

        /// <summary>Leave only rendering: no game logic (including FMOD audio emitters), physics, cameras or UI.</summary>
        private static void Neutralize(GameObject root)
        {
            foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null)
                    continue;
                var ns = behaviour.GetType().Namespace ?? "";
                // Keep URP's light/camera data and TextMeshPro text so the scene still looks right.
                if (ns.StartsWith("UnityEngine.Rendering") || ns.StartsWith("TMPro"))
                    continue;
                behaviour.enabled = false;
            }

            foreach (var c in root.GetComponentsInChildren<Collider>(true))
                c.enabled = false;
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true))
                body.isKinematic = true;
            foreach (var cam in root.GetComponentsInChildren<Camera>(true))
                cam.enabled = false;
            foreach (var canvas in root.GetComponentsInChildren<Canvas>(true))
                if (canvas.renderMode != RenderMode.WorldSpace)
                    canvas.enabled = false;
        }

        /// <summary>All root objects in the DontDestroyOnLoad scene.</summary>
        private HashSet<GameObject> PersistentObjects()
        {
            if (persistentProbe == null)
            {
                persistentProbe = new GameObject("GWYFVR DDOL probe");
                DontDestroyOnLoad(persistentProbe);
            }

            return new HashSet<GameObject>(persistentProbe.scene.GetRootGameObjects());
        }
    }
}
