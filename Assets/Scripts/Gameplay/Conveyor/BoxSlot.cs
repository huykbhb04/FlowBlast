using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// One fixed slot on the bottom spline. Holds the Box GameObject that landed here
    /// and its dedicated BoxContainer (1 container / box).
    ///
    /// Lifecycle:
    ///   Empty -> player taps a box from the grid -> BottomRayManager calls AssignBox()
    ///   Occupied (box on spline, container tracking progress) -> GateMatcher feeds top blocks
    ///   Completed (container.IsCompleted == true) -> ray fly-up animation, ClearSlot()
    /// </summary>
    public class BoxSlot : MonoBehaviour
    {
        [Header("Slot Config")]
        public int SlotIndex;

        [Tooltip("Anchor point on the bottom spline where this slot's box should stand.")]
        public Transform AnchorPoint;

        [Tooltip("Optional: world position when the box is sitting idle at this slot. " +
                 "If null, falls back to AnchorPoint position.")]
        public Transform IdlePoint;

        [Header("State")]
        [SerializeField] private GameObject _currentBox;
        [SerializeField] private BoxContainer _container;

        public GameObject CurrentBox => _currentBox;
        public BoxContainer Container => _container;

        public bool IsEmpty => _currentBox == null;
        public bool IsOccupied => _currentBox != null;
        public bool IsCompleted => _container != null && _container.IsCompleted;

        public Vector3 GetIdlePosition()
        {
            if (IdlePoint != null) return IdlePoint.position;
            if (AnchorPoint != null) return AnchorPoint.position;
            return transform.position;
        }

        public void AssignBox(GameObject box, BoxColor color)
        {
            _currentBox = box;

            // Unsubscribe from the previous container (if any) so we don't double-count
            // a completed event from the previous box placed in this slot.
            if (_container != null)
            {
                _container.OnCompleted -= BoxSlot_HandleContainerCompleted;
            }

            if (_container == null) _container = new BoxContainer();
            _container.Reset(color);

            _container.OnCompleted += BoxSlot_HandleContainerCompleted;

            // Attach a progress label so the player sees how full this box is.
            // The label is hidden by default and only shows once a matching
            // top ball hits the gate (first progress > 0).
            if (box != null)
            {
                var display = box.GetComponent<BoxProgressDisplay>();
                if (display == null) display = box.AddComponent<BoxProgressDisplay>();
                display.Bind(_container);

                // Let HUD rebind its event handlers for this fresh container.
                var hud = GameProgressHUD.Instance;
                if (hud != null) hud.RebindContainer(_container);
            }
        }

        private void BoxSlot_HandleContainerCompleted(BoxContainer _)
        {
            var hud = GameProgressHUD.Instance;
            if (hud != null) hud.NotifyBoxCompleted();
            else Debug.Log($"[BoxSlot] Container completed (no HUD bound).");
        }

        private void OnDestroy()
        {
            // Best-effort cleanup of our event subscription.
            if (_container != null) _container.OnCompleted -= BoxSlot_HandleContainerCompleted;
        }

        public void ClearSlot()
        {
            if (_currentBox != null)
                Destroy(_currentBox);
            _currentBox = null;
            if (_container != null)
            {
                _container.Progress = 0f;
                _container.RequiredColor = BoxColor.Red;
            }
        }

        public BoxContainer GetContainer()
        {
            if (_container == null) _container = new BoxContainer();
            return _container;
        }
    }
}