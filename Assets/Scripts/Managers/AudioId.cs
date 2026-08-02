namespace FlowBlast.Managers
{
    /// <summary>
    /// Identifiers for audio clips managed by <see cref="AudioManager"/>.
    /// Add new entries as the game grows (Booster sounds, UI feedback, etc.).
    /// </summary>
    public enum AudioId
    {
        None = 0,

        // ─── SFX ──────────────────────────────────────────────
        ButtonClick = 10,
        BallEnterBox = 20,
        BoxComplete = 30,
        BoxExit = 40,
        BoxTap = 50,
        Win = 60,
        Lose = 70,
        BoosterActivate = 80,

        // ─── Music ────────────────────────────────────────────
        BackgroundMusic = 100,
        MenuMusic = 110,
    }
}
