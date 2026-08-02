using TMPro;
using UnityEngine;

namespace FlowBlast.Gameplay.Conveyor
{
    public class BoxProgressDisplay : MonoBehaviour
    {
        [Header("Progress Label")]
        [SerializeField] private TMP_Text _progressLabel;

        private bool _isVisible;

        public void Bind(BoxContainer container)
        {
            if (container == null)
            {
                return;
            }

            SetLabelText(0);
            SetLabelVisible(false);

            container.OnProgressChanged -= HandleProgressChanged;
            container.OnProgressChanged += HandleProgressChanged;
            container.OnCompleted -= HandleProgressChanged;
            container.OnCompleted += HandleProgressChanged;
        }

        private void HandleProgressChanged(BoxContainer container)
        {
            if (container == null || _progressLabel == null)
            {
                return;
            }

            int percent = Mathf.Clamp(Mathf.RoundToInt(container.Progress), 0, 100);
            SetLabelText(percent);

            if (!_isVisible && container.Progress > 0f)
            {
                SetLabelVisible(true);
            }

            if (container.IsCompleted)
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
    }
}
