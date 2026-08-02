using System.Linq;
using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Top block conveyor runs along the top spline. When a top block reaches
    /// the gate position, this matcher asks the bottom ray: "is there a Box
    /// in the active slot?"
    ///
    ///   - Match (top color == box.RequiredColor) -> BoxContainer.AddProgress(+ProgressStep),
    ///                                              top block is consumed (destroyed).
    ///   - Miss  (colors differ OR no box)        -> top block continues along the spline
    ///                                              (the conveyor keeps moving it).
    ///   - When a container becomes IsCompleted   -> fly the box up out of the scene,
    ///                                              clear its slot so a new box can land.
    /// </summary>
    public class GateMatcher : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private BottomRayManager _bottomRay;
        [SerializeField] private SplineConveyor _splineConveyor;

        [Header("Match Settings")]
        [Tooltip("Progress added per matching top ball. If <= 0, auto-derived from SplineConveyor.BlocksPerCluster (100 / blocksPerCluster). With 20 balls/color, each ball adds 5%.")]
        [SerializeField] private float _progressStep = 0f;

        [Header("Debug")]
        [SerializeField] private bool _logEvents = true;

        private void Awake()
        {
            if (_bottomRay == null)
                _bottomRay = BottomRayManager.Instance;
            if (_splineConveyor == null) _splineConveyor = FindObjectOfType<SplineConveyor>();

            // AUTO-DERIVE: if the Inspector didn't set _progressStep explicitly, take it
            // from the top SplineConveyor so "n balls = 100%" is consistent regardless
            // of how many balls the user spawned per color cluster.
            // With 80 total balls split across 4 colors -> 20 balls/color -> 5%/ball.
            // With 16 total balls split across 4 colors ->  4 balls/color -> 25%/ball.
            if (_progressStep <= 0f && _splineConveyor != null)
            {
                _progressStep = _splineConveyor.PerBallProgressPercent;
                Debug.Log($"{nameof(GateMatcher)}: _progressStep auto-set to {_progressStep:F2} (= 100/{_splineConveyor.BlocksPerCluster} balls/color) from SplineConveyor.");
            }
            else if (_progressStep <= 0f)
            {
                _progressStep = 25f;
                Debug.LogWarning($"{nameof(GateMatcher)}: _progressStep<=0 and no SplineConveyor found. Defaulting to 25.");
            }
            else
            {
                Debug.Log($"{nameof(GateMatcher)}: _progressStep={_progressStep:F2} (Inspector override).");
            }
        }

        /// <summary>
        /// Get the BlockPool for the given color from the SplineConveyor.
        /// Returns null if pooling is not configured.
        /// </summary>
        private BlockPool FindPoolForColor(BoxColor color)
        {
            return _splineConveyor != null ? _splineConveyor.GetPoolForColor(color) : null;
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
                // MATCH -> pour into container, top block consumed (with dissolve effect).
                container.AddProgress(_progressStep);
                if (_logEvents)
                    Debug.Log($"{nameof(GateMatcher)}: MATCH {topColor} -> slot {slot.SlotIndex} progress={container.Progress}");

                // Stop the top conveyor from advancing this block further while we dissolve it.
                var handle = topBlock.GetComponent<BlockHandle>();
                if (handle != null) handle.MarkConsumed();

                var colored = topBlock.GetComponent<ConveyorColoredBlock>();
                var pool = FindPoolForColor(topColor);
                var dissolve = topBlock.GetComponent<BlockDissolveEffect>();
                if (dissolve == null) dissolve = topBlock.AddComponent<BlockDissolveEffect>();

                // Prefer pool-aware return; fall back to plain Destroy if no pool configured.
                if (pool != null && colored != null)
                    dissolve.PlayAndReturnToPool(pool, colored, null);
                else
                    dissolve.PlayAndDestroy();

                // If this hit completed the container -> fly box up and free slot.
                if (container.IsCompleted)
                {
                    FlyBoxUpAndClearSlot(slot);

                    // After a slot finishes, check if EVERY slot of this color is now done.
                    // If yes, flip the conveyor into "drain mode" so any incoming block of
                    // that color is dissolved at the gate - no more MID matches possible.
                    if (AllSlotsOfColorCompleted(topColor))
                    {
                        MarkColorDone(topColor);
                    }
                }
            }
            else
            {
                // MISS -> top block continues along the top conveyor.
                if (_logEvents)
                    Debug.Log($"{nameof(GateMatcher)}: MISS top={topColor} vs slot{slot.SlotIndex}={container.RequiredColor}, top block continues.");
                ContinueTopBlock(topBlock);
            }
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
                Debug.Log($"{nameof(GateMatcher)}: color {topColor} is DONE - dissolving incoming block at gate.");

            var handle = topBlock.GetComponent<BlockHandle>();
            if (handle != null) handle.MarkConsumed();

            var colored = topBlock.GetComponent<ConveyorColoredBlock>();
            var pool = FindPoolForColor(topColor);
            var dissolve = topBlock.GetComponent<BlockDissolveEffect>();
            if (dissolve == null) dissolve = topBlock.AddComponent<BlockDissolveEffect>();

            if (pool != null && colored != null)
                dissolve.PlayAndReturnToPool(pool, colored, null);
            else
                dissolve.PlayAndDestroy();
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

            // Add BoxTapMover if missing (needed for pool reset state).
            var mover = boxObj.GetComponent<BoxTapMover>();
            if (mover == null) mover = boxObj.AddComponent<BoxTapMover>();

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