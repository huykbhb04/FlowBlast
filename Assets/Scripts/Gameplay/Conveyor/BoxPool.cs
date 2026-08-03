using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Dedicated pool for boxes lifted from the grid and placed on the bottom ray.
    /// Boxes are editor-built prefabs that carry a BoxTapMover component; this pool
    /// recycles them instead of Instantiate/Destroy each time a box is tapped.
    ///
    /// Flow:
    ///   GridManager.OnBoxLifted()  -> BoxPool.Spawn()  -> BoxTapMover.Start() sets color/trapped
    ///   BoxExitAnimator.OnComplete -> BoxPool.Despawn() -> box deactivated, returned to queue
    ///
    /// Attach one BoxPool to the GridManager (or scene root) and wire it into BoxSlot.
    /// </summary>
    public class BoxPool : Common.ComponentPool<BoxTapMover>
    {
        /// <summary>
        /// Reset a BoxTapMover instance for reuse. Called by ComponentPool.Spawn() via OnSpawn.
        /// BoxTapMover.Start() already resolves the color from the grid map at runtime,
        /// so this method focuses on resetting movement state.
        /// </summary>
        public BoxTapMover ResetBox(BoxTapMover mover)
        {
            if (mover == null) return null;

            // BoxTapMover caches its own movement state in private fields.
            // The public Reset() method zeroes those out so the next tap works correctly.
            mover.Reset();

            return mover;
        }

        /// <summary>
        /// Called automatically by ComponentPool.Despawn() via OnDespawn.
        /// Stops any active animations before the box goes back into the queue.
        /// </summary>
        public void OnBoxDespawn(BoxTapMover mover)
        {
            if (mover == null) return;
            mover.StopAllCoroutines();
        }
    }
}
