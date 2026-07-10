using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    public class BoxTapMover : MonoBehaviour
    {
        [Header("Box Identity")]
        [SerializeField] private FlowBlast.Gameplay.Grid.BoxColor boxColor = FlowBlast.Gameplay.Grid.BoxColor.Red;

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
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Input")]
        [SerializeField] private Camera mainCamera;

        [Header("Spline Sampling")]
        [SerializeField] private int sampleCount = 100;

        private bool isMoving;
        private bool isJumping;
        private bool isTrapped;       // Set externally via SetTrapped(); if true this box refuses to leave the grid.
        private float currentDistance;
        private float splineLength;

        private void Start()
        {
            // SplineContainer và Receive Point có thể null khi scene chưa có conveyor.
            // Trong trường hợp đó box vẫn hiển thị bình thường, chỉ là chưa chạy được.

            splineLength = splineContainer != null ? EstimateSplineLength() : 0f;

            // Nếu không đủ reference, log warning thay vì error + không tắt component.
            if (splineContainer == null)
                Debug.LogWarning($"{name}: SplineContainer is not assigned. Box will not move along conveyor until you assign it.");
            if (receivePoint == null)
                Debug.LogWarning($"{name}: Receive Point is not assigned. Tap will not jump to conveyor until you assign it.");

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            // Pull the correct color from the grid cell matching this Box's "Box_R_C" name.
            // Inspector field may be left at default (Red) on prefabs; we override with the live cell color.
            ResolveBoxColorFromGrid();

            // Sync trapped state from GridManager (cell.IsTrapped) using box name "Box_R_C".
            SyncTrappedStateFromGrid();

            // Box ban đầu ở dưới khay, giữ nguyên vị trí hiện tại.
        }

        /// <summary>
        /// Read "Box_{row}_{col}" from this GameObject's name and ask GridManager for the cell color.
        /// If the cell has a BoxColor (non-Empty / non-Exit), override boxColor with it. Otherwise
        /// keep the Inspector-assigned value (so manual prefab overrides still work for non-grid boxes).
        /// </summary>
        private void ResolveBoxColorFromGrid()
        {
            string n = name;
            int us = n.IndexOf('_');
            if (us < 0) return;
            int us2 = n.IndexOf('_', us + 1);
            if (us2 < 0) return;
            if (!int.TryParse(n.Substring(us + 1, us2 - us - 1), out int row)) return;
            if (!int.TryParse(n.Substring(us2 + 1), out int col)) return;

            var gm = FindObjectOfType<FlowBlast.Gameplay.Grid.GridManager>();
            if (gm == null) return;
            var map = gm.GetGridMap();
            if (map == null) return;
            var cell = map.GetCell(row, col);
            if (cell == null) return;
            if (cell.Type != FlowBlast.Gameplay.Grid.CellType.Box) return;

            if (cell.Color != boxColor)
            {
                Debug.Log($"[BoxTapMover] '{name}' color override {boxColor} -> {cell.Color} from grid cell ({row},{col}).");
                boxColor = cell.Color;
            }
        }

        /// <summary>
        /// Read "Box_{row}_{col}" from this GameObject's name and ask GridManager for the cell.
        /// If the cell says IsTrapped, flip this BoxTapMover's isTrapped to true.
        /// </summary>
        private void SyncTrappedStateFromGrid()
        {
            string n = name; // e.g. "Box_0_1" or "Box_0_1(Clone)"
            int us = n.IndexOf('_');
            if (us < 0) return;
            int us2 = n.IndexOf('_', us + 1);
            if (us2 < 0) return;
            if (!int.TryParse(n.Substring(us + 1, us2 - us - 1), out int row)) return;
            if (!int.TryParse(n.Substring(us2 + 1), out int col)) return;

            var gm = FindObjectOfType<FlowBlast.Gameplay.Grid.GridManager>();
            if (gm == null) return;
            var map = gm.GetGridMap();
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

            // Pre-collect live box (row,col) positions so we don't pay FindObjectsOfType per neighbour.
            // Exclude boxes that have already left the grid (moving along the conveyor),
            // since they're no longer blocking neighbours from escaping.
            var liveBoxes = new System.Collections.Generic.HashSet<(int, int)>();
            var all = FindObjectsOfType<BoxTapMover>();
            for (int i = 0; i < all.Length; i++)
            {
                var mb = all[i];
                if (mb == this) continue;                  // skip self - neighbour check is for siblings
                if (mb.isMoving || mb.isJumping) continue; // skip boxes that already left
                string bn = mb.name;
                int bus = bn.IndexOf('_');
                if (bus < 0) continue;
                int bus2 = bn.IndexOf('_', bus + 1);
                if (bus2 < 0) continue;
                if (!int.TryParse(bn.Substring(bus + 1, bus2 - bus - 1), out int br)) continue;
                if (!int.TryParse(bn.Substring(bus2 + 1), out int bc)) continue;
                liveBoxes.Add((br, bc));
            }

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

        private void Update()
        {
            HandleMouseClick();

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

        private void HandleMouseClick()
        {
            if (!Mouse.current.leftButton.wasPressedThisFrame)
            {
                return;
            }

            // DEBUG: log tap attempt always
            if (mainCamera == null)
            {
                Debug.LogError($"{name}: Main Camera is not assigned!");
                return;
            }

            Vector2 mousePos = Mouse.current.position.ReadValue();
            Ray ray = mainCamera.ScreenPointToRay(mousePos);
            Debug.Log($"[BoxTapMover] Tap detected at screen {mousePos}, raycast from {name}...");

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Debug.Log($"[BoxTapMover] Raycast HIT: {hit.transform.name} (isThis={hit.transform == transform})");
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    // Re-evaluate trap state at tap time: previous boxes may have left
                    // the grid since Start(), opening new escape routes for this box.
                    SyncTrappedStateFromGrid();

                    if (isTrapped)
                    {
                        Debug.Log($"[BoxTapMover] BLOCKED: '{name}' is TRAPPED (no escapable neighbour) - cannot leave grid.");
                        return;
                    }

                    // Check RayInputBlocker before jumping
                    var blocker = RayInputBlocker.Instance;
                    if (blocker != null && blocker.IsBlocked)
                    {
                        Debug.Log($"[BoxTapMover] BLOCKED: bottom ray is full ({blocker}).");
                        return;
                    }

                    if (!isMoving && !isJumping)
                    {
                        Debug.Log($"[BoxTapMover] Box '{name}' tapped → jumping to conveyor.");
                        StartCoroutine(JumpToConveyorAndMove());
                    }
                }
            }
            else
            {
                Debug.Log($"[BoxTapMover] Raycast MISS at screen {mousePos}.");
            }
        }

        private IEnumerator JumpToConveyorAndMove()
        {
            if (receivePoint == null || splineContainer == null) yield break;

            // Hand the box to BottomRayManager so slot occupancy is tracked + animation begins at slot idle pos.
            var ray = BottomRayManager.Instance;
            BoxSlot assignedSlot = null;
            if (ray != null)
            {
                assignedSlot = ray.PlaceBox(gameObject, boxColor);
                if (assignedSlot == null)
                {
                    Debug.Log($"[BoxTapMover] '{name}' cannot move - all ray slots occupied.");
                    yield break;
                }
                Debug.Log($"[BoxTapMover] '{name}' → slot {assignedSlot.SlotIndex} (IdlePoint={(assignedSlot.IdlePoint != null ? assignedSlot.IdlePoint.name : "<null>")}), " +
                          $"slotWorld={assignedSlot.GetIdlePosition()}, ray OccupiedCount={ray.OccupiedCount}/{ray.Slots.Count}");
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
