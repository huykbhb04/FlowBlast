using System;
using System.Collections.Generic;
using FlowBlast.UI.Popup;
using UnityEngine;
using UnityEngine.UI;

namespace FlowBlast.Managers
{
    /// <summary>
    /// Central popup manager. Singleton, DontDestroyOnLoad.
    /// Holds a Stack&lt;BasePopup&gt; and an Inspector-configurable [PopupId → Prefab] registry.
    /// </summary>
    public class PopupManager : MonoBehaviour
    {
        [Serializable]
        public struct PopupEntry
        {
            public PopupId id;
            public BasePopup prefab; // Prefab root must have the matching derived script.
        }

        public static PopupManager Instance { get; private set; }

        [Header("PopupManager")]
        [SerializeField] private Transform _canvasRoot;
        [SerializeField] private PopupEntry[] _registry;

        [Header("Overlay")]
        [Tooltip("Sorting order for the dedicated popup overlay canvas. Higher = on top.")]
        [SerializeField] private int _overlaySortOrder = 9999;

        private readonly Stack<BasePopup> _stack = new Stack<BasePopup>();

        public bool IsAnyPopupOpen => _stack.Count > 0;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Look up prefab by popup id. Returns null + logs error if not registered.</summary>
        private BasePopup GetPrefab(PopupId id)
        {
            for (int i = 0; i < _registry.Length; i++)
            {
                if (_registry[i].id == id) return _registry[i].prefab;
            }
            Debug.LogError($"[PopupManager] No registry entry for PopupId.{id}. Add one in the Inspector.");
            return null;
        }

        /// <summary>Instantiate by id directly. Prevents duplicates if already open.</summary>
        public BasePopup Show(PopupId id)
        {
            // Prevent spawning duplicates if popup of this type is already open
            foreach (var openPopup in _stack)
            {
                if (openPopup != null && openPopup.name.StartsWith($"{id}Popup"))
                {
                    Debug.LogWarning($"[PopupManager] PopupId.{id} is already open. Skipping duplicate creation.");
                    return openPopup;
                }
            }

            BasePopup prefab = GetPrefab(id);
            if (prefab == null) return null;
            return InstantiateAndShow(prefab, id);
        }

        // ─── Sugar API for Inspector OnClick wiring ──────────────────────────────
        // Each of these is its own single-overload public method so Unity's
        // Button OnClick dropdown lists it under the PopupManager header.

        public BasePopup OpenPausePopup() { return Show(PopupId.Pause); }
        public BasePopup OpenWinPopup()   { return Show(PopupId.Win); }
        public BasePopup OpenLosePopup()  { return Show(PopupId.Lose); }
        public BasePopup OpenBuyBoosterPopup() { return Show(PopupId.BuyBooster); }

        // Aliases — kept so existing Inspector wiring still resolves.
        public BasePopup ShowPausePopup() { return Show(PopupId.Pause); }
        public BasePopup ShowWinPopup()   { return Show(PopupId.Win); }
        public BasePopup ShowLosePopup()  { return Show(PopupId.Lose); }
        public BasePopup ShowBuyBoosterPopup() { return Show(PopupId.BuyBooster); }

        public void ClosePausePopup() { CloseTopPopup(); }
        public void CloseWinPopup()   { CloseTopPopup(); }
        public void CloseLosePopup()  { CloseTopPopup(); }

        private BasePopup InstantiateAndShow(BasePopup prefab, PopupId id)
        {
            // Parent under the user-assigned canvas root (typically the HUD
            // canvas) so the popup shares the same reference resolution.
            BasePopup instance = Instantiate(prefab, _canvasRoot);
            instance.name = $"{id}Popup (Runtime)";

            // Reset every node's localScale to 1 first.
            NormalizeScaleRecursive(instance.transform);

            // Center the root and zero its rotation/position.
            RectTransform rt = instance.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one;
            }

