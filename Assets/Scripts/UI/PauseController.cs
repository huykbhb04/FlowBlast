using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.UI
{
    /// <summary>
    /// Pause overlay panel. Shown only while <see cref="UIState.Paused"/>.
    /// Wires Resume / Restart / Main Menu buttons to UIManager. When a 300Mind theme
    /// is injected, the placeholder visuals are rebuilt as a dimmed backdrop + centred
    /// panel with title and three themed buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseController : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;

        [Header("Optional Title")]
        [SerializeField] private TextMeshProUGUI _titleText;

        protected override void Awake()
        {
            base.Awake();
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.Paused };
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            WireButtons();
        }

        protected override void OnDisable()
        {
            UnwireButtons();
            base.OnDisable();
        }

        public override void BuildHierarchy(UITheme_300Mind theme)
        {
            if (theme == null) return;
            UnwireButtons();

            UIThemeBuilder.ClearChildren(transform);

            UIThemeBuilder.BuildBackdrop(transform, new Color(0f, 0f, 0f, 0.55f));

            var panel = UIThemeBuilder.BuildPanel(transform, "PausePanelInner", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(720f, 900f), useHeader: false, headerText: null);

            _titleText = UIThemeBuilder.BuildText(panel.transform, theme.titleFont, 96f,
                theme.paletteTitle, TextAlignmentOptions.Center, "PAUSED");
            var trt = _titleText.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -60f);
            trt.sizeDelta = new Vector2(0f, 130f);

            float width = 540f;
            float height = 110f;
            _resumeButton = UIThemeBuilder.BuildButton(panel.transform, "RESUME", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width, height), OnResume, "button");
            _resumeButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 30f);

            _restartButton = UIThemeBuilder.BuildButton(panel.transform, "RESTART", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width, height), OnRestart, "button");
            _restartButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -110f);

            _mainMenuButton = UIThemeBuilder.BuildButton(panel.transform, "MAIN MENU", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width, height), OnMainMenu, "button");
            _mainMenuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -250f);

            WireButtons();
        }

        private void WireButtons()
        {
            if (_resumeButton != null)  _resumeButton.onClick.AddListener(OnResume);
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        private void UnwireButtons()
        {
            if (_resumeButton != null)  _resumeButton.onClick.RemoveListener(OnResume);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(OnMainMenu);
        }

        private void OnResume() => UIManager.Instance?.Resume();
        private void OnRestart() => UIManager.Instance?.RestartLevel();
        private void OnMainMenu() => UIManager.Instance?.ShowMainMenu();
    }
}