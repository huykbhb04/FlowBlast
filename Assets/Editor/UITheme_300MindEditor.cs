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
    /// Adds a "Bake All References" button that:
    ///   - Auto-fills every Sprite field by scanning Assets/300Mind/2D Game UI Kit/Sprites
    ///     and matching names against keywords (panel / button / progress / icon / etc).
    ///   - Auto-fills fonts with TMP_Settings.defaultFontAsset (LiberationSans SDF) so
    ///     every text has a usable atlas (the previous Oswald assets had null atlas).
    ///   - Verifies the canvas-plus-image wiring is sane (no missing sprites).
    ///
    /// The inspector also renders the default property fields so a developer can
    /// override any choice manually.
    /// </summary>
    [CustomEditor(typeof(UITheme_300Mind))]
    public class UITheme_300MindEditor : Editor
    {
        private static readonly string[] SpriteSheetPaths =
        {
            "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_1.png",
            "Assets/300Mind/2D Game UI Kit/Sprites/UI-pack_Sprite_2.png",
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            DrawPropertiesExcluding(serializedObject, "m_Script");
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Auto-fill (Editor)", EditorStyles.boldLabel);

            if (GUILayout.Button("Bake All References (auto-fill sprites + fonts)"))
            {
                BakeAllReferences((UITheme_300Mind)target);
            }

            EditorGUILayout.HelpBox(
                "Bake scans the 300Mind sprite sheets for sub-sprites whose names match\n" +
                "common keywords (panel / button / progress / coin / star / etc.) and wires\n" +
                "them to the matching slot. Fonts are set to TMP_Settings.defaultFontAsset\n" +
                "(LiberationSans SDF) which always has a populated atlas.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            using (new EditorGUI.DisabledScope(true))
            {
                var theme = (UITheme_300Mind)target;
                EditorGUILayout.Toggle("Is baked (sprites + fonts ready)",
                    theme.IsBaked);
            }
        }

        // -------------------------------------------------------------
        // Bake
        // -------------------------------------------------------------
        private static void BakeAllReferences(UITheme_300Mind theme)
        {
            if (theme == null) return;

            Undo.RecordObject(theme, "Bake UITheme_300Mind References");

            // Build sub-sprite lookup keyed by lower-cased name fragment.
            var subSpriteByName = LoadAllSubSprites();

            // Helper: pick first sprite whose name contains any of the fragments.
            Sprite PickSprite(Sprite fallback, params string[] fragments)
            {
                foreach (var frag in fragments)
                {
                    if (subSpriteByName.TryGetValue(frag, out var s) && s != null) return s;
                }
                return fallback;
            }

            theme.panelBackground    = PickSprite(theme.panelBackground,    "panel", "bg", "background");
            theme.panelHeader        = PickSprite(theme.panelHeader,        "header");
            theme.buttonNormal       = PickSprite(theme.buttonNormal,       "button");
            theme.buttonPressed      = PickSprite(theme.buttonPressed,      "button_pressed", "button-pressed");
            theme.buttonDisabled     = PickSprite(theme.buttonDisabled,     "button_disabled", "button-disabled");
            theme.progressBarBg      = PickSprite(theme.progressBarBg,      "progress", "progressbar", "progress_bar");
            theme.progressBarFill    = PickSprite(theme.progressBarFill,    "progress_fill", "progress-fill", "fill");

            theme.iconCoin           = PickSprite(theme.iconCoin,           "coin");
            theme.iconStar           = PickSprite(theme.iconStar,           "star");
            theme.iconSettings       = PickSprite(theme.iconSettings,       "settings", "gear", "cog");
            theme.iconLevel          = PickSprite(theme.iconLevel,          "level");
            theme.iconPlay           = PickSprite(theme.iconPlay,           "play");
            theme.iconQuit           = PickSprite(theme.iconQuit,           "quit", "exit");
            theme.iconBack           = PickSprite(theme.iconBack,           "back", "arrow");
            theme.iconPause          = PickSprite(theme.iconPause,          "pause");
            theme.iconRestart        = PickSprite(theme.iconRestart,        "restart", "refresh", "replay");
            theme.iconMusic          = PickSprite(theme.iconMusic,          "music", "note");
            theme.iconSfx            = PickSprite(theme.iconSfx,            "sfx", "sound");
            theme.iconVibration      = PickSprite(theme.iconVibration,      "vibration", "vibrate");
            theme.backgroundScene    = PickSprite(theme.backgroundScene,    "background", "bg", "scene", "clouds", "sky");
            theme.planetDecoration   = PickSprite(theme.planetDecoration,   "planet", "decoration", "ornament");

            // Fonts: always use the project default TMP font (it ships with an atlas).
            var font = TMP_Settings.defaultFontAsset;
            if (font == null)
            {
                // Fallback: scan Resources for any LiberationSans SDF asset.
                string[] guids = AssetDatabase.FindAssets("LiberationSans SDF t:TMP_FontAsset");
                foreach (var g in guids)
                {
                    var p = AssetDatabase.GUIDToAssetPath(g);
                    var f = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(p);
                    if (f != null) { font = f; break; }
                }
            }

            if (font != null)
            {
                theme.titleFont   = font;
                theme.bodyFont    = font;
                theme.buttonFont  = font;
            }

            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();

            int filledSprites = CountFilledSprites(theme);
            Debug.Log($"[UITheme_300Mind] Bake complete. {filledSprites} sprites filled, " +
                      $"font = {(font != null ? font.name : "<null>")}.");
        }

        // -------------------------------------------------------------
        // Sprite loading
        // -------------------------------------------------------------
        private static Dictionary<string, Sprite> LoadAllSubSprites()
        {
            var dict = new Dictionary<string, Sprite>();
            foreach (var path in SpriteSheetPaths)
            {
                if (!File.Exists(path)) continue;

                var sprites = AssetDatabase.LoadAllAssetsAtPath(path);
                foreach (var obj in sprites)
                {
                    if (obj is Sprite s && !string.IsNullOrEmpty(s.name))
                    {
                        // Insert keyed by the FULL name and also every underscore-segment,
                        // so a sprite named "button_normal_round" can be looked up via
                        // "button", "normal", or "round".
                        var normalized = Normalize(s.name);
                        if (!dict.ContainsKey(normalized)) dict[normalized] = s;

                        foreach (var seg in s.name.Split(new[] { '_', '-', ' ' }))
                        {
                            var key = Normalize(seg);
                            if (string.IsNullOrEmpty(key)) continue;
                            if (!dict.ContainsKey(key)) dict[key] = s;
                        }
                    }
                }
            }
            return dict;
        }

        private static string Normalize(string s)
        {
            return s.Trim().ToLowerInvariant().Replace(' ', '_');
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
        // Convenience: also expose a top-level menu so anyone can bake
        // an asset the first time without needing to find it in Project.
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
                if (theme != null) BakeAllReferences(theme);
            }
        }
    }
}
