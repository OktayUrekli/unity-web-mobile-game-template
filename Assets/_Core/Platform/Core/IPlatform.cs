using System.Threading.Tasks;

namespace _Core.Platform.Core
{
    /// <summary>
    /// Represents a target platform implementation.
    /// </summary>
    public interface IPlatform
    {
        /// <summary>
        /// Initializes the platform and its services.
        /// </summary>
        Task  InitializeAsync(PlatformManager manager);

        /// <summary>
        /// Configures the capabilities supported by the platform.
        /// </summary>
        void ConfigureCapabilities(PlatformCapabilities capabilities);
    }
}