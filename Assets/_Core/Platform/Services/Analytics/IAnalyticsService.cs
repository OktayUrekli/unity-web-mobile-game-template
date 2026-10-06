using System.Collections.Generic;

namespace _Core.Platform.Services.Analytics
{
    /// <summary>
    /// Provides analytics functionality.
    /// </summary>
    public interface IAnalyticsService
    {
        void LogEvent(
            string eventName,
            Dictionary<string, object> parameters = null);
    }
}