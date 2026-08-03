using System;
using UnityEngine;

namespace FlowBlast.UI
{
    /// <summary>
    /// Base behaviour for any screen panel (Main Menu, HUD, Pause, Win, Lose).
    /// A panel is associated with one or more <see cref="UIState"/>. When the UIManager's
    /// state matches one of this panel's <see cref="shownStates"/>, the panel becomes active
    /// (SetActive true); otherwise it hides.
    ///
    /// Wire OnShow / OnHide virtual methods to drive entry/exit animations.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPanel : MonoBehaviour
    {
        [Tooltip("Root GameObject of the panel (defaults to this gameObject).")]
        [SerializeField] private GameObject _root;

        [Tooltip("States in which this panel should be visible.")]
        [SerializeField] protected UIState[] _shownStates = Array.Empty<UIState>();

        public GameObject Root => _root;
        public bool IsVisible { get; private set; }

        protected virtual void Awake()
        {
            if (_root == null) _root = gameObject;
        }

        protected virtual void OnEnable()
        {
            if (UIManager.Instance != null) UIManager.Instance.OnStateChanged += HandleStateChanged;
            Apply(UIManager.Instance != null ? UIManager.Instance.State : UIState.Playing);
        }

        protected virtual void OnDisable()
        {
            if (UIManager.Instance != null) UIManager.Instance.OnStateChanged -= HandleStateChanged;
        }

        private void HandleStateChanged(UIState s) => Apply(s);

        private void Apply(UIState s)
        {
            bool visible = false;
            for (int i = 0; i < _shownStates.Length; i++)
            {
                if (_shownStates[i] == s) { visible = true; break; }
            }
            SetVisible(visible);
        }

        public void SetVisible(bool show)
        {
            if (_root == null) _root = gameObject;
            if (IsVisible == show) return;
            IsVisible = show;
            _root.SetActive(show);
            Debug.Log($"[UIPanel] {name}.SetVisible({show}) states=[{string.Join(",", _shownStates)}]");
            if (show) OnShow(); else OnHide();
        }

        protected virtual void OnShow() { }
        protected virtual void OnHide() { }

        /// <summary>
        /// Optional hook called by <c>UIBootstrap</c> after the panel GameObject is created.
        /// When a <c>UITheme_300Mind</c> is assigned, derived controllers may rebuild their
        /// child hierarchy here using <c>UIThemeBuilder</c> to replace the placeholder visuals.
        /// Default implementation is a no-op so existing controllers are unaffected.
        /// </summary>
        public virtual void BuildHierarchy(UITheme_300Mind theme)
        {
            // no-op
        }
    }
}
