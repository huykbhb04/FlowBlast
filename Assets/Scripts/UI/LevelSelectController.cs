using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.UI
{
    /// <summary>
    /// Level-select panel. Shown only in <see cref="UIState.LevelSelect"/>.
    /// Renders a scrollable list of level buttons (one per unlocked level, plus a few
    /// locked placeholders). When a 300Mind theme is injected, the placeholder visuals
    /// are replaced with a back button + title + scroll view.
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelSelectController : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _backButton;

        private const int TotalLevels = 10;

        protected override void Awake()
        {
            base.Awake();
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.LevelSelect };
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
        }

        protected override void OnDisable()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            base.OnDisable();
        }

        public override void BuildHierarchy(UITheme_300Mind theme)
        {
            if (theme == null) return;
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);

            UIThemeBuilder.ClearChildren(transform);

            if (theme.backgroundScene != null)
            {
                var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
                bg.transform.SetParent(transform, false);
                var bgRt = (RectTransform)bg.transform;
                bgRt.anchorMin = Vector2.zero;
                bgRt.anchorMax = Vector2.one;
                bgRt.offsetMin = Vector2.zero;
                bgRt.offsetMax = Vector2.zero;
                bg.GetComponent<Image>().sprite = theme.backgroundScene;
                bg.GetComponent<Image>().raycastTarget = false;
            }

            _backButton = UIThemeBuilder.BuildIconButton(transform, theme.iconBack, theme,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(100f, 100f), OnBack);
            _backButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(60f, -60f);

            var title = UIThemeBuilder.BuildText(transform, theme.titleFont, 80f,
                theme.paletteTitle, TextAlignmentOptions.Center, "SELECT LEVEL");
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -140f);
            trt.sizeDelta = new Vector2(0f, 130f);

            BuildScrollList(transform, theme);

            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
        }

        private void BuildScrollList(Transform parent, UITheme_300Mind theme)
        {
            // ScrollView container.
            var scrollGo = new GameObject("ScrollView", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
            scrollGo.transform.SetParent(parent, false);
            var srt = (RectTransform)scrollGo.transform;
            srt.anchorMin = new Vector2(0f, 0f);
            srt.anchorMax = new Vector2(1f, 1f);
            srt.pivot = new Vector2(0.5f, 0.5f);
            srt.offsetMin = new Vector2(80f, 200f);
            srt.offsetMax = new Vector2(-80f, -340f);
            scrollGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.3f);

            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(scrollGo.transform, false);
            var vrt = (RectTransform)viewport.transform;
            vrt.anchorMin = Vector2.zero;
            vrt.anchorMax = Vector2.one;
            vrt.offsetMin = Vector2.zero;
            vrt.offsetMax = Vector2.zero;
            viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.01f);
            viewport.GetComponent<Mask>().showMaskGraphic = false;

            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            content.transform.SetParent(viewport.transform, false);
            var crt = (RectTransform)content.transform;
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.sizeDelta = new Vector2(0f, 0f);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(20, 20, 20, 20);
            vlg.spacing = 20f;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var fitter = content.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.content = crt;
            scrollRect.viewport = vrt;
            scrollRect.horizontal = false;
            scrollRect.vertical = true;

            int unlocked = Mathf.Max(1, LevelService.HighestUnlocked);
            for (int i = 1; i <= TotalLevels; i++)
            {
                bool isUnlocked = i <= unlocked;
                BuildLevelEntry(content.transform, theme, i, isUnlocked);
            }
        }

        private void BuildLevelEntry(Transform parent, UITheme_300Mind theme, int index, bool unlocked)
        {
            var row = new GameObject($"Level_{index:D2}", typeof(RectTransform), typeof(LayoutElement));
            row.transform.SetParent(parent, false);
            var le = row.GetComponent<LayoutElement>();
            le.minHeight = 140f;
            le.preferredHeight = 140f;

            var btn = UIThemeBuilder.BuildButton(row.transform, $"LEVEL {index:D2}", theme,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 0f),
                unlocked ? (System.Action)(() => OnLevelSelected(index)) : null,
                "button");
            var brt = btn.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero;
            brt.anchorMax = Vector2.one;
            brt.offsetMin = Vector2.zero;
            brt.offsetMax = Vector2.zero;

            if (!unlocked) btn.interactable = false;
        }

        private void OnLevelSelected(int level)
        {
            LevelService.CurrentLevel = level;
            UIManager.Instance?.StartGame();
        }

        private void OnBack()
        {
            UIManager.Instance?.ShowMainMenu();
        }
    }
}