            // Scale the popup root so it fits (and fills) the current canvas
            // nicely. The prefab was likely authored against a different
            // canvas reference resolution (e.g. 1920x1080 PC) than our
            // mobile HUD canvas (e.g. 1080x1920), so its children render
            // too small unless we re-scale the whole popup.
            FitPopupToCanvas(rt);

            // Force this popup (and any nested canvas) to render above HUD
            // by adding a Canvas with overrideSorting = true.
            EnsureTopSorting(instance.gameObject, _overlaySortOrder);

            // Subscribe to OnClosedEvent so we pop & destroy when fade-out completes.
            instance.OnClosedEvent += () => HandlePopupClosed(instance);

            _stack.Push(instance);
            instance.OnShown();
            return instance;
        }

        private void FitPopupToCanvas(RectTransform root)
        {
            if (root == null || _canvasRoot == null) return;

            RectTransform canvasRT = _canvasRoot as RectTransform;
            if (canvasRT == null) return;

            Vector2 popupSize = root.rect.size;
            Vector2 canvasSize = canvasRT.rect.size;

            if (popupSize.x < 1f || popupSize.y < 1f) return;
            if (canvasSize.x < 1f || canvasSize.y < 1f) return;

            // Fill the canvas so popup and its buttons feel proportional
            // to the HUD viewport. The prefab may have been authored for
            // a different reference resolution, so re-scale to fit.
            const float fillFactor = 0.98f;
            float scaleX = (canvasSize.x * fillFactor) / popupSize.x;
            float scaleY = (canvasSize.y * fillFactor) / popupSize.y;
            float fit = Mathf.Min(scaleX, scaleY);

            if (fit <= 0.001f) return;

            root.localScale = new Vector3(fit, fit, 1f);
        }

        private static void EnsureTopSorting(GameObject go, int sortingOrder)
        {
            Canvas[] nested = go.GetComponentsInChildren<Canvas>(true);
            for (int i = 0; i < nested.Length; i++)
            {
                nested[i].overrideSorting = true;
                nested[i].sortingOrder = sortingOrder;
            }

            // If no Canvas on the popup root itself, add one so the WHOLE
            // popup subtree sorts above HUD regardless of nesting.
            if (go.GetComponent<Canvas>() == null)
            {
                Canvas c = go.AddComponent<Canvas>();
                c.overrideSorting = true;
                c.sortingOrder = sortingOrder;
                go.AddComponent<GraphicRaycaster>();
            }
        }

        private static void NormalizeScaleRecursive(Transform t)
        {
            // Always reset localScale to 1 for every node in the popup tree.
            // (The earlier RectTransform-only check left non-UI transforms
            // with whatever scale the prefab baked in, e.g. 0.154, which
            // then made the rendered popup balloon well past the screen.)
            if (t.localScale != Vector3.one && t.localScale != Vector3.zero)
            {
                t.localScale = Vector3.one;
            }

            for (int i = 0; i < t.childCount; i++)
            {
                NormalizeScaleRecursive(t.GetChild(i));
            }
        }

        private void HandlePopupClosed(BasePopup instance)
        {
            if (_stack.Count == 0) return;

            // Only pop if this is the top of the stack.
            BasePopup top = _stack.Peek();
            if (top == instance)
            {
                _stack.Pop();
                Destroy(instance.gameObject);
                return;
            }

            // Not on top: just destroy (shouldn't normally happen, but be safe).
            Destroy(instance.gameObject);
        }

        /// <summary>Close the topmost popup if any. No-op otherwise.</summary>
        public void CloseTopPopup()
        {
            if (_stack.Count == 0) return;
            BasePopup top = _stack.Peek();
            top.Close();
        }

        /// <summary>Escape hatch: close everything currently in the stack.</summary>
        public void CloseAll()
        {
            while (_stack.Count > 0)
            {
                BasePopup top = _stack.Pop();
                if (top != null) Destroy(top.gameObject);
            }
        }
    }
}