using UnityEngine;

namespace FlowBlast.UI
{
    /// <summary>
    /// Persistent toggle storage for player-facing settings (music, sfx, vibration).
    /// Backed by PlayerPrefs under keys prefixed with "FlowBlast.Setting." so they survive
    /// across sessions. Default value is <c>true</c> for all three (out-of-the-box sound on).
    ///
    /// Toggle UI in <c>SettingsController</c> reads/writes through this service; gameplay
    /// code that needs to honour a toggle can call the matching getter at runtime.
    /// </summary>
    public static class SettingsService
    {
        private const string Prefix = "FlowBlast.Setting.";
        private const string MusicKey = Prefix + "Music";
        private const string SfxKey = Prefix + "Sfx";
        private const string VibrationKey = Prefix + "Vibration";

        public static bool MusicEnabled
        {
            get => PlayerPrefs.GetInt(MusicKey, 1) != 0;
            set { PlayerPrefs.SetInt(MusicKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool SfxEnabled
        {
            get => PlayerPrefs.GetInt(SfxKey, 1) != 0;
            set { PlayerPrefs.SetInt(SfxKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool VibrationEnabled
        {
            get => PlayerPrefs.GetInt(VibrationKey, 1) != 0;
            set { PlayerPrefs.SetInt(VibrationKey, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static void Reset()
        {
            PlayerPrefs.DeleteKey(MusicKey);
            PlayerPrefs.DeleteKey(SfxKey);
            PlayerPrefs.DeleteKey(VibrationKey);
            PlayerPrefs.Save();
        }
    }
}