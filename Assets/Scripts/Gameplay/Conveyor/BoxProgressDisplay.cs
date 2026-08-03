using System.Collections;
using TMPro;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    public class BoxProgressDisplay : MonoBehaviour
    {
        [Header("Progress Label")]
        [SerializeField] private TMP_Text _progressLabel;

        [Header("Count Animation")]
        [Min(1)]
        [SerializeField] private int _percentPerFrame = 1;

        private bool _isVisible;
        private int _displayedPercent;
        private int _targetPercent;
        private Coroutine _countCoroutine;
        private BoxContainer _boundContainer;

        public void Bind(BoxContainer container)
        {
            UnbindCurrentContainer();

            if (container == null)
            {
                return;
            }

            _boundContainer = container;
            _displayedPercent = 0;
            _targetPercent = 0;
            SetLabelText(_displayedPercent);
            SetLabelVisible(false);

            container.OnProgressChanged += HandleProgressChanged;
            container.OnCompleted += HandleProgressChanged;
        }

        private void HandleProgressChanged(BoxContainer container)
        {
            if (container == null || _progressLabel == null)
            {
                return;
            }

            _targetPercent = Mathf.Clamp(Mathf.RoundToInt(container.Progress), 0, 100);

            if (!_isVisible && _targetPercent > 0)
            {
                SetLabelVisible(true);
            }

            if (_countCoroutine == null)
            {
                _countCoroutine = StartCoroutine(CountToTarget());
            }
        }

        private IEnumerator CountToTarget()
        {
            while (_displayedPercent < _targetPercent)
            {
                _displayedPercent = Mathf.Min(_displayedPercent + _percentPerFrame, _targetPercent);
                SetLabelText(_displayedPercent);
                yield return null;
            }

            _countCoroutine = null;

            if (_boundContainer != null && _boundContainer.IsCompleted && _displayedPercent >= 100)
            {
                SetLabelVisible(false);
            }
        }

        private void SetLabelText(int percent)
        {
            if (_progressLabel == null)
            {
                return;
            }

            _progressLabel.SetText("{0}%", percent);
        }

        private void SetLabelVisible(bool visible)
        {
            _isVisible = visible;
            if (_progressLabel != null && _progressLabel.gameObject.activeSelf != visible)
            {
                _progressLabel.gameObject.SetActive(visible);
            }
        }

        private void UnbindCurrentContainer()
        {
            if (_boundContainer != null)
            {
                _boundContainer.OnProgressChanged -= HandleProgressChanged;
                _boundContainer.OnCompleted -= HandleProgressChanged;
                _boundContainer = null;
            }

            if (_countCoroutine != null)
            {
                StopCoroutine(_countCoroutine);
                _countCoroutine = null;
            }
        }

        private void OnDisable()
        {
            UnbindCurrentContainer();
        }

        private void OnDestroy()
        {
            UnbindCurrentContainer();
        }
    }
}
