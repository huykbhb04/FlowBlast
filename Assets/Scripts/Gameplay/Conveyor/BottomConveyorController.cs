using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Manages boxes on the bottom conveyor spline.
    /// Boxes can be added via tap and will move along the spline.
    /// </summary>
    public class BottomConveyorController : MonoBehaviour
    {
        [Header("Spline")]
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private int splineIndex = 0;

        [Header("Box Settings")]
        [SerializeField] private GameObject boxPrefab;
        [SerializeField] private float spacing = 2f;
        
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool loop = true;
        
        [Header("Gate & Receive")]
        [SerializeField] private Transform gatePoint;
        [SerializeField] private Transform receivePoint;
        
        [Header("Rotation")]
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;
        
        [Header("Sampling")]
        [SerializeField] private int sampleCount = 100;

        [Header("Auto Ball Stream")]
        [Tooltip("If true, the bottom conveyor auto-spawns a dense stream of small balls flowing along the spline so the player always sees plenty of balls dissolving.")]
        [SerializeField] private bool autoSpawnStream = true;

        [Tooltip("Number of balls in the auto stream.")]
        [SerializeField] private int streamBallCount = 40;

        [Tooltip("Prefab for auto-spawned balls. Falls back to boxPrefab if null.")]
        [SerializeField] private GameObject streamBallPrefab;

        [Tooltip("Palette colors the auto stream cycles through.")]
        [SerializeField] private BoxColor[] streamColors = new[]
        {
            BoxColor.Red, BoxColor.Yellow, BoxColor.Green, BoxColor.Blue,
            BoxColor.Red, BoxColor.Yellow, BoxColor.Green, BoxColor.Blue
        };

        [Tooltip("How far a stream ball travels before it dissolves (fraction of spline length, 0..1). 1 = full loop.")]
        [Range(0.1f, 1f)]
        [SerializeField] private float streamDissolveFraction = 0.95f;

        private readonly List<ConveyorBox> boxes = new List<ConveyorBox>();
        private float splineLength;
        private readonly List<float> cumulativeDistances = new List<float>();
        private readonly List<float> cumulativeTs = new List<float>();

        private class ConveyorBox
        {
            public Transform Transform;
            public float Distance;
            public bool IsMoving;
            public bool IsStream;
            public float DissolveDistance;
            public bool DissolveStarted;
        }

        private void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogWarning($"{name}: SplineContainer is not assigned. Conveyor will stay idle until you assign a SplineContainer.");
                enabled = false;
                return;
            }

            BuildSplineCache();

            if (autoSpawnStream)
            {
                SpawnStream();
            }
        }

        /// <summary>
        /// Spawn a dense stream of small balls that flow continuously along the spline
        /// and dissolve near the end of each loop, so the player always sees plenty of
        /// dissolving balls on the bottom conveyor (purely decorative / feedback).
        /// </summary>
        private void SpawnStream()
        {
            if (streamBallPrefab == null) streamBallPrefab = boxPrefab;
            if (streamBallPrefab == null)
            {
                Debug.LogWarning($"{name}: autoSpawnStream is on but neither streamBallPrefab nor boxPrefab is assigned. Skipping stream spawn.");
                return;
            }
            if (streamBallCount <= 0 || splineLength <= 0f) return;

            float spacing = Mathf.Max(0.1f, splineLength / streamBallCount);
            float dissolveDistance = splineLength * Mathf.Clamp01(streamDissolveFraction);
            int colorCount = streamColors != null ? streamColors.Length : 0;

            for (int i = 0; i < streamBallCount; i++)
            {
                Vector3 startPos = receivePoint != null
                    ? receivePoint.position
                    : splineContainer.EvaluatePosition(splineIndex, 0f);

                GameObject ball = Instantiate(streamBallPrefab, startPos, Quaternion.identity, transform);
                ball.name = $"__BottomStreamBall_{i}";

                // Apply a stream color (cycles through palette if provided).
                BoxColor col = colorCount > 0 ? streamColors[i % colorCount] : BoxColor.Red;
                ApplyStreamColor(ball, col);

                ConveyorBox box = new ConveyorBox
                {
                    Transform = ball.transform,
                    Distance = i * spacing,
                    IsMoving = true,
                    IsStream = true,
                    DissolveDistance = dissolveDistance,
                    DissolveStarted = false
                };
                boxes.Add(box);
            }

            Debug.Log($"{name}: Spawned {streamBallCount} stream balls (dissolve at {dissolveDistance:F1}/{splineLength:F1}).");
        }

        private void ApplyStreamColor(GameObject ball, BoxColor color)
        {
            if (ball == null) return;

            // Prefer ConveyorColoredBlock if present (matches TopConveyor pipeline).
            var colored = ball.GetComponent<ConveyorColoredBlock>();
            if (colored == null) colored = ball.AddComponent<ConveyorColoredBlock>();
            colored.SetColor(color);

            // Tint renderers via MaterialPropertyBlock (URP _BaseColor + built-in _Color).
            Color c = BoxColorToUnityColor(color);
            int baseColorId = Shader.PropertyToID("_BaseColor");
            int colorId = Shader.PropertyToID("_Color");
            var renderers = ball.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                var rend = renderers[r];
                if (rend == null) continue;
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                rend.GetPropertyBlock(mpb);
                mpb.SetColor(baseColorId, c);
                mpb.SetColor(colorId, c);
                rend.SetPropertyBlock(mpb);
            }
        }

        private static Color BoxColorToUnityColor(BoxColor color)
        {
            switch (color)
            {
                case BoxColor.Red: return new Color(0.95f, 0.20f, 0.20f);
                case BoxColor.Blue: return new Color(0.20f, 0.45f, 0.95f);
                case BoxColor.Green: return new Color(0.25f, 0.80f, 0.30f);
                case BoxColor.Yellow: return new Color(0.95f, 0.85f, 0.20f);
                case BoxColor.Purple: return new Color(0.70f, 0.30f, 0.85f);
                case BoxColor.Orange: return new Color(0.95f, 0.55f, 0.20f);
                default: return Color.white;
            }
        }

        private void Update()
        {
            if (splineLength <= 0f) return;
            
            UpdateBoxDistances();
            UpdateBoxPositions();
        }

        /// <summary>
        /// Add a box to the conveyor at the receive point and start it moving
        /// </summary>
        public void AddBox(Transform boxTransform)
        {
            if (boxTransform == null) return;
            
            // Check minimum spacing from last box
            float minDistance = boxes.Count > 0 ? boxes[boxes.Count - 1].Distance - spacing : 0f;
            
            ConveyorBox box = new ConveyorBox
            {
                Transform = boxTransform,
                Distance = minDistance - spacing,
                IsMoving = true
            };
            
            boxes.Add(box);
            
            // Set initial position at receive point
            Vector3 worldPos = receivePoint != null ? receivePoint.position : splineContainer.EvaluatePosition(splineIndex, 0f);
            boxTransform.position = worldPos;
            
            Debug.Log($"Box added to bottom conveyor. Total boxes: {boxes.Count}");
        }
        
        /// <summary>
        /// Add a box that jumps from a world position to the conveyor
        /// </summary>
        public void AddBoxWithJump(GameObject boxPrefab, Vector3 startPosition, float jumpDuration = 0.2f)
        {
            StartCoroutine(AddBoxWithJumpCoroutine(boxPrefab, startPosition, jumpDuration));
        }
        
        private System.Collections.IEnumerator AddBoxWithJumpCoroutine(GameObject prefab, Vector3 startPosition, float jumpDuration)
        {
            Vector3 targetPos = receivePoint != null ? receivePoint.position : splineContainer.EvaluatePosition(splineIndex, 0f);
            
            // Jump animation
            float elapsed = 0f;
            while (elapsed < jumpDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / jumpDuration);
                // Arc jump
                Vector3 pos = Vector3.Lerp(startPosition, targetPos, t);
                pos.y += Mathf.Sin(t * Mathf.PI) * 0.5f;
                // Would need to instantiate - for now just move
                yield return null;
            }
            
            // Find nearest distance and start moving
            float distance = FindNearestDistanceOnSpline(targetPos);
            
            GameObject newBox = Instantiate(prefab, targetPos, Quaternion.identity);
            ConveyorBox box = new ConveyorBox
            {
                Transform = newBox.transform,
                Distance = distance,
                IsMoving = true
            };
            
            boxes.Add(box);
            Debug.Log($"Box spawned and added to conveyor at distance {distance:F2}. Total boxes: {boxes.Count}");
        }

        private void UpdateBoxDistances()
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (!b.IsMoving) continue;

                b.Distance += moveSpeed * Time.deltaTime;

                // Stream balls dissolve at DissolveDistance, then respawn at 0 to keep
                // the conveyor visually full of dissolving balls.
                if (b.IsStream && !b.DissolveStarted && b.Distance >= b.DissolveDistance)
                {
                    b.DissolveStarted = true;
                    if (b.Transform != null)
                    {
                        var dissolve = b.Transform.GetComponent<BlockDissolveEffect>();
                        if (dissolve == null) dissolve = b.Transform.gameObject.AddComponent<BlockDissolveEffect>();
                        dissolve.PlayAndDestroy();
                    }
                    b.IsMoving = false;
                }
                else if (loop)
                {
                    b.Distance %= splineLength;
                }
                else
                {
                    b.Distance = Mathf.Clamp(b.Distance, 0f, splineLength);
                }
            }
        }

        private void UpdateBoxPositions()
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (b.Transform == null)
                {
                    // Stream ball finished dissolving - respawn at receive point to
                    // keep the conveyor visually full.
                    if (b.IsStream && autoSpawnStream && streamBallPrefab != null && splineLength > 0f)
                    {
                        RespawnStreamBall(b);
                        continue;
                    }
                    boxes.RemoveAt(i);
                    i--;
                    continue;
                }

                float t = DistanceToT(b.Distance);
                Vector3 worldPos = splineContainer.EvaluatePosition(splineIndex, t);
                b.Transform.position = worldPos;

                if (rotateToDirection)
                {
                    Vector3 tangent = splineContainer.EvaluateTangent(splineIndex, t);
                    if (tangent.sqrMagnitude > 0.0001f)
                    {
                        Quaternion lookRot = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                        b.Transform.rotation = lookRot * Quaternion.Euler(rotationOffset);
                    }
                }
            }
        }

        private void RespawnStreamBall(ConveyorBox b)
        {
            Vector3 startPos = receivePoint != null
                ? receivePoint.position
                : splineContainer.EvaluatePosition(splineIndex, 0f);

            GameObject ball = Instantiate(streamBallPrefab, startPos, Quaternion.identity, transform);
            int streamIdx = boxes.IndexOf(b);
            ball.name = $"__BottomStreamBall_{streamIdx}";

            BoxColor col = (streamColors != null && streamColors.Length > 0)
                ? streamColors[streamIdx % streamColors.Length]
                : BoxColor.Red;
            ApplyStreamColor(ball, col);

            b.Transform = ball.transform;
            b.Distance = 0f;
            b.IsMoving = true;
            b.DissolveStarted = false;
        }

        private void BuildSplineCache()
        {
            cumulativeDistances.Clear();
            cumulativeTs.Clear();
            splineLength = 0f;

            Vector3 prevPos = splineContainer.EvaluatePosition(splineIndex, 0f);
            cumulativeDistances.Add(0f);
            cumulativeTs.Add(0f);

            for (int i = 1; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);
                float segmentLength = Vector3.Distance(prevPos, pos);
                splineLength += segmentLength;
                cumulativeDistances.Add(splineLength);
                cumulativeTs.Add(t);
                prevPos = pos;
            }
        }

        private float DistanceToT(float distance)
        {
            if (splineLength <= 0f) return 0f;
            distance = Mathf.Clamp(distance, 0f, splineLength);

            for (int i = 1; i < cumulativeDistances.Count; i++)
            {
                if (distance <= cumulativeDistances[i])
                {
                    float lerp = Mathf.InverseLerp(cumulativeDistances[i - 1], cumulativeDistances[i], distance);
                    return Mathf.Lerp(cumulativeTs[i - 1], cumulativeTs[i], lerp);
                }
            }
            return 1f;
        }

        private float FindNearestDistanceOnSpline(Vector3 worldPosition)
        {
            float nearestDistance = 0f;
            float nearestSqrDist = float.MaxValue;

            Vector3 prevPos = splineContainer.EvaluatePosition(splineIndex, 0f);

            for (int i = 1; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);
                Vector3 closest = GetClosestPointOnSegment(prevPos, pos, worldPosition);
                float sqrDist = (worldPosition - closest).sqrMagnitude;

                if (sqrDist < nearestSqrDist)
                {
                    nearestSqrDist = sqrDist;
                    nearestDistance = splineLength * t;
                }
                prevPos = pos;
            }
            return nearestDistance;
        }

        private Vector3 GetClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float t = Vector3.Dot(p - a, ab) / ab.sqrMagnitude;
            t = Mathf.Clamp01(t);
            return a + ab * t;
        }

        /// <summary>
        /// Get the gate position on the top conveyor spline (for reference)
        /// </summary>
        public Vector3 GetGatePosition()
        {
            return gatePoint != null ? gatePoint.position : Vector3.zero;
        }

        /// <summary>
        /// Get the receive position on the bottom conveyor spline
        /// </summary>
        public Vector3 GetReceivePosition()
        {
            return receivePoint != null ? receivePoint.position : Vector3.zero;
        }
        
        /// <summary>
        /// Clear all boxes from the conveyor
        /// </summary>
        public void ClearAllBoxes()
        {
            foreach (var box in boxes)
            {
                if (box.Transform != null)
                {
                    Destroy(box.Transform.gameObject);
                }
            }
            boxes.Clear();
        }
    }
}
