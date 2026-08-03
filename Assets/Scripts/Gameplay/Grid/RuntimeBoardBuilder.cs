using System.Collections.Generic;
using FlowBlast.Gameplay.Boosters;
using FlowBlast.Gameplay.Conveyor;
using UnityEngine;

namespace FlowBlast.Gameplay.Grid
{
    public sealed class RuntimeBoardBuilder : MonoBehaviour, IBoardBoxRegistry
    {
        [Header("References")]
        [SerializeField] private GridManager _gridManager;
        [Header("Runtime Spawn Root")]
        [Tooltip("All board objects generated from GridMapDataSO will be spawned under this Transform.")]
        [SerializeField] private Transform _mapRoot;

        [Header("Placement")]
        [SerializeField] private bool _centerBoard = true;
        [SerializeField] private Vector3 _cellRotationEuler;

        private readonly List<GameObject> _spawnedObjects = new List<GameObject>();
        private readonly List<BoardBoxReference> _boardBoxes = new List<BoardBoxReference>();
        private readonly List<BoardBoxReference> _activeBoxesBuffer = new List<BoardBoxReference>();

        public Transform MapRoot => _mapRoot;
        public int ActiveBoxCount => GetActiveBoxCount();
        public IReadOnlyList<BoardBoxReference> ActiveBoxes => GetActiveBoxes();

        public void Build(GridMapDataSO config)
        {
            if (config == null)
            {
                Debug.LogError("[RuntimeBoardBuilder] Config is null. Board build cancelled.");
                return;
            }

            GridMapData gridMap = _gridManager != null ? _gridManager.GetGridMap() : null;
            if (gridMap == null)
            {
                Debug.LogError("[RuntimeBoardBuilder] GridMap is not loaded. Call GridManager.LoadMapFromSO before Build.");
                return;
            }

            if (!ValidateSpawnRoots(config))
            {
                return;
            }

            Clear();

            int boxCount = 0;
            int wallCount = 0;
            int exitCount = 0;
            int backgroundCount = 0;

            for (int row = 0; row < config.Rows; row++)
            {
                for (int col = 0; col < config.Cols; col++)
                {
                    GridCell runtimeCell = gridMap.GetCell(row, col);
                    CellDataEntry data = config.GetCell(row, col);
                    Vector3 localPosition = GetLocalPosition(config, row, col);

                    if (config.BuildBackground && TrySpawnCellObject(config.BackgroundPrefab, _mapRoot, $"Cell_{row}_{col}_Background", localPosition, out _))
                    {
                        backgroundCount++;
                    }

                    switch (data.Type)
                    {
                        case CellType.Box:
                            if (SpawnBox(config, runtimeCell, data, row, col, localPosition))
                            {
                                boxCount++;
                            }
                            break;

                        case CellType.Wall:
                            if (TrySpawnCellObject(config.WallPrefab, _mapRoot, $"Wall_{row}_{col}", localPosition, out _))
                            {
                                wallCount++;
                            }
                            break;

                        case CellType.Exit:
                            if (TrySpawnCellObject(config.ExitPrefab, _mapRoot, $"Exit_{row}_{col}", localPosition, out _))
                            {
                                exitCount++;
                            }
                            break;
                    }
                }
            }

            Debug.Log($"[RuntimeBoardBuilder] Built board '{config.MapName}' ({config.Rows}x{config.Cols}) - boxes={boxCount}, walls={wallCount}, exits={exitCount}, backgrounds={backgroundCount}.");
        }

        public void Clear()
        {
            for (int i = _spawnedObjects.Count - 1; i >= 0; i--)
            {
                GameObject spawnedObject = _spawnedObjects[i];
                if (spawnedObject != null)
                {
                    Destroy(spawnedObject);
                }
            }

            _spawnedObjects.Clear();
            _boardBoxes.Clear();
            _activeBoxesBuffer.Clear();
        }

        private int GetActiveBoxCount()
        {
            int count = 0;

            for (int i = 0; i < _boardBoxes.Count; i++)
            {
                if (_boardBoxes[i].IsValid)
                {
                    count++;
                }
            }

            return count;
        }

        private IReadOnlyList<BoardBoxReference> GetActiveBoxes()
        {
            _activeBoxesBuffer.Clear();

            for (int i = 0; i < _boardBoxes.Count; i++)
            {
                BoardBoxReference boxReference = _boardBoxes[i];
                if (boxReference.IsValid)
                {
                    _activeBoxesBuffer.Add(boxReference);
                }
            }

            return _activeBoxesBuffer;
        }

        private bool ValidateSpawnRoots(GridMapDataSO config)
        {
            if (_mapRoot == null && NeedsMapRoot(config))
            {
                Debug.LogError("[RuntimeBoardBuilder] Map Root is not assigned. Assign one Transform for all generated board objects in the scene.");
                return false;
            }

            return true;
        }

        private bool NeedsMapRoot(GridMapDataSO config)
        {
            bool hasBoxPrefab = config.BoxPrefab != null && config.GetCountOfType(CellType.Box) > 0;
            bool hasWallPrefab = config.WallPrefab != null && config.GetCountOfType(CellType.Wall) > 0;
            bool hasBackgroundPrefab = config.BuildBackground && config.BackgroundPrefab != null;
            bool hasExitPrefab = config.ExitPrefab != null && config.GetCountOfType(CellType.Exit) > 0;

            return hasBoxPrefab || hasWallPrefab || hasBackgroundPrefab || hasExitPrefab;
        }

        private bool SpawnBox(GridMapDataSO config, GridCell runtimeCell, CellDataEntry data, int row, int col, Vector3 localPosition)
        {
            GameObject prefab = config.BoxPrefab;
            if (!TrySpawnCellObject(prefab, _mapRoot, $"Box_{row}_{col}", localPosition, out GameObject boxObject))
            {
                return false;
            }

            BoxTapMover mover = boxObject.GetComponent<BoxTapMover>();
            if (mover == null)
            {
                Debug.LogError($"[RuntimeBoardBuilder] Box prefab '{prefab.name}' must include BoxTapMover.");
                _spawnedObjects.Remove(boxObject);
                Destroy(boxObject);
                return false;
            }

            BoxVisualView visualView = boxObject.GetComponent<BoxVisualView>();
            if (visualView != null)
            {
                visualView.SetPalette(config.VisualPalette);
                visualView.Apply(data.Color);
            }

            bool isTrapped = runtimeCell != null && runtimeCell.IsTrapped;
            mover.Initialize(
                _gridManager,
                _gridManager != null ? _gridManager.BottomSpline : null,
                _gridManager != null ? _gridManager.ReceivePoint : null,
                config.VisualPalette,
                data.Color,
                isTrapped);

            _boardBoxes.Add(new BoardBoxReference(row, col, mover));

            return true;
        }

        private bool TrySpawnCellObject(GameObject prefab, Transform parent, string objectName, Vector3 localPosition, out GameObject instance)
        {
            instance = null;
            if (prefab == null || parent == null)
            {
                return false;
            }

            instance = Instantiate(prefab, parent);
            instance.name = objectName;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(_cellRotationEuler);
            _spawnedObjects.Add(instance);
            return true;
        }

        private Vector3 GetLocalPosition(GridMapDataSO config, int row, int col)
        {
            float step = config.CellSize * config.CellSpacing;
            Vector3 offset = Vector3.zero;

            if (_centerBoard)
            {
                offset.x = -(config.Cols - 1) * step * 0.5f;
                offset.z = (config.Rows - 1) * step * 0.5f;
            }

            return config.BoardOrigin + offset + new Vector3(col * step, 0f, -row * step);
        }
    }
}
