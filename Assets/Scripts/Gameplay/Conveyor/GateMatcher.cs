using System.Linq;
using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    public class GateMatcher : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private BottomRayManager _bottomRay;
        [SerializeField] private SplineConveyor _splineConveyor;

        private const float DEFAULT_PROGRESS_STEP = 5f;

        [Header("Debug")]
        [SerializeField] private bool _logEvents = true;

        private void Awake()
        {
            if (_bottomRay == null)
                _bottomRay = BottomRayManager.Instance;
            if (_splineConveyor == null) _splineConveyor = FindObjectOfType<SplineConveyor>();
        }

        /// <summary>
        /// Called by the top conveyor when a top block reaches the gate.
        /// </summary>
        /// <param name="topBlock">The top block GameObject that just arrived.</param>
        /// <param name="topColor">Color of the top block.</param>
        public void OnTopBlockReachedGate(GameObject topBlock, BoxColor topColor)
        {
            if (topBlock == null) return;
            if (_bottomRay == null)
            {
                Debug.LogWarning($"{nameof(GateMatcher)}: BottomRay not assigned, top block passes through.");
                return;
            }

            // Pick the slot to match against. Any occupied slot whose RequiredColor
            // matches the incoming top ball is fair game - we prefer the one with the
            // lowest current progress so empty slots are filled first when we start,
            // and partially-filled slots are finished first when we drain.
            // This is what makes the "1 box in = 1 ball out" rule feel natural: the
            // player taps any Red box -> the first Red ball to reach the gate goes
            // straight into it, no waiting.
            BoxSlot slot = _bottomRay.Slots
                .Where(s => s != null
                    && s.IsOccupied
                    && !s.IsCompleted
                    && s.GetContainer() != null
                    && s.GetContainer().RequiredColor == topColor)
                .OrderBy(s => s.GetContainer().Progress)
                .ThenBy(s => s.SlotIndex)
                .FirstOrDefault();

            if (_logEvents)
            {
                string slotInfo = slot != null
                    ? $"slot{slot.SlotIndex}={slot.GetContainer().RequiredColor}(p={slot.GetContainer().Progress:F0}%)"
                    : "NO_SLOT";
                var occupiedSlots = _bottomRay.Slots
                    .Where(s => s != null && s.IsOccupied && !s.IsCompleted && s.GetContainer() != null)
                    .Select(s => $"slot{s.SlotIndex}:{s.GetContainer().RequiredColor}")
                    .ToArray();
                //Debug.Log($"{nameof(GateMatcher)}: top ball {topColor} at gate, matched {slotInfo}. " +
                         // $"Occupied slots: [{string.Join(", ", occupiedSlots)}]");
            }

            // If every slot of this color has already been completed, dissolve the
            // block right here at the gate. This is how we drain the conveyor cleanly
            // (no mid-flight destroy, no skip frames).
            if (slot == null || _splineConveyor != null && _splineConveyor.IsColorDone(topColor))
            {
                if (_splineConveyor != null && _splineConveyor.IsColorDone(topColor))
                {
                    ConsumeTopBlockDirectly(topBlock, topColor, slot);
                    return;
                }

                // if (_logEvents)
                    //Debug.Log($"{nameof(GateMatcher)}: No occupied slot under gate, top {topColor} block continues.");
                ContinueTopBlock(topBlock);
                return;
            }

            BoxContainer container = slot.GetContainer();
            if (container == null)
            {
                ContinueTopBlock(topBlock);
                return;
            }

            if (container.RequiredColor == topColor)
            {
                if (_logEvents)
                    Debug.Log($"{nameof(GateMatcher)}: MATCH {topColor} -> slot {slot.SlotIndex} progress={container.Progress}");

                ConsumeTopBlockImmediately(topBlock);
                AddProgressAfterBlockConsumed(slot, container, topColor);
            }
            else
            {
                // MISS -> top block continues along the top conveyor.
                if (_logEvents)
                    Debug.Log($"{nameof(GateMatcher)}: MISS top={topColor} vs slot{slot.SlotIndex}={container.RequiredColor}, top block continues.");
                ContinueTopBlock(topBlock);
            }
        }

        private void AddProgressAfterBlockConsumed(BoxSlot slot, BoxContainer container, BoxColor topColor)
        {
            if (slot == null || container == null || slot.GetContainer() != container || container.IsCompleted)
            {
                return;
            }

            container.AddProgress(GetProgressStep());
            if (_logEvents)
            {
                Debug.Log($"{nameof(GateMatcher)}: CONSUMED {topColor} -> slot {slot.SlotIndex} progress={container.Progress}");
            }

            if (!container.IsCompleted)
            {
                return;
            }

            FlyBoxUpAndClearSlot(slot);

            if (AllSlotsOfColorCompleted(topColor))
            {
                MarkColorDone(topColor);
            }
        }

        private float GetProgressStep()
        {
            if (_splineConveyor != null)
            {
                return _splineConveyor.PerBallProgressPercent;
            }

            return DEFAULT_PROGRESS_STEP;
        }

        private void ConsumeTopBlockImmediately(GameObject topBlock)
        {
            if (topBlock == null)
            {
                return;
            }

            if (_splineConveyor != null)
            {
                _splineConveyor.ConsumeBlockImmediately(topBlock.transform);
                return;
            }

            Destroy(topBlock);
        }

        private void ContinueTopBlock(GameObject topBlock)
        {
            // Top conveyor already keeps moving the block. Nothing to do here.
            // Hook left in case we want a future custom path (e.g. send to a recycle bin).
        }

        /// <summary>
        /// Consume a top block at the gate without filling any slot - used when the
        /// color's slots are all full and we just want to dissolve the block cleanly.
        /// </summary>
        private void ConsumeTopBlockDirectly(GameObject topBlock, BoxColor topColor, BoxSlot slot)
        {
            if (topBlock == null) return;

            if (_logEvents)
                Debug.Log($"{nameof(GateMatcher)}: color {topColor} is DONE - consuming incoming block at gate.");

            ConsumeTopBlockImmediately(topBlock);
        }

        /// <summary>
        /// True if every slot whose RequiredColor == color has been fully completed
        /// (progress >= 100) or is currently empty (player hasn't placed a box yet -
        /// we treat those as "not required anymore" so they don't block the cleanup).
        /// Returns true only when no still-running slot of that color remains.
        /// </summary>
        private bool AllSlotsOfColorCompleted(BoxColor color)
        {
            if (_bottomRay == null) return false;
            var slots = _bottomRay.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                var s = slots[i];
                if (s == null) continue;
                var c = s.GetContainer();
                if (c == null) continue;
                if (c.RequiredColor != color) continue;
                if (!s.IsCompleted && s.IsOccupied) return false;
            }
            return true;
        }

        /// <summary>
        /// Tell the top SplineConveyor that every slot of the given color is filled.
        /// The conveyor will then dissolve any incoming block of that color at the gate,
        /// instead of trying to match it against a (now-nonexistent) slot.
        /// </summary>
        private void MarkColorDone(BoxColor color)
        {
            var conveyors = FindObjectsOfType<SplineConveyor>();
            for (int i = 0; i < conveyors.Length; i++)
            {
                if (conveyors[i] != null) conveyors[i].MarkColorDone(color);
            }
            if (_splineConveyor != null) _splineConveyor.MarkColorDone(color);

            // Fire public event so listeners (HUD, WinPopup, etc.) can react.
            OnColorCompleted?.Invoke(color);
        }

        /// <summary>
        /// Raised every time a color's balls are fully consumed and the conveyor
        /// enters drain mode for that color.
        /// </summary>
        public static event System.Action<BoxColor> OnColorCompleted;

        private void FlyBoxUpAndClearSlot(BoxSlot slot)
        {
            if (slot.CurrentBox == null)
            {
                _bottomRay.OnSlotCompleted(slot);
                return;
            }

            if (_logEvents)
                Debug.Log($"{nameof(GateMatcher)}: Slot {slot.SlotIndex} container at 100% -> fly up and clear.");

            GameObject boxObj = slot.CurrentBox;

            // Resolve BoxPool from BoxSlot (auto-find if not assigned).
            var boxPool = slot.BoxPool;

            // Add a fly-up animator on the fly if the prefab didn't ship with one.
            var animator = boxObj.GetComponent<BoxExitAnimator>();
            if (animator == null) animator = boxObj.AddComponent<BoxExitAnimator>();

            BoxTapMover mover = boxObj.GetComponent<BoxTapMover>();

            // Prefer pool-aware exit animation.
            if (boxPool != null && mover != null)
            {
                animator.PlayAndClearAndReturnToPool(
                    () => _bottomRay.OnSlotCompleted(slot),
                    boxPool,
                    mover);
            }
            else
            {
                animator.PlayAndClear(() => _bottomRay.OnSlotCompleted(slot));
            }
        }
    }
}