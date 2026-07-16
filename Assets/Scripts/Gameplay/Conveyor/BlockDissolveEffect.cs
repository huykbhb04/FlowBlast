using System.Collections;
using UnityEngine;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Sink/fade effect used when a top ball matches the gate and is "consumed"
    /// by the bottom box. The ball travels a short distance along the spline tangent
    /// (visually "pouring into" the slot underneath), shrinks, fades its material color,
    /// and is destroyed after the animation completes. Cheap, no shader required.
    ///
    /// Supports pool-aware return via PlayAndReturnToPool(): after the animation
    /// finishes the component is destroyed but the parent ConveyorColoredBlock is
    /// returned to its BlockPool instead of being garbage-collected.
    /// </summary>
    public class BlockDissolveEffect : MonoBehaviour
    {
        [Header("Sink")]
        [Tooltip("Total duration of the consume animation in seconds.")]
        [SerializeField] private float duration = 0.6f;

        [Tooltip("Multiplier on the spline tangent to follow before fading. 1 = 1 spline unit forward.")]
        [SerializeField] private float forwardDistance = 0.6f;

        [Tooltip("Multiplier on Vector3.down used while the ball is being consumed (gives a slight drop toward the ray).")]
        [SerializeField] private float dropDistance = 1.2f;

        [Header("Visual")]
        [Tooltip("Final scale factor at the end of the animation. 0 = shrink completely so it doesn't pop out.")]
        [SerializeField] private float shrinkEndScale = 0f;

        [Tooltip("Easing curve applied to the lerp 0..1 animation progress. Default is ease-in-out.")]
        [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Optional parent SplineConveyor; if not set we auto-find it on Awake (only used to grab the spline tangent).")]
        [SerializeField] private SplineConveyor owningConveyor;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private BlockPool _pool;
        private ConveyorColoredBlock _coloredBlock;
        private System.Action<ConveyorColoredBlock> _onCompleteWithPool;

        /// <summary>Legacy path: animate then destroy the GameObject (no pooling).</summary>
        public void PlayAndDestroy()
        {
            StartCoroutine(RunDissolve(null, null, null));
        }

        /// <summary>
        /// Pool-aware path: after the animation finishes the ConveyorColoredBlock is
        /// returned to its BlockPool instead of being destroyed.
        /// </summary>
        /// <param name="pool">BlockPool to return the block to after animation.</param>
        /// <param name="coloredBlock">ConveyorColoredBlock to recycle. Must be on the same GameObject.</param>
        /// <param name="onComplete">Optional callback fired after the block is returned to the pool.</param>
        public void PlayAndReturnToPool(
            BlockPool pool,
            ConveyorColoredBlock coloredBlock,
            System.Action<ConveyorColoredBlock> onComplete = null)
        {
            _pool = pool;
            _coloredBlock = coloredBlock;
            _onCompleteWithPool = onComplete;
            StartCoroutine(RunDissolve(pool, coloredBlock, onComplete));
        }

        private void Awake()
        {
            if (owningConveyor == null)
            {
                owningConveyor = GetComponentInParent<SplineConveyor>();
            }
        }

        private IEnumerator RunDissolve(
            BlockPool pool,
            ConveyorColoredBlock coloredBlock,
            System.Action<ConveyorColoredBlock> onComplete)
        {
            float elapsed = 0f;
            Vector3 startPosition = transform.position;
            Vector3 startScale = transform.localScale;

            // Direction the ball "pours" toward: along the spline tangent when possible,
            // otherwise world forward so the ball still moves smoothly even when detached.
            Vector3 tangent = owningConveyor != null
                ? owningConveyor.EvaluateWorldTangent(transform.position)
                : Vector3.forward;

            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.forward;
            }

            Vector3 endPosition = startPosition + tangent.normalized * forwardDistance + Vector3.down * dropDistance;

            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            Color[] startColors = new Color[renderers.Length];

            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (mat != null && mat.HasProperty(BaseColorId)) startColors[i] = mat.GetColor(BaseColorId);
                else if (mat != null && mat.HasProperty(ColorId)) startColors[i] = mat.GetColor(ColorId);
                else startColors[i] = Color.white;
            }

            // Continuously ease forward + shrink + fade. We use unscaled delta so pause/slow-motion
            // doesn't stretch the animation out and cause visible overlap with the next ball.
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = easeCurve.Evaluate(t);

                transform.position = Vector3.Lerp(startPosition, endPosition, eased);
                transform.localScale = Vector3.Lerp(startScale, startScale * shrinkEndScale, eased);

                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                    renderers[i].GetPropertyBlock(mpb);
                    Color c = startColors[i];
                    c.a = Mathf.Lerp(1f, 0f, eased);
                    mpb.SetColor(BaseColorId, c);
                    mpb.SetColor(ColorId, c);
                    renderers[i].SetPropertyBlock(mpb);
                }

                yield return null;
            }

            // Snap to final state so a one-frame mismatch doesn't show the ball popped back in.
            transform.position = endPosition;
            transform.localScale = startScale * shrinkEndScale;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                MaterialPropertyBlock mpb = new MaterialPropertyBlock();
                renderers[i].GetPropertyBlock(mpb);
                Color c = startColors[i];
                c.a = 0f;
                mpb.SetColor(BaseColorId, c);
                mpb.SetColor(ColorId, c);
                renderers[i].SetPropertyBlock(mpb);
            }

            // Return the ConveyorColoredBlock to its pool before destroying the GameObject.
            // OnDespawn (BlockPool.OnBlockDespawn) is called inside pool.Despawn()
            // to stop active coroutines on the block.
            if (pool != null && coloredBlock != null)
            {
                pool.Despawn(coloredBlock);
                onComplete?.Invoke(coloredBlock);
            }

            Destroy(gameObject);
        }
    }
}
