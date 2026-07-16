using System;
using System.Collections.Generic;
using UnityEngine;

namespace FlowBlast.Common
{
    /// <summary>
    /// Generic MonoBehaviour object pool. Maintains a warm queue of inactive GameObject
    /// instances and lazily expands if demand exceeds supply. Every retrieved instance has
    /// its OnSpawn callback fired so callers can reset position, color, scale, etc.
    /// Every returned instance fires OnDespawn (if set) before going back into the queue.
    ///
    /// The pool stores GameObjects internally (for cheap SetActive), but exposes T via
    /// the Spawn/Despawn API so callers get typed references without casting.
    ///
    /// Usage:
    ///   var pool = gameObject.AddComponent&lt;ComponentPool&lt;T&gt;&gt;();
    ///   pool.Prefab    = myGameObjectPrefab;       // GameObject prefab
    ///   pool.WarmSize  = 10;
    ///   pool.OnSpawn   = ResetState;               // Func&lt;T, T&gt;
    ///
    ///   T obj = pool.Spawn();
    ///   // use obj ...
    ///   pool.Despawn(obj);
    /// </summary>
    public class ComponentPool<T> : MonoBehaviour where T : Component
    {
        [Tooltip("GameObject prefab to instantiate when the pool needs to grow. " +
                 "Must contain a T component.")]
        public GameObject Prefab;

        [Tooltip("Number of instances to pre-warm on Start().")]
        public int WarmSize = 10;

        [Tooltip("Called every time an instance is retrieved from the pool. " +
                 "Use this to reset position, color, scale, animation state, etc. " +
                 "Return the instance (usually the same object).")]
        public Func<T, T> OnSpawn;

        [Tooltip("Called just before an instance is returned to the pool. " +
                 "Use this for cleanup that must happen before reuse. " +
                 "The instance is still fully active when this fires.")]
        public Action<T> OnDespawn;

        [Header("Debug")]
        [SerializeField] private bool _logStats;

        private readonly Queue<T> _available = new Queue<T>();
        private int _totalSpawned;
        private bool _prefabIsValid = true; // false once CreateInstance() fails permanently

        public int AvailableCount => _available.Count;
        public int TotalSpawned => _totalSpawned;

        private void Start()
        {
            Warm(WarmSize);
        }

        /// <summary>
        /// Pre-instantiate count inactive instances so the first few Spawn() calls
        /// are allocation-free.
        /// </summary>
        public void Warm(int count)
        {
            if (Prefab == null || !_prefabIsValid) return;
            for (int i = 0; i < count; i++)
            {
                T instance = CreateInstance();
                if (instance != null)
                {
                    instance.gameObject.SetActive(false);
                    _available.Enqueue(instance);
                }
            }
        }

        /// <summary>
        /// Retrieve an instance. If the pool is empty a new one is created.
        /// OnSpawn is called so the caller can reset state before use.
        /// </summary>
        public T Spawn()
        {
            if (!_prefabIsValid)
                return null;

            T instance;

            if (_available.Count > 0)
            {
                instance = _available.Dequeue();
            }
            else
            {
                instance = CreateInstance();
            }

            if (instance == null)
            {
                _prefabIsValid = false;
                Debug.LogError($"[ComponentPool<{typeof(T).Name}>] Failed to create instance — prefab '{Prefab?.name}' is invalid. Pool disabled.");
                return null;
            }

            instance.gameObject.SetActive(true);
            _totalSpawned++;

            if (OnSpawn != null)
            {
                instance = OnSpawn(instance);
            }

            if (_logStats)
            {
                Debug.Log($"[ComponentPool<{typeof(T).Name}>] Spawn #{_totalSpawned}, " +
                          $"available={AvailableCount}");
            }

            return instance;
        }

        /// <summary>
        /// Return an instance to the pool. OnDespawn is called first for cleanup.
        /// The GameObject is deactivated immediately so it disappears visually.
        /// </summary>
        public void Despawn(T instance)
        {
            if (instance == null) return;

            OnDespawn?.Invoke(instance);
            instance.gameObject.SetActive(false);
            _available.Enqueue(instance);

            if (_logStats)
            {
                Debug.Log($"[ComponentPool<{typeof(T).Name}>] Despawn, " +
                          $"available={AvailableCount}");
            }
        }

        /// <summary>
        /// Return an instance to the pool after a short delay. Useful when an
        /// animation finishes and we want to wait a frame before recycling.
        /// </summary>
        public void DespawnWithDelay(T instance, float delaySeconds)
        {
            if (instance == null) return;
            StartCoroutine(DespawnAfterSeconds(instance, delaySeconds));
        }

        private System.Collections.IEnumerator DespawnAfterSeconds(T instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            Despawn(instance);
        }

        /// <summary>
        /// Destroy every inactive instance and clear the queue.
        /// Active instances are left alone (call Despawn on them first).
        /// </summary>
        public void Purge()
        {
            while (_available.Count > 0)
            {
                T instance = _available.Dequeue();
                if (instance != null)
                {
                    Destroy(instance.gameObject);
                }
            }
        }

        /// <summary>
        /// Explicitly create one new instance and return its T component.
        /// Useful when the caller wants to bypass the pool (e.g., in a fallback path).
        /// </summary>
        public T CreateNew()
        {
            return CreateInstance();
        }

        private T CreateInstance()
        {
            if (Prefab == null || !_prefabIsValid)
                return null;

            GameObject go = Instantiate(Prefab, transform);
            T comp = go.GetComponent<T>();
            if (comp == null)
            {
                Destroy(go);
                return null;
            }
            return comp;
        }
    }
}
