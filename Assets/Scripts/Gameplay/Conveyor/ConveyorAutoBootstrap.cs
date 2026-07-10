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

        [Tooltip("Distance between the 4 default slot anchors, world units.")]
        [SerializeField] private float _slotSpacing = 1.6f;

        [Tooltip("Y offset for the slot anchors (height above the floor).")]
        [SerializeField] private float _slotHeight = 0f;

        [Tooltip("First slot anchor world position. If both X and Z are zero, falls back to (0,0,0).")]
        [SerializeField] private Vector3 _spawnOrigin = new Vector3(6f, 0f, 0f);

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

            // Root container
            GameObject root = new GameObject("__FlowBlast_Auto_BottomRay");
            Object.DontDestroyOnLoad(root);

            var manager = root.AddComponent<BottomRayManager>();

            // Spawn 4 slot GameObjects with BoxSlot components.
            var slotFields = typeof(BottomRayManager)
                .GetField("_slots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var slotList = (System.Collections.Generic.List<BoxSlot>)slotFields.GetValue(manager);
            slotList.Clear();

            for (int i = 0; i < 4; i++)
            {
                GameObject slotGo = new GameObject($"AutoSlot_{i}");
                slotGo.transform.SetParent(root.transform);
                slotGo.transform.position = _spawnOrigin + new Vector3(i * _slotSpacing, _slotHeight, 0f);

                BoxSlot slot = slotGo.AddComponent<BoxSlot>();
                slot.SlotIndex = i;
                slot.AnchorPoint = slotGo.transform; // self-anchor

                slotList.Add(slot);
            }

            // BottomRayManager.Awake() already ran (we added the component above) but _slots was empty then.
            // Re-assign so IsFull / GetNearestEmptySlot / PlaceBox work.
            // Re-trigger lazy binding through reflection (Awake already set Instance once).
            if (BottomRayManager.Instance == null)
            {
                var instanceField = typeof(BottomRayManager).GetField("Instance",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                instanceField?.SetValue(null, manager);
            }

            if (_logSetup)
                Debug.Log($"[ConveyorAutoBootstrap] Spawned BottomRayManager with 4 default slots at spawnOrigin={_spawnOrigin}.");
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