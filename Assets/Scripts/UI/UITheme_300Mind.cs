using TMPro;
using UnityEngine;

namespace FlowBlast.UI
{
    /// <summary>
    /// Theme data for the "300Mind / 2D Game UI Kit" look. Holds every sprite, font, and colour
    /// a controller may want when rebuilding its hierarchy. The asset is created at Editor time
    /// via the menu <c>FlowBlast/UI/Create 300Mind Theme</c> and assigned to
    /// <c>UIBootstrap._theme</c> in the scene.
    ///
    /// Sprites are 9-sliced rounded rectangles so panels and buttons scale to any portrait size
    /// without distortion.
    /// </summary>
    [CreateAssetMenu(menuName = "FlowBlast/UI/300Mind Theme", fileName = "UITheme_300Mind")]
    public class UITheme_300Mind : ScriptableObject
    {
        // ----- Backgrounds -----
        [Header("Backgrounds (9-slice)")]
        public Sprite panelBackground;
        public Sprite panelHeader;
        public Sprite buttonNormal;
        public Sprite buttonPressed;
        public Sprite buttonDisabled;
        public Sprite progressBarBg;
        public Sprite progressBarFill;

        // ----- Icons -----
        [Header("Icons")]
        public Sprite iconCoin;
        public Sprite iconStar;
        public Sprite iconSettings;
        public Sprite iconLevel;
        public Sprite iconPlay;
        public Sprite iconQuit;
        public Sprite iconBack;
        public Sprite iconPause;
        public Sprite iconRestart;
        public Sprite iconMusic;
        public Sprite iconSfx;
        public Sprite iconVibration;

        // ----- Fonts -----
        [Header("Fonts (TMP)")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;
        public TMP_FontAsset buttonFont;

        // ----- Material -----
        [Header("Materials")]
        [Tooltip("Optional UI material applied to every Image/Button built via UIThemeBuilder. " +
                 "Leave null to use Unity's built-in UI/Default material (works for typical use).")]
        public Material defaultUiMaterial;

        // ----- Palette -----
        [Header("Palette")]
        public Color palettePrimary = new Color(1f, 0.55f, 0.15f, 1f); // orange/yellow
        public Color paletteAccent  = new Color(0.18f, 0.62f, 0.55f, 1f); // teal/green
        public Color paletteText    = new Color(0.18f, 0.10f, 0.06f, 1f); // dark brown
        public Color paletteTitle   = Color.white;

        // ----- Decoration -----
        [Header("Decoration")]
        public Sprite backgroundScene;   // full-screen clouds/hills bg
        public Sprite planetDecoration;  // top-left corner ornament

        /// <summary>True if the asset has a font, a panel sprite, a button sprite, and a UI material.</summary>
        public bool IsBaked =>
            titleFont != null && bodyFont != null && buttonFont != null &&
            panelBackground != null && buttonNormal != null &&
            defaultUiMaterial != null;
    }
}