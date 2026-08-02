using UnityEngine;
using UnityEngine.UI;
using FlowBlast.Core;
using FlowBlast.Managers;

namespace FlowBlast.UI
{
    /// <summary>
    /// Controls the in-game HUD elements: Pause button, coin counter, etc.
    /// Listens to <see cref="GameStateMachine.OnStateChanged"/> to show/hide itself
    /// when the game transitions to non-playing states.
    ///
    /// Attach to the HUD Canvas root (or a child panel).
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("Buttons")]
        [SerializeField] private Button _pauseButton;

        [Header("Coin Display")]
        [Tooltip("Legacy Text for coin count.")]
        [SerializeField] private Text _coinLabel;
        [Tooltip("TMP UGUI text for coin count.")]
        [SerializeField] private TMPro.TextMeshProUGUI _coinTmpLabel;

        [Header("Level Display")]
        [Tooltip("TMP UGUI text for current level, e.g. 'Level 1'.")]
        [SerializeField] private TMPro.TextMeshProUGUI _levelTmpLabel;

        [Header("Format")]
        [SerializeField] private string _coinPrefix = "Coins: ";
        [SerializeField] private string _levelPrefix = "Level ";

        [Header("Visibility")]
        [Tooltip("GameObject to toggle when HUD should be hidden (e.g. during popups).")]
        [SerializeField] private GameObject _hudPanel;

        private void Awake()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.AddListener(OnPauseClicked);
        }

        private void OnEnable()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.OnStateChanged += HandleStateChanged;

            RefreshAll();
        }

        private void OnDisable()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void OnDestroy()
        {
            if (_pauseButton != null)
                _pauseButton.onClick.RemoveListener(OnPauseClicked);
        }

        // ─── Pause ──────────────────────────────────────────────────────

        private void OnPauseClicked()
        {
            if (GameStateMachine.Instance == null) return;

            if (GameStateMachine.Instance.IsPlaying)
                GameStateMachine.Instance.TransitionTo(GameState.Paused);
        }

        // ─── State Visibility ───────────────────────────────────────────

        private void HandleStateChanged(GameState previous, GameState current)
        {
            // Show HUD only during Playing state.
            if (_hudPanel != null)
            {
                _hudPanel.SetActive(current == GameState.Playing);
            }

            // Refresh displays when returning to Playing.
            if (current == GameState.Playing)
                RefreshAll();
        }

        // ─── Refresh ────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshCoinDisplay();
            RefreshLevelDisplay();
        }

        /// <summary>Update the coin display from SaveManager data.</summary>
        public void RefreshCoinDisplay()
        {
            int coins = 0;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
                coins = SaveManager.Instance.Data.Coins;

            string text = _coinPrefix + coins.ToString();

            if (_coinLabel != null)
                _coinLabel.text = text;
            if (_coinTmpLabel != null)
                _coinTmpLabel.text = text;
        }

        /// <summary>Update the level display from SaveManager data.</summary>
        public void RefreshLevelDisplay()
        {
            if (_levelTmpLabel == null) return;

            int level = 1;
            if (SaveManager.Instance != null && SaveManager.Instance.Data != null)
                level = SaveManager.Instance.Data.CurrentLevel + 1;

            _levelTmpLabel.text = _levelPrefix + level.ToString();
        }
    }
}
