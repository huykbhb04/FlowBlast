using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    /// <summary>
    /// Cell type in the grid
    /// </summary>
    public enum CellType
    {
        Empty,      // Trống, có thể di chuyển qua
        Box,        // Có hộp
        Wall,       // Tường cứng, không thể đi qua
        Exit        // Lối ra của grid
    }

    /// <summary>
    /// Color type for boxes
    /// </summary>
    public enum BoxColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange
    }

    /// <summary>
    /// Represents a single cell in the grid
    /// </summary>
    [Serializable]
    public class GridCell
    {
        public CellType Type;
        public BoxColor Color;
        public int Row;
        public int Col;
        public bool IsPathToExit;  // Có đường thông đến exit không
        public bool IsSelectable;  // Có thể chọn được (có đường đến exit và là Box)
        
        // For pathfinding
        [NonSerialized] public bool Visited;
        [NonSerialized] public GridCell Parent;
        [NonSerialized] public int Distance;

        public GridCell()
        {
            Type = CellType.Empty;
            Color = BoxColor.Red;
            IsPathToExit = false;
            IsSelectable = false;
        }

        public GridCell(int row, int col, CellType type = CellType.Empty)
        {
            Row = row;
            Col = col;
            Type = type;
            Color = BoxColor.Red;
            IsPathToExit = false;
            IsSelectable = false;
        }

        public bool HasBox() => Type == CellType.Box;
        public bool IsWall() => Type == CellType.Wall;
        public bool IsExit() => Type == CellType.Exit;
        public bool IsEmpty() => Type == CellType.Empty;

        /// <summary>
        /// Set by MarkSelectableByCeiling: this Box is fully surrounded by non-traversable cells
        /// (walls AND/OR other boxes; not exit) on all 4 sides. It cannot be tapped to leave,
        /// because no escape slot exists. Treat as trapped → ignore clicks.
        /// </summary>
        public bool IsTrapped;

        public Vector2Int Position => new Vector2Int(Row, Col);
    }

    /// <summary>
    /// Map data containing the grid
    /// </summary>
    [Serializable]
    public class GridMapData
    {
        public int Rows;
        public int Cols;
        public GridCell[,] Cells;
        
        // Color map for visualization
        private static readonly Dictionary<BoxColor, Color> ColorMap = new Dictionary<BoxColor, Color>
        {
            { BoxColor.Red, new Color(0.9f, 0.3f, 0.3f) },
            { BoxColor.Blue, new Color(0.3f, 0.5f, 0.9f) },
            { BoxColor.Green, new Color(0.3f, 0.8f, 0.4f) },
            { BoxColor.Yellow, new Color(0.95f, 0.9f, 0.3f) },
            { BoxColor.Purple, new Color(0.7f, 0.3f, 0.8f) },
            { BoxColor.Orange, new Color(0.95f, 0.6f, 0.2f) }
        };

        public GridMapData()
        {
            Rows = 0;
            Cols = 0;
            Cells = new GridCell[0, 0];
        }

        public GridMapData(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            Cells = new GridCell[rows, cols];
            
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Cells[r, c] = new GridCell(r, c, CellType.Empty);
                }
            }
        }

        public GridCell GetCell(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                return null;
            return Cells[row, col];
        }

        public GridCell GetCell(Vector2Int pos)
        {
            return GetCell(pos.x, pos.y);
        }

        public bool IsValidPosition(int row, int col)
        {
            return row >= 0 && row < Rows && col >= 0 && col < Cols;
        }

        public bool IsValidPosition(Vector2Int pos)
        {
            return IsValidPosition(pos.x, pos.y);
        }

        public List<GridCell> GetNeighbors(int row, int col)
        {
            List<GridCell> neighbors = new List<GridCell>();
            
            // 4-directional movement
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };
            
            for (int i = 0; i < 4; i++)
            {
                int nr = row + dr[i];
                int nc = col + dc[i];
                GridCell cell = GetCell(nr, nc);
                if (cell != null && !cell.IsWall())
                {
                    neighbors.Add(cell);
                }
            }
            
            return neighbors;
        }

        public List<GridCell> GetNeighbors(Vector2Int pos)
        {
            return GetNeighbors(pos.x, pos.y);
        }

        public void SetCell(int row, int col, CellType type, BoxColor color = BoxColor.Red)
        {
            GridCell cell = GetCell(row, col);
            if (cell != null)
            {
                cell.Type = type;
                cell.Color = color;
            }
        }

        public void SetWall(int row, int col)
        {
            SetCell(row, col, CellType.Wall);
        }

        public void SetBox(int row, int col, BoxColor color)
        {
            SetCell(row, col, CellType.Box, color);
        }

        public void SetExit(int row, int col)
        {
            SetCell(row, col, CellType.Exit);
        }

        /// <summary>
        /// Find the position of the single Exit cell in the grid.
        /// Returns (-1,-1) if none found.
        /// </summary>
        public Vector2Int GetExitPosition()
        {
            if (Cells == null) return new Vector2Int(-1, -1);
            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    GridCell cell = GetCell(r, c);
                    if (cell != null && cell.Type == CellType.Exit)
                        return new Vector2Int(r, c);
                }
            }
            return new Vector2Int(-1, -1);
        }

        public void RemoveBox(int row, int col)
        {
            SetCell(row, col, CellType.Empty);
        }

        public Color GetBoxColor(BoxColor color)
        {
            return ColorMap.ContainsKey(color) ? ColorMap[color] : Color.white;
        }

        /// <summary>
        /// Create a sample grid map for testing
        /// </summary>
        public static GridMapData CreateSampleMap()
        {
            // 8x8 grid sample
            GridMapData map = new GridMapData(8, 8);
            
            // Set walls
            map.SetWall(0, 2);
            map.SetWall(0, 5);
            map.SetWall(1, 2);
            map.SetWall(2, 0);
            map.SetWall(2, 4);
            map.SetWall(3, 4);
            map.SetWall(3, 6);
            map.SetWall(4, 1);
            map.SetWall(4, 2);
            map.SetWall(5, 4);
            map.SetWall(6, 3);
            map.SetWall(6, 6);
            map.SetWall(7, 1);
            
            // Set exit (bottom right area)
            map.SetExit(7, 7);
            
            // Set boxes with colors
            map.SetBox(0, 0, BoxColor.Red);
            map.SetBox(0, 1, BoxColor.Blue);
            map.SetBox(0, 3, BoxColor.Green);
            map.SetBox(0, 4, BoxColor.Yellow);
            map.SetBox(0, 6, BoxColor.Purple);
            map.SetBox(0, 7, BoxColor.Orange);
            
            map.SetBox(1, 0, BoxColor.Blue);
            map.SetBox(1, 1, BoxColor.Red);
            map.SetBox(1, 3, BoxColor.Green);
            map.SetBox(1, 4, BoxColor.Yellow);
            map.SetBox(1, 5, BoxColor.Red);
            map.SetBox(1, 6, BoxColor.Blue);
            map.SetBox(1, 7, BoxColor.Green);
            
            map.SetBox(2, 1, BoxColor.Yellow);
            map.SetBox(2, 2, BoxColor.Purple);
            map.SetBox(2, 3, BoxColor.Orange);
            map.SetBox(2, 5, BoxColor.Red);
            map.SetBox(2, 6, BoxColor.Blue);
            map.SetBox(2, 7, BoxColor.Green);
            
            map.SetBox(3, 0, BoxColor.Green);
            map.SetBox(3, 1, BoxColor.Blue);
            map.SetBox(3, 2, BoxColor.Red);
            map.SetBox(3, 3, BoxColor.Yellow);
            map.SetBox(3, 5, BoxColor.Purple);
            map.SetBox(3, 7, BoxColor.Orange);
            
            map.SetBox(4, 0, BoxColor.Orange);
            map.SetBox(4, 3, BoxColor.Blue);
            map.SetBox(4, 4, BoxColor.Green);
            map.SetBox(4, 5, BoxColor.Red);
            map.SetBox(4, 6, BoxColor.Yellow);
            map.SetBox(4, 7, BoxColor.Purple);
            
            map.SetBox(5, 0, BoxColor.Red);
            map.SetBox(5, 1, BoxColor.Green);
            map.SetBox(5, 2, BoxColor.Blue);
            map.SetBox(5, 3, BoxColor.Yellow);
            map.SetBox(5, 5, BoxColor.Orange);
            map.SetBox(5, 6, BoxColor.Red);
            map.SetBox(5, 7, BoxColor.Blue);
            
            map.SetBox(6, 0, BoxColor.Purple);
            map.SetBox(6, 1, BoxColor.Yellow);
            map.SetBox(6, 2, BoxColor.Orange);
            map.SetBox(6, 4, BoxColor.Green);
            map.SetBox(6, 5, BoxColor.Red);
            map.SetBox(6, 7, BoxColor.Blue);
            
            map.SetBox(7, 0, BoxColor.Blue);
            map.SetBox(7, 2, BoxColor.Green);
            map.SetBox(7, 3, BoxColor.Red);
            map.SetBox(7, 4, BoxColor.Yellow);
            map.SetBox(7, 5, BoxColor.Orange);
            map.SetBox(7, 6, BoxColor.Purple);
            
            return map;
        }
    }
}
