using System.Collections.Generic;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    /// <summary>
    /// Day03 scene bootstrap. Finds/creates a GridManager and wires click handlers.
    /// After refactor: GridManager loads its own map in Awake; this script only
    /// attaches BoxClickHandler to editor-built boxes (Box_R_C children) so clicks
    /// get routed through BoxClickBus.
    /// </summary>
    public class GridSetup : MonoBehaviour
    {
        [Header("References (will be created/found)")]
        [SerializeField] private Transform receivePoint;

        [Header("Editor-built boxes")]
        [Tooltip("Root transform under which Box_R_C GameObjects live (usually GridSystem).")]
        [SerializeField] private Transform boxRoot;

        private GridManager gridManager;

        private void Start()
        {
            gridManager = FindObjectOfType<GridManager>();
            if (gridManager == null)
            {
                GameObject gridObj = new GameObject("GridManager");
                gridObj.transform.SetParent(transform);
                gridManager = gridObj.AddComponent<GridManager>();
            }

            string rowsXcols = gridManager.GetGridMap() != null
                ? gridManager.GetGridMap().Rows + "x" + gridManager.GetGridMap().Cols
                : "NULL";
            string rootName = boxRoot != null ? boxRoot.name : "<auto>";
            Debug.Log($"[GridSetup] Start: gridManager={(gridManager != null)}, gridMap={rowsXcols}, boxRoot={rootName}");

            AttachBoxClickHandlers();
            SubscribeClickBus();
        }

        private void AttachBoxClickHandlers()
        {
            if (boxRoot == null) boxRoot = gridManager != null ? gridManager.transform : transform;
            if (gridManager == null || gridManager.GetGridMap() == null) return;

            int attached = 0;
            foreach (var cell in IterateBoxCells(gridManager.GetGridMap()))
            {
                Transform t = FindChildRecursive(boxRoot, $"Box_{cell.Row}_{cell.Col}");
                if (t == null) continue;
                if (t.GetComponent<BoxClickHandler>() != null) continue;

                var handler = t.gameObject.AddComponent<BoxClickHandler>();
                handler.Initialize(gridManager, cell, cell.Color);
                attached++;
            }
            Debug.Log($"[GridSetup] Attached BoxClickHandler to {attached} editor-built boxes.");
        }

        private void SubscribeClickBus()
        {
            BoxClickBus.GridCellTapped += OnGridCellTapped;
        }

        private void OnDestroy()
        {
            BoxClickBus.GridCellTapped -= OnGridCellTapped;
        }

        private void OnGridCellTapped(GridCell cell)
        {
            if (cell == null) return;
            Debug.Log($"[GridSetup] Cell tapped at ({cell.Row}, {cell.Col}), Color: {cell.Color}");
        }

        private static IEnumerable<GridCell> IterateBoxCells(GridMapData map)
        {
            for (int r = 0; r < map.Rows; r++)
            for (int c = 0; c < map.Cols; c++)
            {
                var cell = map.GetCell(r, c);
                if (cell != null && cell.Type == CellType.Box) yield return cell;
            }
        }

        private static Transform FindChildRecursive(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindChildRecursive(root.GetChild(i), name);
                if (hit != null) return hit;
            }
            return null;
        }
    }
}