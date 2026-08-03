using System.Collections.Generic;
using FlowBlast.Gameplay.Grid;
using UnityEngine;

namespace FlowBlast.Gameplay.Boosters
{
    public sealed class ShuffleBoxColorsBooster : IBooster
    {
        private readonly List<BoxColor> _colors = new List<BoxColor>(32);

        public BoosterId Id => BoosterId.Shuffle;

        public bool CanUse(BoosterContext context)
        {
            return context != null
                && context.GridManager != null
                && context.GridManager.GetGridMap() != null
                && context.BoardBoxRegistry != null
                && context.BoardBoxRegistry.ActiveBoxCount > 1;
        }

        public bool Use(BoosterContext context)
        {
            if (!CanUse(context))
            {
                return false;
            }

            GridMapData gridMap = context.GridManager.GetGridMap();
            IReadOnlyList<BoardBoxReference> activeBoxes = context.BoardBoxRegistry.ActiveBoxes;

            CollectColors(gridMap, activeBoxes);
            if (_colors.Count <= 1)
            {
                return false;
            }

            ShuffleColors(_colors);
            ApplyColors(gridMap, activeBoxes);
            return true;
        }

        private void CollectColors(GridMapData gridMap, IReadOnlyList<BoardBoxReference> activeBoxes)
        {
            _colors.Clear();

            for (int i = 0; i < activeBoxes.Count; i++)
            {
                BoardBoxReference boxReference = activeBoxes[i];
                GridCell cell = gridMap.GetCell(boxReference.Row, boxReference.Col);

                if (cell != null && cell.Type == CellType.Box)
                {
                    _colors.Add(cell.Color);
                }
            }
        }

        private void ApplyColors(GridMapData gridMap, IReadOnlyList<BoardBoxReference> activeBoxes)
        {
            int colorIndex = 0;

            for (int i = 0; i < activeBoxes.Count; i++)
            {
                BoardBoxReference boxReference = activeBoxes[i];
                GridCell cell = gridMap.GetCell(boxReference.Row, boxReference.Col);

                if (cell == null || cell.Type != CellType.Box)
                {
                    continue;
                }

                BoxColor newColor = _colors[colorIndex];
                colorIndex++;

                cell.Color = newColor;
                boxReference.Mover.ApplyBoxColor(newColor);
            }
        }

        private static void ShuffleColors(List<BoxColor> colors)
        {
            for (int i = colors.Count - 1; i > 0; i--)
            {
                int randomIndex = Random.Range(0, i + 1);
                BoxColor tempColor = colors[i];
                colors[i] = colors[randomIndex];
                colors[randomIndex] = tempColor;
            }
        }
    }
}
