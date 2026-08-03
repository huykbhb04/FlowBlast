using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlowBlast.Core;
using FlowBlast.Managers;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Win popup: Retry (reload level) and Next Level buttons.
    /// Awards coins and unlocks the next level via SaveManager.
    /// </summary>
    public class WinPopup : BasePopup
    {
        [SerializeField] private Button _nextLevelButton;
        [SerializeField] private Button _retryButton;

        [Header("Coin Display (optional)")]
        [Tooltip("Text showing coins earned this level.")]
        [SerializeField] private TMPro.TextMeshProUGUI _coinRewardLabel;

        protected override void Awake()
        {
            base.Awake();

            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(NextLevel);
            if (_retryButton != null) _retryButton.onClick.AddListener(Retry);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_nextLevelButton != null) _nextLevelButton.onClick.RemoveListener(NextLevel);
            if (_retryButton != null) _retryButton.onClick.RemoveListener(Retry);
        }

        public override void OnShown()
        {
            base.OnShown();
            UpdateCoinRewardDisplay();
        }

        /// <summary>
        /// Load the next level. Unlocks it in SaveManager, then reloads the scene
        /// (LevelLoader will pick up the next level config).
        /// </summary>
        public void NextLevel()
        {
            FlowBlast.Gameplay.LevelLoader levelLoader = FlowBlast.Gameplay.LevelLoader.Instance;
            bool loadedNextLevel = levelLoader != null && levelLoader.AdvanceToNextLevel();
            if (levelLoader == null && SaveManager.Instance != null)
            {
                SaveManager.Instance.Data.UnlockNextLevel();
                SaveManager.Instance.Data.CurrentLevel++;
                SaveManager.Instance.Save();
            }

            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.TransitionTo(GameState.Playing);
            }

            Close();
            if (!loadedNextLevel)
            {
                ReloadScene();
            }
        }

        /// <summary>
        /// Replay the current level from scratch.
        /// </summary>
        public void Retry()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.Playing);

            Close();
            ReloadScene();
        }

        private void ReloadScene()
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }

        private void UpdateCoinRewardDisplay()
        {
            if (_coinRewardLabel == null)
            {
                return;
            }

            FlowBlast.Gameplay.LevelLoader levelLoader = FlowBlast.Gameplay.LevelLoader.Instance;
            if (levelLoader != null && levelLoader.CurrentConfig != null)
            {
                _coinRewardLabel.text = $"+{levelLoader.CurrentConfig.CoinReward}";
            }
            else
            {
                _coinRewardLabel.text = "";
            }
        }
    }
}