using System.IO;
using TMPro;
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

        // ============================================================================
        // 300Mind theme baker
        // ============================================================================
        // One-shot menu that bakes the 300Mind theme asset and TMP_FontAsset files.
        // Lives in this file (instead of its own script) to guarantee the menu shows
        // up - this file is known to compile.

        private const string M_FontFolder     = "Assets/UI/Fonts";
        private const string M_ThemeFolder    = "Assets/UI";
        private const string M_ThemeAssetPath = "Assets/UI/UITheme_300Mind.asset";
        private const string M_SpriteRoot     = "Assets/300Mind/2D Game UI Kit/Sprites";
        private const string M_FontRoot       = "Assets/300Mind/2D Game UI Kit/Fonts";

        private struct M_SliceEntry
        {
            public string field; public int sheet; public int col; public int row;
            public M_SliceEntry(string f, int s, int c, int r) { field = f; sheet = s; col = c; row = r; }
        }

        private static readonly M_SliceEntry[] M_SliceMap = new M_SliceEntry[]
        {
            new M_SliceEntry("panelBackground",  0, 0, 0),
            new M_SliceEntry("panelHeader",      0, 1, 0),
            new M_SliceEntry("buttonNormal",     0, 2, 0),
            new M_SliceEntry("buttonPressed",    0, 3, 0),
            new M_SliceEntry("buttonDisabled",   0, 0, 1),
            new M_SliceEntry("progressBarBg",    0, 1, 1),
            new M_SliceEntry("progressBarFill",  0, 2, 1),
            new M_SliceEntry("backgroundScene",  0, 3, 1),
            new M_SliceEntry("planetDecoration", 0, 0, 2),
            new M_SliceEntry("iconCoin",         1, 0, 0),
            new M_SliceEntry("iconStar",         1, 1, 0),
            new M_SliceEntry("iconSettings",     1, 2, 0),
            new M_SliceEntry("iconLevel",        1, 3, 0),
            new M_SliceEntry("iconPlay",         1, 0, 1),
            new M_SliceEntry("iconQuit",         1, 1, 1),
            new M_SliceEntry("iconBack",         1, 2, 1),
            new M_SliceEntry("iconPause",        1, 3, 1),
            new M_SliceEntry("iconRestart",      1, 0, 2),
            new M_SliceEntry("iconMusic",        1, 1, 2),
            new M_SliceEntry("iconSfx",          1, 2, 2),
            new M_SliceEntry("iconVibration",    1, 3, 2),
        };

        [MenuItem("FlowBlast/UI/Create 300Mind Theme")]
        public static void Create300MindThemeMenu()
        {
            Debug.Log("[Create300MindTheme] Menu invoked.");

            EnsureFolder(M_FontFolder);

            // No-op: we don't create Oswald TMP assets here. TMP's CreateFontAsset(Font)
            // at runtime produces an asset without an atlas texture, which then warns
            // "Font Atlas Texture missing" when assigned to a text component.
            // We instead reuse LiberationSans SDF (bundled with TMP Essentials) below.

            // Bake theme asset.
            EnsureFolder(M_ThemeFolder);

            var theme = AssetDatabase.LoadAssetAtPath<UITheme_300Mind>(M_ThemeAssetPath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme_300Mind>();
                AssetDatabase.CreateAsset(theme, M_ThemeAssetPath);
            }

            // Use TMP's built-in LiberationSans SDF as a safe fallback. It already has
            // a populated atlas texture + material + shader, so it renders immediately.
            const string TMP_DEFAULT = "Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset";
            TMP_FontAsset defaultFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TMP_DEFAULT);
            if (defaultFont == null)
            {
                // Last-ditch: grab whatever TMP says is the default.
                defaultFont = TMP_Settings.defaultFontAsset;
            }
            theme.titleFont  = defaultFont;
            theme.bodyFont   = defaultFont;
            theme.buttonFont = defaultFont;

            SliceSheetIntoAssets(M_SpriteRoot + "/UI-pack_Sprite_1.png", out var sheet0);
            SliceSheetIntoAssets(M_SpriteRoot + "/UI-pack_Sprite_2.png", out var sheet1);

            int sliced = 0;
            int missing = 0;
            for (int i = 0; i < M_SliceMap.Length; i++)
            {
                var entry = M_SliceMap[i];
                Sprite sprite = entry.sheet == 0 ? sheet0[entry.row, entry.col] : sheet1[entry.row, entry.col];
                if (sprite == null)
                {
                    missing++;
                    Debug.LogWarning("[Create300MindTheme] No sprite for field '" + entry.field +
                        "' at sheet " + entry.sheet + " row " + entry.row + " col " + entry.col);
                    continue;
                }
                WriteField(theme, entry.field, sprite);
                sliced++;
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string msg = "300Mind theme baked.\nSliced sprites: " + sliced +
                         "\nAsset: " + M_ThemeAssetPath +
                         "\n\nAssign into UIBootstrap on each scene.";
            Debug.Log("[Create300MindTheme] " + msg);
            EditorUtility.DisplayDialog("FlowBlast", msg, "OK");
        }

        private static void SliceSheetIntoAssets(string path, out Sprite[,] grid)
        {
            grid = new Sprite[4, 4];
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning("[Create300MindTheme] No TextureImporter at " + path);
                return;
            }

            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning("[Create300MindTheme] LoadAssetAtPath<Texture2D> returned null for " + path);
                return;
            }

            int cellW = tex.width  / 4;
            int cellH = tex.height / 4;
            if (cellW < 1 || cellH < 1)
            {
                Debug.LogWarning("[Create300MindTheme] Texture too small: " + tex.width + "x" + tex.height);
                return;
            }

            importer.textureType    = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.isReadable       = true;
            importer.mipmapEnabled    = false;
            importer.filterMode       = FilterMode.Bilinear;

            // IMPORTANT: clear existing sprites first so old single sprite doesn't linger.
            importer.spritesheet = new SpriteMetaData[0];

            var meta = new SpriteMetaData[16];
            for (int row = 0; row < 4; row++)
            {
                for (int col = 0; col < 4; col++)
                {
                    int idx = row * 4 + col;
                    string spriteName = Path.GetFileNameWithoutExtension(path) + "_" + col + "_" + row;
                    meta[idx] = new SpriteMetaData
                    {
                        name      = spriteName,
                        rect      = new Rect(col * cellW, (3 - row) * cellH, cellW, cellH), // Unity flips Y
                        pivot     = new Vector2(0.5f, 0.5f),
                        border    = new Vector4(cellW * 0.2f, cellH * 0.2f, cellW * 0.2f, cellH * 0.2f), // 9-slice border
                        alignment = (int)SpriteAlignment.Center,
                    };
                }
            }
            importer.spritesheet = meta;
            importer.SaveAndReimport();

            // Now load the actual Sprite assets by name.
            var allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in allAssets)
            {
                var sp = obj as Sprite;
                if (sp == null) continue;
                // name like "UI-pack_Sprite_1_2_1" -> col=2 row=1
                var parts = sp.name.Split('_');
                if (parts.Length < 2) continue;
                int last  = parts.Length - 1;
                int prev  = parts.Length - 2;
                int r, c;
                if (!int.TryParse(parts[last], out r)) continue;
                if (!int.TryParse(parts[prev], out c)) continue;
                if (r < 0 || r > 3 || c < 0 || c > 3) continue;
                grid[r, c] = sp;
            }
        }

        private static void WriteField(UITheme_300Mind theme, string field, Sprite sprite)
        {
            var f = typeof(UITheme_300Mind).GetField(field);
            if (f == null)
            {
                Debug.LogWarning("[Create300MindTheme] No field '" + field + "' on UITheme_300Mind");
                return;
            }
            f.SetValue(theme, sprite);
        }

        private static void EnsureFolder(string assetsRelative)
        {
            if (AssetDatabase.IsValidFolder(assetsRelative)) return;
            string parent = Path.GetDirectoryName(assetsRelative).Replace("\\", "/");
            string leaf   = Path.GetFileName(assetsRelative);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
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
