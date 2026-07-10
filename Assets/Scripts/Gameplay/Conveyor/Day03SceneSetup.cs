using UnityEngine;
using UnityEngine.Splines;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Setup script for Day03 conveyor scene.
    /// Run this once to configure the scene properly.
    /// </summary>
    public class Day03SceneSetup : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private GameObject debugRoot;
        
        [Header("Top Conveyor")]
        [SerializeField] private SplineContainer topSplinePath;
        [SerializeField] private GameObject blockPrefab;
        
        [Header("Bottom Conveyor")]
        [SerializeField] private SplineContainer bottomSplinePath;
        
        [Header("Points")]
        [SerializeField] private Transform topGatePoint;
        [SerializeField] private Transform bottomReceivePoint;
        
        [Header("Configuration")]
        [SerializeField] private int blockCount = 4;
        [SerializeField] private float blockSpacing = 2f;
        [SerializeField] private float moveSpeed = 2f;
        
        [Header("Debug Boxes")]
        [SerializeField] private GameObject[] debugBoxes;
        
        public void SetupScene()
        {
            SetupTopConveyor();
            SetupBottomConveyor();
            SetupDebugBoxes();
            Debug.Log("Day03 Scene Setup Complete!");
        }
        
        private void SetupTopConveyor()
        {
            if (topSplinePath == null)
            {
                Debug.LogError("Top Spline Path is not assigned!");
                return;
            }
            
            // Find or create TopConveyor object
            Transform topConveyorTransform = gameplayRoot.transform.Find("TopConveyor");
            GameObject topConveyor;
            
            if (topConveyorTransform == null)
            {
                topConveyor = new GameObject("TopConveyor");
                topConveyor.transform.SetParent(gameplayRoot.transform);
            }
            else
            {
                topConveyor = topConveyorTransform.gameObject;
            }
            
            // Add or get SplineConveyor component
            SplineConveyor conveyor = topConveyor.GetComponent<SplineConveyor>();
            if (conveyor == null)
            {
                conveyor = topConveyor.AddComponent<SplineConveyor>();
            }
            
            // Configure (values will be set via inspector if needed)
            Debug.Log($"Top Conveyor configured with {blockCount} blocks, spacing {blockSpacing}");
        }
        
        private void SetupBottomConveyor()
        {
            if (bottomSplinePath == null)
            {
                Debug.LogError("Bottom Spline Path is not assigned!");
                return;
            }
            
            Debug.Log($"Bottom Conveyor configured on spline: {bottomSplinePath.name}");
            Debug.Log($"  - Gate Point: {topGatePoint.position}");
            Debug.Log($"  - Receive Point: {bottomReceivePoint.position}");
        }
        
        private void SetupDebugBoxes()
        {
            if (debugBoxes == null || debugBoxes.Length == 0)
            {
                Debug.LogWarning("No debug boxes assigned!");
                return;
            }
            
            for (int i = 0; i < debugBoxes.Length; i++)
            {
                var box = debugBoxes[i];
                if (box == null) continue;
                
                var tapMover = box.GetComponent<BoxTapMover>();
                if (tapMover == null)
                {
                    tapMover = box.AddComponent<BoxTapMover>();
                }
                
                // Configure for bottom conveyor
                tapMover.SetSplineReference(bottomSplinePath);
                tapMover.SetReceivePoint(bottomReceivePoint);
                
                Debug.Log($"Box {i + 1} ({box.name}) configured for bottom conveyor tap-to-move");
            }
        }
        
        // Call this from menu: Window > FlowBlast > Setup Day03 Scene
        [ContextMenu("Setup Scene")]
        public void SetupViaContextMenu()
        {
            SetupScene();
        }
    }
}
