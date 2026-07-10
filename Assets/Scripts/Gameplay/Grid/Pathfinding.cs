using System.Collections.Generic;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    /// <summary>
    /// BFS/Flood-fill pathfinding to find cells with path to exit
    /// </summary>
    public static class Pathfinding
    {
        /// <summary>
        /// Perform BFS from exit to find all cells that can reach the exit
        /// Updates IsPathToExit and IsSelectable for each cell.
        /// LEGACY - retained for compatibility. New gameplay uses MarkSelectableByCeiling().
        /// </summary>
        public static void CalculatePathsToExit(GridMapData map)
        {
            if (map == null || map.Cells == null)
                return;

            // Reset all cells
            ResetCells(map);

            // Get exit cell by scanning grid for the Exit type
            Vector2Int exitPos = map.GetExitPosition();
            if (exitPos.x < 0)
                return;

            GridCell exitCell = map.GetCell(exitPos);
            if (exitCell == null)
                return;

            // BFS from exit
            Queue<GridCell> queue = new Queue<GridCell>();
            exitCell.Visited = true;
            exitCell.Distance = 0;
            exitCell.IsPathToExit = true;
            queue.Enqueue(exitCell);

            while (queue.Count > 0)
            {
                GridCell current = queue.Dequeue();

                // Get neighbors (4-directional)
                List<GridCell> neighbors = map.GetNeighbors(current.Position);

                foreach (GridCell neighbor in neighbors)
                {
                    // Can only traverse empty cells or cells with boxes
                    // (boxes can be picked up)
                    if (!neighbor.Visited && !neighbor.IsWall())
                    {
                        neighbor.Visited = true;
                        neighbor.Distance = current.Distance + 1;
                        neighbor.Parent = current;
                        neighbor.IsPathToExit = true;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            // Update selectable status (boxes with path to exit)
            UpdateSelectableStatus(map);
        }

        /// <summary>
        /// Cách A: A box at (r,c) is selectable if and only if the cell directly above it
        /// (r-1, c) is Empty (or r == 0). Wall neighbour does not block the path upward.
        ///
        /// Examples:
        ///   [B] row 2, col 0; above (1,0) = Empty -> selectable
        ///   [W][B] row 2; Wall beside box does not matter -> still selectable if above is Empty
        ///   Column of boxes: only the top-most box is selectable (below it has another box above).
        /// </summary>
        public static void MarkSelectableByCeiling(GridMapData map)
        {
            if (map == null || map.Cells == null)
                return;

            // Reset selectable (keep IsPathToExit as legacy data, just clear flag used by gameplay).
            ResetSelectableAndPath(map);

            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.GetCell(r, c);
                    if (cell == null || cell.Type != CellType.Box)
                        continue;

                    // Row 0 = top of grid -> always selectable
                    if (r == 0)
                    {
                        cell.IsSelectable = true;
                        cell.IsPathToExit = true; // legacy visualisation
                        continue;
                    }

                    GridCell above = map.GetCell(r - 1, c);
                    if (above == null)
                        continue;

                    if (above.Type == CellType.Empty)
                    {
                        cell.IsSelectable = true;
                        cell.IsPathToExit = true;
                    }
                }
            }

            // After marking selectable, apply the "trapped" rule: a Box that has no
            // escapable side (all 4 neighbours are either out-of-bounds, Wall, or another
            // Box, AND the box is itself not adjacent to the Exit cell) is trapped and
            // cannot be tapped to leave.
            MarkTrappedBoxes(map);
        }

        /// <summary>
        /// Luật nhốt:
        ///   Một Box bị coi là IsTrapped khi nó không thể đi ra bất kỳ hướng nào.
        ///   Điều kiện:
        ///     - Mọi ô lân cận 4 hướng (lên, xuống, trái, phải) đều KHÔNG phải Empty
        ///       và KHÔNG phải Exit (tức là Wall hoặc Box, hoặc ngoài biên).
        ///     - Đồng thời bản thân Box này cũng không nằm cạnh Exit (kề Exit thì vẫn thoát được).
        ///   Khi IsTrapped = true, Box không thể tap (kể cả khi có selectable khác).
        /// </summary>
        public static void MarkTrappedBoxes(GridMapData map)
        {
            if (map == null || map.Cells == null) return;

            // Find adjacent Exit cells globally (a cell with an exit neighbour is free).
            HashSet<Vector2Int> exitAdjacents = new HashSet<Vector2Int>();
            Vector2Int exitPos = map.GetExitPosition();
            if (exitPos.x >= 0)
            {
                int[] dr = { -1, 1, 0, 0 };
                int[] dc = { 0, 0, -1, 1 };
                for (int i = 0; i < 4; i++)
                {
                    int nr = exitPos.x + dr[i];
                    int nc = exitPos.y + dc[i];
                    if (map.IsValidPosition(nr, nc))
                        exitAdjacents.Add(new Vector2Int(nr, nc));
                }
            }

            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.GetCell(r, c);
                    if (cell == null || cell.Type != CellType.Box)
                    {
                        if (cell != null) cell.IsTrapped = false;
                        continue;
                    }

                    // If this box is adjacent to Exit, never trapped.
                    if (exitAdjacents.Contains(cell.Position))
                    {
                        cell.IsTrapped = false;
                        continue;
                    }

                    bool hasEscape = false;
                    int[] dr2 = { -1, 1, 0, 0 };
                    int[] dc2 = { 0, 0, -1, 1 };
                    for (int i = 0; i < 4 && !hasEscape; i++)
                    {
                        int nr = r + dr2[i];
                        int nc = c + dc2[i];
                        GridCell neighbor = map.GetCell(nr, nc);
                        if (neighbor == null)
                        {
                            // Out-of-bounds -> an escape slot (box can leave the grid).
                            hasEscape = true;
                            continue;
                        }
                        if (neighbor.Type == CellType.Empty || neighbor.Type == CellType.Exit)
                            hasEscape = true;
                    }

                    cell.IsTrapped = !hasEscape;
                }
            }
        }

        /// <summary>
        /// Reset only the flags used by gameplay selection (IsSelectable, IsPathToExit, plus pathfinding scratch).
        /// </summary>
        private static void ResetSelectableAndPath(GridMapData map)
        {
            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.Cells[r, c];
                    cell.Visited = false;
                    cell.Parent = null;
                    cell.Distance = int.MaxValue;
                    cell.IsPathToExit = false;
                    cell.IsSelectable = false;
                    cell.IsTrapped = false;
                }
            }
        }

        /// <summary>
        /// Reset all cells for recalculation
        /// </summary>
        private static void ResetCells(GridMapData map)
        {
            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.Cells[r, c];
                    cell.Visited = false;
                    cell.Parent = null;
                    cell.Distance = int.MaxValue;
                    cell.IsPathToExit = false;
                    cell.IsSelectable = false;
                }
            }
        }

        /// <summary>
        /// Update which cells are selectable (box + path to exit)
        /// </summary>
        private static void UpdateSelectableStatus(GridMapData map)
        {
            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.Cells[r, c];
                    // Box is selectable if it has path to exit
                    cell.IsSelectable = cell.HasBox() && cell.IsPathToExit;
                }
            }
        }

        /// <summary>
        /// Check if a specific cell has path to exit
        /// </summary>
        public static bool HasPathToExit(GridMapData map, Vector2Int position)
        {
            GridCell cell = map.GetCell(position);
            return cell != null && cell.IsPathToExit;
        }

        /// <summary>
        /// Check if a specific cell has path to exit (by row/col)
        /// </summary>
        public static bool HasPathToExit(GridMapData map, int row, int col)
        {
            GridCell cell = map.GetCell(row, col);
            return cell != null && cell.IsPathToExit;
        }

        /// <summary>
        /// Get all selectable boxes (boxes with path to exit)
        /// </summary>
        public static List<GridCell> GetSelectableBoxes(GridMapData map)
        {
            List<GridCell> selectable = new List<GridCell>();

            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.Cells[r, c];
                    if (cell.IsSelectable)
                    {
                        selectable.Add(cell);
                    }
                }
            }

            return selectable;
        }

        /// <summary>
        /// Get path from a cell to exit (for visualization/debug)
        /// </summary>
        public static List<Vector2Int> GetPathToExit(GridMapData map, Vector2Int start)
        {
            GridCell cell = map.GetCell(start);
            if (cell == null || !cell.IsPathToExit)
                return null;

            List<Vector2Int> path = new List<Vector2Int>();
            GridCell current = cell;

            while (current != null)
            {
                path.Add(current.Position);
                current = current.Parent;
            }

            path.Reverse();
            return path;
        }

        /// <summary>
        /// Check if removing a box at position would affect paths
        /// Returns the affected cells that need recalculation
        /// </summary>
        public static bool WouldAffectPath(GridMapData map, Vector2Int position)
        {
            GridCell cell = map.GetCell(position);
            if (cell == null)
                return false;

            // Check if this box is in the path of other boxes to exit
            // Simple heuristic: check if removing it disconnects any other box
            List<GridCell> neighbors = map.GetNeighbors(position);
            
            foreach (var neighbor in neighbors)
            {
                if (neighbor.HasBox() && neighbor.IsPathToExit)
                {
                    // This neighbor might use this cell's position in its path
                    // For simplicity, we always recalculate when removing any box
                    return true;
                }
            }

            return cell.HasBox() && cell.IsPathToExit;
        }
    }
}
