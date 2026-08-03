using System;
using System.Collections.Generic;
using FlowBlast.Gameplay.Boosters;
using FlowBlast.Gameplay.Conveyor;
using UnityEngine;
using UnityEngine.Splines;

namespace FlowBlast.Gameplay.Grid
{
    /// <summary>
    /// After refactor: GridManager owns the GridMapData (read-only mirror of the SO),
    /// and animates boxes that have been lifted off the grid (jump to receive point,
    /// then ride the bottom spline).
    ///
    /// Boxes are NO LONGER spawned here. Editor-built boxes are placed by
    /// GridMapDataSOEditor. Click routing goes through BoxClickBus instead of
    /// through OnBoxClicked here.
    ///
    /// Public surface kept small:
    ///   - GetGridMap():   read-only view used by Pathfinding / UI
    ///   - GetSelectableBoxes() / HasSelectableBoxes(): for UI hooks
    ///   - OnBoxLifted(cell, box):   called by BoxClickHandler when a tap succeeds;
    ///                               kicks off jump + spline animation and removes
    ///                               the cell from the grid map.
    /// </summary>
    public class GridManager : MonoBehaviour
    {
        [Header("Grid Map")]
        [SerializeField] private GridMapDataSO mapDataSO;
        [SerializeField] private bool loadMapOnAwake;

        [Header("Conveyor Animation")]
        [SerializeField] private Transform receivePoint;
        [SerializeField] private SplineContainer bottomSpline;
        [SerializeField] private int bottomSplineIndex = 0;
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool loopSpline = true;

        [Header("Boosters")]
        [SerializeField] private MagnetBoosterEffect _magnetBoosterEffect;

        private GridMapData gridMap;
        private readonly List<BoxInfo> movingBoxes = new List<BoxInfo>();
        private readonly BoardBoxRegistry _boxRegistry = new BoardBoxRegistry();
        private readonly Dictionary<BoosterType, IBooster> _boosters = new Dictionary<BoosterType, IBooster>();
        private bool _isHandBoosterActive;
        private bool _isMagnetBoosterActive;

        public event Action OnBoardStateChanged;

        private class BoxInfo
        {
            public GameObject Visual;
            public Vector3 StartPosition;
            public Vector3 TargetPosition;
            public float Progress;
            public float JumpDuration = 0.2f;
            public bool IsJumping;
            public bool IsOnSpline;
            public float SplineDistance;
            public float SplineLength;
        }

        private void Awake()
        {
            RegisterBoosters();

            if (loadMapOnAwake)
            {
                LoadMapFromSO();
            }
        }

        private void Update()
        {
            UpdateMovingBoxes();
        }

        private void RegisterBoosters()
        {
            _boosters.Clear();
            RegisterBooster(new ShuffleBooster(this, new UnityShuffleRandomProvider()));
            RegisterBooster(new HandBooster(this));
            RegisterBooster(new MagnetBooster(this));
        }

        private void RegisterBooster(IBooster booster)
        {
            if (booster == null)
            {
                return;
            }

            _boosters[booster.Type] = booster;
        }

        private void LoadMapFromSO()
        {
            _isHandBoosterActive = false;
            _isMagnetBoosterActive = false;
            _boxRegistry.Clear();
            gridMap = mapDataSO != null
                ? mapDataSO.ToGridMapData()
                : GridMapData.CreateSampleMap();
            RecalculateBoardState();

            int sel = Pathfinding.GetSelectableBoxes(gridMap).Count;
            int trapped = CountTrappedBoxes(gridMap);
            string mapName = mapDataSO != null ? mapDataSO.MapName : "<sample>";
            Debug.Log($"[GridManager] Loaded map '{mapName}' ({gridMap.Rows}x{gridMap.Cols}) " +
                      $"- {sel} selectable boxes, {trapped} trapped. Box animation ready.");
        }

