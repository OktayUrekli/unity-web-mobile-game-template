using System.Collections.Generic;

namespace _Core.Platform.Services.Analytics
{
    /// <summary>
    /// Provides analytics functionality.
    /// </summary>
    public interface IAnalyticsService
    {
        /// <summary>
        /// Sends <paramref name="eventName"/> and its optional <paramref name="parameters"/> to the platform's
        /// analytics.
        /// </summary>
        void LogEvent(
            string eventName,
            Dictionary<string, object> parameters = null);
    }
}