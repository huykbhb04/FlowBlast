using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlowBlast.UI;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// One-shot Editor helper for the Main Menu scene.
    ///   - Creates Assets/Scenes/MainMenu.unity if missing.
    ///   - Adds Canvas + EventSystem + UIBootstrap.
    ///   - Configures the Canvas for mobile portrait 1080x1920.
    ///   - Sets _stateAtRuntime = MainMenu so the bootstrap opens the menu.
    ///   - Saves the scene.
    ///
    /// Run via: "FlowBlast/UI/Install UI in MainMenu Scene".
    ///
    /// Safe to call multiple times.
    /// </summary>
    public static class InstallInMainMenuScene
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string BootstrapName = "__UIBootstrap";

        [MenuItem("FlowBlast/UI/Install UI in MainMenu Scene")]
        public static void InstallInMainMenuSceneMenu()
        {
            Install(showDialog: true);
        }

        /// <summary>
        /// Programmatic entry point (used by other editor tools / CI).
        /// </summary>
        public static void Install(bool showDialog)
        {
            // 0) Make sure scene folder exists.
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            // 1) Open existing scene, or create a new one if file does not exist.
            UnityEngine.SceneManagement.Scene scene;
            if (File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                if (!scene.IsValid())
                {
                    scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                    EditorSceneManager.SaveScene(scene, ScenePath);
                }
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            // 2) Canvas.
            var canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("Canvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                canvasGo.AddComponent<CanvasScaler>();
                canvasGo.AddComponent<GraphicRaycaster>();
                SceneManager.MoveGameObjectToScene(canvasGo, scene);
            }

            ConfigureCanvasScaler(canvas);

            // 3) EventSystem.
            if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<UnityEngine.EventSystems.EventSystem>();
                es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                SceneManager.MoveGameObjectToScene(es, scene);
            }

            // 4) UIBootstrap.
            var existing = GameObject.Find(BootstrapName);
            if (existing == null) existing = FindGameObjectByName("Bootstrap");

            UIBootstrap bootstrap = null;
            if (existing != null) bootstrap = existing.GetComponent<UIBootstrap>();

            if (bootstrap == null)
            {
                var go = new GameObject(BootstrapName);
                bootstrap = go.AddComponent<UIBootstrap>();
                SceneManager.MoveGameObjectToScene(go, scene);
                Debug.Log("[InstallInMainMenuScene] Added UIBootstrap GameObject to scene.");
            }
            else
            {
                Debug.Log("[InstallInMainMenuScene] UIBootstrap already present, leaving it.");
            }

            // 5) Configure bootstrap for MainMenu mode + mobile portrait.
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            var stateField = typeof(UIBootstrap).GetField("_stateAtRuntime", flags);
            if (stateField != null) stateField.SetValue(bootstrap, UIBootstrap.InitialUIState.MainMenu);

            var refField = typeof(UIBootstrap).GetField("_referenceResolution", flags);
            if (refField != null) refField.SetValue(bootstrap, new Vector2(1080f, 1920f));

            var matchField = typeof(UIBootstrap).GetField("_matchWidthOrHeight", flags);
            if (matchField != null) matchField.SetValue(bootstrap, 0.5f);

            UnityEditor.EditorUtility.SetDirty(bootstrap);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);

            var msg = $"Saved MainMenu scene with UI bootstrap installed: {ScenePath}";
            Debug.Log($"[InstallInMainMenuScene] {msg}");
            if (showDialog) EditorUtility.DisplayDialog("FlowBlast", msg, "OK");
        }

        private static void ConfigureCanvasScaler(Canvas canvas)
        {
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f); // mobile portrait
            scaler.matchWidthOrHeight = 0.5f;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.referencePixelsPerUnit = 100f;
        }

        private static GameObject FindGameObjectByName(string name)
        {
            foreach (var go in Object.FindObjectsOfType<GameObject>())
                if (go.name == name) return go;
            return null;
        }
    }
}
