using System;

namespace _Core.Platform.Services.Privacy.Null
{
    /// <summary>
    /// Provides a safe fallback for platforms without a privacy options form.
    /// </summary>
    public class NullPrivacyService : IPrivacyService
    {
        public bool IsPrivacyOptionsRequired => false;

        public void ShowPrivacyOptions(Action onClosed)
        {
            // There is no privacy options form on this platform.
            onClosed?.Invoke();
        }
    }
}
