using System.Collections;
using UnityEngine;
using UnityEngine.Splines;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    public class BoxTapMover : MonoBehaviour
    {
        private BoxColor currentBoxColor = BoxColorUtility.DefaultColor;

        [Header("Box Visual")]
        [SerializeField] private BoxVisualView boxVisualView;
        [SerializeField] private BoxVisualPaletteSO visualPalette;
        [SerializeField] private BoxProgressDisplay progressDisplay;

        [Header("Spline")]
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private int splineIndex = 0;

        [Header("Landing")]
        [SerializeField] private Transform receivePoint;
        [SerializeField] private float jumpDuration = 0.2f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool loop = true;

        [Header("Rotation")]
        [SerializeField] private bool rotateToDirection = false;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Input")]
        [SerializeField] private Camera mainCamera;

        [Header("Spline Sampling")]
        [SerializeField] private int sampleCount = 100;

        private GridManager gridManager;
        private bool isInitialized;
        private bool isMoving;
        private bool isJumping;
        private bool isTrapped;
        private float currentDistance;
        private float splineLength;

        private void Awake()
        {
            CacheLocalReferences();
        }

        private void Start()
        {
            CacheLocalReferences();

            if (splineContainer != null)
            {
                splineLength = EstimateSplineLength();
            }

            if (!isInitialized)
            {
                ApplyBoxVisual(currentBoxColor);
            }
        }

        public void Initialize(
            GridManager manager,
            SplineContainer container,
            Transform landingPoint,
            BoxVisualPaletteSO palette,
            BoxColor color,
            bool trapped)
        {
            gridManager = manager;
            splineContainer = container;
            receivePoint = landingPoint;
            visualPalette = palette;
            currentBoxColor = color;
            isTrapped = trapped;

            if (manager != null)
            {
                splineIndex = manager.BottomSplineIndex;
                moveSpeed = manager.MoveSpeed;
                loop = manager.LoopSpline;
            }
            isInitialized = true;

            CacheLocalReferences();

            if (splineContainer != null)
            {
                splineLength = EstimateSplineLength();
            }

            ApplyBoxVisual(color);
        }

        private void CacheLocalReferences()
        {
            if (boxVisualView == null)
            {
                boxVisualView = GetComponent<BoxVisualView>();
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }
        }

        public void ApplyBoxColor(BoxColor color)
        {
            currentBoxColor = color;
            ApplyBoxVisual(color);
        }

        public void SetVisualPalette(BoxVisualPaletteSO palette)
        {
            visualPalette = palette;
        }

        public BoxProgressDisplay ProgressDisplay => progressDisplay;

        public bool TryGetGridPosition(out int row, out int col)
        {
            row = -1;
            col = -1;

            string n = name;
            int us = n.IndexOf('_');
            if (us < 0) return false;
            int us2 = n.IndexOf('_', us + 1);
            if (us2 < 0) return false;
            if (!int.TryParse(n.Substring(us + 1, us2 - us - 1), out row)) return false;
            return int.TryParse(n.Substring(us2 + 1), out col);
        }

        private void ApplyBoxVisual(BoxColor color)
        {
            if (boxVisualView == null)
            {
                return;
            }

            if (visualPalette != null)
            {
                boxVisualView.SetPalette(visualPalette);
            }

            boxVisualView.Apply(color);
        }

        /// <summary>
        /// Read "Box_{row}_{col}" from this GameObject's name and ask GridManager for the cell.
        /// If the cell says IsTrapped, flip this BoxTapMover's isTrapped to true.
        /// </summary>
        private void SyncTrappedStateFromGrid()
        {
            if (!TryGetGridPosition(out int row, out int col)) return;

            if (gridManager == null)
            {
                return;
            }

            GridMapData map = gridManager.GetGridMap();
            if (map == null) return;
            var cell = map.GetCell(row, col);
            if (cell == null) return;

            // Only trap boxes - if the scene Box_R_C name maps to a non-Box cell in the grid
            // (out-of-sync scene/map), don't block it.
            if (cell.Type != CellType.Box)
            {
                return;
            }

            // Runtime recompute: a Box is trapped only if all 4 neighbours in the live scene
            // are either non-empty non-exit grid cells OR there is another live BoxTapMover
            // sitting on that neighbour slot. We treat "alive neighbour boxes" as blockers,
            // and out-of-bounds as escape.
            int rows = map.Rows;
            int cols = map.Cols;
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };

            System.Collections.Generic.HashSet<(int, int)> liveBoxes = new System.Collections.Generic.HashSet<(int, int)>();
            CollectLiveBoxPositions(transform.parent, liveBoxes);

            bool hasEscape = false;
            for (int i = 0; i < 4 && !hasEscape; i++)
            {
                int nr = row + dr[i];
                int nc = col + dc[i];
                if (nr < 0 || nr >= rows || nc < 0 || nc >= cols)
                {
                    // Out-of-bounds -> escape slot (box can leave the grid).
                    hasEscape = true;
                    break;
                }
                if (liveBoxes.Contains((nr, nc)))
                {
                    // Another live box still blocks this side.
                    continue;
                }
                GridCell nb = map.GetCell(nr, nc);
                if (nb != null && nb.Type == CellType.Box)
                {
                    // Grid map says "Box" but no live BoxTapMover sits here -> the box
                    // has already left, treat this cell as Empty for escape purposes.
                    hasEscape = true;
                    break;
                }
                if (nb != null && (nb.Type == CellType.Empty || nb.Type == CellType.Exit))
                {
                    hasEscape = true;
                    break;
                }
                // Wall or non-empty -> blocked.
            }

            bool newTrapped = !hasEscape;
            if (isTrapped != newTrapped)
            {
                isTrapped = newTrapped;
                if (isTrapped)
                {
                    Debug.Log($"[BoxTapMover] '{name}' (row={row}, col={col}, color={cell.Color}) re-evaluated TRAPPED (no escapable neighbour).");
                }
                else
                {
                    Debug.Log($"[BoxTapMover] '{name}' (row={row}, col={col}, color={cell.Color}) re-evaluated FREE (escape route available).");
                }
            }
        }

        private void CollectLiveBoxPositions(Transform root, System.Collections.Generic.HashSet<(int, int)> liveBoxes)
        {
            if (root == null || liveBoxes == null)
            {
                return;
            }

            BoxTapMover mover = root.GetComponent<BoxTapMover>();
            if (mover != null && mover != this && !mover.isMoving && !mover.isJumping && mover.TryGetGridPosition(out int row, out int col))
            {
                liveBoxes.Add((row, col));
            }

            for (int i = 0; i < root.childCount; i++)
            {
                CollectLiveBoxPositions(root.GetChild(i), liveBoxes);
            }
        }

        private void Update()
        {
            if (!isMoving || isJumping || splineContainer == null || splineLength <= 0f)
            {
                return;
            }

            currentDistance += moveSpeed * Time.deltaTime;

            if (loop)
            {
                currentDistance %= splineLength;
            }
            else
            {
                currentDistance = Mathf.Clamp(currentDistance, 0f, splineLength);
            }

            float t = DistanceToT(currentDistance);
            ApplySplineTransform(t);
        }

        private void OnMouseDown()
        {
            TryMoveFromTap();
        }

        private void TryMoveFromTap()
        {
            SyncTrappedStateFromGrid();

            if (isTrapped)
            {
                Debug.Log($"[BoxTapMover] BLOCKED: '{name}' is TRAPPED (no escapable neighbour) - cannot leave grid.");
                return;
            }

            RayInputBlocker blocker = RayInputBlocker.Instance;
            if (blocker != null && blocker.IsBlocked)
            {
                Debug.Log($"[BoxTapMover] BLOCKED: bottom ray is full ({blocker}).");
                return;
            }

            if (isMoving || isJumping)
            {
                return;
            }

            RaiseTapEvents();
            Debug.Log($"[BoxTapMover] Box '{name}' tapped → jumping to conveyor.");
            StartCoroutine(JumpToConveyorAndMove());
        }

        private void RaiseTapEvents()
        {
            GridCell cell = null;
            if (gridManager != null && gridManager.GetGridMap() != null && TryGetGridPosition(out int row, out int col))
            {
                cell = gridManager.GetGridMap().GetCell(row, col);
            }

            BoxClickBus.RaiseBoxTapped(gameObject, currentBoxColor);
            BoxClickBus.RaiseGridCellTapped(cell);
        }

        private IEnumerator JumpToConveyorAndMove()
        {
            if (receivePoint == null || splineContainer == null) yield break;

            // Hand the box to BottomRayManager so slot occupancy is tracked + animation begins at slot idle pos.
            var ray = BottomRayManager.Instance;
            BoxSlot assignedSlot = null;
            if (ray != null)
            {
                assignedSlot = ray.PlaceBox(this, currentBoxColor);
                if (assignedSlot == null)
                {
                    Debug.Log($"[BoxTapMover] '{name}' cannot move - all ray slots occupied.");
                    yield break;
                }
                Debug.Log($"[BoxTapMover] '{name}' → slot {assignedSlot.SlotIndex} (IdlePoint={(assignedSlot.IdlePoint != null ? assignedSlot.IdlePoint.name : "<null>")}), " +
                          $"slotWorld={assignedSlot.GetIdlePosition()}, ray OccupiedCount={ray.OccupiedCount}/{ray.Slots.Count}");
                NotifyGridBoxLeft();
            }
            else
            {
                Debug.LogWarning($"[BoxTapMover] '{name}' - BottomRayManager.Instance is null; skipping ray registration.");
            }

            isJumping = true;

            Vector3 startPosition = transform.position;
            // Prefer slot idle position (matches BottomRayManager.PlaceBox snap); fall back to receivePoint.
            Vector3 targetPosition = assignedSlot != null ? assignedSlot.GetIdlePosition() : receivePoint.position;
            Debug.Log($"[BoxTapMover] '{name}' LERP start={startPosition} → target={targetPosition} (duration={jumpDuration}s)");

            float elapsedTime = 0f;

            while (elapsedTime < jumpDuration)
            {
                elapsedTime += Time.deltaTime;
                float t = Mathf.Clamp01(elapsedTime / jumpDuration);

                transform.position = Vector3.Lerp(startPosition, targetPosition, t);
                yield return null;
            }

            transform.position = targetPosition;

            // Tìm vị trí gần nhất trên spline rồi bắt đầu chạy từ đó
            currentDistance = FindNearestDistanceOnSpline(transform.position);
            isJumping = false;
            isMoving = true;

            ApplySplineTransform(DistanceToT(currentDistance));
        }

        private void NotifyGridBoxLeft()
        {
            if (gridManager == null || !TryGetGridPosition(out int row, out int col))
            {
                return;
            }

            gridManager.RemoveBoxFromGrid(row, col);
        }

        private void ApplySplineTransform(float t)
        {
            Vector3 worldPosition = splineContainer.EvaluatePosition(splineIndex, t);
            transform.position = worldPosition;

            if (rotateToDirection)
            {
                Vector3 tangent = splineContainer.EvaluateTangent(splineIndex, t);

                if (tangent.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                    transform.rotation = lookRotation * Quaternion.Euler(rotationOffset);
                }
            }
        }

        private float FindNearestDistanceOnSpline(Vector3 worldPosition)
        {
            float nearestDistance = 0f;
            float nearestSqrDistance = float.MaxValue;

            Vector3 prevPos = splineContainer.EvaluatePosition(splineIndex, 0f);

            for (int i = 1; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);

                Vector3 closestPoint = GetClosestPointOnSegment(prevPos, pos, worldPosition);
                float sqrDistance = (worldPosition - closestPoint).sqrMagnitude;

                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearestDistance = splineLength * t;
                }

                prevPos = pos;
            }

            return nearestDistance;
        }
        
        // Public methods for external setup
        public void SetSplineReference(SplineContainer container)
        {
            splineContainer = container;
            if (splineContainer != null)
            {
                splineLength = EstimateSplineLength();
            }
        }

        public void SetSplineIndex(int index)
        {
            splineIndex = index;
        }

        public void SetMoveSpeed(float speed)
        {
            moveSpeed = speed;
        }

        public void SetLoop(bool shouldLoop)
        {
            loop = shouldLoop;
        }
        
        public void SetReceivePoint(Transform point)
        {
            receivePoint = point;
        }

        /// <summary>
        /// Mark this box as trapped: no escapable neighbour, so it refuses to be tapped off the grid.
        /// </summary>
        public void SetTrapped(bool trapped)
        {
            isTrapped = trapped;
        }

        public bool IsTrapped => isTrapped;
        
        public void Reset()
        {
            isMoving = false;
            isJumping = false;
            currentDistance = 0f;
        }

        private Vector3 GetClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(p - a, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        private float EstimateSplineLength()
        {
            float length = 0f;
            Vector3 prevPos = splineContainer.EvaluatePosition(splineIndex, 0f);

            for (int i = 1; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);
                length += Vector3.Distance(prevPos, pos);
                prevPos = pos;
            }

            return length;
        }

        private float DistanceToT(float distance)
        {
            if (splineLength <= 0f)
            {
                return 0f;
            }

            distance = Mathf.Clamp(distance, 0f, splineLength);
            return distance / splineLength;
        }
    }
}
