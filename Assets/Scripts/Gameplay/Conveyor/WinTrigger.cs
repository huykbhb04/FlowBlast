using UnityEngine;
using FlowBlast.Core;
using FlowBlast.Managers;
using FlowBlast.UI.Popup;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Listens to GameProgressHUDDisplay.IsComplete and transitions to
    /// <see cref="GameState.Win"/> via <see cref="GameStateMachine"/>
    /// the moment all colors on the spline have been consumed.
    ///
    /// The Win popup is shown by GameStateMachine's EnterState(Win) callback.
    ///
    /// Attach this component anywhere in the scene (e.g. on the same Progress HUD
    /// GameObject, or on the root LevelManager). One instance per scene is sufficient.
    /// Fires only once — the first time IsComplete becomes true.
    /// </summary>
    [DisallowMultipleComponent]
    public class WinTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The GameProgressHUDDisplay to watch. If null, searches the scene automatically.")]
        [SerializeField] private GameProgressHUDDisplay progressDisplay;

        [Header("Options")]
        [Tooltip("Delay (seconds) between IsComplete turning true and the popup appearing. " +
                 "Gives the last ball dissolve animation a chance to finish.")]
        [SerializeField, Min(0f)] private float delayBeforePopup = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool logTrigger = true;

        private bool _hasTriggered;
        private float _triggerTime = -1f;

        private void Awake()
        {
            if (progressDisplay == null)
                progressDisplay = GetComponent<GameProgressHUDDisplay>();

            if (progressDisplay == null)
                progressDisplay = Object.FindObjectOfType<GameProgressHUDDisplay>();
        }

        private void OnEnable()
        {
            _hasTriggered = false;
            _triggerTime = -1f;
        }

        private void Update()
        {
            if (_hasTriggered) return;
            if (progressDisplay == null) return;

            // Only check during gameplay.
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.IsPlaying)
                return;

            if (progressDisplay.IsComplete)
            {
                _hasTriggered = true;
                _triggerTime = Time.realtimeSinceStartup;
                if (logTrigger)
                    Debug.Log($"[WinTrigger] All colors consumed — triggering Win in {delayBeforePopup:F2}s.");
            }
            else if (_triggerTime >= 0f)
            {
                // IsComplete flipped back to false (level restart) — reset.
                _hasTriggered = false;
                _triggerTime = -1f;
            }
        }

        private void LateUpdate()
        {
            if (!_hasTriggered || _triggerTime < 0f) return;

            if (Time.realtimeSinceStartup - _triggerTime >= delayBeforePopup)
            {
                TriggerWin();
                _triggerTime = float.NegativeInfinity; // prevent re-trigger
            }
        }

        private void TriggerWin()
        {
            // Award coins from level config.
            AwardLevelCoins();

            // Transition via GameStateMachine (which shows the popup).
            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.TransitionTo(GameState.Win);
            }
            else
            {
                // Fallback: show popup directly if no FSM present.
                if (PopupManager.Instance != null)
                    PopupManager.Instance.Show(PopupId.Win);
                else
                    Debug.LogError("[WinTrigger] No GameStateMachine or PopupManager found.");
            }

            if (logTrigger)
                Debug.Log("[WinTrigger] Win triggered.");
        }

        /// <summary>
        /// Award coins defined in the level's GridMapDataSO.
        /// </summary>
        private void AwardLevelCoins()
        {
            var levelLoader = FindObjectOfType<LevelLoader>();
            if (levelLoader != null && levelLoader.CurrentConfig != null)
            {
                int reward = levelLoader.CurrentConfig.CoinReward;
                if (reward > 0 && SaveManager.Instance != null)
                {
                    SaveManager.Instance.AddCoins(reward);
                    Debug.Log($"[WinTrigger] Awarded {reward} coins.");
                }
            }
        }

        /// <summary>
        /// Manually trigger the win (e.g. from a button or level-design shortcut).
        /// Safe to call even if already triggered.
        /// </summary>
        public void TriggerNow()
        {
            if (_hasTriggered) return;
            _hasTriggered = true;
            TriggerWin();
        }

        /// <summary>
        /// Reset so the trigger can fire again (call when restarting the level).
        /// </summary>
        public void ResetTrigger()
        {
            _hasTriggered = false;
            _triggerTime = -1f;
        }
    }
}
