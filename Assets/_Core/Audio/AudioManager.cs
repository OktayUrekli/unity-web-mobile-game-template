using System.Collections.Generic;
using _Core.Configuration;
using UnityEngine;
using _Core.Managers;
using _Core.Events;
using _Core.Events.Ads;
using _Core.Events.Platform;
using _Core.Events.Settings;
using _Core.Platform.Core;

namespace _Core.Audio
{
    /// <summary>
    /// Plays music and sound effects described by <see cref="SoundData"/> assets, and silences them for the player's
    /// settings, ad requests and platform mute or pause requests (through the mixer, so playback continues).
    /// </summary>
    public class AudioManager : Singleton<AudioManager>
    {
        private const string MusicVolumeParameter = "MusicVolume";
        private const string SfxVolumeParameter = "SFXVolume";
        private const float SilentDecibels = -80f;

        // A few sources so a sound with its own pitch never re-pitches one-shots still playing.
        private const int SfxSourceCount = 4;

        private AudioConfig _audioConfig;

        private AudioSource _musicSource;
        private readonly List<AudioSource> _sfxSources = new();
        private int _nextSfxSource;

        private bool _musicEnabled = true;
        private bool _sfxEnabled = true;
        private bool _adMuted;
        private bool _platformMuted;
        private bool _platformPaused;

        /// <summary>
        /// True while an advertisement request mutes all audio.
        /// </summary>
        public bool IsMutedForAd => _adMuted;

        /// <summary>
        /// True while the platform asks the game to be silent or to pause.
        /// </summary>
        public bool IsMutedByPlatform => _platformMuted || _platformPaused;

        /// <summary>
        /// The music playing now, or null.
        /// </summary>
        public SoundData CurrentMusic { get; private set; }

        /// <summary>
        /// Creates the audio sources and subscribes to everything that mutes them. Called by the Bootstrapper
        /// before <c>SettingsManager</c>, which publishes the player's music/SFX choices.
        /// </summary>
        public void Init()
        {
            GameConfig gameConfig = ConfigurationManager.GameConfig;
            _audioConfig = gameConfig != null ? gameConfig.AudioConfig : null;
            if (_audioConfig == null)
                Debug.LogError("AudioManager: GameConfig has no AudioConfig assigned; audio plays without the mixer.");

            CreateAudioSources();

            // The player's music/SFX choices; SettingsManager publishes them when it loads and on every change.
            EventBus.Subscribe<AudioSettingsChangedEvent>(OnAudioSettingsChanged);

            // Mute for the whole ad request (from request until completion), not only while the ad plays.
            EventBus.Subscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Subscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);

            // The platform (e.g. CrazyGames "muteAudio") can ask for silence at any time.
            PlatformManager platform = PlatformManager.Instance;
            _platformMuted = platform != null && platform.Game != null && platform.Game.IsAudioMutedByPlatform;
            EventBus.Subscribe<PlatformAudioMuteChangedEvent>(OnPlatformAudioMuteChanged);

            // A paused game must be silent too (Yandex Games requires it).
            _platformPaused = platform != null && platform.Game != null && platform.Game.IsPausedByPlatform;
            EventBus.Subscribe<PlatformPauseChangedEvent>(OnPlatformPauseChanged);
            ApplyMixerVolumes();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            // Unsubscribe from global events before destroying the manager.
            EventBus.Unsubscribe<AudioSettingsChangedEvent>(OnAudioSettingsChanged);
            EventBus.Unsubscribe<AdRequestedEvent>(OnAdRequested);
            EventBus.Unsubscribe<AdRequestCompletedEvent>(OnAdRequestCompleted);
            EventBus.Unsubscribe<PlatformAudioMuteChangedEvent>(OnPlatformAudioMuteChanged);
            EventBus.Unsubscribe<PlatformPauseChangedEvent>(OnPlatformPauseChanged);
        }

        /// <summary>
        /// Plays <paramref name="music"/> on the music source. Asking for the track that is already playing keeps it
        /// going, so every scene can request its music (see <see cref="SceneMusic"/>) without restarting it.
        /// </summary>
        public void PlayMusic(SoundData music)
        {
            if (!IsPlayable(music) || _musicSource == null)
                return;

            if (_musicSource.isPlaying && CurrentMusic == music)
                return;

            CurrentMusic = music;
            _musicSource.clip = music.Clip;
            _musicSource.volume = music.Volume;
            _musicSource.pitch = music.Pitch;
            _musicSource.loop = music.Loop;
            _musicSource.Play();
        }

