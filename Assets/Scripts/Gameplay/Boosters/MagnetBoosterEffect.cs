using System.Collections;
using System.Collections.Generic;
using FlowBlast.Gameplay.Conveyor;
using FlowBlast.Gameplay.Grid;
using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class MagnetBoosterEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SplineConveyor _topConveyor;
        [SerializeField] private BottomRayManager _bottomRayManager;

        [Header("Animation")]
        [SerializeField] private float _flyDuration = 0.35f;
        [SerializeField] private float _arcHeight = 1.2f;
        [SerializeField] private AnimationCurve _flyCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private readonly List<ConveyorColoredBlock> _matchingBlocks = new List<ConveyorColoredBlock>(32);
        private readonly HashSet<BoxSlot> _completedSlots = new HashSet<BoxSlot>();

        public bool HasAnyUsableTarget()
        {
            if (_bottomRayManager == null)
            {
                return false;
            }

            IReadOnlyList<BoxSlot> slots = _bottomRayManager.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                if (CanUseOnSlot(slots[i]))
                {
                    return true;
                }
            }

            return false;
        }

        public bool CanUseOnSlot(BoxSlot slot)
        {
            if (!TryGetValidContainer(slot, out BoxContainer container))
            {
                return false;
            }

            return HasMatchingBlocks(container.RequiredColor);
        }

        public bool Play(BoxSlot targetSlot)
        {
            if (!TryGetValidContainer(targetSlot, out BoxContainer container))
            {
                return false;
            }

            if (_topConveyor == null)
            {
                Debug.LogWarning("[MagnetBoosterEffect] Top conveyor is not assigned.");
                return false;
            }

            BoxColor targetColor = container.RequiredColor;
            _topConveyor.CollectActiveBlocksByColor(targetColor, _matchingBlocks);
            if (_matchingBlocks.Count == 0)
            {
                return false;
            }

            float progressStep = _topConveyor.PerBallProgressPercent;
            for (int i = 0; i < _matchingBlocks.Count; i++)
            {
                ConveyorColoredBlock block = _matchingBlocks[i];
                if (block == null || !_topConveyor.MarkBlockConsumedForMagnet(block))
                {
                    continue;
                }

                StartCoroutine(FlyBlockToSlot(block, targetSlot, progressStep));
            }

            return true;
        }

        private bool HasMatchingBlocks(BoxColor color)
        {
            if (_topConveyor == null)
            {
                return false;
            }

            _topConveyor.CollectActiveBlocksByColor(color, _matchingBlocks);
            return _matchingBlocks.Count > 0;
        }

        private bool TryGetValidContainer(BoxSlot slot, out BoxContainer container)
        {
            container = null;
            if (slot == null || !slot.IsOccupied || slot.IsCompleted)
            {
                return false;
            }

            container = slot.GetContainer();
            return container != null && !container.IsCompleted;
        }

        private IEnumerator FlyBlockToSlot(ConveyorColoredBlock block, BoxSlot targetSlot, float progressAmount)
        {
            if (block == null || targetSlot == null)
            {
                yield break;
            }

            Transform blockTransform = block.transform;
            Vector3 startPosition = blockTransform.position;
            Vector3 startScale = blockTransform.localScale;
            float elapsed = 0f;
            float addedProgress = 0f;

            while (elapsed < _flyDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / Mathf.Max(_flyDuration, 0.01f));
                float eased = _flyCurve != null ? _flyCurve.Evaluate(normalizedTime) : normalizedTime;
                Vector3 targetPosition = GetTargetPosition(targetSlot);
                Vector3 linearPosition = Vector3.Lerp(startPosition, targetPosition, eased);
                linearPosition.y += Mathf.Sin(eased * Mathf.PI) * _arcHeight;
                blockTransform.position = linearPosition;
                blockTransform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);

                AddProgressDelta(targetSlot, progressAmount, eased, ref addedProgress);
                yield return null;
            }

            AddProgressDelta(targetSlot, progressAmount, 1f, ref addedProgress);
            CompleteBlock(block);
            TryCompleteSlot(targetSlot);
        }

        private Vector3 GetTargetPosition(BoxSlot targetSlot)
        {
            if (targetSlot != null && targetSlot.CurrentBox != null)
            {
                return targetSlot.CurrentBox.transform.position;
            }

            return targetSlot != null ? targetSlot.GetIdlePosition() : Vector3.zero;
        }

        private void AddProgressDelta(BoxSlot targetSlot, float progressAmount, float normalizedProgress, ref float addedProgress)
        {
            BoxContainer container = targetSlot != null ? targetSlot.GetContainer() : null;
            if (container == null || container.IsCompleted)
            {
                return;
            }

            float targetProgress = progressAmount * normalizedProgress;
            float delta = Mathf.Max(0f, targetProgress - addedProgress);
            if (delta <= 0f)
            {
                return;
            }

            container.AddProgress(delta);
            addedProgress += delta;
        }

        private void CompleteBlock(ConveyorColoredBlock block)
        {
            if (block == null)
            {
                return;
            }

            BlockPool pool = _topConveyor != null ? _topConveyor.GetPoolForColor(block.Color) : null;
            if (pool != null)
            {
                pool.Despawn(block);
                return;
            }

            Destroy(block.gameObject);
        }

        private void TryCompleteSlot(BoxSlot targetSlot)
        {
            if (targetSlot == null || !targetSlot.IsCompleted || _completedSlots.Contains(targetSlot))
            {
                return;
            }

            _completedSlots.Add(targetSlot);
            StartCoroutine(CompleteSlotAsync(targetSlot));
        }

        private IEnumerator CompleteSlotAsync(BoxSlot targetSlot)
        {
            yield return null;

            if (targetSlot == null)
            {
                yield break;
            }

            BoxColor completedColor = targetSlot.GetContainer() != null
                ? targetSlot.GetContainer().RequiredColor
                : BoxColorUtility.DefaultColor;

            if (targetSlot.CurrentBox != null)
            {
                BoxExitAnimator animator = targetSlot.CurrentBox.GetComponent<BoxExitAnimator>();
                if (animator == null)
                {
                    animator = targetSlot.CurrentBox.AddComponent<BoxExitAnimator>();
                }

                BoxTapMover mover = targetSlot.CurrentBox.GetComponent<BoxTapMover>();
                animator.PlayAndClearAndReturnToPool(
                    () => ClearCompletedSlot(targetSlot, completedColor),
                    targetSlot.BoxPool,
                    mover);
                yield break;
            }

            ClearCompletedSlot(targetSlot, completedColor);
        }

        private void ClearCompletedSlot(BoxSlot targetSlot, BoxColor completedColor)
        {
            if (_bottomRayManager != null)
            {
                _bottomRayManager.OnSlotCompleted(targetSlot);
            }
            else
            {
                targetSlot.ClearSlot();
            }

            _completedSlots.Remove(targetSlot);

            if (_topConveyor != null && AreAllSlotsOfColorCompleted(completedColor))
            {
                _topConveyor.MarkColorDone(completedColor);
            }
        }

        private bool AreAllSlotsOfColorCompleted(BoxColor color)
        {
            if (_bottomRayManager == null)
            {
                return false;
            }

            IReadOnlyList<BoxSlot> slots = _bottomRayManager.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                BoxSlot slot = slots[i];
                if (slot == null)
                {
                    continue;
                }

                BoxContainer container = slot.GetContainer();
                if (container == null || container.RequiredColor != color)
                {
                    continue;
                }

                if (slot.IsOccupied && !slot.IsCompleted)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
