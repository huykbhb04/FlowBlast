using System;
using UnityEngine;
using UnityEngine.UI;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Tracks how many boxes the player has successfully completed (matched a full
    /// container to 100%) and renders "X/Y" on a UI Text or TextMeshProUGUI label.
    ///
    /// Total "Y" comes from either:
    ///   1. The currently loaded GridMapDataSO (Pathfinding.GetSelectableBoxes count),
    ///      so the HUD matches the level design.
    ///   2. A manual override (`manualTotal`) for tests / menu screens.
    ///
    /// Increments are driven by BoxContainer.OnCompleted (one per box that fills).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameProgressHUD : MonoBehaviour
    {
        public static GameProgressHUD Instance { get; private set; }

        [Header("UI Targets")]
        [Tooltip("UI Text (legacy). Assign either this or 'tmpLabel' (or both).")]
        [SerializeField] private Text label;

        [Tooltip("TextMeshPro - UGUI label. Assign either this or 'label' (or both).")]
        [SerializeField] private TMPro.TextMeshProUGUI tmpLabel;

        [Header("Format")]
        [Tooltip("Format string. Use {0} for current count, {1} for total. Default = '{0}/{1}'.")]
        [SerializeField] private string format = "{0}/{1}";

        [Tooltip("Prefix text (optional). E.g. 'Boxes: '.")]
        [SerializeField] private string prefix = "Boxes: ";

        [Header("Total Source")]
        [Tooltip("What to count as the 'total' boxes in the progress text:\n" +
                 "  'Selectable' - only currently escapable boxes (recomputes as you free them).\n" +
                 "  'TotalBoxes' - every Box cell on the grid (selectable + trapped).\n" +
                 "  'Manual'     - use the 'manualTotal' field below, ignore grid.")]
        public TotalSourceMode totalSource = TotalSourceMode.TotalBoxes;

        public enum TotalSourceMode
        {
            Selectable,
            TotalBoxes,
            Manual
        }

        [Tooltip("Fallback total used when 'manualTotal' is selected as source, or when no GridManager can be found.")]
        [SerializeField] private int manualTotal = 4;

        [Tooltip("If true, log progress changes to the console.")]
        [SerializeField] private bool logProgress = true;

        [Header("Visuals")]
        [Tooltip("Color of the text on the default state.")]
        [SerializeField] private Color normalColor = Color.white;

        [Tooltip("Color of the text when all boxes are completed (currentCount == total).")]
        [SerializeField] private Color completedColor = new Color(0.5f, 1f, 0.5f);

        private int _currentCount;
        private int _total;

        public int CurrentCount => _currentCount;
        public int Total => _total;
        public bool IsCompleted => _total > 0 && _currentCount >= _total;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            RecomputeTotal();
            Refresh();
            // Drive increments from BoxSlot.BoxSlot_HandleContainerCompleted, which we
            // hook into every newly assigned BoxContainer. No need to re-subscribe here.
        }

        /// <summary>
        /// Called by LevelLoader to explicitly set the total box count for this level.
        /// Switches to Manual mode so the HUD uses the provided value instead of
        /// auto-counting from the grid.
        /// </summary>
        public void Bind(int targetTotal)
        {
            totalSource = TotalSourceMode.Manual;
            manualTotal = Mathf.Max(targetTotal, 1);
            _currentCount = 0;
            _total = manualTotal;
            Refresh();
        }

        private void OnDisable()
        {
            // BoxSlot handles its own subscription lifetime via OnDestroy + AssignBox.
            if (Instance == this) Instance = null;
        }

        private void BindToAllSlots(BottomRayManager ray)
        {
            // No-op: subscriptions are managed per-slot via RebindContainer() called
            // from BoxSlot.AssignBox(). Kept here as a hook for future use.
        }

        /// <summary>
        /// Public hook so BoxSlot can rebind the HUD's event handlers on a fresh
        /// container. Currently a no-op (the HUD drives increments via
        /// NotifyBoxCompleted() instead), but kept for API symmetry.
        /// </summary>
        public void RebindContainer(BoxContainer container)
        {
            // No-op - HUD listens through BoxSlot.BoxSlot_HandleContainerCompleted.
            _ = container;
        }

        /// <summary>
        /// Called by BoxSlot when its BoxContainer reports completion.
        /// </summary>
        public void NotifyBoxCompleted()
        {
            _currentCount++;
            if (logProgress)
            {
                Debug.Log($"[GameProgressHUD] Box completed ({_currentCount}/{_total}).");
            }
            Refresh();
        }

        /// <summary>
        /// Recompute Total from the current GridMapDataSO. Behaviour depends on
        /// 'totalSource':
        ///   Selectable : count cells where cell.IsSelectable && HasBox()
        ///   TotalBoxes : count every Box cell on the grid (selectable + trapped)
        ///   Manual     : fall back to manualTotal
        /// </summary>
        public void RecomputeTotal()
        {
            int newTotal = manualTotal;

            if (totalSource != TotalSourceMode.Manual)
            {
                var gm = FindObjectOfType<GridManager>();
                if (gm != null)
                {
                    var map = gm.GetGridMap();
                    if (map != null && map.Cells != null)
                    {
                        int counted = 0;
                        for (int r = 0; r < map.Rows; r++)
                        {
                            for (int c = 0; c < map.Cols; c++)
                            {
                                var cell = map.Cells[r, c];
                                if (cell == null) continue;
                                if (!cell.HasBox()) continue;
                                if (totalSource == TotalSourceMode.Selectable && !cell.IsSelectable) continue;
                                counted++;
                            }
                        }
                        if (counted > 0) newTotal = counted;
                    }
                }
            }

            _total = Mathf.Max(newTotal, 1);
            if (_currentCount > _total) _currentCount = _total;
        }

        /// <summary>
        /// Reset the HUD back to 0/total (call when starting a new level).
        /// </summary>
        public void ResetCount()
        {
            _currentCount = 0;
            RecomputeTotal();
            Refresh();
            if (BottomRayManager.Instance != null) BindToAllSlots(BottomRayManager.Instance);
        }

        private void Refresh()
        {
            if (_total <= 0) RecomputeTotal();

            // Build text safely. Two reasons:
            //   1) Format string may be empty / malformed in Inspector.
            //   2) String.Format crashes when arg count doesn't match placeholders.
            // We always produce "{current}/{total}" at minimum, then append prefix and
            // honour a format string that has EXACTLY one or two {0} placeholders.
            string body = SafeFormat(format, _currentCount, _total, fallback: _currentCount + "/" + _total);
            string text = (prefix ?? string.Empty) + body;

            bool done = IsCompleted;
            Color c = done ? completedColor : normalColor;

            if (label != null)
            {
                label.text = text;
                label.color = c;
            }
            if (tmpLabel != null)
            {
                tmpLabel.text = text;
                tmpLabel.color = c;
            }
        }

        /// <summary>
        /// Format helper that never throws - falls back to 'fallback' string if the
        /// user-entered format string has mismatched placeholders / placeholders != 2.
        /// </summary>
        private static string SafeFormat(string fmt, int current, int total, string fallback)
        {
            if (string.IsNullOrEmpty(fmt)) return fallback;

            // Count {0} and {1} placeholders exactly (single-digit indices only).
            int count0 = CountSubstring(fmt, "{0}");
            int count1 = CountSubstring(fmt, "{1}");

            // If a {1} placeholder exists but no {0}, or any {N>=2}, abort to fallback.
            // Otherwise call String.Format with the right number of args.
            try
            {
                if (count0 >= 1 && count1 >= 1)
                    return string.Format(fmt, current, total);
                if (count0 >= 1 && count1 == 0)
                    return string.Format(fmt, current);
                return fallback;
            }
            catch (FormatException)
            {
                return fallback;
            }
        }

        private static int CountSubstring(string s, string needle)
        {
            if (string.IsNullOrEmpty(s) || string.IsNullOrEmpty(needle)) return 0;
            int n = 0, idx = 0;
            while ((idx = s.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
            {
                n++;
                idx += needle.Length;
            }
            return n;
        }
    }
}
