using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlowBlast.Core;
using FlowBlast.Managers;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Pause popup: Replay (reload scene), Continue (resume), Home (main menu),
    /// and toggles for SFX/Music via AudioManager.
    /// Restores Time.timeScale when resuming.
    /// </summary>
    public class PausePopup : BasePopup
    {
        [Header("Navigation")]
        [SerializeField] private Button _replayButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _homeButton;

        [Header("Audio Toggles")]
        [SerializeField] private Button _sfxToggleButton;
        [SerializeField] private Button _musicToggleButton;

        [Header("Toggle Visuals (optional)")]
        [Tooltip("Image/GameObject shown when SFX is ON.")]
        [SerializeField] private GameObject _sfxOnIndicator;
        [Tooltip("Image/GameObject shown when SFX is OFF.")]
        [SerializeField] private GameObject _sfxOffIndicator;
        [Tooltip("Image/GameObject shown when Music is ON.")]
        [SerializeField] private GameObject _musicOnIndicator;
        [Tooltip("Image/GameObject shown when Music is OFF.")]
        [SerializeField] private GameObject _musicOffIndicator;

        protected override void Awake()
        {
            base.Awake();

            if (_replayButton != null) _replayButton.onClick.AddListener(Replay);
            if (_continueButton != null) _continueButton.onClick.AddListener(Continue);
            if (_homeButton != null) _homeButton.onClick.AddListener(Home);
            if (_sfxToggleButton != null) _sfxToggleButton.onClick.AddListener(ToggleSfx);
            if (_musicToggleButton != null) _musicToggleButton.onClick.AddListener(ToggleMusic);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_replayButton != null) _replayButton.onClick.RemoveListener(Replay);
            if (_continueButton != null) _continueButton.onClick.RemoveListener(Continue);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(Home);
            if (_sfxToggleButton != null) _sfxToggleButton.onClick.RemoveListener(ToggleSfx);
            if (_musicToggleButton != null) _musicToggleButton.onClick.RemoveListener(ToggleMusic);
        }

        public override void OnShown()
        {
            base.OnShown();
            RefreshToggleVisuals();
        }

        // ─── Navigation ─────────────────────────────────────────────────

        public void Replay()
        {
            // Resume time before reloading.
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.Playing);

            Close();

            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }

        public void Continue()
        {
            // Resume gameplay.
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.Playing);

            Close();
        }

        public void Home()
        {
            // Resume time before leaving.
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.MainMenu);

            Close();

            // Load scene 0 (MainMenu) if available.
            if (SceneManager.sceneCountInBuildSettings > 1)
                SceneManager.LoadScene(0);
            else
            {
                Debug.Log("[PausePopup] No MainMenu scene in build — reloading current.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
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
    }
}