using FlowBlast.Core;
using FlowBlast.Managers;
using UnityEngine;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Dev-only hotkeys to test popup system via GameStateMachine.
    /// Attach to any GameObject in the scene (commonly the PopupManager itself).
    ///
    /// All inputs go through GameStateMachine so state transitions are consistent
    /// and popups are never spawned in duplicate.
    /// </summary>
    public class PopupDemoTriggers : MonoBehaviour
    {
        [Header("Hotkeys (dev only)")]
        [SerializeField] private KeyCode _pauseKey = KeyCode.Escape;
        [SerializeField] private KeyCode _winKey = KeyCode.Space;
        [SerializeField] private KeyCode _loseKey = KeyCode.L;
        [SerializeField] private KeyCode _closeAllKey = KeyCode.C;

        private void Update()
        {
            // Pause toggle: routes through GameStateMachine to avoid duplicate popups.
            if (Input.GetKeyDown(_pauseKey))
            {
                if (GameStateMachine.Instance != null)
                {
                    GameStateMachine.Instance.TogglePause();
                }
                else
                {
                    // Fallback: toggle via PopupManager directly (no FSM in scene).
                    PopupManager pm = PopupManager.Instance;
                    if (pm != null)
                    {
                        if (pm.IsAnyPopupOpen) pm.CloseTopPopup();
                        else pm.Show(PopupId.Pause);
                    }
                }
            }

            // Win: only during Playing state.
            if (Input.GetKeyDown(_winKey))
            {
                if (GameStateMachine.Instance != null && GameStateMachine.Instance.IsPlaying)
                {
                    GameStateMachine.Instance.TransitionTo(GameState.Win);
                }
            }

            // Lose: only during Playing state.
            if (Input.GetKeyDown(_loseKey))
            {
                if (GameStateMachine.Instance != null && GameStateMachine.Instance.IsPlaying)
                {
                    GameStateMachine.Instance.TransitionTo(GameState.Lose);
                }
            }

            // Close all: escape hatch.
            if (Input.GetKeyDown(_closeAllKey) && _closeAllKey != _pauseKey)
            {
                PopupManager pm = PopupManager.Instance;
                if (pm != null) pm.CloseAll();

                // Also return FSM to Playing so the game is unblocked.
                if (GameStateMachine.Instance != null &&
                    GameStateMachine.Instance.CurrentState != GameState.Playing)
                {
                    GameStateMachine.Instance.TransitionTo(GameState.Playing);
                }
            }
        }
    }
}