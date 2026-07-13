using System;
using System.Collections.Generic;
using FlowBlast.UI.Popup;
using UnityEngine;

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

        /// <summary>Generic entry — instantiate prefab whose root component is T.</summary>
        public T Show<T>() where T : BasePopup
        {
            Type target = typeof(T);
            for (int i = 0; i < _registry.Length; i++)
            {
                BasePopup prefab = _registry[i].prefab;
                if (prefab == null) continue;
                if (prefab.GetType() != target) continue;

                return (T)InstantiateAndShow(prefab, _registry[i].id);
            }

            Debug.LogError($"[PopupManager] No prefab of type {target.Name} found in registry.");
            return null;
        }

        /// <summary>Instantiate by id directly (used by the sugar methods).</summary>
        public BasePopup Show(PopupId id)
        {
            BasePopup prefab = GetPrefab(id);
            if (prefab == null) return null;
            return InstantiateAndShow(prefab, id);
        }

        public T ShowPausePopup<T>() where T : BasePopup { return Show<T>(); }
        public BasePopup ShowPausePopup() { return Show(PopupId.Pause); }
        public BasePopup ShowWinPopup() { return Show(PopupId.Win); }
        public BasePopup ShowLosePopup() { return Show(PopupId.Lose); }

        private BasePopup InstantiateAndShow(BasePopup prefab, PopupId id)
        {
            BasePopup instance = Instantiate(prefab, _canvasRoot);
            instance.name = $"{id}Popup (Runtime)";

            // Subscribe to OnClosedEvent so we pop & destroy when fade-out completes.
            instance.OnClosedEvent += () => HandlePopupClosed(instance);

            _stack.Push(instance);
            instance.OnShown();
            return instance;
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