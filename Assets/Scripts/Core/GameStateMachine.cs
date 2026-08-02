using System;
using UnityEngine;
using FlowBlast.UI.Popup;

namespace FlowBlast.Core
{
    /// <summary>
    /// Central Finite State Machine that drives the game flow.
    /// Singleton, DontDestroyOnLoad.
    ///
    /// State transitions trigger <see cref="OnStateChanged"/> and perform
    /// side-effects such as pausing time, showing popups, etc.
    ///
    /// Usage:
    ///   GameStateMachine.Instance.TransitionTo(GameState.Playing);
    /// </summary>
    public class GameStateMachine : MonoBehaviour
    {
        public static GameStateMachine Instance { get; private set; }

        /// <summary>
        /// Fires whenever the state changes. Args: (previousState, newState).
        /// </summary>
        public event Action<GameState, GameState> OnStateChanged;

        [Header("Debug")]
        [SerializeField] private bool _logTransitions = true;

        private GameState _currentState = GameState.Loading;

        /// <summary>Current game state (read-only).</summary>
        public GameState CurrentState => _currentState;

        /// <summary>Convenience: true when the game is in <see cref="GameState.Playing"/>.</summary>
        public bool IsPlaying => _currentState == GameState.Playing;

        /// <summary>Convenience: true when the game is paused.</summary>
        public bool IsPaused => _currentState == GameState.Paused;

        // ─── Lifecycle ──────────────────────────────────────────────────

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

        private void Start()
        {
            // Auto-start into Playing if no other system drives the initial state.
            // MainMenu scene (if exists) would call TransitionTo(MainMenu) from its own Start().
            if (_currentState == GameState.Loading)
                TransitionTo(GameState.Playing);
        }

        // ─── Public API ─────────────────────────────────────────────────

        /// <summary>
        /// Transition to a new state. Performs exit logic for the old state,
        /// enter logic for the new state, and fires <see cref="OnStateChanged"/>.
        /// Rejects no-op transitions (same state).
        /// </summary>
        public void TransitionTo(GameState newState)
        {
            if (newState == _currentState) return;

            GameState previous = _currentState;

            // ─── EXIT old state ─────────────────────────────────────────
            ExitState(previous);

            // ─── ENTER new state ────────────────────────────────────────
            _currentState = newState;
            EnterState(newState);

            if (_logTransitions)
                Debug.Log($"[GameStateMachine] {previous} → {newState}");

            OnStateChanged?.Invoke(previous, newState);
        }

        /// <summary>
        /// Shortcut: if currently Paused, resume to Playing.
        /// If currently Playing, pause.
        /// </summary>
        public void TogglePause()
        {
            if (_currentState == GameState.Playing)
                TransitionTo(GameState.Paused);
            else if (_currentState == GameState.Paused)
                TransitionTo(GameState.Playing);
        }

        // ─── State Handlers ─────────────────────────────────────────────

        private void ExitState(GameState state)
        {
            switch (state)
            {
                case GameState.Paused:
                    Time.timeScale = 1f;
                    break;
            }
        }

        private void EnterState(GameState state)
        {
            switch (state)
            {
                case GameState.Playing:
                    Time.timeScale = 1f;
                    break;

                case GameState.Paused:
                    Time.timeScale = 0f;
                    ShowPopup(PopupId.Pause);
                    break;

                case GameState.Win:
                    // Don't pause time — let win animations play out.
                    ShowPopup(PopupId.Win);
                    break;

                case GameState.Lose:
                    ShowPopup(PopupId.Lose);
                    break;
            }
        }

        private void ShowPopup(PopupId id)
        {
            if (FlowBlast.Managers.PopupManager.Instance != null)
                FlowBlast.Managers.PopupManager.Instance.Show(id);
            else
                Debug.LogWarning($"[GameStateMachine] PopupManager.Instance is null — cannot show {id}.");
        }
    }
}
