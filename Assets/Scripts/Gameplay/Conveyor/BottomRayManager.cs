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
        public BoxSlot PlaceBox(GameObject box, BoxColor color)
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

            slot.AssignBox(box, color);
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
    }
}