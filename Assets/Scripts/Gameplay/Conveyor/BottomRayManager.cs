using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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
        public static BottomRayManager Instance;

        [Header("Slots (assign 4 in inspector)")]
        [SerializeField] private List<BoxSlot> _slots = new List<BoxSlot>(4);

        [Header("Debug")]
        [SerializeField] private bool _logEvents = true;

        public IReadOnlyList<BoxSlot> Slots => _slots;

        public int OccupiedCount => _slots.Count(s => s != null && s.IsOccupied);
        public bool IsFull => _slots.Count > 0 && _slots.All(s => s != null && s.IsOccupied);

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Find the empty slot nearest to the receive point (slot index 0 by convention).
        /// Returns null if all slots are occupied.
        /// </summary>
        public BoxSlot GetNearestEmptySlot()
        {
            return _slots
                .Where(s => s != null && s.IsEmpty)
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
            // Snap box to slot idle position immediately; BoxTapMover handles smooth flight separately.
            box.transform.position = slot.GetIdlePosition();
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

            // Ensure the list is large enough to hold slotCount entries.
            while (_slots.Count < slotCount)
            {
                _slots.Add(null);
            }

            // Deactivate any surplus slots beyond slotCount.
            for (int i = slotCount; i < _slots.Count; i++)
            {
                if (_slots[i] != null)
                    _slots[i].gameObject.SetActive(false);
            }

            Debug.Log($"{name}: SetupSlots — count={slotCount}, capacity={capacity}, active slots: {_slots.Count}");
        }

        private float _slotCapacity = 100f;
    }
}