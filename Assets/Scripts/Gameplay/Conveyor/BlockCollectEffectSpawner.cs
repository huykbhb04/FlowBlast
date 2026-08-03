using System.Collections;
using System.Collections.Generic;
using FlowBlast.Gameplay.Grid;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    public sealed class BlockCollectEffectSpawner : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private SmallBlockCollectEffect _smallBlockPrefab;
        [SerializeField] private Transform _poolRoot;
        [SerializeField] private BoxVisualPaletteSO _visualPalette;

        [Header("Spawn")]
        [Min(1)]
        [SerializeField] private int _blocksPerConsume = 4;
        [Min(0)]
        [SerializeField] private int _prewarmCount = 64;
        [SerializeField] private float _spawnSpreadRadius = 0.18f;

        [Header("Motion")]
        [Min(0.01f)]
        [SerializeField] private float _flyDuration = 0.25f;
        [Min(0f)]
        [SerializeField] private float _staggerDelay = 0.03f;

        private readonly Queue<SmallBlockCollectEffect> _availableEffects = new Queue<SmallBlockCollectEffect>();
        private readonly List<SmallBlockCollectEffect> _spawnedEffects = new List<SmallBlockCollectEffect>();

        private void Awake()
        {
            EnsurePoolRoot();
            WarmPool(_prewarmCount);
        }

        public void SetVisualPalette(BoxVisualPaletteSO visualPalette)
        {
            _visualPalette = visualPalette;
        }

        public void Play(Vector3 startPosition, Transform target, BoxColor color)
        {
            if (_smallBlockPrefab == null || target == null)
            {
                return;
            }

            StartCoroutine(PlayRoutine(startPosition, target, color));
        }

        private IEnumerator PlayRoutine(Vector3 startPosition, Transform target, BoxColor color)
        {
            for (int i = 0; i < _blocksPerConsume; i++)
            {
                SmallBlockCollectEffect effect = SpawnEffect();
                if (effect != null)
                {
                    Vector3 spread = GetSpreadOffset(i);
                    effect.Play(startPosition + spread, target, color, _visualPalette, _flyDuration, ReleaseEffect);
                }

                if (_staggerDelay > 0f && i < _blocksPerConsume - 1)
                {
                    float elapsed = 0f;
                    while (elapsed < _staggerDelay)
                    {
                        elapsed += Time.deltaTime;
                        yield return null;
                    }
                }
            }
        }

        private SmallBlockCollectEffect SpawnEffect()
        {
            SmallBlockCollectEffect effect = _availableEffects.Count > 0
                ? _availableEffects.Dequeue()
                : CreateEffect();

            if (effect == null)
            {
                return null;
            }

            effect.gameObject.SetActive(true);
            return effect;
        }

        private SmallBlockCollectEffect CreateEffect()
        {
            if (_smallBlockPrefab == null)
            {
                return null;
            }

            SmallBlockCollectEffect effect = Instantiate(_smallBlockPrefab, _poolRoot);
            effect.gameObject.SetActive(false);
            _spawnedEffects.Add(effect);
            return effect;
        }

        private void ReleaseEffect(SmallBlockCollectEffect effect)
        {
            if (effect == null)
            {
                return;
            }

            effect.StopActiveAnimation();
            effect.gameObject.SetActive(false);
            effect.transform.SetParent(_poolRoot, false);
            _availableEffects.Enqueue(effect);
        }

        private void WarmPool(int count)
        {
            for (int i = 0; i < count; i++)
            {
                SmallBlockCollectEffect effect = CreateEffect();
                if (effect != null)
                {
                    _availableEffects.Enqueue(effect);
                }
            }
        }

        private Vector3 GetSpreadOffset(int index)
        {
            if (_spawnSpreadRadius <= 0f)
            {
                return Vector3.zero;
            }

            float angle = index * Mathf.PI * 2f / Mathf.Max(1, _blocksPerConsume);
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _spawnSpreadRadius;
        }

        private void EnsurePoolRoot()
        {
            if (_poolRoot != null)
            {
                return;
            }

            GameObject poolRootObject = new GameObject("SmallBlockCollectEffectPool");
            poolRootObject.transform.SetParent(transform, false);
            _poolRoot = poolRootObject.transform;
        }
    }
}
