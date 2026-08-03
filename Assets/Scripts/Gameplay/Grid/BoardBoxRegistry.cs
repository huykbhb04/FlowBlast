using System.Collections.Generic;
using FlowBlast.Gameplay.Conveyor;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    public sealed class BoardBoxRegistry
    {
        private readonly Dictionary<Vector2Int, BoxTapMover> _boxesByPosition = new Dictionary<Vector2Int, BoxTapMover>();

        public int Count => _boxesByPosition.Count;

        public void Clear()
        {
            _boxesByPosition.Clear();
        }

        public void Register(int row, int col, BoxTapMover mover)
        {
            if (mover == null)
            {
                return;
            }

            _boxesByPosition[new Vector2Int(row, col)] = mover;
        }

        public void Unregister(int row, int col)
        {
            _boxesByPosition.Remove(new Vector2Int(row, col));
        }

        public bool TryGetBox(int row, int col, out BoxTapMover mover)
        {
            return _boxesByPosition.TryGetValue(new Vector2Int(row, col), out mover);
        }

        public void ApplyColor(int row, int col, BoxColor color)
        {
            if (!_boxesByPosition.TryGetValue(new Vector2Int(row, col), out BoxTapMover mover) || mover == null)
            {
                return;
            }

            mover.ApplyBoxColor(color);
        }

        public void ApplyTrappedState(int row, int col, bool isTrapped)
        {
            if (!_boxesByPosition.TryGetValue(new Vector2Int(row, col), out BoxTapMover mover) || mover == null)
            {
                return;
            }

            mover.SetTrapped(isTrapped);
        }
    }
}
