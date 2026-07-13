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

        protected override void Awake()
        {
            base.Awake();

            if (_nextLevelButton != null) _nextLevelButton.onClick.AddListener(NextLevel);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (_nextLevelButton != null) _nextLevelButton.onClick.RemoveListener(NextLevel);
        }

        public void NextLevel()
        {
            Debug.Log("[WinPopup] NextLevel clicked — TODO: load next level");
            Close();
        }
    }
}