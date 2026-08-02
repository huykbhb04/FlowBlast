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

        [Header("Pooling")]
        [Tooltip("BoxPool to return boxes to when they complete. Auto-resolves if not assigned.")]
        [SerializeField] private BoxPool _boxPool;

        [Header("State")]
        [SerializeField] private GameObject _currentBox;
        [SerializeField] private BoxContainer _container;

        public GameObject CurrentBox => _currentBox;
        public BoxContainer Container => _container;

        public BoxPool BoxPool
        {
            get
            {
                if (_boxPool != null) return _boxPool;
                _boxPool = FindObjectOfType<BoxPool>();
                return _boxPool;
            }
        }

        private bool _hasRuntimeIdlePosition;
        private Vector3 _runtimeIdlePosition;
        private bool _shouldSnapCurrentBox;

        public bool IsEmpty => _currentBox == null;
        public bool IsOccupied => _currentBox != null;
        public bool IsCompleted => _container != null && _container.IsCompleted;
        public bool ShouldSnapCurrentBox => _shouldSnapCurrentBox;

        public Vector3 GetIdlePosition()
        {
            if (_hasRuntimeIdlePosition)
            {
                return _runtimeIdlePosition;
            }

            if (IdlePoint != null)
            {
                return IdlePoint.position;
            }

            if (AnchorPoint != null)
            {
                return AnchorPoint.position;
            }

            return transform.position;
        }

        public void SetRuntimeIdlePosition(Vector3 position)
        {
            _runtimeIdlePosition = position;
            _hasRuntimeIdlePosition = true;
        }

        public void ClearRuntimeIdlePosition()
        {
            _hasRuntimeIdlePosition = false;
        }

        public void SetBoxSnapEnabled(bool isEnabled)
        {
            _shouldSnapCurrentBox = isEnabled;
        }

        public void AssignBox(GameObject box, BoxColor color, BoxProgressDisplay progressDisplay)
        {
            _currentBox = box;
            _shouldSnapCurrentBox = false;

            // Unsubscribe from the previous container (if any) so we don't double-count
            // a completed event from the previous box placed in this slot.
            if (_container != null)
            {
                _container.OnCompleted -= BoxSlot_HandleContainerCompleted;
            }

            if (_container == null) _container = new BoxContainer();
            _container.Reset(color);

            _container.OnCompleted += BoxSlot_HandleContainerCompleted;

            if (box != null)
            {
                if (progressDisplay != null)
                {
                    progressDisplay.Bind(_container);
                }
                else
                {
                    Debug.LogWarning($"[BoxSlot] Box '{box.name}' is missing BoxProgressDisplay reference. Assign it on BoxTapMover in the box prefab.");
                }

                GameProgressHUD hud = GameProgressHUD.Instance;
                if (hud != null)
                {
                    hud.RebindContainer(_container);
                }
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

        /// <summary>
        /// Clear the slot and return the box to the BoxPool if pooling is configured.
        /// This is safe to call even if the box was already returned to the pool
        /// (e.g. when FlyBoxUpAndClearSlot already called PlayAndClearAndReturnToPool).
        /// </summary>
        public void ClearSlot()
        {
            if (_currentBox != null)
            {
                // If the box has already been returned to the pool and deactivated,
                // just null out the reference. Otherwise, return it to the pool
                // (or fall back to Destroy if no pool is configured).
                if (!_currentBox.activeSelf && BoxPool != null)
                {
                    // Box is already pooled; just clear the reference.
                }
                else
                {
                    var mover = _currentBox.GetComponent<BoxTapMover>();
                    if (mover != null && BoxPool != null)
                    {
                        BoxPool.Despawn(mover);
                    }
                    else
                    {
                        Destroy(_currentBox);
                    }
                }
                _currentBox = null;
                _shouldSnapCurrentBox = false;
            }
            if (_container != null)
            {
                _container.Progress = 0f;
                _container.RequiredColor = BoxColorUtility.DefaultColor;
            }
        }

        public BoxContainer GetContainer()
        {
            return _container;
        }
    }
}