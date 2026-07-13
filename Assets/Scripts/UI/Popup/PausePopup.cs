using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Pause popup: Replay (reload scene), Continue (close), Home (TODO stub).
    /// Does NOT touch Time.timeScale — gameplay decides its own pause.
    /// </summary>
    public class PausePopup : BasePopup
    {
        [SerializeField] private Button _replayButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private Button _homeButton;

        private void Awake()
        {
            base.Awake();

            if (_replayButton != null) _replayButton.onClick.AddListener(Replay);
            if (_continueButton != null) _continueButton.onClick.AddListener(Continue);
            if (_homeButton != null) _homeButton.onClick.AddListener(Home);
        }

        private void OnDestroy()
        {
            if (_replayButton != null) _replayButton.onClick.RemoveListener(Replay);
            if (_continueButton != null) _continueButton.onClick.RemoveListener(Continue);
            if (_homeButton != null) _homeButton.onClick.RemoveListener(Home);
        }

        public void Replay()
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }

        public void Continue()
        {
            Close();
        }

        public void Home()
        {
            Debug.Log("[PausePopup] Home clicked — TODO: load home scene");
            Close();
        }
    }
}