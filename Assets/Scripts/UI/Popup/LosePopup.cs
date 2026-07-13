using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Lose popup: Replay button reloads the current scene.
    /// </summary>
    public class LosePopup : BasePopup
    {
        [SerializeField] private Button _replayButton;

        private void Awake()
        {
            base.Awake();

            if (_replayButton != null) _replayButton.onClick.AddListener(Replay);
        }

        private void OnDestroy()
        {
            if (_replayButton != null) _replayButton.onClick.RemoveListener(Replay);
        }

        public void Replay()
        {
            int buildIndex = SceneManager.GetActiveScene().buildIndex;
            SceneManager.LoadScene(buildIndex);
        }
    }
}