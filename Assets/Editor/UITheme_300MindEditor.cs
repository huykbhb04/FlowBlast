using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlowBlast.UI;

namespace FlowBlast.EditorTools
{
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
            if (GUILayout.Button("Bake All References")) BakeAllReferences((UITheme_300Mind)target);
            if (GUILayout.Button("Create + Assign UI/Default Material")) BakeMaterial((UITheme_300Mind)target);
            EditorGUILayout.HelpBox("Sprite_2 PRIMARY: row3=buttons, row2=progress, row1=headered, row0=plain panels. Sprite_1 icons only.", MessageType.Info);
            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(true))
            {
                var theme = (UITheme_300Mind)target;
                EditorGUILayout.Toggle("Is baked", theme.IsBaked);
                EditorGUILayout.IntField("Sprites filled", CountFilledSprites(theme));
            }
        }

        private static void BakeAllReferences(UITheme_300Mind theme)
        {
            if (theme == null) return;
            Undo.RecordObject(theme, "Bake UITheme_300Mind References");
            var byPath = BuildSpriteGridIndex();
            theme.panelBackground   = Pick(byPath, SpriteSheet2, 0, 0);
            theme.panelHeader       = Pick(byPath, SpriteSheet2, 0, 1);
            theme.buttonNormal      = Pick(byPath, SpriteSheet2, 0, 3);
            theme.buttonPressed     = Pick(byPath, SpriteSheet2, 1, 3);
            theme.buttonDisabled    = Pick(byPath, SpriteSheet2, 3, 3);
            theme.progressBarBg     = Pick(byPath, SpriteSheet2, 0, 2);
            theme.progressBarFill   = Pick(byPath, SpriteSheet2, 2, 2);
            theme.iconCoin          = Pick(byPath, SpriteSheet1, 0, 1);
            theme.iconStar          = Pick(byPath, SpriteSheet1, 1, 1);
            theme.iconSettings      = Pick(byPath, SpriteSheet1, 2, 1);
            theme.iconLevel         = Pick(byPath, SpriteSheet1, 3, 1);
            theme.iconPlay          = Pick(byPath, SpriteSheet1, 0, 2);
            theme.iconQuit          = Pick(byPath, SpriteSheet1, 1, 2);
            theme.iconBack          = Pick(byPath, SpriteSheet1, 2, 2);
            theme.iconPause         = Pick(byPath, SpriteSheet1, 3, 2);
            theme.iconRestart       = Pick(byPath, SpriteSheet1, 0, 3);
            theme.iconMusic         = Pick(byPath, SpriteSheet1, 1, 3);
            theme.iconSfx           = Pick(byPath, SpriteSheet1, 2, 3);
            theme.iconVibration     = Pick(byPath, SpriteSheet1, 3, 3);
            theme.backgroundScene   = Pick(byPath, SpriteSheet1, 0, 0);
            theme.planetDecoration  = Pick(byPath, SpriteSheet1, 3, 0);

            var font = ResolveDefaultFont();
            if (font != null)
            {
                theme.titleFont = font;
                theme.bodyFont = font;
                theme.buttonFont = font;
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            int filled = CountFilledSprites(theme);
            Debug.Log($"[UITheme_300Mind] Bake complete. {filled} sprites filled, font={(font!=null?font.name:"<null>")}.");
        }

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
                if (sh == null) { Debug.LogError("[UITheme] No UI shader found."); return; }
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

        private static Dictionary<string, Dictionary<(int col, int row), Sprite>> BuildSpriteGridIndex()
        {
            var dict = new Dictionary<string, Dictionary<(int, int), Sprite>>();
            BuildSheetIndex(SpriteSheet2, 4, dict);
            BuildSheetIndex(SpriteSheet1, 4, dict);
            return dict;
        }

        private static void BuildSheetIndex(string path, int cols,
            Dictionary<string, Dictionary<(int col, int row), Sprite>> dict)
        {
            if (!File.Exists(path)) return;
            var sheet = new Dictionary<(int, int), Sprite>();
            foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (!(obj is Sprite s)) continue;
                if (s.name == Path.GetFileNameWithoutExtension(path)) continue;
                var parts = s.name.Split('_');
                if (parts.Length < 4) continue;
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
            foreach (var g in AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset"))
            {
                var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(g));
                if (f != null && f.atlasTexture != null) return f;
            }
            return font;
        }

        private static int CountFilledSprites(UITheme_300Mind theme)
        {
            int n = 0;
            foreach (var s in new[] { theme.panelBackground, theme.panelHeader, theme.buttonNormal, theme.buttonPressed, theme.buttonDisabled, theme.progressBarBg, theme.progressBarFill, theme.iconCoin, theme.iconStar, theme.iconSettings, theme.iconLevel, theme.iconPlay, theme.iconQuit, theme.iconBack, theme.iconPause, theme.iconRestart, theme.iconMusic, theme.iconSfx, theme.iconVibration, theme.backgroundScene, theme.planetDecoration })
                if (s != null) n++;
            return n;
        }

        [MenuItem("FlowBlast/UI/Bake 300Mind Theme (auto-fill)")]
        private static void BakeFromMenu()
        {
            var guids = AssetDatabase.FindAssets("UITheme_300Mind t:ScriptableObject");
            if (guids.Length == 0) { Debug.LogWarning("[UITheme] No UITheme_300Mind asset found."); return; }
            foreach (var g in guids)
            {
                var t = AssetDatabase.LoadAssetAtPath<UITheme_300Mind>(AssetDatabase.GUIDToAssetPath(g));
                if (t != null) { BakeAllReferences(t); BakeMaterial(t); }
            }
        }
    }
}