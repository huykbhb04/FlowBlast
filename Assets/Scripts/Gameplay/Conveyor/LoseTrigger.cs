using UnityEngine;
using FlowBlast.Core;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Detects the lose condition and transitions to <see cref="GameState.Lose"/>.
    ///
    /// Lose = bottom belt is full (all slots occupied) AND
    ///        there are no more selectable boxes left on the grid.
    ///
    /// Attach to any persistent scene object (e.g. LevelManager root).
    /// Checks once per frame during <see cref="GameState.Playing"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoseTrigger : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("BottomRayManager to check slot occupancy. Auto-detected if null.")]
        [SerializeField] private BottomRayManager _bottomRay;

        [Tooltip("GridManager to check remaining selectable boxes. Auto-detected if null.")]
        [SerializeField] private GridManager _gridManager;

        [Header("Options")]
        [Tooltip("Delay (seconds) after condition is met before showing LosePopup.")]
        [SerializeField, Min(0f)] private float _delayBeforePopup = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _logTrigger = true;

        private bool _hasTriggered;
        private float _conditionMetTime = -1f;

        private void Awake()
        {
            if (_bottomRay == null)
                _bottomRay = FindObjectOfType<BottomRayManager>();

            if (_gridManager == null)
                _gridManager = FindObjectOfType<GridManager>();
        }

        private void OnEnable()
        {
            _hasTriggered = false;
            _conditionMetTime = -1f;
        }

        private void Update()
        {
            if (_hasTriggered) return;

            // Only check during gameplay.
            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.IsPlaying)
                return;

            if (_bottomRay == null || _gridManager == null) return;

            bool slotsFull = _bottomRay.IsFull;
            bool noMoves = !_gridManager.HasSelectableBoxes();

            if (slotsFull && noMoves)
            {
                if (_conditionMetTime < 0f)
                {
                    _conditionMetTime = Time.realtimeSinceStartup;
                    if (_logTrigger)
                        Debug.Log($"[LoseTrigger] Lose condition detected — waiting {_delayBeforePopup:F2}s before popup.");
                }

                if (Time.realtimeSinceStartup - _conditionMetTime >= _delayBeforePopup)
                {
                    _hasTriggered = true;
                    TriggerLose();
                }
            }
            else
            {
                // Condition no longer met (e.g. a slot freed up), reset timer.
                _conditionMetTime = -1f;
            }
        }

        private void TriggerLose()
        {
            if (_logTrigger)
                Debug.Log("[LoseTrigger] Lose triggered!");

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.TransitionTo(GameState.Lose);
            else
                Debug.LogWarning("[LoseTrigger] GameStateMachine.Instance is null.");
        }

        /// <summary>Reset so the trigger can fire again (call on level restart).</summary>
        public void ResetTrigger()
        {
            _hasTriggered = false;
            _conditionMetTime = -1f;
        }
    }
}
