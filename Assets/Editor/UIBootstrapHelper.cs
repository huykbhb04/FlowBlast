using UnityEditor;
using UnityEngine;
using FlowBlast.UI;

namespace FlowBlast.EditorTools
{
    /// <summary>
    /// Helpers exposed as menu items so the developer can re-wire a UIBootstrap to the
    /// 300Mind theme without touching the inspector (useful when the theme field is null
    /// because the asset reference was lost during a scene merge).
    /// </summary>
    public static class UIBootstrapHelper
    {
        [MenuItem("FlowBlast/UI/Bind 300Mind Theme to UIBootstrap(s)")]
        private static void BindTheme()
        {
            var themeGuids = AssetDatabase.FindAssets("UITheme_300Mind t:ScriptableObject");
            if (themeGuids.Length == 0)
            {
                Debug.LogWarning("[UITheme] No UITheme_300Mind asset found. Create one first via Assets/Create/FlowBlast/UI/300Mind Theme.");
                return;
            }
            var themePath = AssetDatabase.GUIDToAssetPath(themeGuids[0]);
            var theme = AssetDatabase.LoadAssetAtPath<UITheme_300Mind>(themePath);
            if (theme == null)
            {
                Debug.LogWarning($"[UITheme] Could not load theme at {themePath}.");
                return;
            }

            var bootstraps = Object.FindObjectsOfType<UIBootstrap>(includeInactive: true);
            if (bootstraps.Length == 0)
            {
                Debug.LogWarning("[UITheme] No UIBootstrap in the open scene(s).");
                return;
            }

            foreach (var b in bootstraps)
            {
                var so = new SerializedObject(b);
                var prop = so.FindProperty("_theme");
                if (prop == null) continue;
                prop.objectReferenceValue = theme;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(b);
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            Debug.Log($"[UITheme] Bound '{theme.name}' to {bootstraps.Length} UIBootstrap(s).");
        }

        [MenuItem("FlowBlast/UI/Force Rebuild All UI Panels")]
        private static void RebuildAll()
        {
            var bootstraps = Object.FindObjectsOfType<UIBootstrap>(includeInactive: true);
            foreach (var b in bootstraps)
            {
                b.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);
            }
            Debug.Log($"[UITheme] Force-rebuild requested on {bootstraps.Length} bootstrap(s). Switch to Play mode to see results.");
        }
    }
}