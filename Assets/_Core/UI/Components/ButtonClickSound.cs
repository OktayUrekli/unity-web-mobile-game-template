using _Core.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.UI.Components
{
    /// <summary>
    /// Plays a click when its <see cref="Button"/> is clicked: <see cref="sound"/> if set, otherwise the default
    /// <see cref="AudioConfig.ButtonClickSound"/>. Add it to every button that should click, instead of playing the
    /// sound from each click handler.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class ButtonClickSound : MonoBehaviour
    {
        [Tooltip("Optional; empty plays the default button click from AudioConfig.")]
        [SerializeField] private SoundData sound;

        private Button _button;

        private void Awake()
        {
            _button = GetComponent<Button>();
            _button.onClick.AddListener(Play);
        }

        private void OnDestroy()
        {
            if (_button != null)
                _button.onClick.RemoveListener(Play);
        }

        private void Play()
        {
            AudioManager audioManager = AudioManager.Instance;
            if (audioManager == null)
                return;

            if (sound != null)
                audioManager.PlaySfx(sound);
            else
                audioManager.PlayButtonClick();
        }
    }
}
