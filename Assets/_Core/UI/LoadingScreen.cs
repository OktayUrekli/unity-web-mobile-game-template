using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace _Core.UI
{
    /// <summary>
    /// Boot loading screen in the bootstrap scene: game logo, game name and a progress bar.
    /// The <see cref="Bootstrap.Bootstrapper"/> reports real progress (platform, localization, managers,
    /// first scene load); the bar moves smoothly towards it. Destroyed with the bootstrap scene.
    /// Games change only the logo sprite and the name text on the prefab.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private Image logoImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private Image progressFill;
        [Tooltip("Bar speed in full widths per second; keeps step jumps from looking abrupt.")]
        [SerializeField] private float fillSpeed = 2f;

        // Longest CompleteAsync waits for the bar animation; the boot never waits on the bar beyond this.
        private const float MaxCompleteSeconds = 1f;

        private float _target;
        private float _displayed;

        /// <summary>
        /// Progress shown by the bar right now (0..1); trails the reported progress.
        /// </summary>
        public float DisplayedProgress => _displayed;

        private void Awake()
        {
            // The game name is not localized; an empty text falls back to Player Settings > Product Name.
            if (titleText != null && string.IsNullOrWhiteSpace(titleText.text))
                titleText.text = Application.productName;

            if (logoImage != null && logoImage.sprite == null)
                logoImage.enabled = false;

            ApplyFill();
        }

        private void Update()
        {
            // Exact comparison: MoveTowards lands exactly on the target. An approximate one could stop the bar
            // a hair below 1, and CompleteAsync would then wait for it forever.
            if (_displayed >= _target)
                return;

            _displayed = Mathf.MoveTowards(_displayed, _target, fillSpeed * Time.unscaledDeltaTime);
            ApplyFill();
        }

        /// <summary>
        /// Reports overall loading progress (0..1). Never moves the bar backwards.
        /// </summary>
        public void SetProgress(float progress)
        {
            _target = Mathf.Max(_target, Mathf.Clamp01(progress));
        }

        /// <summary>
        /// Sets the bar to full and waits until it is drawn full, so the player sees it complete.
        /// Frame-polled (no threads on WebGL) and bounded: after <see cref="MaxCompleteSeconds"/> the bar
        /// jumps to full.
        /// </summary>
        public async Task CompleteAsync()
        {
            SetProgress(1f);

            float deadline = Time.realtimeSinceStartup + MaxCompleteSeconds;
            while (this != null && isActiveAndEnabled && _displayed < 1f && Time.realtimeSinceStartup < deadline)
                await Task.Yield();

            if (this == null)
                return;

            _displayed = 1f;
            ApplyFill();
        }

        private void ApplyFill()
        {
            if (progressFill != null)
                progressFill.fillAmount = _displayed;
        }
    }
}
