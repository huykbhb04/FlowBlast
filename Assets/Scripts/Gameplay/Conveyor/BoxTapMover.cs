using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;

namespace FlowBlast.Gameplay.Conveyor
{
    public class BoxTapMover : MonoBehaviour
    {
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
        private float currentDistance;
        private float splineLength;

        private void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogError($"{name}: SplineContainer is not assigned!");
                enabled = false;
                return;
            }

            if (receivePoint == null)
            {
                Debug.LogError($"{name}: Receive Point is not assigned!");
                enabled = false;
                return;
            }

            if (mainCamera == null)
            {
                mainCamera = Camera.main;
            }

            splineLength = EstimateSplineLength();

            // Box ban đầu ở dưới khay, giữ nguyên vị trí hiện tại.
        }

        private void Update()
        {
            HandleMouseClick();

            if (!isMoving || isJumping || splineLength <= 0f)
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

            if (mainCamera == null)
            {
                Debug.LogError($"{name}: Main Camera is not assigned!");
                return;
            }

            Ray ray = mainCamera.ScreenPointToRay(Mouse.current.position.ReadValue());

            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    if (!isMoving && !isJumping)
                    {
                        StartCoroutine(JumpToConveyorAndMove());
                    }
                }
            }
        }

        private IEnumerator JumpToConveyorAndMove()
        {
            isJumping = true;

            Vector3 startPosition = transform.position;
            Vector3 targetPosition = receivePoint.position;

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
