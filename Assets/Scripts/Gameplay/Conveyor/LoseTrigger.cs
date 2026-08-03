using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FlowBlast.Core;

namespace FlowBlast.Gameplay.Conveyor
{
    [DisallowMultipleComponent]
    public class LoseTrigger : MonoBehaviour
    {
        private const int REQUIRED_BOTTOM_BOX_COUNT = 4;

        [Header("References")]
        [SerializeField] private BottomRayManager _bottomRay;
        [SerializeField] private SplineConveyor _topConveyor;

        [Header("Options")]
        [SerializeField, Min(0f)] private float _delayBeforePopup = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _logTrigger = true;

        private Coroutine _triggerCoroutine;
        private bool _hasTriggered;

        private void OnDisable()
        {
            UnsubscribeBottomRay();
            UnsubscribeTopConveyor();
            StopTriggerCoroutine();
        }

        public void Initialize(BottomRayManager bottomRay, SplineConveyor topConveyor)
        {
            if (_bottomRay == bottomRay && _topConveyor == topConveyor)
            {
                return;
            }

            UnsubscribeBottomRay();
            UnsubscribeTopConveyor();
            _bottomRay = bottomRay;
            _topConveyor = topConveyor;
            SubscribeBottomRay();
            SubscribeTopConveyor();
        }

        public void ResetTrigger()
        {
            _hasTriggered = false;
            StopTriggerCoroutine();
            EvaluateLoseCondition();
        }

        private void SubscribeBottomRay()
        {
            if (_bottomRay == null)
            {
                return;
            }

            _bottomRay.OnSlotStateChanged += HandleGameplayStateChanged;
        }

        private void UnsubscribeBottomRay()
        {
            if (_bottomRay == null)
            {
                return;
            }

            _bottomRay.OnSlotStateChanged -= HandleGameplayStateChanged;
        }

        private void SubscribeTopConveyor()
        {
            if (_topConveyor == null)
            {
                return;
            }

            _topConveyor.OnBlockStateChanged += HandleGameplayStateChanged;
        }

        private void UnsubscribeTopConveyor()
        {
            if (_topConveyor == null)
            {
                return;
            }

            _topConveyor.OnBlockStateChanged -= HandleGameplayStateChanged;
        }

        private void HandleGameplayStateChanged()
        {
            EvaluateLoseCondition();
        }

        private void EvaluateLoseCondition()
        {
            if (_hasTriggered)
            {
                return;
            }

            if (GameStateMachine.Instance != null && !GameStateMachine.Instance.IsPlaying)
            {
                StopTriggerCoroutine();
                return;
            }

            if (!IsLoseConditionMet())
            {
                StopTriggerCoroutine();
                return;
            }

            StartTriggerCoroutine();
        }

        private bool IsLoseConditionMet()
        {
            if (_bottomRay == null || _topConveyor == null)
            {
                return false;
            }

            if (_bottomRay.OccupiedCount != REQUIRED_BOTTOM_BOX_COUNT)
            {
                return false;
            }

            return !HasAnyMatchingTopBlockForBottomSlots();
        }

        private bool HasAnyMatchingTopBlockForBottomSlots()
        {
            IReadOnlyList<BoxSlot> slots = _bottomRay.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                BoxSlot slot = slots[i];
                if (slot == null || !slot.IsOccupied || slot.IsCompleted)
                {
                    continue;
                }

                BoxContainer container = slot.GetContainer();
                if (container == null)
                {
                    continue;
                }

                if (_topConveyor.HasActiveBlockWithColor(container.RequiredColor))
                {
                    return true;
                }
            }

            return false;
        }

        private void StartTriggerCoroutine()
        {
            if (_triggerCoroutine != null)
            {
                return;
            }

            _triggerCoroutine = StartCoroutine(TriggerLoseAfterDelay());
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

        private IEnumerator TriggerLoseAfterDelay()
        {
            if (_logTrigger)
            {
                Debug.Log($"[LoseTrigger] Lose condition detected — waiting {_delayBeforePopup:F2}s before popup.");
            }

            if (_delayBeforePopup > 0f)
            {
                yield return new WaitForSeconds(_delayBeforePopup);
            }

            _triggerCoroutine = null;
            if (!IsLoseConditionMet())
            {
                yield break;
            }

            _hasTriggered = true;
            TriggerLose();
        }

        private void TriggerLose()
        {
            if (_logTrigger)
            {
                Debug.Log("[LoseTrigger] Lose triggered!");
            }

            if (GameStateMachine.Instance != null)
            {
                GameStateMachine.Instance.TransitionTo(GameState.Lose);
            }
            else
            {
                Debug.LogWarning("[LoseTrigger] GameStateMachine.Instance is null.");
            }
        }
    }
}
