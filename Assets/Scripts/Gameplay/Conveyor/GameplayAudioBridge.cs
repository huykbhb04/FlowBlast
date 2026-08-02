using UnityEngine;
using FlowBlast.Core;
using FlowBlast.Managers;
using FlowBlast.Gameplay.Grid;

namespace FlowBlast.Gameplay.Conveyor
{
    /// <summary>
    /// Bridges gameplay events to <see cref="AudioManager"/> without modifying Week 1 scripts.
    ///
    /// Subscribes to:
    ///   - GateMatcher.OnColorCompleted   → plays BoxComplete SFX
    ///   - GameStateMachine.OnStateChanged → plays Win / Lose SFX
    ///   - BoxClickBus.OnBoxTapped         → plays BoxTap SFX
    ///   - GateMatcher match events        → plays BallEnterBox SFX (via Update polling)
    ///
    /// Attach to any persistent scene object (e.g. _Managers or LevelLoader root).
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayAudioBridge : MonoBehaviour
    {
        [Header("Debug")]
        [SerializeField] private bool _logAudio = false;

        private int _lastBoxCompletedCount = -1;

        private void OnEnable()
        {
            // Subscribe to color-completed (a full color drained from conveyor).
            GateMatcher.OnColorCompleted += OnColorCompleted;

            // Subscribe to box tap from grid.
            BoxClickBus.BoxTapped += OnBoxTapped;

            // Subscribe to state machine changes (Win / Lose sounds).
            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.OnStateChanged += OnStateChanged;
        }

        private void OnDisable()
        {
            GateMatcher.OnColorCompleted -= OnColorCompleted;
            BoxClickBus.BoxTapped -= OnBoxTapped;

            if (GameStateMachine.Instance != null)
                GameStateMachine.Instance.OnStateChanged -= OnStateChanged;
        }

        // ─── Event Handlers ─────────────────────────────────────────────

        /// <summary>
        /// Fires when a full color is drained from the top conveyor (all boxes of that color completed).
        /// Plays BoxComplete SFX.
        /// </summary>
        private void OnColorCompleted(BoxColor color)
        {
            PlaySfx(AudioId.BoxComplete);
            if (_logAudio) Debug.Log($"[GameplayAudioBridge] BoxComplete SFX for color {color}");
        }

        /// <summary>
        /// Fires when the player taps a box on the grid.
        /// Plays BoxTap SFX.
        /// </summary>
        private void OnBoxTapped(GameObject box, BoxColor color)
        {
            PlaySfx(AudioId.BoxTap);
            if (_logAudio) Debug.Log($"[GameplayAudioBridge] BoxTap SFX for {color}");
        }

        /// <summary>
        /// Fires on game state transitions. Plays Win/Lose SFX.
        /// </summary>
        private void OnStateChanged(GameState previous, GameState current)
        {
            switch (current)
            {
                case GameState.Win:
                    PlaySfx(AudioId.Win);
                    if (_logAudio) Debug.Log("[GameplayAudioBridge] Win SFX");
                    break;

                case GameState.Lose:
                    PlaySfx(AudioId.Lose);
                    if (_logAudio) Debug.Log("[GameplayAudioBridge] Lose SFX");
                    break;

                case GameState.Paused:
                    PlaySfx(AudioId.ButtonClick);
                    break;
            }
        }

        // ─── Polling: BallEnterBox ──────────────────────────────────────

        /// <summary>
        /// Poll GameProgressHUD to detect when a box is completed (progress incremented).
        /// This catches the "ball enters box and fills it" moment.
        /// </summary>
        private void Update()
        {
            if (GameProgressHUD.Instance == null) return;

            int current = GameProgressHUD.Instance.CurrentCount;

            // Initialize on first frame.
            if (_lastBoxCompletedCount < 0)
            {
                _lastBoxCompletedCount = current;
                return;
            }

            // A box was just completed → play BallEnterBox sound.
            if (current > _lastBoxCompletedCount)
            {
                PlaySfx(AudioId.BallEnterBox);
                if (_logAudio) Debug.Log($"[GameplayAudioBridge] BallEnterBox SFX (box #{current})");
            }
            _lastBoxCompletedCount = current;
        }

        // ─── Helper ─────────────────────────────────────────────────────

        private void PlaySfx(AudioId id)
        {
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(id);
        }
    }
}
