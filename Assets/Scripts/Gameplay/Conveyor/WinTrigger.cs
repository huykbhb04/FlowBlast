using System.Collections;
using FlowBlast.Core;
using FlowBlast.Gameplay.Grid;
using FlowBlast.Managers;
using FlowBlast.UI.Popup;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    [DisallowMultipleComponent]
    public class WinTrigger : MonoBehaviour
    {
        [Header("Options")]
        [SerializeField, Min(0f)] private float _delayBeforeWinPopup = 0.8f;

        [Header("Debug")]
        [SerializeField] private bool _logTrigger = true;

        private GridManager _gridManager;
        private BottomRayManager _bottomRayManager;
        private LevelLoader _levelLoader;
        private Coroutine _triggerCoroutine;
        private bool _hasTriggered;

        private void OnDisable()
        {
            UnsubscribeGridManager();
            UnsubscribeBottomRayManager();
        }

        public void Initialize(GridManager gridManager, BottomRayManager bottomRayManager, LevelLoader levelLoader)
        {
            if (_gridManager == gridManager && _bottomRayManager == bottomRayManager && _levelLoader == levelLoader)
            {
                return;
            }

            UnsubscribeGridManager();
            UnsubscribeBottomRayManager();
            _gridManager = gridManager;
            _bottomRayManager = bottomRayManager;
            _levelLoader = levelLoader;
            SubscribeGridManager();
            SubscribeBottomRayManager();
        }

        public void TriggerNow()
        {
            TryTriggerBoardCleared();
        }

        public void ResetTrigger()
        {
            _hasTriggered = false;
            StopTriggerCoroutine();
            TryTriggerBoardCleared();
        }

        private void SubscribeGridManager()
        {
            if (_gridManager == null)
            {
                return;
            }

            _gridManager.OnBoardStateChanged += HandleBoardStateChanged;
        }

        private void UnsubscribeGridManager()
        {
            if (_gridManager == null)
            {
                return;
            }

            _gridManager.OnBoardStateChanged -= HandleBoardStateChanged;
        }

        private void SubscribeBottomRayManager()
        {
            if (_bottomRayManager == null)
            {
                return;
            }

            _bottomRayManager.OnSlotStateChanged += HandleSlotStateChanged;
        }

        private void UnsubscribeBottomRayManager()
        {
            if (_bottomRayManager == null)
            {
                return;
            }

            _bottomRayManager.OnSlotStateChanged -= HandleSlotStateChanged;
        }

        private void HandleBoardStateChanged()
        {
            TryTriggerBoardCleared();
        }

        private void HandleSlotStateChanged()
        {
            TryTriggerBoardCleared();
        }

        private void TryTriggerBoardCleared()
        {
            if (_hasTriggered || _gridManager == null || _gridManager.HasAnyBoxOnGrid())
            {
                return;
            }

            if (_bottomRayManager != null && _bottomRayManager.HasOccupiedSlots)
            {
                return;
            }

            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.IsPlaying)
            {
                return;
            }

            _hasTriggered = true;
            StopTriggerCoroutine();
            _triggerCoroutine = StartCoroutine(TriggerBoardClearedAfterDelay());

            if (_logTrigger)
            {
                Debug.Log($"[WinTrigger] Board cleared — showing win popup in {_delayBeforeWinPopup:F2}s.");
            }
        }

        private IEnumerator TriggerBoardClearedAfterDelay()
        {
            if (_delayBeforeWinPopup > 0f)
            {
                yield return new WaitForSeconds(_delayBeforeWinPopup);
            }

            AwardLevelCoins();
            _triggerCoroutine = null;
            ShowWinPopup();

            if (_logTrigger)
            {
                Debug.Log("[WinTrigger] Level cleared — WinPopup shown.");
            }
        }

        private void StopTriggerCoroutine()
        {
            if (_triggerCoroutine == null)
            {
                return;
            }

            StopCoroutine(_triggerCoroutine);
            _triggerCoroutine = null;
        }

        private void AwardLevelCoins()
        {
            if (_levelLoader == null || _levelLoader.CurrentConfig == null)
            {
                return;
            }

            int reward = _levelLoader.CurrentConfig.CoinReward;
            if (reward <= 0 || SaveManager.Instance == null)
            {
                return;
            }

            SaveManager.Instance.AddCoins(reward);
            if (_logTrigger)
            {
                Debug.Log($"[WinTrigger] Awarded {reward} coins.");
            }
        }

        private void ShowWinPopup()
        {
            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.TransitionTo(GameState.Win);
                return;
            }

            if (PopupManager.Instance != null)
            {
                PopupManager.Instance.Show(PopupId.Win);
                return;
            }

            Debug.LogError("[WinTrigger] GameStateMachine and PopupManager are missing, cannot show WinPopup.");
        }
    }
}
