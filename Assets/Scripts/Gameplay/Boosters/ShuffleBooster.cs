using System.Collections.Generic;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class ShuffleBooster : IBooster
    {
        private readonly GridManager _gridManager;
        private readonly IShuffleRandomProvider _randomProvider;
        private readonly List<GridCell> _boxCells = new List<GridCell>(64);
        private readonly List<BoxColor> _colors = new List<BoxColor>(64);

        public ShuffleBooster(GridManager gridManager, IShuffleRandomProvider randomProvider)
        {
            _gridManager = gridManager;
            _randomProvider = randomProvider ?? new UnityShuffleRandomProvider();
        }

        public BoosterType Type => BoosterType.Shuffle;

        public bool CanUse()
        {
            GridMapData map = _gridManager != null ? _gridManager.GetGridMap() : null;
            return map != null && HasEnoughDistinctColors(map);
        }

        public bool Use()
        {
            GridMapData map = _gridManager != null ? _gridManager.GetGridMap() : null;
            if (map == null)
            {
                return false;
            }

            CollectBoxCells(map);
            if (_boxCells.Count <= 1)
            {
                return false;
            }

            CacheColors();
            if (!TryShuffleColors())
            {
                return false;
            }

            ApplyColors();
            _gridManager.RecalculateBoardState();
            _gridManager.SyncRegisteredBoxStates();
            return true;
        }

        private bool HasEnoughDistinctColors(GridMapData map)
        {
            bool hasFirstColor = false;
            BoxColor firstColor = BoxColorUtility.DefaultColor;

            for (int row = 0; row < map.Rows; row++)
            {
                for (int col = 0; col < map.Cols; col++)
                {
                    GridCell cell = map.GetCell(row, col);
                    if (cell == null || cell.Type != CellType.Box)
                    {
                        continue;
                    }

                    if (!hasFirstColor)
                    {
                        hasFirstColor = true;
                        firstColor = cell.Color;
                        continue;
                    }

                    if (cell.Color != firstColor)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void CollectBoxCells(GridMapData map)
        {
            _boxCells.Clear();
            for (int row = 0; row < map.Rows; row++)
            {
                for (int col = 0; col < map.Cols; col++)
                {
                    GridCell cell = map.GetCell(row, col);
                    if (cell != null && cell.Type == CellType.Box)
                    {
                        _boxCells.Add(cell);
                    }
                }
            }
        }

        private void CacheColors()
        {
            _colors.Clear();
            for (int i = 0; i < _boxCells.Count; i++)
            {
                _colors.Add(_boxCells[i].Color);
            }
        }

        private bool TryShuffleColors()
        {
            if (!HasMultipleDistinctColors())
            {
                return false;
            }

            const int MAX_ATTEMPTS = 8;
            for (int attempt = 0; attempt < MAX_ATTEMPTS; attempt++)
            {
                ShuffleColors();
                if (HasChangedOrder())
                {
                    return true;
                }
            }

            RotateToDifferentOrder();
            return HasChangedOrder();
        }

        private bool HasMultipleDistinctColors()
        {
            BoxColor firstColor = _colors[0];
            for (int i = 1; i < _colors.Count; i++)
            {
                if (_colors[i] != firstColor)
                {
                    return true;
                }
            }

            return false;
        }

        private void ShuffleColors()
        {
            for (int i = _colors.Count - 1; i > 0; i--)
            {
                int randomIndex = _randomProvider.Range(0, i + 1);
                BoxColor temp = _colors[i];
                _colors[i] = _colors[randomIndex];
                _colors[randomIndex] = temp;
            }
        }

        private bool HasChangedOrder()
        {
            for (int i = 0; i < _boxCells.Count; i++)
            {
                if (_boxCells[i].Color != _colors[i])
                {
                    return true;
                }
            }

            return false;
        }

        private void RotateToDifferentOrder()
        {
            BoxColor firstColor = _colors[0];
            for (int i = 1; i < _colors.Count; i++)
            {
                _colors[i - 1] = _colors[i];
            }

            _colors[_colors.Count - 1] = firstColor;
        }

        private void ApplyColors()
        {
            for (int i = 0; i < _boxCells.Count; i++)
            {
                GridCell cell = _boxCells[i];
                BoxColor color = _colors[i];
                cell.Color = color;
                _gridManager.ApplyRegisteredBoxColor(cell.Row, cell.Col, color);
            }
        }
    }
}
