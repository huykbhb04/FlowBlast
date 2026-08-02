using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Auto-bootstrap: if the scene does NOT have a BottomRayManager + RayInputBlocker already wired up,
    /// this component spawns them at runtime so gameplay works out-of-the-box during development.
    ///
    /// - Creates 4 default BoxSlot anchors laid out horizontally (BoxSpawnPoint..BoxSpawnPoint+spread).
    /// - Creates one GameObject holding BottomRayManager with those slots wired in.
    /// - Creates one GameObject holding RayInputBlocker pointing at the same BottomRayManager.
    /// - Marks them with the FlowBlast_AutoSpawned tag so they can be cleaned up on quit.
    ///
    /// If a BottomRayManager already exists in the scene, this is a no-op.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public class ConveyorAutoBootstrap : MonoBehaviour
    {
        [Header("Auto-spawn defaults")]
        [Tooltip("If true and no BottomRayManager exists in any scene, create one with default settings.")]
        [SerializeField] private bool _autoSpawnIfMissing = true;

        [Header("Debug")]
        [SerializeField] private bool _logSetup = true;

        private void Awake()
        {
            Debug.Log("[ConveyorAutoBootstrap] Awake start - looking for BottomRayManager.");
            EnsureBottomRayManager();
            EnsureRayInputBlocker();
            Debug.Log($"[ConveyorAutoBootstrap] Awake done - bottomRay={BottomRayManager.Instance != null} blocker={RayInputBlocker.Instance != null}");
        }

        private void EnsureBottomRayManager()
        {
            if (BottomRayManager.Instance != null)
            {
                if (_logSetup)
                    Debug.Log("[ConveyorAutoBootstrap] BottomRayManager already present, skipping spawn.");
                return;
            }

            if (!_autoSpawnIfMissing)
            {
                Debug.LogWarning("[ConveyorAutoBootstrap] _autoSpawnIfMissing=false and no BottomRayManager in scene; gameplay will fall back to GridToWorld target.");
                return;
            }

            GameObject root = new GameObject("__FlowBlast_Auto_BottomRay");
            Object.DontDestroyOnLoad(root);
            root.AddComponent<BottomRayManager>();

            if (_logSetup)
                Debug.Log("[ConveyorAutoBootstrap] Spawned BottomRayManager with 4 runtime slots.");
        }

        private void EnsureRayInputBlocker()
        {
            if (RayInputBlocker.Instance != null)
            {
                if (_logSetup)
                    Debug.Log("[ConveyorAutoBootstrap] RayInputBlocker already present, skipping spawn.");
                return;
            }

            if (BottomRayManager.Instance == null)
            {
                Debug.LogWarning("[ConveyorAutoBootstrap] Cannot spawn RayInputBlocker - BottomRayManager still null.");
                return;
            }

            GameObject blockerGo = new GameObject("__FlowBlast_Auto_InputBlocker");
            Object.DontDestroyOnLoad(blockerGo);
            var blocker = blockerGo.AddComponent<RayInputBlocker>();

            // Wire _bottomRay to the singleton via reflection.
            var f = typeof(RayInputBlocker).GetField("_bottomRay",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            f?.SetValue(blocker, BottomRayManager.Instance);

            if (_logSetup)
                Debug.Log("[ConveyorAutoBootstrap] Spawned RayInputBlocker.");
        }
    }
}