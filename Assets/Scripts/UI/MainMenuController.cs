using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlowBlast.Core;
using FlowBlast.Managers;

namespace FlowBlast.UI
{
    /// <summary>
    /// Main Menu screen controller.
    /// Provides Play (start/resume), Level Select stub, and Settings (audio toggles).
    ///
    /// This component lives in the MainMenu scene. When the user taps Play,
    /// it loads the gameplay scene (build index 1) and transitions FSM to Playing.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _playButton;
        [SerializeField] private Button _levelSelectButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _quitButton;

        [Header("Settings Panel (toggle visibility)")]
        [SerializeField] private GameObject _settingsPanel;
        [SerializeField] private Button _sfxToggleButton;
        [SerializeField] private Button _musicToggleButton;
        [SerializeField] private Button _settingsCloseButton;

        [Header("Toggle Indicators")]
        [SerializeField] private GameObject _sfxOnIndicator;
        [SerializeField] private GameObject _sfxOffIndicator;
        [SerializeField] private GameObject _musicOnIndicator;
        [SerializeField] private GameObject _musicOffIndicator;

        [Header("Display")]
        [Tooltip("TMP label to show current level, e.g. 'Level 1'.")]
        [SerializeField] private TMPro.TextMeshProUGUI _currentLevelLabel;
        [Tooltip("TMP label to show coin count.")]
        [SerializeField] private TMPro.TextMeshProUGUI _coinLabel;

        [Header("Scene")]
        [Tooltip("Build index of the gameplay scene to load when Play is pressed.")]
        [SerializeField] private int _gameplaySceneIndex = 1;

        private void Awake()
        {
            if (_playButton != null) _playButton.onClick.AddListener(OnPlayClicked);
            if (_levelSelectButton != null) _levelSelectButton.onClick.AddListener(OnLevelSelectClicked);
            if (_settingsButton != null) _settingsButton.onClick.AddListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.AddListener(OnQuitClicked);
            if (_sfxToggleButton != null) _sfxToggleButton.onClick.AddListener(ToggleSfx);
            if (_musicToggleButton != null) _musicToggleButton.onClick.AddListener(ToggleMusic);
            if (_settingsCloseButton != null) _settingsCloseButton.onClick.AddListener(CloseSettings);
        }

        private void Start()
        {
            // Ensure FSM is in MainMenu state.
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.MainMenu);

            // Ensure time is running (in case we came from a paused state).
            Time.timeScale = 1f;

            RefreshDisplay();
            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_playButton != null) _playButton.onClick.RemoveListener(OnPlayClicked);
            if (_levelSelectButton != null) _levelSelectButton.onClick.RemoveListener(OnLevelSelectClicked);
            if (_settingsButton != null) _settingsButton.onClick.RemoveListener(OnSettingsClicked);
            if (_quitButton != null) _quitButton.onClick.RemoveListener(OnQuitClicked);
            if (_sfxToggleButton != null) _sfxToggleButton.onClick.RemoveListener(ToggleSfx);
            if (_musicToggleButton != null) _musicToggleButton.onClick.RemoveListener(ToggleMusic);
            if (_settingsCloseButton != null) _settingsCloseButton.onClick.RemoveListener(CloseSettings);
        }

        // ─── Navigation ─────────────────────────────────────────────────

        private void OnPlayClicked()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioId.ButtonClick);

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.Playing);

            SceneManager.LoadScene(_gameplaySceneIndex);
        }

        private void OnLevelSelectClicked()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioId.ButtonClick);

            // TODO: Open Level Select screen/popup when implemented.
            Debug.Log("[MainMenuController] Level Select — TODO");
        }

        private void OnSettingsClicked()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioId.ButtonClick);

            if (_settingsPanel != null) _settingsPanel.SetActive(true);
            RefreshToggleVisuals();
        }

        private void CloseSettings()
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(AudioId.ButtonClick);

            if (_settingsPanel != null) _settingsPanel.SetActive(false);
        }

        private void OnQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ─── Audio Toggles ──────────────────────────────────────────────

        private void ToggleSfx()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleSfx();
                AudioManager.Instance.PlaySfx(AudioId.ButtonClick);
            }
            RefreshToggleVisuals();
        }

        private void ToggleMusic()
        {
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ToggleMusic();
                AudioManager.Instance.PlaySfx(AudioId.ButtonClick);
            }
            RefreshToggleVisuals();
        }

        private void RefreshToggleVisuals()
        {
            bool sfxOn = AudioManager.Instance != null ? AudioManager.Instance.IsSfxOn : true;
            bool musicOn = AudioManager.Instance != null ? AudioManager.Instance.IsMusicOn : true;

            if (_sfxOnIndicator != null) _sfxOnIndicator.SetActive(sfxOn);
            if (_sfxOffIndicator != null) _sfxOffIndicator.SetActive(!sfxOn);
            if (_musicOnIndicator != null) _musicOnIndicator.SetActive(musicOn);
            if (_musicOffIndicator != null) _musicOffIndicator.SetActive(!musicOn);
        }

        // ─── Display ────────────────────────────────────────────────────

        private void RefreshDisplay()
        {
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
            {
                var data = SaveManager.Instance.Data;
                if (_currentLevelLabel != null)
                    _currentLevelLabel.text = $"Level {data.CurrentLevel + 1}";
                if (_coinLabel != null)
                    _coinLabel.text = data.Coins.ToString();
            }
        }
    }
}
