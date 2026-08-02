using System;

namespace FlowBlast.Data
{
    /// <summary>
    /// Plain data class serialised to / from JSON via SaveManager.
    /// Contains all persistent player state: progress, economy, settings.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        /// <summary>Index of the level the player is currently on (0-based).</summary>
        public int CurrentLevel = 0;

        /// <summary>Highest level the player has unlocked (0-based, inclusive).</summary>
        public int HighestUnlockedLevel = 0;

        /// <summary>Player's coin balance.</summary>
        public int Coins = 0;

        /// <summary>Sound effects toggle.</summary>
        public bool IsSfxOn = true;

        /// <summary>Background music toggle.</summary>
        public bool IsMusicOn = true;

        /// <summary>
        /// Per-level star count. Index = level index, value = 0-3 stars.
        /// Grows dynamically as the player progresses.
        /// </summary>
        public int[] LevelStars = new int[0];

        // ─── Helpers ─────────────────────────────────────────────────────

        /// <summary>
        /// Set star count for a specific level, expanding the array if needed.
        /// Only overwrites if the new star count is higher than the stored one.
        /// </summary>
        public void SetStars(int levelIndex, int stars)
        {
            if (levelIndex < 0) return;
            if (LevelStars == null || LevelStars.Length <= levelIndex)
            {
                int[] expanded = new int[levelIndex + 1];
                if (LevelStars != null)
                    Array.Copy(LevelStars, expanded, LevelStars.Length);
                LevelStars = expanded;
            }
            if (stars > LevelStars[levelIndex])
                LevelStars[levelIndex] = stars;
        }

        /// <summary>
        /// Get star count for a level. Returns 0 if level has not been played.
        /// </summary>
        public int GetStars(int levelIndex)
        {
            if (LevelStars == null || levelIndex < 0 || levelIndex >= LevelStars.Length)
                return 0;
            return LevelStars[levelIndex];
        }

        /// <summary>
        /// Unlock the next level if it hasn't been unlocked yet.
        /// </summary>
        public void UnlockNextLevel()
        {
            int next = CurrentLevel + 1;
            if (next > HighestUnlockedLevel)
                HighestUnlockedLevel = next;
        }
    }
}
