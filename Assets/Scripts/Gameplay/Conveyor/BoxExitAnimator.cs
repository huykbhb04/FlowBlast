using System;
using System.Collections;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Plays a short exit animation on a bottom box once its container hits 100%.
    /// This animation is intentionally synchronized with the top ball
    /// <see cref="BlockDissolveEffect"/> so both disappear at the same moment:
    ///   * identical duration
    ///   * identical ease-in-out curve
    ///   * identical shrink-to-zero scale
    ///   * identical alpha fade
    /// The box travels upward (toward the top conveyor) instead of sinking so it
    /// visibly "hands off" to the consuming top ball. On completion, fires
    /// <paramref name="onComplete"/> so the slot can be freed.
    /// </summary>
    public class BoxExitAnimator : MonoBehaviour
    {
        [Header("Sync with BlockDissolveEffect")]
        [Tooltip("Total duration in seconds. Must equal BlockDissolveEffect.duration.")]
        [SerializeField] private float duration = 0.6f;

        [Tooltip("World-space rise distance while the box exits (positive Y).")]
        [SerializeField] private float riseHeight = 1.2f;

        [Tooltip("Final scale factor at the end of the animation. 0 = shrink completely.")]
        [SerializeField] private float endScale = 0f;

        [Tooltip("Easing curve applied to the lerp 0..1 animation progress. Must equal BlockDissolveEffect.easeCurve.")]
        [SerializeField] private AnimationCurve easeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public void PlayAndClear(Action onComplete)
        {
            StartCoroutine(RunExit(onComplete));
        }

        private IEnumerator RunExit(Action onComplete)
        {
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);

            // Cache start colors once so the fade begins from the box's current tinted state.
            Color[] startColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                Material mat = renderers[i] != null ? renderers[i].sharedMaterial : null;
                if (mat != null && mat.HasProperty(BaseColorId)) startColors[i] = mat.GetColor(BaseColorId);
                else if (mat != null && mat.HasProperty(ColorId)) startColors[i] = mat.GetColor(ColorId);
                else startColors[i] = Color.white;
            }

            Vector3 endPos = startPos + Vector3.up * riseHeight;
            Vector3 endScale = startScale * this.endScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = easeCurve.Evaluate(t);

                transform.position = Vector3.Lerp(startPos, endPos, eased);
                transform.localScale = Vector3.Lerp(startScale, endScale, eased);

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

            // Snap to final state so the box doesn't pop back in for a frame.
            transform.position = endPos;
            transform.localScale = endScale;
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

            onComplete?.Invoke();
        }
    }
}