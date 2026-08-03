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
        Pink,
        Green,
        Yellow,
        Purple,
        Red,
        Blue,
        Gray,
        Orange,
        White
    }

    public static class BoxColorUtility
    {
        public static BoxColor DefaultColor
        {
            get
            {
                BoxColor[] values = (BoxColor[])Enum.GetValues(typeof(BoxColor));
                return values.Length > 0 ? values[0] : (BoxColor)0;
            }
        }

        public static List<BoxColor> CreateDefaultPalette()
        {
            BoxColor[] values = (BoxColor[])Enum.GetValues(typeof(BoxColor));
            return new List<BoxColor>(values);
        }
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
            Color = BoxColorUtility.DefaultColor;
            IsPathToExit = false;
            IsSelectable = false;
        }

        public GridCell(int row, int col, CellType type = CellType.Empty)
        {
            Row = row;
            Col = col;
            Type = type;
            Color = BoxColorUtility.DefaultColor;
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

        public void SetCell(int row, int col, CellType type)
        {
            SetCell(row, col, type, BoxColorUtility.DefaultColor);
        }

        public void SetCell(int row, int col, CellType type, BoxColor color)
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
            
            List<BoxColor> palette = BoxColorUtility.CreateDefaultPalette();
            int colorIndex = 0;
            for (int r = 0; r < map.Rows; r++)
            {
                for (int c = 0; c < map.Cols; c++)
                {
                    GridCell cell = map.GetCell(r, c);
                    if (cell != null && cell.IsEmpty())
                    {
                        map.SetBox(r, c, palette[colorIndex % palette.Count]);
                        colorIndex++;
                    }
                }
            }
            
            return map;
        }
    }
}