        /// <summary>
        /// Public overload used by LevelLoader to load a specific level config.
        /// </summary>
        public void LoadMapFromSO(GridMapDataSO config)
        {
            mapDataSO = config;
            LoadMapFromSO();
        }

        private static int CountTrappedBoxes(GridMapData map)
        {
            int n = 0;
            for (int r = 0; r < map.Rows; r++)
                for (int c = 0; c < map.Cols; c++)
                {
                    var cell = map.GetCell(r, c);
                    if (cell != null && cell.IsTrapped) n++;
                }
            return n;
        }

        public void RemoveBoxFromGrid(int row, int col)
        {
            if (gridMap == null)
            {
                return;
            }

            _boxRegistry.Unregister(row, col);
            gridMap.RemoveBox(row, col);
            RecalculateBoardState();
            SyncRegisteredBoxStates();
        }

        /// <summary>
        /// Called by BoxClickHandler after the blocker check passed.
        /// Removes the cell from the grid map, recalculates paths, and animates the box.
        /// </summary>
        public void OnBoxLifted(GridCell cell, GameObject boxObj)
        {
            if (cell == null || gridMap == null) return;

            // Hand box to BottomRayManager (places it in nearest empty slot).
            var ray = FlowBlast.Gameplay.Conveyor.BottomRayManager.Instance;
            FlowBlast.Gameplay.Conveyor.BoxSlot assignedSlot = null;
            if (ray != null && boxObj != null)
            {
                assignedSlot = ray.PlaceBox(boxObj, cell.Color);
            }

            // Jump target = slot idle position if placed, else receivePoint fallback.
            Vector3 startPos = boxObj != null ? boxObj.transform.position : Vector3.zero;
            Vector3 targetPos;
            if (assignedSlot != null)
                targetPos = assignedSlot.GetIdlePosition();
            else if (receivePoint != null)
                targetPos = receivePoint.position;
            else
                targetPos = startPos + Vector3.left * 3f;

            if (boxObj != null)
            {
                movingBoxes.Add(new BoxInfo
                {
                    Visual = boxObj,
                    StartPosition = startPos,
                    TargetPosition = targetPos,
                    Progress = 0f,
                    IsJumping = true
                });
            }

            gridMap.RemoveBox(cell.Row, cell.Col);
            _boxRegistry.Unregister(cell.Row, cell.Col);
            RecalculateBoardState();
            SyncRegisteredBoxStates();
        }

        /// <summary>
        /// Animate boxes that are jumping onto the conveyor and sliding along the spline.
        /// </summary>
        private void UpdateMovingBoxes()
        {
            for (int i = movingBoxes.Count - 1; i >= 0; i--)
            {
                BoxInfo box = movingBoxes[i];
                if (box.Visual == null)
                {
                    movingBoxes.RemoveAt(i);
                    continue;
                }

                if (box.IsJumping)
                {
                    box.Progress += Time.deltaTime / box.JumpDuration;
                    if (box.Progress >= 1f)
                    {
                        box.Progress = 0f;
                        box.IsJumping = false;
                        box.IsOnSpline = true;
                        box.Visual.transform.position = box.TargetPosition;
                        box.SplineLength = 10f;
                        box.SplineDistance = 0f;
                    }
                    else
                    {
                        Vector3 pos = Vector3.Lerp(box.StartPosition, box.TargetPosition, box.Progress);
                        float height = Mathf.Sin(box.Progress * Mathf.PI) * 0.8f;
                        pos.y += height;
                        box.Visual.transform.position = pos;
                    }
                }
                else if (box.IsOnSpline && bottomSpline != null)
                {
                    box.SplineDistance += moveSpeed * Time.deltaTime;
                    if (loopSpline) box.SplineDistance %= box.SplineLength;
                    else box.SplineDistance = Mathf.Clamp(box.SplineDistance, 0f, box.SplineLength);

                    float t = box.SplineDistance / box.SplineLength;
                    Vector3 worldPos = bottomSpline.EvaluatePosition(bottomSplineIndex, t);
                    box.Visual.transform.position = worldPos;

                    Vector3 tangent = bottomSpline.EvaluateTangent(bottomSplineIndex, t);
                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                        box.Visual.transform.rotation = lookRot;
                    }
                }
            }
        }

