using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    [System.Serializable]
    public class ColoredBlockPrefab
    {
        public GameObject Prefab;
        public BoxColor Color = BoxColorUtility.DefaultColor;
    }

    /// <summary>
    /// Lightweight per-block handle attached by SplineConveyor when a block is spawned.
    /// GateMatcher calls MarkConsumed() to stop the conveyor from advancing this block
    /// while the dissolve effect plays.
    /// </summary>
    public class BlockHandle : MonoBehaviour
    {
        public bool IsConsumed { get; private set; }

        public void MarkConsumed()
        {
            IsConsumed = true;
        }

        /// <summary>
        /// Reset consumed flag so the block is eligible for matching again after
        /// being recycled from the BlockPool.
        /// </summary>
        public void Reset()
        {
            IsConsumed = false;
        }
    }

    [DefaultExecutionOrder(-100)]
    public class SplineConveyor : MonoBehaviour
    {
        [Header("Spline")]
        [SerializeField] private SplineContainer splineContainer;
        [SerializeField] private int splineIndex = 0;

        [Header("Blocks (one entry per color used by the map)")]
        [SerializeField]
        private List<ColoredBlockPrefab> blockPrefabs = new List<ColoredBlockPrefab>();

        [Header("Block Visual")]
        [SerializeField] private BoxVisualPaletteSO visualPalette;

        [Header("Block count")]
        [Tooltip("Total number of blocks to spawn on the spline.")]
        [SerializeField] private int blockCount = 4;
        [SerializeField] private float spacing = 1.5f;

        public enum BlockLayout
        {
            /// <summary>Color[ i mod palette.Count ] - red/blue/yellow/green interleaved.</summary>
            Interleaved,
            /// <summary>Group balls of the same color together: 20 Reds, then 20 Blues, etc. Default.</summary>
            Clustered,
            /// <summary>Same as Clustered but each cluster can use any color from the palette (no fixed order, still grouped).</summary>
            ClusteredAnyOrder,
        }

        [Tooltip("How the spawned blocks pick a color from the palette.")]
        [SerializeField] private BlockLayout layout = BlockLayout.Clustered;

        [Tooltip("When layout is Clustered / ClusteredAnyOrder, how many blocks per color cluster before the color rolls over.")]
        [Min(1)]
        [SerializeField] private int blocksPerCluster = 20;

        [Header("Movement")]
        [SerializeField] private float moveSpeed = 2f;
        [SerializeField] private bool loop = true;

        [Header("Gate Timing")]
        [Tooltip("How long the dissolve / consume animation runs when a top ball matches " +
                 "the gate. Must match BlockDissolveEffect.duration and BoxExitAnimator.duration. " +
                 "Used to compute the minimum spacing so two balls can never reach the gate " +
                 "faster than this duration (avoids visible 'skip' when 2 balls overlap).")]
        [SerializeField] private float dissolveDuration = 0.6f;

        [Header("Rotation")]
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private Vector3 rotationOffset = Vector3.zero;

        [Header("Sampling")]
        [SerializeField] private int sampleCount = 100;

        [Header("Auto-sync with GridMapDataSO")]
        [Tooltip("If true, on Start() pull AvailableColors from GridManager and spawn one block per color (overrides blockPrefabs).")]
        [SerializeField] private bool autoSyncFromGridManager = true;
        [SerializeField] private bool spawnOnStart;

        [Header("Cluster Wrap Guard")]
        [Tooltip("If true, blocksPerCluster is capped automatically so each cluster fits within 1/paletteSize of the spline. " +
                 "If false, the Inspector value of blocksPerCluster is respected even if clusters wrap around the spline " +
                 "(useful for long splines or when you want exactly N balls/color regardless of geometry).")]
        [SerializeField] private bool capBlocksPerClusterToSpline = false;

        [Header("Pooling")]
        [Tooltip("One BlockPool component per color in blockPrefabs, in the same order. " +
                 "Set via SetBlockPools() or assign in Inspector.")]
        [SerializeField] private BlockPool[] _poolsPerColor;

        [Header("Gate")]
        [Tooltip("World transform marking the gate position on the spline. When a top block crosses this point, GateMatcher fires.")]
        [SerializeField] private Transform gateAnchor;
        [Tooltip("Component that handles match/miss at the gate. Assigned via Inspector or auto-resolved.")]
        [SerializeField] private GateMatcher gateMatcher;

        private readonly List<Transform> blocks = new List<Transform>();
        private readonly List<float> blockDistances = new List<float>();
        private readonly List<bool> blockGateFired = new List<bool>();
        private readonly List<float> cumulativeDistances = new List<float>();
        private readonly List<float> cumulativeTs = new List<float>();

        // Track which blocks belong to which color so we can despawn a whole color
        // cluster once all slots of that color have been filled (no more matching needed).
        private readonly Dictionary<BoxColor, List<int>> blockIndicesByColor = new Dictionary<BoxColor, List<int>>();

        // Colors whose slots are all completed. GateMatcher dissolves any incoming
        // block of these colors at the gate so we don't need to destroy them mid-flight.
        private readonly HashSet<BoxColor> colorsDone = new HashSet<BoxColor>();

        private float splineLength = 0f;
        private float gateDistance = -1f;
        private GridMapDataSO _pendingConfig; // deferred from SetupFromConfig before Start() ran

        /// <summary>
        /// Register block pools before Start() is called (e.g. from an Awake bootstrapper).
        /// The pools array must match blockPrefabs in length and color order.
        /// </summary>
        public void SetBlockPools(BlockPool[] pools)
        {
            _poolsPerColor = pools;
        }

        /// <summary>
        /// Get the BlockPool that manages the given color. Returns null if not found.
        /// </summary>
        public BlockPool GetPoolForColor(BoxColor color)
        {
            if (_poolsPerColor == null) return null;
            for (int i = 0; i < _poolsPerColor.Length; i++)
            {
                if (_poolsPerColor[i] != null && _poolsPerColor[i].Color == color)
                    return _poolsPerColor[i];
            }
            return null;
        }

        /// <summary>
        /// Despawn a block back to its color pool if pooling is configured;
        /// otherwise fall back to a plain Destroy.  Call this instead of
        /// Destroy(block.gameObject) so blocks are recycled between level restarts.
        /// </summary>
        private void DespawnFromPool(Transform blockTransform)
        {
            if (blockTransform == null) return;

            var colored = blockTransform.GetComponent<ConveyorColoredBlock>();
            if (colored != null)
            {
                var pool = GetPoolForColor(colored.Color);
                if (pool != null)
                {
                    pool.Despawn(colored);
                    return;
                }
            }

            Destroy(blockTransform.gameObject);
        }

        private void Start()
        {
            if (splineContainer == null)
            {
                Debug.LogError($"{name}: SplineContainer is not assigned!");
                enabled = false;
                return;
            }

            ResolveVisualPalette();
            ResolveBlockPrefabs();

            // SAFETY: Force a strict clustered layout. The top conveyor must NEVER show
            // interleaved colors to the player, otherwise the gate match logic looks broken.
            layout = BlockLayout.Clustered;
            if (blocksPerCluster < 2)
            {
                Debug.LogWarning($"{name}: blocksPerCluster={blocksPerCluster} is too small to form a cluster. Forcing to 4.");
                blocksPerCluster = 4;
            }

            if (gateMatcher == null) gateMatcher = FindObjectOfType<GateMatcher>();

            BuildSplineCache();

            if (!spawnOnStart && _pendingConfig == null)
            {
                return;
            }

            if (blockPrefabs.Count == 0 || blockPrefabs[0].Prefab == null)
            {
                Debug.LogError($"{name}: No valid block prefabs resolved (need at least one entry with a Prefab).");
                enabled = false;
                return;
            }

            // If LevelLoader called SetupFromConfig() before Start(), honour that config
            // instead of the Inspector defaults.
            if (_pendingConfig != null)
            {
                Debug.Log($"{name}: applying deferred config from LevelLoader ({_pendingConfig.MapName}).");
                var deferred = _pendingConfig;
                _pendingConfig = null;
                SetupFromConfig(deferred);
                return;
            }

            // SAFETY: Cap blocksPerCluster so each cluster fits within a single arc of
            // the spline (1/paletteSize of the loop). Otherwise blocks wrap multiple
            // times and colors visually overlap, looking like interleaved spawning.
            // Required: blocksPerCluster * effectiveSpacing <= splineLength / paletteSize.
            // Use a generous spacing budget to keep gate smooth: max(moveSpeed * dissolveDuration, 0.5).
            {
                int palCount = Mathf.Max(blockPrefabs.Count, 1);
                float arcPerCluster = splineLength / palCount;
                float safetySpacing = Mathf.Max(Mathf.Max(0.1f, moveSpeed * dissolveDuration), 0.5f);
                int safetyClusterSize = Mathf.Max(2, Mathf.FloorToInt(arcPerCluster / safetySpacing));
                if (safetyClusterSize < 4) safetyClusterSize = 4; // need >= 4 to look like a cluster
                if (capBlocksPerClusterToSpline && blocksPerCluster > safetyClusterSize)
                {
                    Debug.LogWarning($"{name}: blocksPerCluster={blocksPerCluster} too large (would wrap). Capping to {safetyClusterSize} so clusters fit within {arcPerCluster:F2} units each.");
                    blocksPerCluster = safetyClusterSize;
                }
                else if (!capBlocksPerClusterToSpline)
                {
                    Debug.Log($"{name}: capBlocksPerClusterToSpline=false -> respecting Inspector blocksPerCluster={blocksPerCluster} (clusters may wrap if it exceeds {safetyClusterSize}).");
                }
            }

            // SAFETY: If blockCount is set, force it to be exactly
            // paletteSize * blocksPerCluster so every cluster cycle appears at the
            // same arc length on the spline and never wraps awkwardly.
            {
                int palCountF = blockPrefabs.Count;
                int desired = palCountF * blocksPerCluster;
                if (blockCount != desired)
                {
                    Debug.LogWarning($"{name}: forcing blockCount={blockCount} -> {desired} so clusters fit one palette rotation on the spline.");
                    blockCount = desired;
                }
            }

            ResolveGateDistance();
            SetupBlockPools();
            SpawnBlocks();
        }

        /// <summary>
        /// Auto-create BlockPool components matching the resolved blockPrefabs palette.
        /// Called from Start() after ResolveBlockPrefabs() so we know the palette size.
        /// If _poolsPerColor is already set in the Inspector, that array is respected.
        /// </summary>
        private void SetupBlockPools()
        {
            if (blockPrefabs == null || blockPrefabs.Count == 0)
            {
                Debug.LogWarning($"{name}: blockPrefabs is empty, skipping pool setup.");
                return;
            }

            if (_poolsPerColor != null && _poolsPerColor.Length == blockPrefabs.Count)
            {
                // Inspector-assigned pools: just refresh their color and warm size.
                for (int i = 0; i < _poolsPerColor.Length; i++)
                {
                    if (_poolsPerColor[i] != null)
                    {
                        _poolsPerColor[i].SetColor(blockPrefabs[i].Color);
                        _poolsPerColor[i].SetVisualPalette(visualPalette);
                    }
                }
                return;
            }

            // Auto-create pools if Inspector didn't pre-assign them.
            // Remove any stale BlockPools first so we don't double-up.
            var existing = GetComponents<BlockPool>();
            for (int i = existing.Length - 1; i >= 0; i--) Destroy(existing[i]);

            _poolsPerColor = new BlockPool[blockPrefabs.Count];
            for (int i = 0; i < blockPrefabs.Count; i++)
            {
                var entry = blockPrefabs[i];
                if (entry == null || entry.Prefab == null) continue;

                var poolGO = new GameObject($"BlockPool_{entry.Color}");
                poolGO.transform.SetParent(transform);
                var pool = poolGO.AddComponent<BlockPool>();

                // Set prefab and color; BlockPool.CreateAndWrap() handles
                // adding ConveyorColoredBlock at spawn-time so every returned
                // block is guaranteed valid regardless of the prefab's contents.
                pool.Prefab = entry.Prefab;
                pool.SetColor(entry.Color);
                pool.SetVisualPalette(visualPalette);
                pool.WarmSize = Mathf.Max(blocksPerCluster, 4);

                _poolsPerColor[i] = pool;
            }

            Debug.Log($"{name}: auto-created {_poolsPerColor.Length} BlockPools (warmSize={blocksPerCluster}).");
        }

        /// <summary>
        /// Convert gateAnchor world position into a spline-arc distance so we can detect
        /// when a moving block has reached the gate. Falls back to ~75% of the spline
        /// length if no anchor is set or the anchor isn't near the spline.
        /// </summary>
        private void ResolveGateDistance()
        {
            gateDistance = -1f;

            if (splineLength <= 0f) return;

            if (gateAnchor == null)
            {
                gateDistance = splineLength * 0.75f;
                return;
            }

            Vector3 anchorPos = gateAnchor.position;
            float bestDistance = 0f;
            float bestSqr = float.MaxValue;

            Vector3 prev = splineContainer.EvaluatePosition(splineIndex, 0f);
            float running = 0f;

            for (int i = 1; i <= sampleCount; i++)
            {
                float t = (float)i / sampleCount;
                Vector3 pos = splineContainer.EvaluatePosition(splineIndex, t);
                running += Vector3.Distance(prev, pos);

                float sqr = (pos - anchorPos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    bestDistance = running;
                }

                prev = pos;
            }

            gateDistance = Mathf.Clamp(bestDistance, 0f, splineLength);

            if (_logGateDebug)
                Debug.Log($"{name}: gate anchor '{gateAnchor.name}' projected to spline distance {gateDistance:F2}/{splineLength:F2}");
        }

        [Header("Debug")]
        [SerializeField] private bool _logGateDebug = true;

        /// <summary>
        /// Pull the color palette from GridManager if autoSyncFromGridManager is on and the
        /// list is still empty. Otherwise keep the manually-assigned blockPrefabs.
        /// </summary>
        private void ResolveVisualPalette()
        {
            if (visualPalette != null)
            {
                return;
            }

            var gm = FindObjectOfType<FlowBlast.Gameplay.Grid.GridManager>();
            if (gm != null)
            {
                visualPalette = gm.GetBoxVisualPalette();
            }
        }

        private void ResolveBlockPrefabs()
        {
            if (!autoSyncFromGridManager) return;
            if (blockPrefabs != null && blockPrefabs.Count > 0 && blockPrefabs[0].Prefab != null)
                return; // user assigned manually, respect that

            // Find any GridManager in the scene to read AvailableColors from its SO.
            var gm = FindObjectOfType<FlowBlast.Gameplay.Grid.GridManager>();
            if (gm == null)
            {
                Debug.LogWarning($"{name}: autoSyncFromGridManager is on but no GridManager was found; leaving blockPrefabs empty.");
                return;
            }

            if (visualPalette == null)
            {
                visualPalette = gm.GetBoxVisualPalette();
            }

            var palette = gm.GetAvailableColors();
            if (palette == null || palette.Count == 0) return;

            // Try to find a "neutral" ball prefab by name (Ball4/ball) so we still create blocks
            // even if no ColoredBlockPrefab entries are configured. Fallback to any child of this
            // object named like a ball, else to blockPrefabs[0] if set, else nothing.
            GameObject defaultPrefab = FindFallbackBallPrefab();
            if (defaultPrefab == null)
            {
                Debug.LogWarning($"{name}: autoSyncFromGridManager found {palette.Count} colors, but no fallback ball prefab available. Assign blockPrefabs manually.");
                return;
            }

            if (blockPrefabs == null) blockPrefabs = new List<ColoredBlockPrefab>();
            blockPrefabs.Clear();
            for (int i = 0; i < palette.Count; i++)
            {
                blockPrefabs.Add(new ColoredBlockPrefab { Prefab = defaultPrefab, Color = palette[i] });
            }

            // Override blockCount so that one block is spawned per color when auto-sync is active.
            if (blockCount <= 0) blockCount = palette.Count;

            Debug.Log($"{name}: auto-sync enabled - spawned {blockPrefabs.Count} colored blocks (one per BoxColor in palette).");
        }

        private GameObject FindFallbackBallPrefab()
        {
            // Prefer a prefab reference already on the component (legacy single-prefab usage).
            if (blockPrefabs != null)
            {
                for (int i = 0; i < blockPrefabs.Count; i++)
                {
                    if (blockPrefabs[i] != null && blockPrefabs[i].Prefab != null)
                        return blockPrefabs[i].Prefab;
                }
            }

            // Try to find box.prefab in the scene / project — it has BoxTapMover + BoxCollider
            // and is guaranteed to have a visible mesh. We add ConveyorColoredBlock at spawn time
            // so no prefab modification is needed.
#if UNITY_EDITOR
            var boxPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/box.prefab");
            if (boxPrefab != null) return boxPrefab;
#endif

            // Legacy: look for Ball4.fbx (FBX mesh, no GameObject-level components).
            // Note: FBX can't be instantiated directly as a GameObject - this will return null.
            // Keeping for reference only; use box.prefab above instead.
#if UNITY_EDITOR
            var ball4 = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Model/Ball4.fbx");
            if (ball4 != null) return ball4;
#endif

            // Search the scene for a fallback ball-like object to clone.
            var existing = GameObject.Find("Ball4");
            if (existing != null) return existing;

            return null;
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

        /// <summary>
        /// Apply a BoxColor to a Renderer freshly spawned on the conveyor.
        /// Uses MaterialPropertyBlock so we don't leak material instances per ball.
        /// </summary>
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");

        internal static void ApplyColorToRenderer(Renderer rend, BoxColor color, BoxVisualPaletteSO palette = null)
        {
            if (rend == null) return;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);

            if (palette == null)
            {
                return;
            }

            if (palette.SharedMaterial != null)
            {
                rend.sharedMaterial = palette.SharedMaterial;
            }

            if (palette.TryGetTexture(color, out Texture texture))
            {
                mpb.SetTexture(BaseMapId, texture);
                mpb.SetTexture(MainTexId, texture);
                rend.SetPropertyBlock(mpb);
            }
        }

        private void SpawnBlocks()
        {
            // GUARD: if Start() already spawned blocks before SetupFromConfig() was called,
            // wipe them so we don't end up with two overlapping conveyor loops.
            if (blocks.Count > 0)
            {
                Debug.LogWarning($"{name}: SpawnBlocks called with {blocks.Count} existing blocks — clearing before re-spawn (likely Start() ran before LoadLevel()).");
                for (int i = 0; i < blocks.Count; i++)
                {
                    if (blocks[i] != null)
                        DespawnFromPool(blocks[i]);
                }
                blocks.Clear();
                blockDistances.Clear();
                blockGateFired.Clear();
                blockIndicesByColor.Clear();
                colorsDone.Clear();
            }

            int paletteSize = Mathf.Max(blockPrefabs.Count, 1);

            // When clustered, we want exactly (palette * blocksPerCluster) total if blockCount
            // wasn't set explicitly larger. Otherwise keep blockCount and just cycle the
            // clustered pattern through enough color rotations to fill it.
            int effectiveClusterStep = layout switch
            {
                BlockLayout.Interleaved => 1,
                _                        => Mathf.Max(blocksPerCluster, 1),
            };

            int total = blockCount;
            // For clustered layouts, clamp so we land on clean cluster boundaries IF user
            // left blockCount at 0; otherwise respect their number.
            if (blockCount <= 0 && effectiveClusterStep > 0)
            {
                total = paletteSize * effectiveClusterStep;
            }

            // To make clusters look joined like the reference game, fit `total` blocks
            // evenly along the spline when the user hasn't already chosen a custom spacing.
            // (set spacing to a very small value via Inspector to opt out.)
            //
            // Also enforce a MINIMUM spacing based on moveSpeed so that two consecutive
            // blocks can never reach the gate faster than the dissolve animation takes
            // (otherwise we get visible "skip" because two balls dissolve on top of each
            // other within a single dissolve duration).
            float minGateSpacing = Mathf.Max(0.1f, moveSpeed * dissolveDuration);
            float autoFit = splineLength / Mathf.Max(1, total);
            float effectiveSpacing = spacing;
            if (autoFit < effectiveSpacing) effectiveSpacing = autoFit;
            if (effectiveSpacing < minGateSpacing) effectiveSpacing = minGateSpacing;

            // CLUSTER WRAP GUARD: For Clustered layouts, force each cluster of
            // `effectiveClusterStep` blocks to fit within a single arc of the spline
            // (a fraction `1/paletteSize` of the loop). Otherwise blocks wrap the
            // spline multiple times and the colors visually overlap, making it look
            // like the spawn is interleaved.
            if (layout == BlockLayout.Clustered || layout == BlockLayout.ClusteredAnyOrder)
            {
                float clusterArc = splineLength / Mathf.Max(1, paletteSize);
                float maxClusterSpacing = clusterArc / Mathf.Max(1, effectiveClusterStep);
                // Only shrink if currently too large; never grow beyond Inspector spacing.
                if (effectiveSpacing > maxClusterSpacing)
                {
                    effectiveSpacing = Mathf.Max(0.1f, maxClusterSpacing);
                    Debug.LogWarning($"{name}: spacing would cause clusters to wrap. Clamping to {effectiveSpacing:F2} so each cluster fits in {clusterArc:F2} units of spline ({paletteSize} clusters).");
                }
            }

            for (int i = 0; i < total; i++)
            {
                int prefabIndex;
                switch (layout)
                {
                    case BlockLayout.Interleaved:
                        prefabIndex = i % paletteSize;
                        break;
                    case BlockLayout.ClusteredAnyOrder:
                        {
                            // Group blocks into clusters of `effectiveClusterStep`, but pick a
                            // single (possibly random) color per cluster. Use a stable
                            // per-cluster hash so each cluster sticks to one color.
                            int clusterIndex = i / effectiveClusterStep;
                            int hash = (int)(clusterIndex * 73856093) ^ unchecked((int)0x9E3779B1);
                            int pick = Mathf.Abs(hash) % paletteSize;
                            // For the very first cluster, force it to palette[0] so the
                            // cluster at the front of the spline has a deterministic color.
                            if (clusterIndex == 0) pick = 0;
                            prefabIndex = pick;
                            break;
                        }
                    case BlockLayout.Clustered:
                    default:
                        {
                            int clusterIndex = i / effectiveClusterStep;
                            prefabIndex = clusterIndex % paletteSize;
                            break;
                        }
                }

                var entry = blockPrefabs[prefabIndex];
                if (entry == null || entry.Prefab == null) continue;

                // Try to retrieve from the corresponding color pool; fall back to Instantiate.
                ConveyorColoredBlock coloredComp;
                GameObject blockObject;

                // Short-circuit if pool was permanently disabled by a prior CreateInstance() failure.
                // _prefabIsValid is set inside ComponentPool.Spawn(), but this check guards
                // against stale references that were never nulled out.
                if (_poolsPerColor != null && prefabIndex < _poolsPerColor.Length
                    && _poolsPerColor[prefabIndex] != null
                    && _poolsPerColor[prefabIndex].Prefab != null)
                {
                    var pooled = _poolsPerColor[prefabIndex].Spawn();
                    if (pooled != null)
                    {
                        pooled.transform.SetParent(transform);
                        coloredComp = pooled;
                        blockObject = pooled.gameObject;
                    }
                    else
                    {
                        // Pool can't serve this color (prefab lacks ConveyorColoredBlock or
                        // was permanently disabled). Null it out so Instantiate fallback fires.
                        _poolsPerColor[prefabIndex] = null;
                        blockObject = Instantiate(entry.Prefab, transform);
                        coloredComp = blockObject.GetComponent<ConveyorColoredBlock>();
                        if (coloredComp == null) coloredComp = blockObject.AddComponent<ConveyorColoredBlock>();
                    }
                }
                else
                {
                    blockObject = Instantiate(entry.Prefab, transform);
                    coloredComp = blockObject.GetComponent<ConveyorColoredBlock>();
                    if (coloredComp == null) coloredComp = blockObject.AddComponent<ConveyorColoredBlock>();
                }

                Transform blockTransform = coloredComp.transform;

                float startDistance = i * effectiveSpacing;

                blocks.Add(blockTransform);
                blockDistances.Add(startDistance);
                blockGateFired.Add(false);

                if (!blockIndicesByColor.TryGetValue(entry.Color, out var colorList))
                {
                    colorList = new List<int>();
                    blockIndicesByColor[entry.Color] = colorList;
                }
                colorList.Add(blocks.Count - 1);

                // coloredComp was already retrieved from the pool (or created fresh).
                // The pool's OnSpawn callback already called ResetBlock → SetColor,
                // but we re-apply here so the fallback Instantiate path also tints correctly.
                coloredComp.SetColor(entry.Color);

                // Attach a BlockHandle so GateMatcher can mark this block as consumed.
                var handle = blockObject.GetComponent<BlockHandle>();
                if (handle == null) handle = blockObject.AddComponent<BlockHandle>();

                // Tint all renderers on the spawned block to match the color.
                var renderers = blockObject.GetComponentsInChildren<Renderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    ApplyColorToRenderer(renderers[r], entry.Color, visualPalette);
                }

                UpdateBlockTransform(blockTransform, startDistance);
            }

            // Debug: dump the color sequence to Console so we can verify clusters are clean.
            var sb = new System.Text.StringBuilder();
            sb.Append($"{name}: spawned {blocks.Count} blocks (layout={layout}, paletteSize={paletteSize}, blocksPerCluster={blocksPerCluster}, blockCount={blockCount}, effectiveSpacing={effectiveSpacing:F2}): ");
            for (int i = 0; i < blocks.Count; i++)
            {
                var col = blocks[i].GetComponent<ConveyorColoredBlock>();
                sb.Append(col != null ? col.Color.ToString() : "?");
                if (i < blocks.Count - 1) sb.Append(", ");
            }
            Debug.Log(sb.ToString());
        }

        private void UpdateBlockDistances()
        {
            for (int i = 0; i < blockDistances.Count; i++)
            {
                if (blocks[i] == null) continue;

                // Skip blocks the gate has marked consumed (dissolve effect handles destruction).
                var handle = blocks[i].GetComponent<BlockHandle>();
                if (handle != null && handle.IsConsumed) continue;

                float prevDistance = blockDistances[i];

                blockDistances[i] += moveSpeed * Time.deltaTime;

                if (loop)
                {
                    blockDistances[i] %= splineLength;
                }
                else
                {
                    blockDistances[i] = Mathf.Clamp(blockDistances[i], 0f, splineLength);
                }

                // Detect crossing the gate exactly once per loop revolution.
                if (gateDistance >= 0f && !blockGateFired[i])
                {
                    // prev < gateDist < new means we just passed the gate this frame.
                    if (prevDistance < gateDistance && blockDistances[i] >= gateDistance)
                    {
                        blockGateFired[i] = true;
                        FireGateMatch(blocks[i]);
                    }
                }

                // Reset the "fired" flag once the block has lapped back past 0
                // so it can fire again on the next revolution.
                if (loop && blockDistances[i] < prevDistance)
                {
                    blockGateFired[i] = false;
                }
            }
        }

        private void FireGateMatch(Transform blockTransform)
        {
            if (gateMatcher == null) return;
            var colored = blockTransform.GetComponent<ConveyorColoredBlock>();
            if (colored == null) return;
            gateMatcher.OnTopBlockReachedGate(blockTransform.gameObject, colored.Color);
        }

        private void UpdateBlockPositions()
        {
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] == null) continue;

                // Do not overwrite the dissolve animation's scale/transform progress.
                var handle = blocks[i].GetComponent<BlockHandle>();
                if (handle != null && handle.IsConsumed) continue;

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

        /// <summary>
        /// Mark every block of a given color as "no longer needed". We do NOT
        /// destroy them immediately - that would cause mid-flight balls to pop
        /// out of the spline. Instead:
        ///   * blocks at the gate will be dissolved naturally by GateMatcher
        ///     on their next pass (when colorsDone contains the color).
        ///   * blocks mid-spline keep moving until they reach the gate.
        /// Result: no skip frames, no jitter; the conveyor just drains itself.
        /// </summary>
        public void MarkColorDone(BoxColor color)
        {
            if (colorsDone.Contains(color)) return;
            colorsDone.Add(color);

            int alive = 0;
            if (blockIndicesByColor.TryGetValue(color, out var indices))
            {
                for (int k = 0; k < indices.Count; k++)
                {
                    int i = indices[k];
                    if (i >= 0 && i < blocks.Count && blocks[i] != null) alive++;
                }
            }

            if (_logGateDebug)
                Debug.Log($"{name}: color {color} marked DONE - {alive} surviving blocks will dissolve naturally when they reach the gate.");
        }

        /// <summary>
        /// True if the given color's slots have all been completed and any incoming
        /// block of that color should be consumed at the gate (no slot check needed).
        /// </summary>
        public bool IsColorDone(BoxColor color) => colorsDone.Contains(color);

        /// <summary>
        /// Current effective blocks-per-cluster (after safety capping). Each color cluster
        /// on the top spline contains this many blocks before the color rolls over.
        /// </summary>
        public int BlocksPerCluster => blocksPerCluster;

        /// <summary>
        /// Number of distinct colors in the active palette (blockPrefabs.Count).
        /// </summary>
        public int PaletteSize => blockPrefabs != null ? blockPrefabs.Count : 0;

        /// <summary>
        /// Each top ball that matches a slot's color adds (100 / BlocksPerCluster) percent
        /// progress, so exactly BlocksPerCluster matches fill a container to 100%.
        /// Falls back to 20 if BlocksPerCluster &lt;= 0.
        /// </summary>
        public float PerBallProgressPercent
        {
            get
            {
                int n = Mathf.Max(blocksPerCluster, 1);
                return 100f / n;
            }
        }

        public void CollectActiveBlocksByColor(BoxColor color, List<ConveyorColoredBlock> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            for (int i = 0; i < blocks.Count; i++)
            {
                Transform blockTransform = blocks[i];
                if (blockTransform == null || !blockTransform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                BlockHandle handle = blockTransform.GetComponent<BlockHandle>();
                if (handle != null && handle.IsConsumed)
                {
                    continue;
                }

                ConveyorColoredBlock coloredBlock = blockTransform.GetComponent<ConveyorColoredBlock>();
                if (coloredBlock != null && coloredBlock.Color == color)
                {
                    results.Add(coloredBlock);
                }
            }
        }

        public bool MarkBlockConsumedForMagnet(ConveyorColoredBlock coloredBlock)
        {
            if (coloredBlock == null)
            {
                return false;
            }

            Transform blockTransform = coloredBlock.transform;
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != blockTransform)
                {
                    continue;
                }

                BlockHandle handle = blockTransform.GetComponent<BlockHandle>();
                if (handle != null && handle.IsConsumed)
                {
                    return false;
                }

                if (handle == null)
                {
                    handle = blockTransform.gameObject.AddComponent<BlockHandle>();
                }

                handle.MarkConsumed();
                blocks[i] = null;
                return true;
            }

            return false;
        }

        /// <summary>
        /// World-space tangent on the spline closest to a given world position.
        /// Used by BlockDissolveEffect to make a consumed block visually "pour"
        /// forward along the spline instead of vanishing in place.
        /// Returns Vector3.zero if the spline has not been initialized.
        /// </summary>
        public Vector3 EvaluateWorldTangent(Vector3 worldPosition)
        {
            if (splineContainer == null || splineLength <= 0f || cumulativeTs.Count == 0)
                return Vector3.zero;

            // Find the spline sample closest to worldPosition.
            float bestSqr = float.MaxValue;
            int bestIndex = 0;
            Vector3 prev = splineContainer.EvaluatePosition(splineIndex, cumulativeTs[0]);
            float running = 0f;

            // Walk segments between cached samples to find nearest point.
            float bestDist = 0f;
            float bestRunning = 0f;
            float bestRunningAfter = 0f;
            Vector3 bestBefore = prev;
            Vector3 bestAfter = prev;

            for (int i = 1; i < cumulativeTs.Count; i++)
            {
                Vector3 after = splineContainer.EvaluatePosition(splineIndex, cumulativeTs[i]);
                float segLen = Vector3.Distance(prev, after);
                float runningAfter = running + segLen;

                Vector3 ab = after - prev;
                float ab2 = ab.sqrMagnitude;
                if (ab2 > 0.0001f)
                {
                    float u = Mathf.Clamp01(Vector3.Dot(worldPosition - prev, ab) / ab2);
                    Vector3 closest = prev + ab * u;
                    float sqr = (worldPosition - closest).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        bestIndex = i;
                        bestDist = running + segLen * u;
                        bestRunning = running;
                        bestRunningAfter = runningAfter;
                        bestBefore = prev;
                        bestAfter = after;
                    }
                }

                running = runningAfter;
                prev = after;
            }

            Vector3 dir = bestAfter - bestBefore;
            if (dir.sqrMagnitude < 0.0001f)
                return Vector3.forward;
            return dir.normalized;
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
                    // Try to return blocks to their pools; fall back to Destroy.
                    DespawnFromPool(blocks[i]);
                }
            }

            blocks.Clear();
            blockDistances.Clear();
            blockGateFired.Clear();
            colorsDone.Clear();
            blockIndicesByColor.Clear();

            BuildSplineCache();
            SpawnBlocks();
        }

        /// <summary>
        /// Setup this conveyor from a level config ScriptableObject.
        /// Called by LevelLoader to drive the conveyor entirely from SO data.
        /// Resolves block prefabs from GridManager, then spawns blocks.
        /// </summary>
        public void SetupFromConfig(GridMapDataSO config)
        {
            if (config == null)
            {
                Debug.LogWarning($"{name}: SetupFromConfig received null config.");
                return;
            }

            // Stop current blocks before reconfiguring.
            for (int i = 0; i < blocks.Count; i++)
            {
                if (blocks[i] != null)
                    DespawnFromPool(blocks[i]);
            }
            blocks.Clear();
            blockDistances.Clear();
            blockGateFired.Clear();
            colorsDone.Clear();
            blockIndicesByColor.Clear();

            // GUARD: if splineLength is 0 (Start() hasn't run yet), defer to Start()
            // which will call BuildSplineCache() + SpawnBlocks(). Skip full setup here.
            if (splineLength <= 0f)
            {
                Debug.Log($"{name}: SetupFromConfig called before Start() — deferring full init to Start().");
                _pendingConfig = config;
                return;
            }
            _pendingConfig = null;

            // Apply conveyor parameters from SO.
            visualPalette = config.VisualPalette;
            moveSpeed = config.BlockSpeed;
            blocksPerCluster = config.BlocksPerCluster;
            BlocksPerSecond = config.BlocksPerSecond;

            // Auto-resolve block prefabs from GridManager so palette matches the level.
            autoSyncFromGridManager = true;
            blockPrefabs.Clear();
            ResolveBlockPrefabs();

            if (blockPrefabs.Count == 0 || blockPrefabs[0].Prefab == null)
            {
                Debug.LogWarning($"{name}: SetupFromConfig could not resolve blockPrefabs.");
                return;
            }

            // Ensure clustered layout (matches Start() safety).
            layout = BlockLayout.Clustered;
            if (blocksPerCluster < 2)
                blocksPerCluster = 4;

            // SAFETY: Clamp blocksPerCluster so clusters don't wrap the spline.
            if (splineLength > 0f && capBlocksPerClusterToSpline)
            {
                int palCount = Mathf.Max(blockPrefabs.Count, 1);
                float arcPerCluster = splineLength / palCount;
                float safetySpacing = Mathf.Max(Mathf.Max(0.1f, moveSpeed * dissolveDuration), 0.5f);
                int safetyClusterSize = Mathf.Max(2, Mathf.FloorToInt(arcPerCluster / safetySpacing));
                if (safetyClusterSize < 4) safetyClusterSize = 4;
                if (blocksPerCluster > safetyClusterSize)
                    blocksPerCluster = safetyClusterSize;
            }

            // Force blockCount to clean cluster boundaries.
            int paletteSize = blockPrefabs.Count;
            int desired = paletteSize * blocksPerCluster;
            if (blockCount != desired)
                blockCount = desired;

            Debug.Log($"{name}: SetupFromConfig — speed={moveSpeed}, cluster={blocksPerCluster}, " +
                      $"paletteSize={paletteSize}, blockCount={blockCount}, " +
                      $"blocksPerSecond={BlocksPerSecond}");

            BuildSplineCache();
            ResolveGateDistance();
            SetupBlockPools();
            SpawnBlocks();
        }

        /// <summary>
        /// Current spawn rate from the level config (blocks per second).
        /// Used by LevelLoader to sync with config values.
        /// </summary>
        public float BlocksPerSecond { get; private set; } = 1f;
    }
}
