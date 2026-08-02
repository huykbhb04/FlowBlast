using System;
using System.Collections.Generic;
using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Dedicated pool for top blocks on the SplineConveyor. Each BlockPool is
    /// responsible for one specific BoxColor so SplineConveyor can query the right
    /// pool when spawning or recycling a block of a given color.
    ///
    /// This pool manages its own GameObject queue (independent of ComponentPool)
    /// so that it can handle prefabs that don't carry ConveyorColoredBlock at
    /// edit-time — we AddComponent at spawn-time on the cloned instance.
    ///
    /// Pool lifecycle:
    ///   Warm()      -> pre-instantiate WarmSize inactive instances
    ///   Spawn()     -> GetFromQueue / Instantiate + AttachComponent + ResetBlock
    ///   Despawn()   -> Deactivate + ResetHandle + Enqueue
    /// </summary>
    public class BlockPool : MonoBehaviour
    {
        [Header("Block Pool Config")]
        [Tooltip("Which BoxColor this pool manages. Must match one entry in SplineConveyor's blockPrefabs list.")]
        [SerializeField] private BoxColor _color = BoxColorUtility.DefaultColor;

        [Tooltip("GameObject prefab to instantiate when the pool needs to grow. " +
                 "May or may not have ConveyorColoredBlock — we handle that at spawn time.")]
        [SerializeField] private GameObject _prefab;

        [SerializeField] private BoxVisualPaletteSO _visualPalette;

        [Tooltip("Number of instances to pre-warm on Start().")]
        [SerializeField] private int _warmSize = 4;

        public int WarmSize
        {
            get => _warmSize;
            set => _warmSize = value;
        }

        [Header("Debug")]
        [SerializeField] private bool _logStats;

        private readonly Queue<ConveyorColoredBlock> _available = new Queue<ConveyorColoredBlock>();
        private int _totalSpawned;
        private bool _prewarmFailed;

        /// <summary>Which color this pool handles.</summary>
        public BoxColor Color => _color;
        public GameObject Prefab
        {
            get => _prefab;
            set => _prefab = value;
        }

        /// <summary>Public accessor matching ComponentPool API.</summary>
        public int AvailableCount => _available.Count;
        public int TotalSpawned => _totalSpawned;

        public void SetColor(BoxColor color) => _color = color;
        public void SetVisualPalette(BoxVisualPaletteSO visualPalette) => _visualPalette = visualPalette;

        private void Start()
        {
            Warm(_warmSize);
        }

        /// <summary>
        /// Pre-instantiate count inactive instances so the first few Spawn() calls
        /// are allocation-free.
        /// </summary>
        public void Warm(int count)
        {
            if (_prefab == null || _prewarmFailed) return;
            for (int i = 0; i < count; i++)
            {
                var instance = CreateAndWrap();
                if (instance != null)
                {
                    instance.gameObject.SetActive(false);
                    _available.Enqueue(instance);
                }
            }
        }

        /// <summary>
        /// Retrieve a block. If the queue is empty a new one is created.
        /// If the prefab lacked ConveyorColoredBlock at edit-time, we add it
        /// to the cloned instance here so every returned block is valid.
        /// </summary>
        public ConveyorColoredBlock Spawn()
        {
            ConveyorColoredBlock block;

            if (_available.Count > 0)
            {
                block = _available.Dequeue();
            }
            else
            {
                block = CreateAndWrap();
            }

            if (block == null)
            {
                _prewarmFailed = true;
                Debug.LogError($"[BlockPool<{_color}>] CreateAndWrap() failed — prefab '{_prefab?.name}' " +
                               "is invalid or lacks a Renderer. Pool disabled.");
                return null;
            }

            block.gameObject.SetActive(true);
            _totalSpawned++;

            // Reset block state for reuse.
            block = ResetBlock(block);

            if (_logStats)
            {
                Debug.Log($"[BlockPool<{_color}>] Spawn #{_totalSpawned}, available={AvailableCount}");
            }

            return block;
        }

        /// <summary>
        /// Return a block to the pool. OnDespawn fires first for cleanup.
        /// </summary>
        public void Despawn(ConveyorColoredBlock block)
        {
            if (block == null) return;

            OnBlockDespawn(block);
            block.gameObject.SetActive(false);
            _available.Enqueue(block);

            if (_logStats)
            {
                Debug.Log($"[BlockPool<{_color}>] Despawn, available={AvailableCount}");
            }
        }

        /// <summary>
        /// Return a block to the pool after a short delay. Useful when an
        /// animation finishes and we want to wait before recycling.
        /// </summary>
        public void DespawnWithDelay(ConveyorColoredBlock block, float delaySeconds)
        {
            if (block == null) return;
            StartCoroutine(DespawnAfterSeconds(block, delaySeconds));
        }

        private System.Collections.IEnumerator DespawnAfterSeconds(ConveyorColoredBlock block, float delay)
        {
            yield return new WaitForSeconds(delay);
            Despawn(block);
        }

        /// <summary>
        /// Instantiate the prefab and attach ConveyorColoredBlock if missing.
        /// Returns the ConveyorColoredBlock component on the new instance.
        /// </summary>
        private ConveyorColoredBlock CreateAndWrap()
        {
            if (_prefab == null) return null;

            GameObject go = Instantiate(_prefab, transform);
            // Strip any existing color suffix so we always name fresh instances correctly.
            string baseName = go.name;
            foreach (BoxColor c in Enum.GetValues(typeof(BoxColor)))
                baseName = baseName.Replace("_" + c, "");
            go.name = baseName + "_" + _color;

            // If the prefab doesn't carry ConveyorColoredBlock (e.g. plain box.prefab),
            // add it here so the pool is always guaranteed to return valid blocks.
            var cc = go.GetComponent<ConveyorColoredBlock>();
            if (cc == null)
            {
                cc = go.AddComponent<ConveyorColoredBlock>();
                cc.SetColor(_color);
                Debug.Log($"[BlockPool<{_color}>] AddComponent<ConveyorColoredBlock> to instance '{go.name}'.");
            }

            return cc;
        }

        /// <summary>
        /// Reset all per-instance state so the block is ready for a fresh use.
        /// </summary>
        private ConveyorColoredBlock ResetBlock(ConveyorColoredBlock block)
        {
            if (block == null) return null;

            // Ensure correct color tag
            block.SetColor(_color);

            // Reset consumed flag so the block can be matched again
            var handle = block.GetComponent<BlockHandle>();
            if (handle != null) handle.Reset();

            // Re-apply color to all renderers so the block has the correct tint
            var renderers = block.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SplineConveyor.ApplyColorToRenderer(renderers[i], _color, _visualPalette);
            }

            // Reset scale to (1,1,1) so a mid-dissolve scale doesn't persist into reuse
            block.transform.localScale = Vector3.one;

            return block;
        }

        /// <summary>
        /// Pre-recycle cleanup: stop active dissolve/movement coroutines.
        /// </summary>
        private void OnBlockDespawn(ConveyorColoredBlock block)
        {
            if (block == null) return;

            var dissolve = block.GetComponent<BlockDissolveEffect>();
            if (dissolve != null) dissolve.StopAllCoroutines();

            var tap = block.GetComponent<BoxTapMover>();
            if (tap != null) tap.StopAllCoroutines();
        }

        /// <summary>
        /// Destroy every inactive instance and clear the queue.
        /// </summary>
        public void Purge()
        {
            while (_available.Count > 0)
            {
                var block = _available.Dequeue();
                if (block != null) Destroy(block.gameObject);
            }
        }
    }
}
