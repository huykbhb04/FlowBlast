using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Win popup: single NextLevel button (stub for now — logs TODO).
    /// </summary>
    public class WinPopup : BasePopup
    {
        [SerializeField] private Button _nextLevelButton;

        private void Awake()
        {
            base.Awake();

            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(NextLevel);
        }

        private void OnDestroy()
        {
            if (_nextLevelButton != null) _nextLevelButton.onClick.RemoveListener(NextLevel);
        }

        public void NextLevel()
        {
            Debug.Log("[WinPopup] NextLevel clicked — TODO: load next level");
            Close();
        }
    }
}