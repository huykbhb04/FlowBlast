using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace FlowBlast.UI
{
    /// <summary>
    /// Settings panel. Shown only in <see cref="UIState.Settings"/>.
    /// Three toggles (Music / SFX / Vibration) backed by <see cref="SettingsService"/>.
    /// When a 300Mind theme is injected, the placeholder visuals are rebuilt with a back
    /// button + title + three themed toggles + credits text.
    /// </summary>
    [DisallowMultipleComponent]
    public class SettingsController : UIPanel
    {
        [Header("Buttons")]
        [SerializeField] private Button _backButton;

        [Header("Toggles")]
        [SerializeField] private Toggle _musicToggle;
        [SerializeField] private Toggle _sfxToggle;
        [SerializeField] private Toggle _vibrationToggle;

        protected override void Awake()
        {
            base.Awake();
            if (_shownStates == null || _shownStates.Length == 0)
                _shownStates = new[] { UIState.Settings };
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            WireToggles();
            SyncFromService();
        }

        protected override void OnDisable()
        {
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            UnwireToggles();
            base.OnDisable();
        }

        public override void BuildHierarchy(UITheme_300Mind theme)
        {
            if (theme == null) return;
            if (_backButton != null) _backButton.onClick.RemoveListener(OnBack);
            UnwireToggles();

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
                theme.paletteTitle, TextAlignmentOptions.Center, "SETTINGS");
            var trt = title.rectTransform;
            trt.anchorMin = new Vector2(0f, 1f);
            trt.anchorMax = new Vector2(1f, 1f);
            trt.pivot = new Vector2(0.5f, 1f);
            trt.anchoredPosition = new Vector2(0f, -140f);
            trt.sizeDelta = new Vector2(0f, 130f);

            _musicToggle = UIThemeBuilder.BuildToggle(transform, "Music", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(820f, 120f), SettingsService.MusicEnabled);
            _musicToggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 260f);

            _sfxToggle = UIThemeBuilder.BuildToggle(transform, "SFX", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(820f, 120f), SettingsService.SfxEnabled);
            _sfxToggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, 100f);

            _vibrationToggle = UIThemeBuilder.BuildToggle(transform, "Vibration", theme,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(820f, 120f), SettingsService.VibrationEnabled);
            _vibrationToggle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -60f);

            var credits = UIThemeBuilder.BuildText(transform, theme.bodyFont, 32f,
                theme.paletteTitle, TextAlignmentOptions.Center, "Made with 300Mind UI Kit");
            var crt = credits.rectTransform;
            crt.anchorMin = new Vector2(0f, 0f);
            crt.anchorMax = new Vector2(1f, 0f);
            crt.pivot = new Vector2(0.5f, 0f);
            crt.anchoredPosition = new Vector2(0f, 200f);
            crt.sizeDelta = new Vector2(0f, 60f);

            if (_backButton != null) _backButton.onClick.AddListener(OnBack);
            WireToggles();
            SyncFromService();
        }

        private void WireToggles()
        {
            if (_musicToggle != null)
                _musicToggle.onValueChanged.AddListener(v => SettingsService.MusicEnabled = v);
            if (_sfxToggle != null)
                _sfxToggle.onValueChanged.AddListener(v => SettingsService.SfxEnabled = v);
            if (_vibrationToggle != null)
                _vibrationToggle.onValueChanged.AddListener(v => SettingsService.VibrationEnabled = v);
        }

        private void UnwireToggles()
        {
            if (_musicToggle != null) _musicToggle.onValueChanged.RemoveAllListeners();
            if (_sfxToggle != null) _sfxToggle.onValueChanged.RemoveAllListeners();
            if (_vibrationToggle != null) _vibrationToggle.onValueChanged.RemoveAllListeners();
        }

        private void SyncFromService()
        {
            if (_musicToggle != null) _musicToggle.SetIsOnWithoutNotify(SettingsService.MusicEnabled);
            if (_sfxToggle != null) _sfxToggle.SetIsOnWithoutNotify(SettingsService.SfxEnabled);
            if (_vibrationToggle != null) _vibrationToggle.SetIsOnWithoutNotify(SettingsService.VibrationEnabled);
        }

        private void OnBack()
        {
            UIManager.Instance?.ShowMainMenu();
        }
    }
}