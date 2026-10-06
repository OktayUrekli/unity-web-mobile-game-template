using System.Collections.Generic;

namespace _Core.Platform.Services.Analytics.Null
{
    /// <summary>
    /// Provides a safe fallback when analytics are not supported.
    /// </summary>
    public class NullAnalyticsService : IAnalyticsService
    {
        public void LogEvent(
            string eventName,
            Dictionary<string, object> parameters = null)
        {
            // Ignore analytics events when analytics are not supported.
        }
    }
}
