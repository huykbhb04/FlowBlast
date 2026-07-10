using System;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    /// <summary>
    /// Lightweight singleton holding an event for "a box was tapped by the player".
    /// Editor-built boxes attach BoxClickHandler which forwards clicks here.
    /// BottomRayManager / RayInputBlocker / GridSetup subscribe in Awake.
    /// Replaces the previous GridManager.OnBoxClicked routing.
    /// </summary>
    public static class BoxClickBus
    {
        /// <summary>
        /// Fired by BoxClickHandler when a box collider receives a tap.
        /// Args: the box GameObject, the box's color (or default if unknown).
        /// </summary>
        public static event Action<GameObject, BoxColor> BoxTapped;
        /// <summary>
        /// Fired by BoxClickHandler with the originating grid cell when known.
        /// Subscribers can use this to react to grid coordinates (pathing, etc.).
        /// May be null when the tap is not a grid box.
        /// </summary>
        public static event Action<GridCell> GridCellTapped;

        public static void RaiseBoxTapped(GameObject box, BoxColor color)
        {
            if (box == null) return;
            BoxTapped?.Invoke(box, color);
        }

        public static void RaiseGridCellTapped(GridCell cell)
        {
            if (cell == null) return;
            GridCellTapped?.Invoke(cell);
        }
    }
}