using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Stops the grid from accepting new box taps when all 4 slots are occupied.
    /// After refactor: BoxClickHandler consults IsBlocked directly before calling GridManager.OnBoxLifted.
    /// </summary>
    public class RayInputBlocker : MonoBehaviour
    {
        public static RayInputBlocker Instance;

        [SerializeField] private BottomRayManager _bottomRay;

        public bool IsBlocked
        {
            get
            {
                if (_bottomRay == null)
                    _bottomRay = BottomRayManager.Instance;
                return _bottomRay != null && _bottomRay.IsFull;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        /// <summary>
        /// Called by BottomRayManager whenever a slot becomes free.
        /// Currently just logs; future: could trigger a "go ahead" SFX/UI.
        /// </summary>
        public static void RefreshInputState()
        {
            if (Instance == null) return;
            if (Instance._bottomRay == null) return;
            Debug.Log($"{nameof(RayInputBlocker)}: refreshed. IsBlocked={Instance.IsBlocked}");
        }
    }
}