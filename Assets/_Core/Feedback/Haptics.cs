using _Core.Settings;
using UnityEngine;

namespace _Core.Feedback
{
    /// <summary>
    /// Device vibration that respects the player's vibration setting (<see cref="SettingsManager.IsVibrationEnabled"/>).
    /// Vibrates on Android and iOS; does nothing in the Editor, on WebGL and on desktop.
    /// </summary>
    public static class Haptics
    {
        /// <summary>
        /// True on platforms where <see cref="Vibrate"/> can vibrate (Android, iOS; also in the Editor while one
        /// of them is the build target). Settings screens hide the vibration toggle elsewhere.
        /// </summary>
        public static bool IsSupported
        {
            get
            {
#if UNITY_ANDROID || UNITY_IOS
                return true;
#else
                return false;
#endif
            }
        }

        /// <summary>
        /// A short vibration (game over, hit, reward). Use sparingly.
        /// </summary>
        public static void Vibrate()
        {
            SettingsManager settings = SettingsManager.Instance;
            if (settings != null && !settings.IsVibrationEnabled)
                return;

#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Referencing Handheld.Vibrate adds the VIBRATE permission to the Android manifest.
            Handheld.Vibrate();
#endif
        }
    }
}