        public void RegisterBoardBox(int row, int col, BoxTapMover mover)
        {
            _boxRegistry.Register(row, col, mover);
        }

        public void UnregisterBoardBox(int row, int col)
        {
            _boxRegistry.Unregister(row, col);
        }

        public void ApplyRegisteredBoxColor(int row, int col, BoxColor color)
        {
            _boxRegistry.ApplyColor(row, col, color);
        }

        public void RecalculateBoardState()
        {
            if (gridMap == null)
            {
                return;
            }

            Pathfinding.MarkSelectableByCeiling(gridMap);
            OnBoardStateChanged?.Invoke();
        }

        public void SyncRegisteredBoxStates()
        {
            if (gridMap == null)
            {
                return;
            }

            for (int row = 0; row < gridMap.Rows; row++)
            {
                for (int col = 0; col < gridMap.Cols; col++)
                {
                    GridCell cell = gridMap.GetCell(row, col);
                    if (cell != null && cell.Type == CellType.Box)
                    {
                        _boxRegistry.ApplyTrappedState(row, col, cell.IsTrapped);
                    }
                }
            }
        }

        public bool HasAnyBoxOnGrid()
        {
            if (gridMap == null)
            {
                return false;
            }

            for (int row = 0; row < gridMap.Rows; row++)
            {
                for (int col = 0; col < gridMap.Cols; col++)
                {
                    GridCell cell = gridMap.GetCell(row, col);
                    if (cell != null && cell.Type == CellType.Box)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public void ActivateHandBooster()
        {
            _isHandBoosterActive = true;
            OnBoardStateChanged?.Invoke();
        }

        public bool TryConsumeHandBoosterOverride()
        {
            if (!_isHandBoosterActive)
            {
                return false;
            }

            _isHandBoosterActive = false;
            OnBoardStateChanged?.Invoke();
            return true;
        }

        public bool IsHandBoosterActive => _isHandBoosterActive;

        public bool CanActivateMagnetBooster()
        {
            return !_isMagnetBoosterActive
                && _magnetBoosterEffect != null
                && _magnetBoosterEffect.HasAnyUsableTarget();
        }

        public void ActivateMagnetBooster()
        {
            _isMagnetBoosterActive = true;
            OnBoardStateChanged?.Invoke();
        }

        public bool TryUseMagnetBoosterOnBox(BoxTapMover boxMover)
        {
            if (!_isMagnetBoosterActive || boxMover == null || _magnetBoosterEffect == null)
            {
                return false;
            }

            if (!TryGetOccupiedSlot(boxMover.gameObject, out BoxSlot slot))
            {
                Debug.Log("[GridManager] Magnet Booster requires selecting a box already placed on the bottom ray.");
                return false;
            }

            if (!_magnetBoosterEffect.CanUseOnSlot(slot))
            {
                Debug.Log("[GridManager] Magnet Booster found no matching top blocks for the selected box.");
                return false;
            }

            bool played = _magnetBoosterEffect.Play(slot);
            if (played)
            {
                _isMagnetBoosterActive = false;
                OnBoardStateChanged?.Invoke();
            }

            return played;
        }

        private bool TryGetOccupiedSlot(GameObject boxObject, out BoxSlot targetSlot)
        {
            targetSlot = null;
            BottomRayManager bottomRayManager = BottomRayManager.Instance;
            if (bottomRayManager == null || boxObject == null)
            {
                return false;
            }

            IReadOnlyList<BoxSlot> slots = bottomRayManager.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                BoxSlot slot = slots[i];
                if (slot != null && slot.CurrentBox == boxObject)
                {
                    targetSlot = slot;
                    return true;
                }
            }

            return false;
        }

        public bool IsMagnetBoosterActive => _isMagnetBoosterActive;

        public bool CanUseBooster(BoosterType boosterType)
        {
            return _boosters.TryGetValue(boosterType, out IBooster booster) && booster.CanUse();
        }

        public bool UseBooster(BoosterType boosterType)
        {
            if (!_boosters.TryGetValue(boosterType, out IBooster booster))
            {
                Debug.LogWarning($"[GridManager] Booster '{boosterType}' is not registered.");
                return false;
            }

            bool used = booster.Use();
            if (!used)
            {
                Debug.Log($"[GridManager] Booster '{boosterType}' cannot be used right now.");
            }

            return used;
        }

        public bool UseShuffleBooster()
        {
            return UseBooster(BoosterType.Shuffle);
        }

        public bool UseHandBooster()
        {
            return UseBooster(BoosterType.Hand);
        }

        public bool UseMagnetBooster()
        {
            return UseBooster(BoosterType.Magnet);
        }

        public GridMapData GetGridMap() => gridMap;
        public BoxVisualPaletteSO GetBoxVisualPalette() => mapDataSO != null ? mapDataSO.VisualPalette : null;
        public Transform ReceivePoint => receivePoint;
        public SplineContainer BottomSpline => bottomSpline;
        public int BottomSplineIndex => bottomSplineIndex;
        public float MoveSpeed => moveSpeed;
        public bool LoopSpline => loopSpline;
        public List<GridCell> GetSelectableBoxes() =>
            gridMap != null ? Pathfinding.GetSelectableBoxes(gridMap) : new List<GridCell>();
        public bool HasSelectableBoxes() => GetSelectableBoxes().Count > 0;

        /// <summary>
        /// Returns the palette of box colors used by this map's SO, in order.
        /// Falls back to a default 6-color palette when no SO is assigned.
        /// </summary>
        public List<BoxColor> GetAvailableColors()
        {
            if (mapDataSO != null && mapDataSO.AvailableColors != null && mapDataSO.AvailableColors.Count > 0)
                return mapDataSO.AvailableColors;
            return BoxColorUtility.CreateDefaultPalette();
        }
    }

    /// <summary>
    /// Attached to every editor-built Box_R_C GameObject.
    /// Listens for OnMouseDown and routes the click through BoxClickBus +
    /// asks the GridManager (if any) to lift the box.
    ///
    /// The click path:
    ///   OnMouseDown
    ///     -> BoxClickBus.BoxTapped (BottomRayManager / GridSetup may subscribe)
    ///     -> RayInputBlocker.IsBlocked check (skip lift if blocked)
    ///     -> GridManager.OnBoxLifted (animation + remove from map)
    /// </summary>
    public class BoxClickHandler : MonoBehaviour
    {
        private GridManager gridManager;
        private GridCell cell;
        private BoxColor color = BoxColorUtility.DefaultColor;

        public void Initialize(GridManager manager, GridCell gridCell, BoxColor boxColor)
        {
            gridManager = manager;
            cell = gridCell;
            color = boxColor;
        }

        private void OnMouseDown()
        {
            if (cell != null && cell.IsTrapped)
            {
                Debug.Log($"[BoxClickHandler] BLOCKED: '{name}' is TRAPPED at ({cell.Row},{cell.Col}) - no escapable neighbour, cannot be lifted.");
                return;
            }

            // Lightweight observer: raise event for any subscriber, but do NOT drive animation.
            // Animation is owned by BoxTapMover on the same box.
            BoxClickBus.RaiseBoxTapped(gameObject, color);
            BoxClickBus.RaiseGridCellTapped(cell);
            Debug.Log($"[BoxClickHandler] OnMouseDown on '{name}', cell=({cell?.Row},{cell?.Col})");
        }
    }
}