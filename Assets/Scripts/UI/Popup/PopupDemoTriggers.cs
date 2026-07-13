using FlowBlast.Managers;
using UnityEngine;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Dev-only hotkeys to test popup system without wiring gameplay triggers.
    /// Attach to any GameObject in the scene (commonly the PopupManager itself).
    /// All inputs use the legacy Input system (Input.GetKeyDown) — switch to the
    /// new Input System package by replacing with Keyboard.current.* if needed.
    /// </summary>
    public class PopupDemoTriggers : MonoBehaviour
    {
        [Header("Hotkeys (dev only)")]
        [SerializeField] private KeyCode _pauseKey = KeyCode.Escape;
        [SerializeField] private KeyCode _winKey = KeyCode.Space;
        [SerializeField] private KeyCode _loseKey = KeyCode.L;
        [SerializeField] private KeyCode _closeTopKey = KeyCode.Escape;
        [SerializeField] private KeyCode _closeAllKey = KeyCode.C;

        [Header("Behaviour")]
        [Tooltip("If true, the same pause key will toggle: open if closed, close-top if open.")]
        [SerializeField] private bool _pauseKeyToggles = true;

        private void Update()
        {
            PopupManager pm = PopupManager.Instance;
            if (pm == null) return;

            // Pause: either show, or close-top if already open.
            if (Input.GetKeyDown(_pauseKey))
            {
                if (_pauseKeyToggles && pm.IsAnyPopupOpen)
                {
                    pm.CloseTopPopup();
                }
                else
                {
                    pm.OpenPausePopup();
                }
            }

            // Win / Lose: only fire when no popup is currently open.
            if (Input.GetKeyDown(_winKey) && !pm.IsAnyPopupOpen)
            {
                pm.OpenWinPopup();
            }

            if (Input.GetKeyDown(_loseKey) && !pm.IsAnyPopupOpen)
            {
                pm.OpenLosePopup();
            }

            // Close-top / Close-all.
            if (Input.GetKeyDown(_closeTopKey) && _closeTopKey != _pauseKey)
            {
                pm.CloseTopPopup();
            }

            if (Input.GetKeyDown(_closeAllKey))
            {
                pm.CloseAll();
            }
        }
    }
}