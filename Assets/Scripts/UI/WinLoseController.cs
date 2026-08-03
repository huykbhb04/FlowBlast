using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.UI
{
    /// <summary>
    /// Combined Win / Lose panel. Same component can be placed twice (once for Win,
    /// once for Lose) since <see cref="_shownStates"/> is serialized per-instance.
    ///
    /// When a 300Mind theme is injected, the placeholder visuals are rebuilt with a
    /// coloured overlay + centred panel. Win variant shows "VICTORY!" + reward + Next
    /// Level; Lose variant shows "TRY AGAIN" + Restart.
    /// </summary>
    [DisallowMultipleComponent]
    public class WinLoseController : UIPanel
    {
        [Header("Reward")]
        [Tooltip("Coins granted to the wallet when this Win panel shows. Lose panel: set 0.")]
        [SerializeField] private int _coinsReward = 10;

        [Header("Buttons")]
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _mainMenuButton;

        [Header("Optional Texts")]
        [SerializeField] private TextMeshProUGUI _rewardText;
        [SerializeField] private TextMeshProUGUI _titleText;

        private bool _alreadyRewarded;

        protected override void Awake()
        {
            base.Awake();
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.Won };
        }

        protected override void OnEnable()
        {
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.Won };

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

            bool isWin = _shownStates != null && System.Array.IndexOf(_shownStates, UIState.Won) >= 0;

            UIThemeBuilder.ClearChildren(transform);

            Color overlayTint = isWin
                ? new Color(1f, 0.85f, 0.3f, 0.3f)
                : new Color(0.9f, 0.2f, 0.2f, 0.3f);
            UIThemeBuilder.BuildBackdrop(transform, overlayTint);

            var panel = UIThemeBuilder.BuildPanel(transform, "ResultPanel", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(780f, 950f), useHeader: false, headerText: null);

            _titleText = UIThemeBuilder.BuildText(panel.transform, theme.titleFont, 110f,
                isWin ? theme.palettePrimary : new Color(0.95f, 0.3f, 0.3f, 1f),
                TextAlignmentOptions.Center, isWin ? "VICTORY!" : "TRY AGAIN");
            var trt = _titleText.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -80f);
            trt.sizeDelta = new Vector2(0f, 150f);

            if (isWin && theme.iconCoin != null)
            {
                var coinIcon = UIThemeBuilder.BuildIcon(panel.transform, theme.iconCoin,
                    new Vector2(100f, 100f), theme);
                coinIcon.rectTransform.anchoredPosition = new Vector2(-80f, -10f);
            }

            _rewardText = UIThemeBuilder.BuildText(panel.transform, theme.titleFont, 80f,
                theme.paletteAccent, TextAlignmentOptions.Center, _coinsReward > 0 ? $"+{_coinsReward}" : "");
            var rrt = _rewardText.rectTransform;
            rrt.anchorMin = new Vector2(0f, 1f);
            rrt.anchorMax = new Vector2(1f, 1f);
            rrt.pivot = new Vector2(0.5f, 1f);
            rrt.anchoredPosition = new Vector2(0f, -260f);
            rrt.sizeDelta = new Vector2(0f, 120f);

            float width = 540f;
            float height = 110f;

            if (isWin)
            {
                _nextButton = UIThemeBuilder.BuildButton(panel.transform, "NEXT LEVEL", theme,
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(width, height), OnNext, "button");
                _nextButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -130f);
            }

            _restartButton = UIThemeBuilder.BuildButton(panel.transform, isWin ? "RESTART" : "RETRY", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width, height), OnRestart, "button");
            _restartButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -270f);

            _mainMenuButton = UIThemeBuilder.BuildButton(panel.transform, "MAIN MENU", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(width, height), OnMainMenu, "button");
            _mainMenuButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -390f);

            WireButtons();
        }

        protected override void OnShow()
        {
            if (!_alreadyRewarded && _coinsReward > 0 && _shownStates != null
                && System.Array.IndexOf(_shownStates, UIState.Won) >= 0)
            {
                CoinService.Add(_coinsReward);
                LevelService.CompleteLevel(LevelService.CurrentLevel);
                _alreadyRewarded = true;
            }

            if (_rewardText != null)
                _rewardText.text = _coinsReward > 0 ? $"+{_coinsReward}" : string.Empty;
        }

        protected override void OnHide() => _alreadyRewarded = false;

        private void WireButtons()
        {
            if (_nextButton != null) _nextButton.onClick.AddListener(OnNext);
            if (_restartButton != null) _restartButton.onClick.AddListener(OnRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.AddListener(OnMainMenu);
        }

        private void UnwireButtons()
        {
            if (_nextButton != null) _nextButton.onClick.RemoveListener(OnNext);
            if (_restartButton != null) _restartButton.onClick.RemoveListener(OnRestart);
            if (_mainMenuButton != null) _mainMenuButton.onClick.RemoveListener(OnMainMenu);
        }

        private void OnNext()
        {
            LevelService.CompleteLevel(LevelService.CurrentLevel);
            UIManager.Instance?.RestartLevel();
        }

        private void OnRestart() => UIManager.Instance?.RestartLevel();
        private void OnMainMenu() => UIManager.Instance?.ShowMainMenu();
    }
}