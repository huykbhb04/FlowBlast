using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.UI
{
    /// <summary>
    /// Title screen panel. Hidden during gameplay, shown only in <see cref="UIState.MainMenu"/>.
    /// Hooks the onClick callbacks of the Play / Levels / Settings / Quit buttons to
    /// <see cref="UIManager"/>. When a 300Mind theme is injected via
    /// <see cref="BuildHierarchy"/>, the placeholder visuals are replaced with a background
    /// image, a title, four themed buttons, and a coin label at the bottom.
    /// </summary>
    [DisallowMultipleComponent]
    public class MainMenuController : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _quitButton;
        [SerializeField] private Button _levelsButton;
        [SerializeField] private Button _settingsButton;

        [Header("Optional Texts")]
        [Tooltip("TMP text that displays total coins on the menu.")]
        [SerializeField] private TextMeshProUGUI _coinsText;

        [Header("Optional Title")]
        [SerializeField] private TextMeshProUGUI _titleText;

        protected override void Awake()
        {
            base.Awake();
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.MainMenu };
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

            // Full-screen background.
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

            // Title.
            _titleText = UIThemeBuilder.BuildText(transform, theme.titleFont, 96f,
                theme.paletteTitle, TextAlignmentOptions.Center, "FLOW BLAST");
            var titleRt = _titleText.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.anchoredPosition = new Vector2(0f, -120f);
            titleRt.sizeDelta = new Vector2(0f, 200f);

            // Four buttons stacked top-center.
            float centerX = 0.5f;
            float width = 540f;
            float height = 110f;
            float[] ys = { -440f, -580f, -720f, -860f };
            _playButton     = UIThemeBuilder.BuildButton(transform, "PLAY",     theme,
                new Vector2(centerX, 1f), new Vector2(centerX, 1f), new Vector2(0.5f, 1f),
                new Vector2(width, height), OnPlay, "button");
            _levelsButton   = UIThemeBuilder.BuildButton(transform, "LEVELS",   theme,
                new Vector2(centerX, 1f), new Vector2(centerX, 1f), new Vector2(0.5f, 1f),
                new Vector2(width, height), OnLevels, "button");
            _settingsButton = UIThemeBuilder.BuildButton(transform, "SETTINGS", theme,
                new Vector2(centerX, 1f), new Vector2(centerX, 1f), new Vector2(0.5f, 1f),
                new Vector2(width, height), OnSettings, "button");
            _quitButton     = UIThemeBuilder.BuildButton(transform, "QUIT",     theme,
                new Vector2(centerX, 1f), new Vector2(centerX, 1f), new Vector2(0.5f, 1f),
                new Vector2(width, height), OnQuit, "button");

            // Anchor each button to its y position via separate anchoring GameObjects.
            // BuildButton uses identical anchor presets for all four; we shift each by
            // creating a tiny holder so the buttons stack at distinct heights.
            _playButton.GetComponent<RectTransform>().anchoredPosition     = new Vector2(0f, ys[0]);
            _levelsButton.GetComponent<RectTransform>().anchoredPosition   = new Vector2(0f, ys[1]);
            _settingsButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, ys[2]);
            _quitButton.GetComponent<RectTransform>().anchoredPosition     = new Vector2(0f, ys[3]);

            // Coin label at bottom.
            _coinsText = UIThemeBuilder.BuildText(transform, theme.bodyFont, 48f,
                theme.paletteTitle, TextAlignmentOptions.Center, "0");
            var coinRt = _coinsText.rectTransform;
            coinRt.anchorMin = new Vector2(0f, 0f);
            coinRt.anchorMax = new Vector2(1f, 0f);
            coinRt.pivot = new Vector2(0.5f, 0f);
            coinRt.anchoredPosition = new Vector2(0f, 80f);
            coinRt.sizeDelta = new Vector2(0f, 80f);

            if (theme.iconCoin != null)
            {
                UIThemeBuilder.BuildIcon(transform, theme.iconCoin, new Vector2(80f, 80f), theme)
                    .rectTransform.anchoredPosition = new Vector2(0f, 120f);
            }

            WireButtons();
        }

        protected override void OnShow()
        {
            if (_coinsText != null) _coinsText.text = CoinService.Coins.ToString();
        }

        private void WireButtons()
        {
            if (_playButton != null)     _playButton.onClick.AddListener(OnPlay);
            if (_quitButton != null)     _quitButton.onClick.AddListener(OnQuit);
            if (_levelsButton != null)   _levelsButton.onClick.AddListener(OnLevels);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettings);
        }

        private void UnwireButtons()
        {
            if (_playButton != null)     _playButton.onClick.RemoveListener(OnPlay);
            if (_quitButton != null)     _quitButton.onClick.RemoveListener(OnQuit);
            if (_levelsButton != null)   _levelsButton.onClick.RemoveListener(OnLevels);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettings);
        }

        private void OnPlay()     { var m = UIManager.Instance; if (m != null) m.StartGame(); }
        private void OnLevels()   { var m = UIManager.Instance; if (m != null) m.ShowLevelSelect(); }
        private void OnSettings() { var m = UIManager.Instance; if (m != null) m.ShowSettings(); }
        private void OnQuit()     { var m = UIManager.Instance; if (m != null) m.QuitGame(); }
    }
}