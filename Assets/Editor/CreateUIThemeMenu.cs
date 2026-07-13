using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// One-shot Editor menu that bakes the 300Mind theme into Unity assets so the
    /// runtime UI can pick them up.
    ///
    /// Two menu items are exposed under "FlowBlast/UI":
    ///  - <c>Create 300Mind Fonts</c>  : reads the Oswald TTF files in
    ///    Assets/300Mind/.../Fonts and creates three TMP_FontAsset files under
    ///    Assets/UI/Fonts/.
    ///  - <c>Create 300Mind Theme</c>  : creates Assets/UI/UITheme_300Mind.asset,
    ///    wires the three TMP font assets, and slices the two sprite sheets
    ///    (UI-pack_Sprite_1.png, UI-pack_Sprite_2.png) at runtime via <c>Sprite.Create</c>
    ///    so they can be referenced by the theme.
    ///
    /// Both items are idempotent: running them again leaves existing assets intact.
    /// </summary>
    public static class CreateUIThemeMenu
    {
        private const string FontFolder     = "Assets/UI/Fonts";
        private const string ThemeFolder    = "Assets/UI";
        private const string ThemeAssetPath = "Assets/UI/UITheme_300Mind.asset";

        private const string SpriteRoot = "Assets/300Mind/2D Game UI Kit/Sprites";
        private const string FontRoot   = "Assets/300Mind/2D Game UI Kit/Fonts";

        // Sprite sheet layout. Both PNGs in the 300Mind kit are organised on a 4x4 grid
        // (4 columns x 4 rows = 16 cells). Cell (col, row) is the sprite at that position.
        private const int SheetCols = 4;
        private const int SheetRows = 4;

        private struct SliceEntry
        {
            public string field;
            public int sheet;
            public int col;
            public int row;
            public SliceEntry(string f, int s, int c, int r) { field = f; sheet = s; col = c; row = r; }
        }

        // Cell coordinates in each sheet corresponding to named sprites.
        private static readonly SliceEntry[] SliceMap =
        {
            new SliceEntry("panelBackground",  0, 0, 0),
            new SliceEntry("panelHeader",      0, 1, 0),
            new SliceEntry("buttonNormal",     0, 2, 0),
            new SliceEntry("buttonPressed",    0, 3, 0),
            new SliceEntry("buttonDisabled",   0, 0, 1),
            new SliceEntry("progressBarBg",    0, 1, 1),
            new SliceEntry("progressBarFill",  0, 2, 1),
            new SliceEntry("backgroundScene",  0, 3, 1),
            new SliceEntry("planetDecoration", 0, 0, 2),
            new SliceEntry("iconCoin",         1, 0, 0),
            new SliceEntry("iconStar",         1, 1, 0),
            new SliceEntry("iconSettings",     1, 2, 0),
            new SliceEntry("iconLevel",        1, 3, 0),
            new SliceEntry("iconPlay",         1, 0, 1),
            new SliceEntry("iconQuit",         1, 1, 1),
            new SliceEntry("iconBack",         1, 2, 1),
            new SliceEntry("iconPause",        1, 3, 1),
            new SliceEntry("iconRestart",      1, 0, 2),
            new SliceEntry("iconMusic",        1, 1, 2),
            new SliceEntry("iconSfx",          1, 2, 2),
            new SliceEntry("iconVibration",    1, 3, 2),
        };

        // -----------------------------------------------------------------
        // Menu entries
        // -----------------------------------------------------------------

        [MenuItem("FlowBlast/UI/300Mind/Bake All (Fonts + Theme)")]
        public static void CreateFontsMenu()
        {
            CreateFonts();
            CreateTheme();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
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

            CreateOneFont("Oswald-Bold",     FontRoot + "/Oswald-Bold.ttf",     FontFolder + "/Oswald-Bold.asset");
            CreateOneFont("Oswald-Regular",  FontRoot + "/Oswald-Regular.ttf",  FontFolder + "/Oswald-Regular.asset");
            CreateOneFont("Oswald-SemiBold", FontRoot + "/Oswald-SemiBold.ttf", FontFolder + "/Oswald-SemiBold.asset");
        }

        private static void CreateOneFont(string name, string ttfPath, string outPath)
        {
            if (!File.Exists(ttfPath))
            {
                Debug.LogWarning("[CreateUIThemeMenu] TTF not found: " + ttfPath);
                return;
            }

            // Keep existing assets to preserve user tweaks.
            if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outPath) != null)
            {
                Debug.Log("[CreateUIThemeMenu] Font asset already present: " + outPath);
                return;
            }

            Font font = null;
            try
            {
                font = new Font(ttfPath);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CreateUIThemeMenu] Failed to load TTF '" + ttfPath + "': " + e.Message);
                return;
            }

            if (font == null)
            {
                Debug.LogWarning("[CreateUIThemeMenu] Font is null after loading '" + ttfPath + "'.");
                return;
            }

            TMP_FontAsset asset = TMP_FontAsset.CreateFontAsset(font);
            if (asset == null)
            {
                Debug.LogWarning("[CreateUIThemeMenu] TMP_FontAsset.CreateFontAsset returned null for '" + name + "'.");
                return;
            }

            AssetDatabase.CreateAsset(asset, outPath);
            AssetDatabase.ImportAsset(outPath);
            Debug.Log("[CreateUIThemeMenu] Created TMP font: " + outPath);
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
            var sheet0 = LoadSheet(SpriteRoot + "/UI-pack_Sprite_1.png");
            var sheet1 = LoadSheet(SpriteRoot + "/UI-pack_Sprite_2.png");

            for (int i = 0; i < SliceMap.Length; i++)
            {
                var entry = SliceMap[i];
                Texture2D tex = entry.sheet == 0 ? sheet0 : sheet1;
                if (tex == null) continue;

                Sprite sprite = SliceCell(tex, entry.col, entry.row);
                if (sprite == null) continue;

                WriteField(theme, entry.field, sprite);
            }

            EditorUtility.SetDirty(theme);
            Debug.Log("[CreateUIThemeMenu] Theme baked: " + ThemeAssetPath);
        }

        private static Texture2D LoadSheet(string path)
        {
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
            {
                Debug.LogWarning("[CreateUIThemeMenu] Sprite sheet not found: " + path);
                return null;
            }

            // Ensure the texture is readable so we can build sub-sprites from it.
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.SaveAndReimport();
                tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return tex;
        }

        private static Sprite SliceCell(Texture2D tex, int col, int row)
        {
            if (tex == null || tex.width < SheetCols || tex.height < SheetRows) return null;
            int cellW = tex.width / SheetCols;
            int cellH = tex.height / SheetRows;
            int x = col * cellW;
            int y = row * cellH;
            var rect = new Rect(x, y, cellW, cellH);
            var pivot = new Vector2(0.5f, 0.5f);
            var sprite = Sprite.Create(tex, rect, pivot, 100f, 0,
                SpriteMeshType.FullRect, Vector4.zero, false);
            sprite.name = "Slice_" + col + "_" + row;
            return sprite;
        }

        private static void WriteField(UITheme_300Mind theme, string field, Sprite sprite)
        {
            var f = typeof(UITheme_300Mind).GetField(field);
            if (f == null)
            {
                Debug.LogWarning("[CreateUIThemeMenu] No field '" + field + "' on UITheme_300Mind");
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