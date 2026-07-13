using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlowBlast.UI;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// Custom inspector for <see cref="UITheme_300Mind"/>.
    ///
    /// Adds a "Bake All References" button that wires every Sprite / Material / Font slot
    /// of the theme to a real asset from the 300Mind kit (or sensible fallback) so the
    /// MainMenu builder doesn't fall back to flat coloured quads.
    ///
    /// Mapping rules (driven by layout, NOT name - all sub-sprites in the kit are named
    /// <c>UI-pack_Sprite_{N}_{X}_{Y}</c> so name-based heuristics cannot tell a coin from
    /// a button):
    ///
    ///  * <c>UI-pack_Sprite_2.png</c> is a 4x4 grid of panel / progress / button families
    ///    (col x row):
    ///        row 0 (y=864) -> panels      (teal / orange / blue / beige)
    ///        row 1 (y=576) -> headered panels
    ///        row 2 (y=288) -> progress bars
    ///        row 3 (y=0)   -> rounded buttons
    ///  * <c>UI-pack_Sprite_1.png</c> is a 9x9 grid of decorations (banners, icon-buttons,
    ///    badges, scroll background, cloud/planet backgrounds).
    /// </summary>
    [CustomEditor(typeof(UITheme_300Mind))]
    public class UITheme_300MindEditor : Editor
    {
        private const string SpriteSheet1 = "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png";
        private const string SpriteSheet2 = "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Auto-fill (Editor)", EditorStyles.boldLabel);

            if (GUILayout.Button("Bake All References (auto-fill sprites + fonts + material)"))
            {
                BakeAllReferences((UITheme_300Mind)target);
            }

            if (GUILayout.Button("Create + Assign UI/Default Material"))
            {
                BakeMaterial((UITheme_300Mind)target);
            }

            EditorGUILayout.HelpBox(
                "Bake maps sprites by their GRID POSITION in the 2 sheets (not by name):\n" +
                "  - Sprite_2 row 0 = panels, row 1 = headered panels, row 2 = progress bars, row 3 = buttons\n" +
                "  - Sprite_1 = decorations, banners, icon-button tiles, clouds background\n" +
                "Icons (coin/star/play/...) the kit doesn't ship are left null so the builder can fall back to TMP text labels.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(true))
            {
                var theme = (UITheme_300Mind)target;
                EditorGUILayout.Toggle("Is baked (sprites + fonts + material ready)",
                    theme.IsBaked);
                EditorGUILayout.IntField("Sprites filled", CountFilledSprites(theme));
            }
        }

        // -------------------------------------------------------------
        // Bake - fills sprites + fonts
        // -------------------------------------------------------------
        private static void BakeAllReferences(UITheme_300Mind theme)
        {
            if (theme == null) return;
            Undo.RecordObject(theme, "Bake UITheme_300Mind References");

            var byPath = BuildSpriteGridIndex();

            // ---- Sprite_1 grid (9x9) is the rich, self-contained kit. 9-col layout:
            //   row 0-1 col 0-1 : large dialog banners / Settings panel
            //   row 2-4         : rounded buttons (Yes / No / OK / Back etc.)
            //   row 5-6         : icon-button variants, planet decoration
            //   row 8 col 0     : clouds background
            //
            // Sprite_2 is the simple progress-bar panels; rows are 4x wide instead of 9.
            //
            // Pick by grid position, NOT name (all sub-sprites share the auto-generated
            // name "UI-pack_Sprite_X_col_row").
            theme.panelBackground   = Pick(byPath, SpriteSheet1, 0, 0); // wide dialog banner
            theme.panelHeader       = Pick(byPath, SpriteSheet1, 2, 1); // panel with header strip
            theme.buttonNormal      = Pick(byPath, SpriteSheet1, 0, 3); // primary button tile
            theme.buttonPressed     = Pick(byPath, SpriteSheet1, 1, 3); // alternate color button
            theme.buttonDisabled    = Pick(byPath, SpriteSheet1, 3, 3); // muted button tile
            theme.progressBarBg     = Pick(byPath, SpriteSheet2, 0, 2); // progress panel (4-col sheet)
            theme.progressBarFill   = Pick(byPath, SpriteSheet2, 1, 2);
            theme.backgroundScene   = Pick(byPath, SpriteSheet1, 0, 8); // clouds bg
            theme.planetDecoration  = Pick(byPath, SpriteSheet1, 6, 5); // top-left ornament

            // Icons: kit only ships icon-button tiles, no standalone coin/star/etc.
            // Leave them null - UIThemeBuilder falls back to TMP text. Users can override
            // any slot manually in the inspector afterwards.
            theme.iconCoin      = null;
            theme.iconStar      = null;
            theme.iconSettings  = null;
            theme.iconLevel     = null;
            theme.iconPlay      = null;
            theme.iconQuit      = null;
            theme.iconBack      = null;
            theme.iconPause     = null;
            theme.iconRestart   = null;
            theme.iconMusic     = null;
            theme.iconSfx       = null;
            theme.iconVibration = null;

            // ---- Fonts ----
            var font = ResolveDefaultFont();
            if (font != null)
            {
                theme.titleFont  = font;
                theme.bodyFont   = font;
                theme.buttonFont = font;
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            int filled = CountFilledSprites(theme);
            Debug.Log($"[UITheme_300Mind] Bake complete. {filled} sprites filled, " +
                      $"font = {(font != null ? font.name : "<null>")}, " +
                      $"material = {(theme.defaultUiMaterial != null ? theme.defaultUiMaterial.name : "<null>")}.");
        }

        // -------------------------------------------------------------
        // Material - create or find UI/Default material
        // -------------------------------------------------------------
        private static void BakeMaterial(UITheme_300Mind theme)
        {
            if (theme == null) return;
            Undo.RecordObject(theme, "Bake UITheme Material");

            const string matPath = "Assets/UI/UITheme_300Mind_Mat.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (existing == null)
            {
                Shader sh = Shader.Find("UI/Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh == null)
                {
                    Debug.LogError("[UITheme_300Mind] Cannot find UI/Default shader.");
                    return;
                }

                Directory.CreateDirectory("Assets/UI");
                var mat = new Material(sh) { name = "UITheme_300Mind_Mat" };
                AssetDatabase.CreateAsset(mat, matPath);
                AssetDatabase.SaveAssets();
                existing = mat;
            }

            theme.defaultUiMaterial = existing;
            EditorUtility.SetDirty(theme);
            Debug.Log($"[UITheme_300Mind] Material assigned: {existing.name}");
        }

        // -------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------
        /// <summary>
        /// Build a lookup: path -> (col,row) -> Sprite.  Sprite_2 is 4 wide, Sprite_1 is 9 wide.
        /// Indexing is col*x + row*width so the first sprite is (col=0,row=0).
        /// </summary>
        private static Dictionary<string, Dictionary<(int col, int row), Sprite>> BuildSpriteGridIndex()
        {
            var dict = new Dictionary<string, Dictionary<(int, int), Sprite>>();
            BuildSheetIndex(SpriteSheet2, 4, dict);
            BuildSheetIndex(SpriteSheet1, 9, dict);
            return dict;
        }

        private static void BuildSheetIndex(string path, int cols,
            Dictionary<string, Dictionary<(int col, int row), Sprite>> dict)
        {
            if (!File.Exists(path)) return;
            var sheet = new Dictionary<(int, int), Sprite>();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var obj in sprites)
            {
                if (!(obj is Sprite s)) continue;
                if (s.name == Path.GetFileNameWithoutExtension(path)) continue; // root sprite

                // Distinguish root sprites (e.g. "UI-pack_Sprite_2_0") from sub-sprites
                // (e.g. "UI-pack_Sprite_2_0_3"). Only sub-sprites have the col_row form.
                var parts = s.name.Split('_');
                if (parts.Length < 4) continue; // root: UI-pack_Sprite_X_N
                // Tail must be exactly two numeric segments.
                if (!int.TryParse(parts[parts.Length - 2], out int col)) continue;
                if (!int.TryParse(parts[parts.Length - 1], out int row)) continue;
                sheet[(col, row)] = s;
            }
            dict[path] = sheet;
        }

        private static Sprite Pick(Dictionary<string, Dictionary<(int col, int row), Sprite>> dict,
            string path, int col, int row)
        {
            if (dict.TryGetValue(path, out var sheet) && sheet.TryGetValue((col, row), out var s))
                return s;
            return null;
        }

        private static TMP_FontAsset ResolveDefaultFont()
        {
            var font = TMP_Settings.defaultFontAsset;
            if (font != null && font.atlasTexture != null) return font;

            string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
            foreach (var g in guids)
            {
                var p = AssetDatabase.GUIDToAssetPath(g);
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
                if (f != null && f.atlasTexture != null) return f;
            }
            return font;
        }

        private static int CountFilledSprites(UITheme_300Mind theme)
        {
            int n = 0;
            foreach (var s in new Sprite[]
                     {
                         theme.panelBackground, theme.panelHeader,
                         theme.buttonNormal, theme.buttonPressed, theme.buttonDisabled,
                         theme.progressBarBg, theme.progressBarFill,
                         theme.iconCoin, theme.iconStar, theme.iconSettings, theme.iconLevel,
                         theme.iconPlay, theme.iconQuit, theme.iconBack, theme.iconPause,
                         theme.iconRestart, theme.iconMusic, theme.iconSfx, theme.iconVibration,
                         theme.backgroundScene, theme.planetDecoration,
                     })
            {
                if (s != null) n++;
            }
            return n;
        }

        // -------------------------------------------------------------
        // Top-level menu
        // -------------------------------------------------------------
        [MenuItem("FlowBlast/UI/Bake 300Mind Theme (auto-fill)")]
        private static void BakeFromMenu()
        {
            string[] guids = AssetDatabase.FindAssets("UITheme_300Mind t:ScriptableObject");
            if (guids.Length == 0)
            {
                Debug.LogWarning("[UITheme_300Mind] No asset named UITheme_300Mind found. " +
                                 "Create one via Assets/Create/FlowBlast/UI/300Mind Theme first.");
                return;
            }

            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                var theme = AssetDatabase.LoadAssetAtPath<UITheme_300Mind>(path);
                if (theme != null)
                {
                    BakeAllReferences(theme);
                    BakeMaterial(theme);
                }
            }
        }
    }
}