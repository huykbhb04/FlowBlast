using System;
using UnityEngine;

namespace FlowBlast.Managers
{
    /// <summary>
    /// ScriptableObject mapping <see cref="AudioId"/> → <see cref="AudioClip"/>.
    /// Create via: Assets → Create → FlowBlast → Audio Library.
    /// Assign clips in the Inspector, then drag the asset into <see cref="AudioManager"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioLibrary", menuName = "FlowBlast/Audio Library", order = 10)]
    public class AudioLibrarySO : ScriptableObject
    {
        [Serializable]
        public struct AudioEntry
        {
            public AudioId Id;
            public AudioClip Clip;
            [Range(0f, 1f)] public float Volume;

            public AudioEntry(AudioId id, AudioClip clip, float volume = 1f)
            {
                Id = id;
                Clip = clip;
                Volume = volume;
            }
        }

        [SerializeField] private AudioEntry[] _entries = new AudioEntry[0];

        /// <summary>
        /// Look up a clip by <see cref="AudioId"/>.
        /// Returns (clip, volume). Clip is null if not found.
        /// </summary>
        public (AudioClip clip, float volume) Get(AudioId id)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i].Id == id)
                    return (_entries[i].Clip, _entries[i].Volume);
            }
            return (null, 1f);
        }
    }
}
