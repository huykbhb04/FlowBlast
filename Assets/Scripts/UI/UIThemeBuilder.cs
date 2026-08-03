using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI
{
    /// <summary>
    /// Static helper methods that build 300Mind-styled UI elements (panel, button, text,
    /// icon, progress bar, toggle, backdrop) under a given parent <c>RectTransform</c>.
    ///
    /// Anchors are in normalized canvas coordinates (0..1) where (0.5, 0.5) is the centre.
    /// Sizes are passed in pixels at the theme's reference resolution (1080x1920 portrait);
    /// the parent canvas's <c>CanvasScaler</c> scales the result down for smaller screens.
    /// </summary>
    public static class UIThemeBuilder
    {
        // Reference canvas size. Callers pass anchors normalized to this size.
        public const float RefW = 1080f;
        public const float RefH = 1920f;

        // ----------------------------------------------------------------------
        // Public API
        // ----------------------------------------------------------------------

        /// <summary>
        /// Apply the theme's <c>defaultUiMaterial</c> to a <see cref="Graphic"/> if one
        /// is provided. When the theme has no material, the Graphic keeps whatever
        /// Unity assigned at construction (the built-in UI/Default material).
        /// </summary>
        public static void ApplyThemeMaterial(Graphic graphic, UITheme_300Mind theme)
        {
            if (graphic == null) return;
            if (theme != null && theme.defaultUiMaterial != null)
            {
                graphic.material = theme.defaultUiMaterial;
            }
        }

        public static GameObject BuildPanel(Transform parent, string name, UITheme_300Mind theme,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 size, bool useHeader, string headerText)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = theme != null ? theme.panelBackground : null;
            img.type = theme != null && theme.panelBackground != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = theme != null ? Color.white : new Color(0.08f, 0.12f, 0.2f, 1f);
            img.raycastTarget = true;
            ApplyThemeMaterial(img, theme);

            if (useHeader && headerText != null)
            {
                var header = BuildText(go.transform, theme != null ? theme.titleFont : null,
                    64f, theme != null ? theme.paletteTitle : Color.white,
                    TextAlignmentOptions.Center, headerText);
                var hrt = header.rectTransform;
                hrt.anchorMin = new Vector2(0f, 1f);
                hrt.anchorMax = new Vector2(1f, 1f);
                hrt.pivot = new Vector2(0.5f, 1f);
                hrt.anchoredPosition = new Vector2(0f, -40f);
                hrt.sizeDelta = new Vector2(0f, 110f);
            }
            return go;
        }

        public static Button BuildButton(Transform parent, string label, UITheme_300Mind theme,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size,
            System.Action onClick, string fontRole = "button")
        {
            var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = theme != null ? theme.buttonNormal : null;
            img.type = theme != null && theme.buttonNormal != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = theme != null ? theme.palettePrimary : new Color(0.2f, 0.4f, 0.8f, 1f);
            ApplyThemeMaterial(img, theme);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 1f, 1f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var font = PickFont(theme, fontRole);
            if (!string.IsNullOrEmpty(label) && font != null)
            {
                var text = BuildText(go.transform, font, 36f,
                    theme != null ? theme.paletteText : Color.white,
                    TextAlignmentOptions.Center, label);
                var trt = text.rectTransform;
                trt.anchorMin = Vector2.zero;
                trt.anchorMax = Vector2.one;
                trt.offsetMin = new Vector2(20f, 8f);
                trt.offsetMax = new Vector2(-20f, -8f);
            }

            return btn;
        }

        public static Button BuildIconButton(Transform parent, Sprite icon, UITheme_300Mind theme,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, System.Action onClick)
        {
            var go = new GameObject("IconBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = theme != null ? theme.buttonNormal : null;
            img.type = theme != null && theme.buttonNormal != null ? Image.Type.Sliced : Image.Type.Simple;
            img.color = new Color(1f, 1f, 1f, 0.9f);
            ApplyThemeMaterial(img, theme);
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            if (icon != null)
            {
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(go.transform, false);
                var irt = (RectTransform)iconGo.transform;
                irt.anchorMin = new Vector2(0.5f, 0.5f);
                irt.anchorMax = new Vector2(0.5f, 0.5f);
                irt.pivot = new Vector2(0.5f, 0.5f);
                irt.sizeDelta = new Vector2(size.x * 0.55f, size.y * 0.55f);
                var iimg = iconGo.GetComponent<Image>();
                iimg.sprite = icon;
                iimg.preserveAspect = true;
                iimg.raycastTarget = false;
                ApplyThemeMaterial(iimg, theme);
            }
            return btn;
        }

        public static TextMeshProUGUI BuildText(Transform parent, TMP_FontAsset font,
            float size, Color color, TextAlignmentOptions align, string content)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = content ?? string.Empty;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            if (font != null && font.atlasTexture != null)
            {
                tmp.font = font;
            }
            else
            {
                // Fallback to TMP's built-in font when the supplied asset has no atlas
                // (e.g. CreateFontAsset at runtime never populated glyphs).
                var fallback = TMP_Settings.defaultFontAsset;
                if (fallback != null && fallback.atlasTexture != null)
                {
                    tmp.font = fallback;
                }
            }
            return tmp;
        }

        public static Image BuildIcon(Transform parent, Sprite icon, Vector2 size, UITheme_300Mind theme)
        {
            var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            img.sprite = icon;
            img.preserveAspect = true;
            img.raycastTarget = false;
            img.color = theme != null ? theme.paletteAccent : Color.white;
            ApplyThemeMaterial(img, theme);
            return img;
        }

        public static (Image background, Image fill) BuildProgressBar(Transform parent, UITheme_300Mind theme,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size)
        {
            var bgGo = new GameObject("ProgressBg", typeof(RectTransform), typeof(Image));
            bgGo.transform.SetParent(parent, false);
            var bgRt = (RectTransform)bgGo.transform;
            bgRt.anchorMin = anchorMin;
            bgRt.anchorMax = anchorMax;
            bgRt.pivot = pivot;
            bgRt.sizeDelta = size;
            var bgImg = bgGo.GetComponent<Image>();
            bgImg.sprite = theme != null ? theme.progressBarBg : null;
            bgImg.type = theme != null && theme.progressBarBg != null ? Image.Type.Sliced : Image.Type.Simple;
            bgImg.color = new Color(0f, 0f, 0f, 0.5f);
            bgImg.raycastTarget = false;

            var fillGo = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(bgGo.transform, false);
            var fillRt = (RectTransform)fillGo.transform;
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(1f, 1f);
            fillRt.pivot = new Vector2(0.5f, 0.5f);
            fillRt.offsetMin = new Vector2(4f, 4f);
            fillRt.offsetMax = new Vector2(-4f, -4f);
            var fillImg = fillGo.GetComponent<Image>();
            fillImg.sprite = theme != null ? theme.progressBarFill : null;
            fillImg.type = theme != null && theme.progressBarFill != null ? Image.Type.Sliced : Image.Type.Simple;
            fillImg.color = theme != null ? theme.paletteAccent : new Color(0.18f, 0.62f, 0.55f, 1f);
            fillImg.raycastTarget = false;
            return (bgImg, fillImg);
        }

        public static Toggle BuildToggle(Transform parent, string label, UITheme_300Mind theme,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size, bool initial)
        {
            var go = new GameObject(label + "_Toggle", typeof(RectTransform), typeof(Image), typeof(Toggle));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;

            var bgImg = go.GetComponent<Image>();
            bgImg.sprite = theme != null ? theme.panelBackground : null;
            bgImg.type = theme != null && theme.panelBackground != null ? Image.Type.Sliced : Image.Type.Simple;
            bgImg.color = new Color(1f, 1f, 1f, 0.95f);

            var checkmark = new GameObject("Checkmark", typeof(RectTransform), typeof(Image));
            checkmark.transform.SetParent(go.transform, false);
            var crt = (RectTransform)checkmark.transform;
            crt.anchorMin = new Vector2(0.1f, 0.15f);
            crt.anchorMax = new Vector2(0.45f, 0.85f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            var cimg = checkmark.GetComponent<Image>();
            cimg.sprite = theme != null ? theme.iconPlay : null;
            cimg.color = theme != null ? theme.palettePrimary : new Color(1f, 0.7f, 0.2f, 1f);
            cimg.raycastTarget = false;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = bgImg;
            toggle.graphic = cimg;
            toggle.isOn = initial;

            var font = PickFont(theme, "body");
            if (font != null)
            {
                var text = BuildText(go.transform, font, 36f,
                    theme != null ? theme.paletteText : Color.black,
                    TextAlignmentOptions.MidlineLeft, label);
                var trt = text.rectTransform;
                trt.anchorMin = new Vector2(0.5f, 0f);
                trt.anchorMax = new Vector2(1f, 1f);
                trt.offsetMin = new Vector2(20f, 0f);
                trt.offsetMax = new Vector2(-10f, 0f);
            }

            return toggle;
        }

        public static Image BuildBackdrop(Transform parent, Color color)
        {
            var go = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            img.raycastTarget = true;
            return img;
        }

        // ----------------------------------------------------------------------
        // Internal helpers
        // ----------------------------------------------------------------------

        private static TMP_FontAsset PickFont(UITheme_300Mind theme, string role)
        {
            if (theme == null) return null;
            switch (role)
            {
                case "title":  return theme.titleFont;
                case "body":   return theme.bodyFont;
                case "button": return theme.buttonFont != null ? theme.buttonFont : theme.bodyFont;
                default:       return theme.bodyFont;
            }
        }

        /// <summary>Destroy every child of <paramref name="parent"/> so a theme can rebuild from scratch.</summary>
        public static void ClearChildren(Transform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (Application.isPlaying) Object.Destroy(child);
                else Object.DestroyImmediate(child);
            }
        }
    }
}