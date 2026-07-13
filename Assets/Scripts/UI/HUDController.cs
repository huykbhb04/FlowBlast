using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlowBlast.Gameplay.Conveyor;

namespace FlowBlast.UI
{
    /// <summary>
    /// Heads-up display panel: shows progress (X/Y), coin count, and current level,
    /// and holds the Pause button.
    ///
    /// Visible during <see cref="UIState.Playing"/> only. Pauses the game when the Pause
    /// button is pressed. When a 300Mind theme is injected, the placeholder visuals are
    /// rebuilt with a top-bar layout (pause top-left, coin top-right, progress center,
    /// level label bottom-center).
    /// </summary>
    [DisallowMultipleComponent]
    public class HUDController : UIPanel
    {
        [Header("Labels")]
        [SerializeField] private TextMeshProUGUI _progressLabel;
        [SerializeField] private TextMeshProUGUI _coinLabel;
        [SerializeField] private TextMeshProUGUI _levelLabel;

        [Header("Buttons")]
        [SerializeField] private Button _pauseButton;

        [Header("Wiring")]
        [Tooltip("Optional: existing GameProgressHUD on the scene. If null, we'll look one up at OnEnable.")]
        [SerializeField] private GameProgressHUD _progress;

        private Image _progressFill;

        protected override void Awake()
        {
            base.Awake();
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.Playing };
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_progress == null) _progress = FindObjectOfType<GameProgressHUD>();
            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPause);
            Refresh();
        }

        protected override void OnDisable()
        {
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPause);
            base.OnDisable();
        }

        private void Update()
        {
            Refresh();
        }

        public override void BuildHierarchy(UITheme_300Mind theme)
        {
            if (theme == null) return;
            if (_pauseButton != null) _pauseButton.onClick.RemoveListener(OnPause);

            UIThemeBuilder.ClearChildren(transform);

            // Pause icon button (top-left).
            _pauseButton = UIThemeBuilder.BuildIconButton(transform, theme.iconPause, theme,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(100f, 100f), OnPause);
            var prt = _pauseButton.GetComponent<RectTransform>();
            prt.anchoredPosition = new Vector2(60f, -60f);

            // Coin icon + label (top-right).
            if (theme.iconCoin != null)
                UIThemeBuilder.BuildIcon(transform, theme.iconCoin, new Vector2(80f, 80f), theme)
                    .rectTransform.anchoredPosition = new Vector2(-160f, -60f);
            _coinLabel = UIThemeBuilder.BuildText(transform, theme.bodyFont, 42f,
                theme.paletteTitle, TextAlignmentOptions.MidlineRight, "0");
            var crt = _coinLabel.rectTransform;
            crt.anchorMin = new Vector2(1f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.anchoredPosition = new Vector2(-60f, -60f);
            crt.sizeDelta = new Vector2(220f, 80f);

            // Progress bar (top-center).
            var bar = UIThemeBuilder.BuildProgressBar(transform, theme,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(560f, 50f));
            bar.background.rectTransform.anchoredPosition = new Vector2(0f, -150f);
            _progressFill = bar.fill;

            _progressLabel = UIThemeBuilder.BuildText(bar.background.transform, theme.bodyFont, 32f,
                theme.paletteTitle, TextAlignmentOptions.Center, "0/0");
            var plrt = _progressLabel.rectTransform;
            plrt.anchorMin = Vector2.zero;
            plrt.anchorMax = Vector2.one;
            plrt.offsetMin = Vector2.zero;
            plrt.offsetMax = Vector2.zero;

            // Level label (bottom-center).
            _levelLabel = UIThemeBuilder.BuildText(transform, theme.titleFont, 64f,
                theme.paletteTitle, TextAlignmentOptions.Center, "LEVEL 1");
            var lrt = _levelLabel.rectTransform;
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0f);
            lrt.pivot = new Vector2(0.5f, 0f);
            lrt.anchoredPosition = new Vector2(0f, 120f);
            lrt.sizeDelta = new Vector2(0f, 100f);

            if (_pauseButton != null) _pauseButton.onClick.AddListener(OnPause);
            Refresh();
        }

        private void Refresh()
        {
            if (_progress != null && _progressLabel != null)
            {
                _progressLabel.text = $"{_progress.CurrentCount}/{_progress.Total}";
                if (_progressFill != null && _progress.Total > 0)
                    _progressFill.fillAmount = (float)_progress.CurrentCount / _progress.Total;
            }
            if (_coinLabel != null) _coinLabel.text = CoinService.Coins.ToString();
            if (_levelLabel != null) _levelLabel.text = $"LEVEL {LevelService.CurrentLevel}";
        }

        private void OnPause()
        {
            var mgr = UIManager.Instance;
            if (mgr != null) mgr.Pause();
        }
    }
}