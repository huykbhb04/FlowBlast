using TMPro;
using UnityEngine;
using UnityEngine.UI;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Drives the "Progress" HUD element (radial Image + child TextMeshPro).
    ///
    /// Tracks how many colors have been fully consumed on the top spline:
    ///   - Y  = total distinct colors on the spline  (SplineConveyor.PaletteSize)
    ///   - X  = number of colors already drained     (GateMatcher.OnColorCompleted counter)
    ///   - Format: "X/Y"  e.g.  "2/5"
    ///
    /// When X == Y the conveyor is empty → WinPopup logic is handled separately
    /// (e.g. via GameProgressHUD or a dedicated win trigger).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class GameProgressHUDDisplay : MonoBehaviour
    {
        [Header("UI Targets")]
        [Tooltip("Image on the same GameObject used as the fill bar (radial or horizontal).")]
        [SerializeField] private Image fillImage;

        [Tooltip("Child TextMeshProUGUI that shows the 'X/Y' text.")]
        [SerializeField] private TextMeshProUGUI progressLabel;

        [Header("Format")]
        [Tooltip("Text shown before the fraction, e.g. 'Colors: ' or '' for none.")]
        [SerializeField] private string prefix = "";

        [Header("Visuals")]
        [Tooltip("If > 0, smoothly lerp fillAmount each frame toward the target.")]
        [SerializeField, Min(0f)] private float fillLerpSpeed = 8f;

        [Tooltip("Bar fill color while progress is being made.")]
        [SerializeField] private Color activeColor = new Color(0.91f, 0.27f, 0.27f);

        [Tooltip("Bar fill color when all colors are done (X == Y).")]
        [SerializeField] private Color completedColor = new Color(0.96f, 0.86f, 0.27f);

        [Header("Debug")]
        [SerializeField] private bool logChanges = false;

        // State
        private int _completedCount;
        private int _totalColors;
        private float _targetFill;
        private float _currentFill;
        private bool _subscribed;

        public int CompletedCount => _completedCount;
        public int TotalColors => _totalColors;
        public bool IsComplete => _totalColors > 0 && _completedCount >= _totalColors;

        private void Reset()
        {
            fillImage = GetComponent<Image>();
            if (fillImage == null) fillImage = GetComponentInChildren<Image>(true);
            progressLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void Awake()
        {
            if (fillImage == null) fillImage = GetComponent<Image>();
            if (progressLabel == null) progressLabel = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        private void OnEnable()
        {
            RefreshTotal();
            Subscribe(true);
            Refresh();
        }

        private void OnDisable()
        {
            Subscribe(false);
        }

        private void Update()
        {
            if (fillLerpSpeed > 0f)
                _currentFill = Mathf.MoveTowards(_currentFill, _targetFill, fillLerpSpeed * Time.unscaledDeltaTime);
            else
                _currentFill = _targetFill;

            ApplyVisual(_currentFill);
        }

        /// <summary>
        /// Re-read the total color count from SplineConveyor. Call this if the
        /// palette changes mid-game (e.g. level data swap).
        /// </summary>
        public void RefreshTotal()
        {
            var conveyor = Object.FindObjectOfType<SplineConveyor>();
            _totalColors = (conveyor != null) ? conveyor.PaletteSize : 1;
            _totalColors = Mathf.Max(_totalColors, 1);
            if (logChanges)
                Debug.Log($"[GameProgressHUDDisplay] Total colors (Y) = {_totalColors}");
        }

        /// <summary>
        /// Force-reset to 0 / total (call when restarting a level).
        /// </summary>
        public void ResetCount()
        {
            _completedCount = 0;
            _targetFill = 0f;
            _currentFill = 0f;
            if (logChanges)
                Debug.Log("[GameProgressHUDDisplay] Count reset to 0.");
            Refresh();
        }

        private void Subscribe(bool attach)
        {
            if (attach && !_subscribed)
            {
                GateMatcher.OnColorCompleted += OnColorDone;
                _subscribed = true;
            }
            else if (!attach && _subscribed)
            {
                GateMatcher.OnColorCompleted -= OnColorDone;
                _subscribed = false;
            }
        }

        private void OnColorDone(BoxColor color)
        {
            _completedCount = Mathf.Min(_completedCount + 1, _totalColors);
            _targetFill = (_totalColors > 0) ? (float)_completedCount / _totalColors : 0f;

            if (logChanges)
                Debug.Log($"[GameProgressHUDDisplay] Color {color} completed → {_completedCount}/{_totalColors}");

            Refresh();
        }

        private void Refresh()
        {
            _targetFill = (_totalColors > 0) ? (float)_completedCount / _totalColors : 0f;
        }

        private void ApplyVisual(float fill01)
        {
            if (fillImage != null)
            {
                fillImage.fillAmount = Mathf.Clamp01(fill01);
                fillImage.color = Color.Lerp(activeColor, completedColor, fill01 >= 0.9999f ? 1f : 0f);
            }

            if (progressLabel != null)
            {
                progressLabel.text = $"{prefix}{_completedCount}/{_totalColors}";
            }
        }

        private void OnDestroy()
        {
            Subscribe(false);
        }
    }
}
