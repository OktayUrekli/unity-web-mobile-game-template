using UnityEngine;

namespace _Core.Audio
{
    /// <summary>
    /// Plays a scene's music when the scene starts. Put one in each scene that has music; a track already playing
    /// keeps going, so scenes sharing a track don't restart it. Leave <see cref="music"/> empty to stop the music.
    /// </summary>
    public class SceneMusic : MonoBehaviour
    {
        [Tooltip("Music of this scene. Empty stops the music.")]
        [SerializeField] private SoundData music;

        private void Start()
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
                return;

            if (music != null)
                audioManager.PlayMusic(music);
            else
                audioManager.StopMusic();
        }
    }
}
