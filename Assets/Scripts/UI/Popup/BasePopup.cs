using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.UI.Popup
{
    /// <summary>
    /// Abstract base for every popup managed by <see cref="FlowBlast.Managers.PopupManager"/>.
    /// Owns the CanvasGroup fade lifecycle and an optional backdrop click-to-close button.
    /// Subclasses override <see cref="OnShown"/> / <see cref="OnClosed"/> to hook custom logic.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class BasePopup : MonoBehaviour
    {
        [Header("BasePopup")]
        [SerializeField] protected CanvasGroup _canvasGroup;
        [SerializeField] protected Button _backdropButton;
        [SerializeField] protected float _fadeDuration = 0.15f;

        public event Action OnClosedEvent;

        private bool _isClosing;

        protected virtual void Awake()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }

            if (_backdropButton != null)
            {
                _backdropButton.onClick.AddListener(Close);
            }

            // Start invisible; Show() will fade in.
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
        }

        protected virtual void OnDestroy()
        {
            if (_backdropButton != null)
            {
                _backdropButton.onClick.RemoveListener(Close);
            }
        }

        /// <summary>
        /// Called by PopupManager right after the prefab is instantiated and parented.
        /// Default: enable GameObject and fade alpha 0 → 1.
        /// </summary>
        public virtual void OnShown()
        {
            gameObject.SetActive(true);
            _canvasGroup.blocksRaycasts = true;
            _canvasGroup.interactable = true;
            StartCoroutine(FadeRoutine(0f, 1f, _fadeDuration));
        }

        /// <summary>
        /// Called by PopupManager when the popup is being closed (top of stack).
        /// Default: fade out, then notify PopupManager to pop & destroy.
        /// </summary>
        public virtual void OnClosed()
        {
            if (_isClosing) return;
            _isClosing = true;

            StartCoroutine(CloseRoutine());
        }

        /// <summary>Convenience for buttons to call directly on the popup.</summary>
        public void Close()
        {
            OnClosed();
        }

        private IEnumerator CloseRoutine()
        {
            yield return FadeRoutine(_canvasGroup.alpha, 0f, _fadeDuration);
            _canvasGroup.blocksRaycasts = false;
            _canvasGroup.interactable = false;
            OnClosedEvent?.Invoke();
        }

        private IEnumerator FadeRoutine(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                _canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }
    }
}
