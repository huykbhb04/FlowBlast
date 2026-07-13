using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FlowBlast.UI
{
    /// <summary>
    /// Single point of truth for which screen the player sees:
    ///   - <see cref="MainMenu"/> : title screen with Play / Quit.
    ///   - <see cref="Playing"/>  : in-game HUD (progress, coin, pause button).
    ///   - <see cref="Paused"/>   : pause overlay.
    ///   - <see cref="Won"/>      : level-cleared panel.
    ///   - <see cref="Lost"/>     : (optional, for future fail-state).
    /// Switching state hides/shows the matching panel and broadcasts a global event.
    ///
    /// Multiple panels can be requested to show by their host components; UIManager only sets
    /// which <see cref="UIState"/> is the "active" one (driving Time.timeScale, blocking input).
    /// </summary>
    public enum UIState
    {
        MainMenu,
        Playing,
        Paused,
        Won,
        Lost,
        LevelSelect,
        Settings,
    }

    [DisallowMultipleComponent]
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private UIState _initialState = UIState.Playing;
        [SerializeField] private string _gameplaySceneName = "Day03_ConveyorTest";

        public UIState State { get; private set; } = UIState.MainMenu;
        public event Action<UIState> OnStateChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            SetState(_initialState);
        }

        public void SetState(UIState next)
        {
            if (State == next) return;
            State = next;

            switch (next)
            {
                case UIState.MainMenu:    Time.timeScale = 1f; break;
                case UIState.LevelSelect: Time.timeScale = 1f; break;
                case UIState.Settings:    Time.timeScale = 1f; break;
                case UIState.Playing:     Time.timeScale = 1f; break;
                case UIState.Paused:      Time.timeScale = 0f; break;
                case UIState.Won:         Time.timeScale = 0f; break;
                case UIState.Lost:        Time.timeScale = 0f; break;
            }

            OnStateChanged?.Invoke(next);
        }

        // ---------- High-level navigation helpers ----------

        public void ShowMainMenu()
        {
            Time.timeScale = 1f;
            if (SceneManager.GetActiveScene().name != "MainMenu")
            {
                // Optional - MainMenu scene may not exist yet, only load if present in build settings.
                if (Application.CanStreamedLevelBeLoaded("MainMenu"))
                    SceneManager.LoadScene("MainMenu");
                else
                    SetState(UIState.MainMenu);
            }
            else
            {
                SetState(UIState.MainMenu);
            }
        }

        public void StartGame()
        {
            if (Application.CanStreamedLevelBeLoaded(_gameplaySceneName))
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(_gameplaySceneName);
            }
            else
            {
                // Fall back: just flip the state (panels toggle in-scene).
                SetState(UIState.Playing);
            }
        }

        public void Pause()        => SetState(UIState.Paused);
        public void Resume()       => SetState(UIState.Playing);
        public void Win()          => SetState(UIState.Won);
        public void Lose()         => SetState(UIState.Lost);
        public void RestartLevel() => StartGame();
        public void ShowLevelSelect() => SetState(UIState.LevelSelect);
        public void ShowSettings()    => SetState(UIState.Settings);

        /// <summary>Quit the player. In Editor this stops Play mode.</summary>
        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
