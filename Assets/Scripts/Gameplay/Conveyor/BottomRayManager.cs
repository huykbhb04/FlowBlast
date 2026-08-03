using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Splines;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Manages the bottom ray: 4 BoxSlot positions on the bottom spline.
    /// When a Box is tapped from the grid, the GridManager hands the box
    /// to this manager, which places it in the empty slot closest to the
    /// receive point.
    ///
    /// Provides:
    ///   - GetNearestEmptySlot(): index of the next slot to fill.
    ///   - IsFull(): true when no slot can accept a new box.
    ///   - OnSlotCompleted(): called when a BoxContainer hits 100%.
    /// </summary>
    public class BottomRayManager : MonoBehaviour
    {
        private const int MAX_BOX_COUNT = 4;

        public static BottomRayManager Instance;

        private readonly List<BoxSlot> _slots = new List<BoxSlot>(MAX_BOX_COUNT);

        [Header("Slot Layout")]
        [SerializeField] private SplineContainer _slotSpline;
        [SerializeField] private int _slotSplineIndex;
        [Range(0f, 1f)]
        [SerializeField] private float _slotStartT = 0.08f;
        [Range(0f, 1f)]
        [SerializeField] private float _slotEndT = 0.36f;
        [SerializeField] private Vector3 _slotPositionOffset = Vector3.zero;
        [SerializeField] private bool _autoEvenlySpaceSlots = true;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 2f;
        [SerializeField] private bool _loopSpline = true;

        [Header("Debug")]
        [SerializeField] private bool _logEvents = true;

        private float _slotMovementOffsetT;
        private float _slotSplineLength;

        public event Action OnSlotStateChanged;

        public IReadOnlyList<BoxSlot> Slots => _slots;

        public int OccupiedCount => _slots.Count(s => s != null && s.gameObject.activeInHierarchy && s.IsOccupied);
        public bool HasOccupiedSlots => OccupiedCount > 0;
        public bool IsFull => CountActiveSlots() > 0 && _slots
            .Where(s => s != null && s.gameObject.activeInHierarchy)
            .All(s => s.IsOccupied);

        private void Awake()
        {
            Instance = this;
            EnsureRuntimeSlots();
            ApplyEvenSlotSpacing();
        }

        private void OnValidate()
        {
            _slotEndT = Mathf.Max(_slotStartT, _slotEndT);
        }

        private void Update()
        {
            UpdateSlotMovement();
            SnapOccupiedBoxesToSlots();
        }

        /// <summary>
        /// Find the empty slot nearest to the receive point (slot index 0 by convention).
        /// Returns null if all slots are occupied.
        /// </summary>
        public BoxSlot GetNearestEmptySlot()
        {
            return _slots
                .Where(s => s != null && s.gameObject.activeInHierarchy && s.IsEmpty)
                .OrderBy(s => s.SlotIndex)
                .FirstOrDefault();
        }

        /// <summary>
        /// Place a Box GameObject into the slot nearest to the receive point.
        /// Returns the slot it was placed into, or null if no slot available.
        /// </summary>
        public BoxSlot PlaceBox(BoxTapMover boxMover, BoxColor color)
        {
            if (boxMover == null)
            {
                Debug.LogError($"{nameof(BottomRayManager)}: PlaceBox called with null box mover.");
                return null;
            }

            return PlaceBox(boxMover.gameObject, color, boxMover.ProgressDisplay);
        }

        public BoxSlot PlaceBox(GameObject box, BoxColor color)
        {
            return PlaceBox(box, color, null);
        }

        private BoxSlot PlaceBox(GameObject box, BoxColor color, BoxProgressDisplay progressDisplay)
        {
            if (box == null)
            {
                Debug.LogError($"{nameof(BottomRayManager)}: PlaceBox called with null box.");
                return null;
            }

            BoxSlot slot = GetNearestEmptySlot();
            if (slot == null)
            {
                Debug.Log($"[BottomRayManager] All {Slots.Count} slots are full, cannot place box.");
                return null;
            }

            slot.AssignBox(box, color, progressDisplay);
            OnSlotStateChanged?.Invoke();
            Debug.Log($"[BottomRayManager] Placed {color} box into slot {slot.SlotIndex}. " +
                      $"Now occupied: {OccupiedCount}/{Slots.Count}.");
            return slot;
        }

        /// <summary>
        /// Called by GateMatcher (or BoxContainer.OnCompleted) when a slot's container fills up.
        /// Marks the slot as cleared. The actual fly-up animation is the responsibility of
        /// the box itself (BoxExitAnimator) before calling this.
        /// </summary>
        public void OnSlotCompleted(BoxSlot slot)
        {
            if (slot == null) return;
            Debug.Log($"[BottomRayManager] Slot {slot.SlotIndex} completed, clearing. " +
                      $"Now occupied: {OccupiedCount}/{Slots.Count}.");
            slot.ClearSlot();
            OnSlotStateChanged?.Invoke();
        }

        /// <summary>
        /// Editor / runtime helper: get the world position a box should sit at in this slot.
        /// </summary>
        public Vector3 GetSlotWorldPosition(BoxSlot slot)
        {
            return slot != null ? slot.GetIdlePosition() : Vector3.zero;
        }

        /// <summary>
        /// Called by LevelLoader to configure slots from a level config SO.
        /// When slotCount differs from the currently active slots, rebuilds
        /// the slot list: enables/disables existing slots, or spawns new ones
        /// from a SlotPrefab if available and the list is too short.
        /// </summary>
        public void SetupSlots(int slotCount, float capacity)
        {
            _slotCapacity = capacity;

            if (slotCount <= 0)
            {
                Debug.LogWarning($"{name}: SetupSlots called with slotCount={slotCount}, ignoring.");
                return;
            }

            int activeSlotCount = Mathf.Min(slotCount, MAX_BOX_COUNT);
            if (slotCount > MAX_BOX_COUNT)
            {
                Debug.LogWarning($"{name}: slotCount={slotCount} exceeds max {MAX_BOX_COUNT}. Clamping to {MAX_BOX_COUNT}.");
            }

            EnsureRuntimeSlots();
            UpdateActiveSlots(activeSlotCount);
            ApplyEvenSlotSpacing();
            OnSlotStateChanged?.Invoke();

            Debug.Log($"{name}: SetupSlots — requested={slotCount}, active={activeSlotCount}, capacity={capacity}.");
        }

        public void SetSlotSpline(SplineContainer slotSpline, int splineIndex)
        {
            _slotSpline = slotSpline;
            _slotSplineIndex = Mathf.Max(0, splineIndex);
            _slotSplineLength = EstimateSlotSplineLength();
            ApplyEvenSlotSpacing();
        }

        public void SetMovement(float moveSpeed, bool loopSpline)
        {
            _moveSpeed = Mathf.Max(0f, moveSpeed);
            _loopSpline = loopSpline;
        }

        private void EnsureRuntimeSlots()
        {
            while (_slots.Count < MAX_BOX_COUNT)
            {
                int slotIndex = _slots.Count;
                GameObject slotObject = new GameObject($"BottomRaySlot_{slotIndex}");
                slotObject.transform.SetParent(transform, false);

                BoxSlot slot = slotObject.AddComponent<BoxSlot>();
                slot.SlotIndex = slotIndex;
                _slots.Add(slot);
            }

            for (int i = 0; i < _slots.Count; i++)
            {
                BoxSlot slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SlotIndex = i;
                slot.AnchorPoint = null;
                slot.IdlePoint = null;
            }
        }

        private void UpdateActiveSlots(int activeSlotCount)
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                BoxSlot slot = _slots[i];
                if (slot == null)
                {
                    continue;
                }

                slot.SlotIndex = i;
                slot.gameObject.SetActive(i < activeSlotCount);
            }
        }

        private void ApplyEvenSlotSpacing()
        {
            if (!_autoEvenlySpaceSlots || _slotSpline == null || _slots == null)
            {
                return;
            }

            int activeSlotCount = CountActiveSlots();
            if (activeSlotCount <= 0)
            {
                return;
            }

            float startT = Mathf.Clamp01(_slotStartT);
            float endT = Mathf.Clamp01(_slotEndT);
            if (endT < startT)
            {
                endT = startT;
            }

            int placedSlotIndex = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                BoxSlot slot = _slots[i];
                if (slot == null || !slot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                float slotSpacingT = (endT - startT) / MAX_BOX_COUNT;
                float baseT = startT + slotSpacingT * placedSlotIndex;
                float t = NormalizeSplineT(baseT + _slotMovementOffsetT);
                Vector3 position = _slotSpline.EvaluatePosition(_slotSplineIndex, t);
                position += _slotPositionOffset;

                slot.SetRuntimeIdlePosition(position);
                placedSlotIndex++;
            }
        }

        private void UpdateSlotMovement()
        {
            if (_slotSpline == null || _moveSpeed <= 0f)
            {
                return;
            }

            if (_slotSplineLength <= 0f)
            {
                _slotSplineLength = EstimateSlotSplineLength();
            }

            if (_slotSplineLength <= 0f)
            {
                return;
            }

            _slotMovementOffsetT += (_moveSpeed / _slotSplineLength) * Time.deltaTime;
            if (_loopSpline)
            {
                _slotMovementOffsetT = Mathf.Repeat(_slotMovementOffsetT, 1f);
            }
            else
            {
                _slotMovementOffsetT = Mathf.Clamp01(_slotMovementOffsetT);
            }

            ApplyEvenSlotSpacing();
        }

        private void SnapOccupiedBoxesToSlots()
        {
            for (int i = 0; i < _slots.Count; i++)
            {
                BoxSlot slot = _slots[i];
                if (slot == null
                    || !slot.gameObject.activeInHierarchy
                    || slot.CurrentBox == null
                    || !slot.ShouldSnapCurrentBox)
                {
                    continue;
                }

                slot.CurrentBox.transform.position = slot.GetIdlePosition();
            }
        }

        private float NormalizeSplineT(float t)
        {
            if (_loopSpline)
            {
                return Mathf.Repeat(t, 1f);
            }

            return Mathf.Clamp01(t);
        }

        private float EstimateSlotSplineLength()
        {
            if (_slotSpline == null)
            {
                return 0f;
            }

            const int SAMPLE_COUNT = 64;
            float length = 0f;
            Vector3 previousPosition = _slotSpline.EvaluatePosition(_slotSplineIndex, 0f);
            for (int i = 1; i <= SAMPLE_COUNT; i++)
            {
                float t = (float)i / SAMPLE_COUNT;
                Vector3 position = _slotSpline.EvaluatePosition(_slotSplineIndex, t);
                length += Vector3.Distance(previousPosition, position);
                previousPosition = position;
            }

            return length;
        }

        private int CountActiveSlots()
        {
            EnsureRuntimeSlots();

            int activeSlotCount = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                BoxSlot slot = _slots[i];
                if (slot != null && slot.gameObject.activeInHierarchy)
                {
                    activeSlotCount++;
                }
            }

            return activeSlotCount;
        }

        private float _slotCapacity = 100f;
    }
}