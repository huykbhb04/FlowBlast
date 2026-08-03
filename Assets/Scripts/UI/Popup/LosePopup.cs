using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FlowBlast.Core;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Lose popup: Replay button restarts the current level via GameStateMachine.
    /// </summary>
    public class LosePopup : BasePopup
    {
        [SerializeField] private Button _replayButton;
        [SerializeField] private Button _homeButton;

        protected override void Awake()
        {
            base.Awake();

            if (_replayButton != null) _replayButton.onClick.AddListener(Replay);
            if (_homeButton != null) _homeButton.onClick.AddListener(Home);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_replayButton != null) _replayButton.onClick.RemoveListener(Replay);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(Home);
        }

        /// <summary>
        /// Restart the current level.
        /// </summary>
        public void Replay()
        {
            // Transition back to Playing (resets timeScale if needed).
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.Playing);

            Close();

            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }

        /// <summary>
        /// Go back to main menu / level select.
        /// </summary>
        public void Home()
        {
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.MainMenu);

            Close();

            // Load scene 0 (MainMenu) if it exists; otherwise reload current.
            if (SceneManager.sceneCountInBuildSettings > 1)
                SceneManager.LoadScene(0);
            else
            {
                Debug.Log("[LosePopup] No MainMenu scene in build — reloading current.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
        }
    }
}