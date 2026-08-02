using UnityEngine;
using FlowBlast.Data;

namespace FlowBlast.Managers
{
    /// <summary>
    /// Singleton audio system. Manages two AudioSources (music + SFX).
    /// Clips are looked up by <see cref="AudioId"/> from an <see cref="AudioLibrarySO"/>.
    /// Integrates with <see cref="SaveManager"/> for persistent mute settings.
    ///
    /// Setup:
    ///   1. Create an AudioLibrarySO asset (FlowBlast → Audio Library).
    ///   2. Assign clips in the Inspector.
    ///   3. Attach AudioManager to a persistent GameObject.
    ///   4. Drag the AudioLibrarySO into the _library field.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Library")]
        [SerializeField] private AudioLibrarySO _library;

        [Header("Sources")]
        [SerializeField] private AudioSource _musicSource;
        [SerializeField] private AudioSource _sfxSource;

        [Header("Defaults")]
        [SerializeField, Range(0f, 1f)] private float _defaultMusicVolume = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _defaultSfxVolume = 1f;

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

            EnsureSources();
            ApplySavedSettings();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ─── Initialisation ─────────────────────────────────────────────

        private void EnsureSources()
        {
            if (_musicSource == null)
            {
                GameObject musicGO = new GameObject("MusicSource");
                musicGO.transform.SetParent(transform);
                _musicSource = musicGO.AddComponent<AudioSource>();
                _musicSource.loop = true;
                _musicSource.playOnAwake = false;
            }

            if (_sfxSource == null)
            {
                GameObject sfxGO = new GameObject("SFXSource");
                sfxGO.transform.SetParent(transform);
                _sfxSource = sfxGO.AddComponent<AudioSource>();
                _sfxSource.loop = false;
                _sfxSource.playOnAwake = false;
            }
        }

        private void ApplySavedSettings()
        {
            SaveData save = SaveManager.Instance != null ? SaveManager.Instance.Data : null;
            if (save != null)
            {
                _musicSource.mute = !save.IsMusicOn;
                _sfxSource.mute = !save.IsSfxOn;
            }

            _musicSource.volume = _defaultMusicVolume;
            _sfxSource.volume = _defaultSfxVolume;
        }

        // ─── Public SFX API ─────────────────────────────────────────────

        /// <summary>Play a one-shot SFX by AudioId.</summary>
        public void PlaySfx(AudioId id)
        {
            if (_library == null) return;
            var (clip, volume) = _library.Get(id);
            if (clip != null)
                _sfxSource.PlayOneShot(clip, volume * _sfxSource.volume);
        }

        /// <summary>Play a one-shot SFX from a raw AudioClip.</summary>
        public void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip != null)
                _sfxSource.PlayOneShot(clip, volume * _sfxSource.volume);
        }

        // ─── Public Music API ───────────────────────────────────────────

        /// <summary>Start playing background music by AudioId. Loops by default.</summary>
        public void PlayMusic(AudioId id, bool loop = true)
        {
            if (_library == null) return;
            var (clip, volume) = _library.Get(id);
            if (clip == null) return;

            _musicSource.clip = clip;
            _musicSource.volume = volume * _defaultMusicVolume;
            _musicSource.loop = loop;
            _musicSource.Play();
        }

        /// <summary>Stop the currently playing music.</summary>
        public void StopMusic()
        {
            _musicSource.Stop();
        }

        // ─── Volume & Mute ──────────────────────────────────────────────

        public void SetMusicVolume(float volume)
        {
            _defaultMusicVolume = Mathf.Clamp01(volume);
            _musicSource.volume = _defaultMusicVolume;
        }

        public void SetSfxVolume(float volume)
        {
            _defaultSfxVolume = Mathf.Clamp01(volume);
            _sfxSource.volume = _defaultSfxVolume;
        }

        /// <summary>Toggle music on/off. Persists via SaveManager.</summary>
        public void ToggleMusic()
        {
            _musicSource.mute = !_musicSource.mute;
            PersistMuteSettings();
        }

        /// <summary>Toggle SFX on/off. Persists via SaveManager.</summary>
        public void ToggleSfx()
        {
            _sfxSource.mute = !_sfxSource.mute;
            PersistMuteSettings();
        }

        public bool IsMusicOn => !_musicSource.mute;
        public bool IsSfxOn => !_sfxSource.mute;

        private void PersistMuteSettings()
        {
            if (SaveManager.Instance == null) return;
            SaveManager.Instance.Data.IsMusicOn = IsMusicOn;
            SaveManager.Instance.Data.IsSfxOn = IsSfxOn;
            SaveManager.Instance.Save();
        }
    }
}
