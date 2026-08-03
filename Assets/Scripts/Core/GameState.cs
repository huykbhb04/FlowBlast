namespace FlowBlast.Core
{
    /// <summary>
    /// All possible states for the game's finite state machine.
    /// </summary>
    public enum GameState
    {
        /// <summary>Game is initializing / loading.</summary>
        Loading = 0,

        /// <summary>Main menu screen (level select, settings).</summary>
        MainMenu = 1,

        /// <summary>Active gameplay — player can interact with grid & conveyor.</summary>
        Playing = 2,

        /// <summary>Game is paused — Time.timeScale = 0, popup shown.</summary>
        Paused = 3,

        /// <summary>Player completed all target boxes — win popup shown.</summary>
        Win = 4,

        /// <summary>Player lost (slots full, no moves left) — lose popup shown.</summary>
        Lose = 5,
    }
}
