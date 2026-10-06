using UnityEngine;

namespace _Core.Audio
{
    /// <summary>
    /// One sound or music track: the clip and how to play it. Create one asset per sound
    /// (Create > Audio > Sound), reference it from a serialized field and play it with
    /// <see cref="AudioManager.PlaySfx"/> or <see cref="AudioManager.PlayMusic"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Sound", menuName = "Audio/Sound")]
    public class SoundData : ScriptableObject
    {
        [SerializeField] private AudioClip clip;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 1f;

        [Range(0.1f, 3f)]
        [SerializeField] private float pitch = 1f;

        [Tooltip("Music only: loop the track.")]
        [SerializeField] private bool loop;

        /// <summary>
        /// The clip to play; a sound without one plays nothing.
        /// </summary>
        public AudioClip Clip => clip;

        /// <summary>
        /// Volume, 0 to 1.
        /// </summary>
        public float Volume => volume;

        /// <summary>
        /// Playback pitch (1 = original).
        /// </summary>
        public float Pitch => pitch;

        /// <summary>
        /// True when music loops.
        /// </summary>
        public bool Loop => loop;
    }
}
