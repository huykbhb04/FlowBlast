using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FlowBlast.UI;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// One-shot installer that wipes and rebuilds the MainMenu scene's UI from scratch.
    /// Safe to call multiple times.
    ///
    /// Actions:
    ///   1. Opens Assets/Scenes/MainMenu.unity (creating an empty scene if missing).
    ///   2. Deletes every Canvas, EventSystem, and any GameObject with a UIBootstrap /
    ///      UI controller component already in the scene.
    ///   3. Creates a fresh Canvas (Scale With Screen Size 1080x1920), EventSystem, and
    ///      a __UIBootstrap GameObject with UIBootstrap + UITheme_300Mind assigned.
    ///   4. Saves the scene.
    ///
    /// Run via: "FlowBlast/UI/(Re)Build MainMenu UI".
    /// </summary>
    public static class RebuildMainMenuUI
    {
        private const string SceneFolder = "Assets/Scenes";
        private const string ScenePath = "Assets/Scenes/MainMenu.unity";
        private const string ThemePath = "Assets/UI/UITheme_300Mind.asset";
        private const string BootstrapName = "__UIBootstrap";

        [MenuItem("FlowBlast/UI/(Re)Build MainMenu UI")]
        public static void RebuildMenuUI()
        {
            // 1. Make sure the scene file exists.
            EnsureFolder(SceneFolder);
            if (!File.Exists(ScenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
            }

            // 2. Open it (forces unsaved-scene prompt is bypassed because we just
            //    saved above if we created it).
            var opened = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!opened.IsValid())
            {
                EditorUtility.DisplayDialog("FlowBlast", "Failed to open scene at " + ScenePath, "OK");
                return;
            }

            // 3. Strip every old Canvas / EventSystem / UIBootstrap / *Controller.
            WipeExistingUI();

            // 4. Build fresh.
            BuildFreshUI();

            // 5. Save.
            EditorSceneManager.MarkSceneDirty(opened);
            EditorSceneManager.SaveScene(opened);

            EditorUtility.DisplayDialog("FlowBlast",
                "MainMenu UI rebuilt.\n\nScene: " + ScenePath +
                "\nA Canvas (1080x1920, Scale With Screen Size), an EventSystem, and a __UIBootstrap with the 300Mind theme have been added.\n\nPress Play.",
                "OK");
            Debug.Log("[RebuildMainMenuUI] Done. Scene saved: " + ScenePath);
        }

        private static void EnsureFolder(string assetFolder)
        {
            if (AssetDatabase.IsValidFolder(assetFolder)) return;
            var parent = Path.GetDirectoryName(assetFolder).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            var leaf = Path.GetFileName(assetFolder);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void WipeExistingUI()
        {
            // Delete bootstrap GO first (it carries references to canvas/panels).
            var existingBootstrap = GameObject.Find(BootstrapName);
            if (existingBootstrap != null) Object.DestroyImmediate(existingBootstrap);

            // Delete every Canvas-bearing GameObject.
            var canvases = Object.FindObjectsOfType<Canvas>();
            foreach (var c in canvases)
            {
                if (c != null && c.gameObject != null) Object.DestroyImmediate(c.gameObject);
            }

            // Delete leftover EventSystems.
            var eventSystems = Object.FindObjectsOfType<EventSystem>();
            foreach (var es in eventSystems)
            {
                if (es != null && es.gameObject != null) Object.DestroyImmediate(es.gameObject);
            }

            // Catch any orphan *Controller scripts that may have been left alive.
            var controllers = Object.FindObjectsOfType<MonoBehaviour>();
            foreach (var mb in controllers)
            {
                if (mb == null) continue;
                var t = mb.GetType().FullName ?? string.Empty;
                if (t.StartsWith("FlowBlast.UI.") && t.EndsWith("Controller"))
                {
                    if (mb.gameObject != null) Object.DestroyImmediate(mb.gameObject);
                }
            }

            // Also strip UIBootstrap that might be on a differently named GO.
            var strayBootstraps = Object.FindObjectsOfType<UIBootstrap>();
            foreach (var b in strayBootstraps)
            {
                if (b != null && b.gameObject != null) Object.DestroyImmediate(b.gameObject);
            }
        }

        private static void BuildFreshUI()
        {
            // Canvas
            var canvasGo = new GameObject("Canvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // EventSystem
            var esGo = new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));

            // Bootstrap
            var bootGo = new GameObject(BootstrapName);
            var bootstrap = bootGo.AddComponent<UIBootstrap>();
            bootstrap.enabled = true;
            // Make sure runtime auto-build is also enabled so the runtime panels show up.
            var so = new SerializedObject(bootstrap);
            var autoBuild = so.FindProperty("_autoBuildIfMissing");
            if (autoBuild != null) autoBuild.boolValue = true;
            var stateProp = so.FindProperty("_stateAtRuntime");
            if (stateProp != null)
            {
                // Set by index: 0 = MainMenu, 1 = Playing (matches InitialUIState declaration).
                stateProp.enumValueIndex = 0;
            }
            var themeProp = so.FindProperty("_theme");
            if (themeProp != null)
            {
                var theme = AssetDatabase.LoadAssetAtPath<UITheme_300Mind>(ThemePath);
                if (theme != null)
                {
                    themeProp.objectReferenceValue = theme;
                }
                else
                {
                    Debug.LogWarning("[RebuildMainMenuUI] Could not load theme at " + ThemePath +
                        " - UIBootstrap._theme will be null.");
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();

            // Position bootstrap so it's easy to find (header of hierarchy).
            bootGo.transform.SetAsFirstSibling();
        }
    }
}
