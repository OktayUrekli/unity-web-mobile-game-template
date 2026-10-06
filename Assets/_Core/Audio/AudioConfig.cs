using UnityEngine;
using UnityEngine.Audio;

namespace _Core.Audio
{
    /// <summary>
    /// The audio mixer and its groups, and the sounds the framework itself plays.
    /// The mixer must expose <c>MusicVolume</c> and <c>SFXVolume</c>.
    /// </summary>
    [CreateAssetMenu(fileName = "AudioConfig", menuName = "Audio/AudioConfig")]
    public class AudioConfig : ScriptableObject
    {
        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Tooltip("Played by every ButtonClickSound that has no sound of its own.")]
        [SerializeField] private SoundData buttonClickSound;

        /// <summary>
        /// Mixer whose exposed volumes mute music and sound effects.
        /// </summary>
        public AudioMixer AudioMixer => audioMixer;

        /// <summary>
        /// Output group of the music source.
        /// </summary>
        public AudioMixerGroup MusicGroup => musicGroup;

        /// <summary>
        /// Output group of the sound effect sources.
        /// </summary>
        public AudioMixerGroup SfxGroup => sfxGroup;

        /// <summary>
        /// Default UI button click.
        /// </summary>
        public SoundData ButtonClickSound => buttonClickSound;
    }
}
