using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// One-shot Editor menu that bakes the 300Mind theme into Unity assets so the
    /// runtime UI can pick them up. Two steps:
    ///
    /// 1. <c>FlowBlast/UI/Create 300Mind Fonts</c> - reads the Oswald TTF files in
    ///    Assets/300Mind/.../Fonts and creates three TMP_FontAsset files under
    ///    Assets/UI/Fonts/ (Bold / Regular / SemiBold).
    ///
    /// 2. <c>FlowBlast/UI/Create 300Mind Theme</c> - creates Assets/UI/UITheme_300Mind.asset,
    ///    wires the three TMP font assets and slices the two sprite sheets
    ///    (UI-pack_Sprite_1.png, UI-pack_Sprite_2.png) at runtime via <c>Sprite.Create</c>
    ///    so they can be referenced by the theme.
    ///
    /// Both steps are idempotent: running them again just overwrites the existing assets.
    /// </summary>
    public static class CreateUIThemeMenu
    {
        private const string FontFolder = "Assets/UI/Fonts";
        private const string ThemeFolder = "Assets/UI";
        private const string ThemeAssetPath = "Assets/UI/UITheme_300Mind.asset";

        private const string SpriteRoot = "Assets/300Mind/2D Game UI Kit/Sprites";
        private const string FontRoot = "Assets/300Mind/2D Game UI Kit/Fonts";

        // Sprite sheet layout. Both PNGs are organised on a 4x4 grid in the 300Mind kit
        // (4 columns, 4 rows = 16 cells). Cell (col, row) is the sprite at that position.
        private const int SheetCols = 4;
        private const int SheetRows = 4;

        // Cell coordinates in each sheet (col, row, 0-indexed) corresponding to named sprites.
        // These defaults cover the most common kit layout. If a cell is empty the slice is
        // skipped and that field stays null on the theme asset (controller falls back to color).
        private static readonly (string field, int sheet, int col, int row)[] SliceMap =
        {
            ("panelBackground", 0, 0, 0),
            ("panelHeader",     0, 1, 0),
            ("buttonNormal",    0, 2, 0),
            ("buttonPressed",   0, 3, 0),
            ("buttonDisabled",  0, 0, 1),
            ("progressBarBg",   0, 1, 1),
            ("progressBarFill", 0, 2, 1),
            ("backgroundScene", 0, 3, 1),
            ("planetDecoration",0, 0, 2),
            ("iconCoin",        1, 0, 0),
            ("iconStar",        1, 1, 0),
            ("iconSettings",    1, 2, 0),
            ("iconLevel",       1, 3, 0),
            ("iconPlay",        1, 0, 1),
            ("iconQuit",        1, 1, 1),
            ("iconBack",        1, 2, 1),
            ("iconPause",       1, 3, 1),
            ("iconRestart",     1, 0, 2),
            ("iconMusic",       1, 1, 2),
            ("iconSfx",         1, 2, 2),
            ("iconVibration",   1, 3, 2),
        };

        // -----------------------------------------------------------------
        // Menu entries
        // -----------------------------------------------------------------

        [MenuItem("FlowBlast/UI/Create 300Mind Fonts")]
        public static void CreateFontsMenu()
        {
            CreateFonts();
        }

        [MenuItem("FlowBlast/UI/Create 300Mind Theme")]
        public static void CreateThemeMenu()
        {
            CreateFonts();
            CreateTheme();
            EditorUtility.DisplayDialog("FlowBlast",
                "300Mind theme baked.\n\nAssign the asset:\n  " + ThemeAssetPath +
                "\n\ninto the UIBootstrap component on each scene.", "OK");
        }

        // -----------------------------------------------------------------
        // Step 1: bake fonts
        // -----------------------------------------------------------------

        private static void CreateFonts()
        {
            EnsureFolder(FontFolder);

            CreateFont("Oswald-Bold",     FontRoot + "/Oswald-Bold.ttf",     FontFolder + "/Oswald-Bold.asset");
            CreateFont("Oswald-Regular",  FontRoot + "/Oswald-Regular.ttf",  FontFolder + "/Oswald-Regular.asset");
            CreateFont("Oswald-SemiBold", FontRoot + "/Oswald-SemiBold.ttf", FontFolder + "/Oswald-SemiBold.asset");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void CreateFont(string name, string ttfPath, string outPath)
        {
            if (!File.Exists(ttfPath))
            {
                Debug.LogWarning($"[CreateUIThemeMenu] TTF not found: {ttfPath}");
                return;
            }

            // If an asset already exists, keep the existing one so the user keeps any tweaks.
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath);
            if (existing != null)
            {
                Debug.Log($"[CreateUIThemeMenu] Font asset already present: {outPath}");
                return;
            }

            var ttfData = File.ReadAllBytes(ttfPath);
            var faceInfo = new FaceInfo
            {
                familyName = "Oswald",
                styleName = name.Contains("Bold") ? (name.Contains("Semi") ? "SemiBold" : "Bold") : "Regular",
                pointSize = 90,
                scale = 1f,
                lineHeight = 90,
                ascent = 75,
                descent = -20,
                underlineOffset = -2f,
                underlineThickness = 1f,
                strikethroughOffset = 12f,
                strikethroughThickness = 2f,
            };

            var asset = TMP_FontAsset.CreateFontAsset(fontSource: null,
                faceInfo: faceInfo,
                samplingPointSize: 90,
                atlasPadding: 9,
                atlasWidth: 1024,
                atlasHeight: 1024,
                atlasPopulationMode: AtlasPopulationMode.Dynamic,
                enableMultiAtlasSupport: true);

            // Wire the actual font file from disk.
            var font = new Font(ttfPath);
            var newAsset = TMP_FontAsset.CreateFontAsset(font);
            AssetDatabase.CreateAsset(newAsset, outPath);
            AssetDatabase.ImportAsset(outPath);
            Debug.Log($"[CreateUIThemeMenu] Created TMP font: {outPath}");
            // Suppress unused variable warning for asset above.
            _ = asset;
        }

        // -----------------------------------------------------------------
        // Step 2: bake theme asset
        // -----------------------------------------------------------------

        private static void CreateTheme()
        {
            EnsureFolder(ThemeFolder);

            var theme = AssetDatabase.LoadAssetAtPath<UITheme_300Mind>(ThemeAssetPath);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<UITheme_300Mind>();
                AssetDatabase.CreateAsset(theme, ThemeAssetPath);
            }

            // Wire fonts.
            theme.titleFont  = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + "/Oswald-Bold.asset");
            theme.bodyFont   = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + "/Oswald-Regular.asset");
            theme.buttonFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontFolder + "/Oswald-SemiBold.asset");

            // Wire sprites by slicing the PNGs into a grid.
            var sheet0 = LoadSheetPixels(SpriteRoot + "/UI-pack_Sprite_1.png");
            var sheet1 = LoadSheetPixels(SpriteRoot + "/UI-pack_Sprite_2.png");

            foreach (var entry in SliceMap)
            {
                var sheet = entry.sheet == 0 ? sheet0 : sheet1;
                if (sheet.tex == null) continue;
                var sprite = SliceCell(sheet, entry.col, entry.row);
                if (sprite == null) continue;
                WriteField(theme, entry.field, sprite);
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[CreateUIThemeMenu] Theme baked: {ThemeAssetPath}");
        }

        private struct SheetPixels
        {
            public Texture2D tex;
            public int width;
            public int height;
        }

        private static SheetPixels LoadSheetPixels(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning($"[CreateUIThemeMenu] Sprite sheet not found: {path}");
                return new SheetPixels();
            }
            // Ensure non-readable textures are readable for sprite slicing.
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return new SheetPixels { tex = tex, width = tex.width, height = tex.height };
        }

        private static Sprite SliceCell(SheetPixels sheet, int col, int row)
        {
            if (sheet.tex == null || sheet.width == 0 || sheet.height == 0) return null;
            int cellW = sheet.width / SheetCols;
            int cellH = sheet.height / SheetRows;
            int x = col * cellW;
            int y = row * cellH;
            var rect = new Rect(x, y, cellW, cellH);
            var pivot = new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(sheet.tex, rect, pivot, 100f, 0,
                SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = $"Slice_{col}_{row}";
            return sprite;
        }

        private static void WriteField(UITheme_300Mind theme, string field, Sprite sprite)
        {
            var f = typeof(UITheme_300Mind).GetField(field);
            if (f == null)
            {
                Debug.LogWarning($"[CreateUIThemeMenu] No field '{field}' on UITheme_300Mind");
                return;
            }
            f.SetValue(theme, sprite);
        }

        private static void EnsureFolder(string assetsRelative)
        {
            if (AssetDatabase.IsValidFolder(assetsRelative)) return;
            var parent = Path.GetDirectoryName(assetsRelative).Replace("\\", "/");
            var leaf   = Path.GetFileName(assetsRelative);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}