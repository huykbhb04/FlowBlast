using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FlowBlast.Gameplay.Grid
{
    [Serializable]
    public struct CellDataEntry
    {
        public CellType Type;
        public BoxColor Color;

        public CellDataEntry(CellType type, BoxColor color)
        {
            Type = type;
            Color = color;
        }

        public static CellDataEntry Empty
        {
            get { return new CellDataEntry(CellType.Empty, BoxColorUtility.DefaultColor); }
        }
    }

    [CreateAssetMenu(fileName = "NewGridMap", menuName = "FlowBlast/Grid Map Data", order = 1)]
    public class GridMapDataSO : ScriptableObject
    {
        [Header("Grid Dimensions")]
        [Min(2)] public int Rows = 8;
        [Min(2)] public int Cols = 8;

        [Header("Cell Data")]
        public List<CellDataEntry> Cells = new List<CellDataEntry>();

        [Header("Map Info")]
        public string MapName = "New Map";
        [Range(1, 5)] public int Difficulty = 1;

        [Header("Level Progression")]
        [Tooltip("Zero-based index of this level in the level sequence.")]
        public int LevelIndex = 0;

        [Tooltip("Coins awarded when the player completes this level.")]
        [Min(0)] public int CoinReward = 50;

        [Tooltip("Completed boxes needed for 1 star.")]
        [Min(1)] public int StarThreshold1 = 1;
        [Tooltip("Completed boxes needed for 2 stars.")]
        [Min(1)] public int StarThreshold2 = 3;
        [Tooltip("Completed boxes needed for 3 stars.")]
        [Min(1)] public int StarThreshold3 = 5;

        [Header("Box Visual")]
        public BoxVisualPaletteSO VisualPalette;

        [Header("Box Colors")]
        public List<BoxColor> AvailableColors = BoxColorUtility.CreateDefaultPalette();

        [Header("Scene Prefabs")]
        public GameObject BoxPrefab;
        public GameObject WallPrefab;

        [Header("Board Build Settings")]
        public float CellSize = 1f;
        public float CellSpacing = 1f;
        public Vector3 BoardOrigin = Vector3.zero;

        [Header("Conveyor")]
        [Min(0.1f)] public float BlockSpeed = 3f;
        [Min(1)] public int BlocksPerCluster = 20;

        [Header("Slot & Target")]
        [Range(1, 4)] public int SlotCount = 4;
        public float SlotCapacity = 100f;

        [Header("Block Sequence")]
        public bool DeriveBlockSequenceFromGrid = true;
        public List<BoxColor> BlockSequenceOverride;

        [Header("Target")]
        public int TargetBoxCount = 0;

        public CellDataEntry GetCell(int row, int col)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                return CellDataEntry.Empty;

            int index = row * Cols + col;
            if (index < 0 || index >= Cells.Count)
                return CellDataEntry.Empty;

            return Cells[index];
        }

        public void SetCell(int row, int col, CellDataEntry data)
        {
            if (row < 0 || row >= Rows || col < 0 || col >= Cols)
                return;

            int index = row * Cols + col;
            if (index >= 0 && index < Cells.Count)
            {
                Cells[index] = data;
            }
        }

        public void SetCellType(int row, int col, CellType type)
        {
            SetCellType(row, col, type, BoxColorUtility.DefaultColor);
        }

        public void SetCellType(int row, int col, CellType type, BoxColor color)
        {
            SetCell(row, col, new CellDataEntry(type, color));
        }

        /// <summary>
        /// Resize the Cells list to match Rows*Cols, padding with Empty entries
        /// or trimming excess entries.
        /// </summary>
        public void EnsureCellListSize()
        {
            int expected = Rows * Cols;
            while (Cells.Count < expected)
                Cells.Add(CellDataEntry.Empty);
            while (Cells.Count > expected)
                Cells.RemoveAt(Cells.Count - 1);
        }

        private void OnValidate()
        {
            Rows = Mathf.Max(2, Rows);
            Cols = Mathf.Max(2, Cols);
            EnsureCellListSize();
            // EnsureExitExists() is intentionally disabled in the new gameplay flow:
            // boxes no longer "reach" an Exit. Existing Exit cells are preserved
            // (so old maps still load) but no new Exit is auto-generated.
        }

        /// <summary>
        /// Ensure exactly one Exit cell exists. If missing or duplicates found,
        /// place a new Exit at a random border cell (or reuse the first existing).
        /// DISABLED in the new gameplay flow (box-leave-upward). Kept as a no-op
        /// so existing maps that DO have an Exit still load and serialise correctly.
        /// </summary>
        public void EnsureExitExists()
        {
            // Intentionally a no-op. The new flow does not use Exit cells.
            // Method retained for backwards compatibility with older code that
            // still calls EnsureExitExists() from the editor.
            int expected = Rows * Cols;
            if (Cells.Count != expected) EnsureCellListSize();

            // If there are duplicates, collapse to the first one to keep data valid.
            int firstExit = -1;
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].Type == CellType.Exit)
                {
                    if (firstExit < 0) firstExit = i;
                    else Cells[i] = CellDataEntry.Empty;
                }
            }
            // Do NOT create a new Exit if none exists.
        }

        private int[] BuildBorderIndices()
        {
            int rows = Rows, cols = Cols;
            int perim = 2 * rows + 2 * (cols - 2);
            if (perim <= 0) return new int[0];
            int[] arr = new int[perim];
            int k = 0;
            for (int c = 0; c < cols; c++) { arr[k++] = c; }                  // top
            for (int r = 1; r < rows - 1; r++) { arr[k++] = r * cols + cols - 1; } // right
            if (rows > 1) for (int c = cols - 1; c >= 0; c--) { arr[k++] = (rows - 1) * cols + c; } // bottom
            if (cols > 1) for (int r = rows - 2; r >= 1; r--) { arr[k++] = r * cols; } // left
            return arr;
        }

        /// <summary>
        /// Re-place the Exit at a random border cell (used by Random Map).
        /// </summary>
        public void RepositionExit()
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].Type == CellType.Exit)
                    Cells[i] = CellDataEntry.Empty;
            }
            int[] border = BuildBorderIndices();
            int chosen = border.Length > 0 ? border[Random.Range(0, border.Length)] : Cells.Count - 1;
            Cells[chosen] = new CellDataEntry(CellType.Exit, BoxColorUtility.DefaultColor);
        }

        public void ClearGrid()
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                Cells[i] = CellDataEntry.Empty;
            }
        }

        public void FillGrid(CellType type)
        {
            FillGrid(type, BoxColorUtility.DefaultColor);
        }

        public void FillGrid(CellType type, BoxColor color)
        {
            for (int i = 0; i < Cells.Count; i++)
            {
                Cells[i] = new CellDataEntry(type, color);
            }
        }

        public GridMapData ToGridMapData()
        {
            GridMapData mapData = new GridMapData(Rows, Cols);

            for (int r = 0; r < Rows; r++)
            {
                for (int c = 0; c < Cols; c++)
                {
                    CellDataEntry cellData = GetCell(r, c);
                    mapData.SetCell(r, c, cellData.Type, cellData.Color);
                }
            }

            return mapData;
        }

        public int GetCountOfType(CellType type)
        {
            int count = 0;
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].Type == type)
                    count++;
            }
            return count;
        }

        public bool IsValidMap()
        {
            // New flow: a map is valid as long as it has at least one Box cell
            // and Rows/Cols are consistent with Cells list size.
            if (Rows < 2 || Cols < 2) return false;
            if (Cells.Count != Rows * Cols) return false;
            return GetCountOfType(CellType.Box) > 0;
        }

        public void GenerateRandomMap(int wallPercentMin, int wallPercentMax)
        {
            EnsureCellListSize();
            ClearGrid();
            // RepositionExit() intentionally NOT called: new flow has no Exit.
            // Just clear the grid, then place walls and boxes.

            int wallPercent = Random.Range(wallPercentMin, wallPercentMax + 1);
            int totalCells = Rows * Cols;
            int wallCount = Mathf.RoundToInt(totalCells * wallPercent / 100f);

            List<int> indices = new List<int>();
            for (int i = 0; i < totalCells; i++) indices.Add(i);
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = indices[i];
                indices[i] = indices[j];
                indices[j] = tmp;
            }

            int placed = 0;
            for (int i = 0; i < indices.Count && placed < wallCount; i++)
                Cells[indices[i]] = new CellDataEntry(CellType.Wall, BoxColorUtility.DefaultColor);

            // Place boxes on remaining empties
            List<BoxColor> palette = (AvailableColors != null && AvailableColors.Count > 0) ? AvailableColors : null;
            for (int i = 0; i < Cells.Count; i++)
            {
                if (Cells[i].Type == CellType.Empty)
                {
                    BoxColor c = palette != null
                        ? palette[Random.Range(0, palette.Count)]
                        : BoxColorUtility.DefaultColor;
                    Cells[i] = new CellDataEntry(CellType.Box, c);
                }
            }
        }

        public Dictionary<BoxColor, int> GetBoxCountByColor()
        {
            var counts = new Dictionary<BoxColor, int>();
            foreach (BoxColor c in System.Enum.GetValues(typeof(BoxColor)))
                counts[c] = 0;

            foreach (var cell in Cells)
            {
                if (cell.Type == CellType.Box)
                    counts[cell.Color]++;
            }
            return counts;
        }

        public List<BoxColor> BuildBlockSequence()
        {
            if (BlockSequenceOverride != null && BlockSequenceOverride.Count > 0)
                return new List<BoxColor>(BlockSequenceOverride);

            var counts = GetBoxCountByColor();
            var sequence = new List<BoxColor>();
            foreach (var kvp in counts)
            {
                for (int i = 0; i < kvp.Value; i++)
                    sequence.Add(kvp.Key);
            }
            return sequence;
        }

        public int GetTotalBoxCount()
        {
            int total = 0;
            foreach (var cell in Cells)
                if (cell.Type == CellType.Box) total++;
            return total;
        }
    }
}