using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace FlowBlast.Gameplay.Conveyor
{
    public class SplineConveyor : MonoBehaviour
    {
        [Header("Spline")]
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private int splineIndex = 0;

        [Header("Block")]
        [SerializeField] private GameObject blockPrefab;
        [SerializeField] private int blockCount = 4;
        [SerializeField] private float spacing = 1.5f;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool loop = true;

        [Header("Rotation")]
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Sampling")]
        [SerializeField] private int sampleCount = 100;

        private readonly List<Transform> blocks = new List<Transform>();
        private readonly List<float> blockDistances = new List<float>();
        private readonly List<float> cumulativeDistances = new List<float>();
        private readonly List<float> cumulativeTs = new List<float>();

        private float splineLength = 0f;

        private void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogError($"{name}: SplineContainer is not assigned!");
                enabled = false;
                return;
            }

            if (blockPrefab == null)
            {
                Debug.LogError($"{name}: Block Prefab is not assigned!");
                enabled = false;
                return;
            }

            BuildSplineCache();
            SpawnBlocks();
        }

        private void Update()
        {
            if (splineLength <= 0f)
            {
                return;
            }

            UpdateBlockDistances();
            UpdateBlockPositions();
        }

        private void SpawnBlocks()
        {
            blocks.Clear();
            blockDistances.Clear();

            for (int i = 0; i < blockCount; i++)
            {
                GameObject blockObject = Instantiate(blockPrefab, transform);
                Transform blockTransform = blockObject.transform;

                float startDistance = i * spacing;

                blocks.Add(blockTransform);
                blockDistances.Add(startDistance);

                UpdateBlockTransform(blockTransform, startDistance);
            }
        }

        private void UpdateBlockDistances()
        {
            for (int i = 0; i < blockDistances.Count; i++)
            {
                blockDistances[i] += moveSpeed * Time.deltaTime;

                if (loop)
                {
                    blockDistances[i] %= splineLength;
                }
                else
                {
                    blockDistances[i] = Mathf.Clamp(blockDistances[i], 0f, splineLength);
                }
            }
        }

        private void UpdateBlockPositions()
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                UpdateBlockTransform(blocks[i], blockDistances[i]);
            }
        }

        private void UpdateBlockTransform(Transform blockTransform, float distance)
        {
            float t = DistanceToT(distance);

            Vector3 worldPosition = splineContainer.EvaluatePosition(splineIndex, t);
            blockTransform.position = worldPosition;

            if (rotateToDirection)
            {
                Vector3 tangent = splineContainer.EvaluateTangent(splineIndex, t);

                if (tangent.sqrMagnitude > 0.0001f)
                {
                    Quaternion lookRotation = Quaternion.LookRotation(tangent.normalized, Vector3.up);
                    blockTransform.rotation = lookRotation * Quaternion.Euler(rotationOffset);
                }
            }
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
            if (splineLength <= 0f)
            {
                return 0f;
            }

            distance = Mathf.Clamp(distance, 0f, splineLength);

            for (int i = 1; i < cumulativeDistances.Count; i++)
            {
                if (distance <= cumulativeDistances[i])
                {
                    float d0 = cumulativeDistances[i - 1];
                    float d1 = cumulativeDistances[i];
                    float t0 = cumulativeTs[i - 1];
                    float t1 = cumulativeTs[i];

                    float lerp = Mathf.InverseLerp(d0, d1, distance);
                    return Mathf.Lerp(t0, t1, lerp);
                }
            }

            return 1f;
        }

        public void ResetConveyor()
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != null)
                {
                    Destroy(blocks[i].gameObject);
                }
            }

            blocks.Clear();
            blockDistances.Clear();

            BuildSplineCache();
            SpawnBlocks();
        }
    }
}
