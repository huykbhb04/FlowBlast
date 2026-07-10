using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

public class SplineFollower : MonoBehaviour
{
    [Header("Spline")]
    [SerializeField] private SplineContainer splineContainer;
    [SerializeField] private int splineIndex = 0;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f; // đơn vị: mét/giây
    [SerializeField] private bool loop = true;

    [Header("Rotation")]
    [SerializeField] private bool rotateToDirection = true;
    [SerializeField] private Vector3 rotationOffset = new Vector3(0f, 0f, 0f);

    [Header("Sampling")]
    [SerializeField] private int sampleCount = 100;

    private float currentDistance = 0f;
    private float splineLength = 0f;

    private List<float> cumulativeDistances = new List<float>();
    private List<float> cumulativeTs = new List<float>();

private void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogWarning($"{name}: SplineContainer is not assigned. SplineFollower will stay idle until you assign a SplineContainer.");
                enabled = false;
                return;
            }

            BuildSplineCache();

            if (splineLength <= 0f)
            {
                Debug.LogWarning($"{name}: Invalid spline length. SplineFollower will stay idle.");
                enabled = false;
                return;
            }

            // Đặt object vào vị trí đầu spline ngay từ đầu
            UpdateFollower(0f);
        }

    private void Update()
    {
        if (splineLength <= 0f) return;

        currentDistance += moveSpeed * Time.deltaTime;

        if (loop)
        {
            currentDistance %= splineLength;
        }
        else
        {
            currentDistance = Mathf.Clamp(currentDistance, 0f, splineLength);
        }

        UpdateFollower(currentDistance);
    }

    private void UpdateFollower(float distance)
    {
        float t = DistanceToT(distance);

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

            float segment = Vector3.Distance(prevPos, pos);
            splineLength += segment;

            cumulativeDistances.Add(splineLength);
            cumulativeTs.Add(t);

            prevPos = pos;
        }
    }

    private float DistanceToT(float distance)
    {
        if (splineLength <= 0f)
            return 0f;

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

    // Nếu bạn muốn restart chạy lại từ đầu
    public void ResetFollower()
    {
        currentDistance = 0f;
        UpdateFollower(0f);
    }

    // Nếu muốn đổi spline runtime
    public void SetSpline(SplineContainer newContainer, int newSplineIndex = 0)
    {
        splineContainer = newContainer;
        splineIndex = newSplineIndex;
        BuildSplineCache();
        ResetFollower();
    }
}