        /// <summary>
        /// Stops the current music.
        /// </summary>
        public void StopMusic()
        {
            CurrentMusic = null;

            if (_musicSource != null)
                _musicSource.Stop();
        }

        /// <summary>
        /// Plays a sound effect.
        /// </summary>
        public void PlaySfx(SoundData sound)
        {
            if (!IsPlayable(sound) || _sfxSources.Count == 0)
                return;

            AudioSource source = GetSfxSource(sound.Pitch);
            source.pitch = sound.Pitch;
            source.PlayOneShot(sound.Clip, sound.Volume);
        }

        /// <summary>
        /// Plays the default UI click (<see cref="AudioConfig.ButtonClickSound"/>).
        /// </summary>
        public void PlayButtonClick()
        {
            if (_audioConfig != null && _audioConfig.ButtonClickSound != null)
                PlaySfx(_audioConfig.ButtonClickSound);
        }

        private static bool IsPlayable(SoundData sound)
        {
            if (sound == null)
            {
                Debug.LogWarning("AudioManager: no sound assigned; nothing played.");
                return false;
            }

            if (sound.Clip == null)
            {
                Debug.LogWarning($"AudioManager: sound '{sound.name}' has no clip.", sound);
                return false;
            }

            return true;
        }

        private void OnPlatformAudioMuteChanged(PlatformAudioMuteChangedEvent gameEvent)
        {
            _platformMuted = gameEvent.IsMuted;
            ApplyMixerVolumes();
        }

        private void OnPlatformPauseChanged(PlatformPauseChangedEvent gameEvent)
        {
            _platformPaused = gameEvent.IsPaused;
            ApplyMixerVolumes();
        }

        private void OnAdRequested(AdRequestedEvent gameEvent)
        {
            _adMuted = true;
            ApplyMixerVolumes();
        }

        private void OnAdRequestCompleted(AdRequestCompletedEvent gameEvent)
        {
            _adMuted = false;
            ApplyMixerVolumes();
        }

        // Music off silences the music mixer group; playback continues so turning it on resumes mid-track.
        private void OnAudioSettingsChanged(AudioSettingsChangedEvent gameEvent)
        {
            _musicEnabled = gameEvent.IsMusicEnabled;
            _sfxEnabled = gameEvent.IsSfxEnabled;
            ApplyMixerVolumes();
        }

        /// <summary>
        /// Writes the effective levels to the mixer: full volume for enabled groups, silence for disabled ones or while muted.
        /// </summary>
        private void ApplyMixerVolumes()
        {
            if (_audioConfig == null || _audioConfig.AudioMixer == null)
                return;

            bool muted = _adMuted || _platformMuted || _platformPaused;
            _audioConfig.AudioMixer.SetFloat(MusicVolumeParameter, muted || !_musicEnabled ? SilentDecibels : 0f);
            _audioConfig.AudioMixer.SetFloat(SfxVolumeParameter, muted || !_sfxEnabled ? SilentDecibels : 0f);
        }

        private void CreateAudioSources()
        {
            GameObject musicObject = new GameObject("MusicSource");
            musicObject.transform.SetParent(transform);
            _musicSource = musicObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.outputAudioMixerGroup = _audioConfig != null ? _audioConfig.MusicGroup : null;

            for (int i = 0; i < SfxSourceCount; i++)
            {
                GameObject sfxObject = new GameObject($"SFXSource{i}");
                sfxObject.transform.SetParent(transform);
                AudioSource sfxSource = sfxObject.AddComponent<AudioSource>();
                sfxSource.outputAudioMixerGroup = _audioConfig != null ? _audioConfig.SfxGroup : null;
                sfxSource.playOnAwake = false;
                _sfxSources.Add(sfxSource);
            }
        }

        // A source already at this pitch (one-shots overlap on it), else an idle one, else the next in turn.
        private AudioSource GetSfxSource(float pitch)
        {
            foreach (AudioSource source in _sfxSources)
            {
                if (Mathf.Approximately(source.pitch, pitch))
                    return source;
            }

            foreach (AudioSource source in _sfxSources)
            {
                if (!source.isPlaying)
                    return source;
            }

            AudioSource next = _sfxSources[_nextSfxSource];
            _nextSfxSource = (_nextSfxSource + 1) % _sfxSources.Count;
            return next;
        }
    }
}
