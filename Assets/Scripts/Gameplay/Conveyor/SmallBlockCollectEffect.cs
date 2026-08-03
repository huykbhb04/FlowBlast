using System.Collections;
using FlowBlast.Gameplay.Grid;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    public sealed class SmallBlockCollectEffect : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Renderer[] _renderers;
        [SerializeField] private ConveyorColoredBlock _coloredBlock;

        [Header("Motion")]
        [SerializeField] private AnimationCurve _easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [SerializeField] private float _arcHeight = 0.35f;
        [SerializeField] private float _endScale = 0.65f;

        private Coroutine _flyCoroutine;
        private Vector3 _initialScale;

        private void Awake()
        {
            _initialScale = transform.localScale;
        }

        private void OnValidate()
        {
            CacheEditorReferences();
        }

        private void CacheEditorReferences()
        {
            if (_coloredBlock == null)
            {
                _coloredBlock = GetComponent<ConveyorColoredBlock>();
            }

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }
        }

        public void Play(
            Vector3 startPosition,
            Transform target,
            BoxColor color,
            BoxVisualPaletteSO visualPalette,
            float duration,
            System.Action<SmallBlockCollectEffect> onCompleted)
        {
            StopActiveAnimation();
            ApplyColor(color, visualPalette);
            gameObject.SetActive(true);
            transform.position = startPosition;
            transform.localScale = _initialScale;
            _flyCoroutine = StartCoroutine(FlyToTarget(startPosition, target, duration, onCompleted));
        }

        public void StopActiveAnimation()
        {
            if (_flyCoroutine != null)
            {
                StopCoroutine(_flyCoroutine);
                _flyCoroutine = null;
            }
        }

        private IEnumerator FlyToTarget(
            Vector3 startPosition,
            Transform target,
            float duration,
            System.Action<SmallBlockCollectEffect> onCompleted)
        {
            float elapsed = 0f;
            float safeDuration = Mathf.Max(0.01f, duration);

            while (elapsed < safeDuration)
            {
                elapsed += Time.deltaTime;
                float normalizedTime = Mathf.Clamp01(elapsed / safeDuration);
                float eased = _easeCurve.Evaluate(normalizedTime);
                Vector3 targetPosition = target != null ? target.position : startPosition;
                Vector3 position = Vector3.Lerp(startPosition, targetPosition, eased);
                position.y += Mathf.Sin(normalizedTime * Mathf.PI) * _arcHeight;

                transform.position = position;
                transform.localScale = Vector3.Lerp(_initialScale, _initialScale * _endScale, eased);
                yield return null;
            }

            if (target != null)
            {
                transform.position = target.position;
            }

            _flyCoroutine = null;
            onCompleted?.Invoke(this);
        }

        private void ApplyColor(BoxColor color, BoxVisualPaletteSO visualPalette)
        {
            if (_coloredBlock != null)
            {
                _coloredBlock.SetColor(color);
            }

            if (_renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                SplineConveyor.ApplyColorToRenderer(_renderers[i], color, visualPalette);
            }
        }
    }
}
