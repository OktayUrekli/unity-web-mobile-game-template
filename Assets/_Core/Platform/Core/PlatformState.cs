namespace _Core.Platform.Core
{
    /// <summary>
    /// Represents the current initialization state of a platform.
    /// </summary>
    public enum PlatformState
    {
        None,
        Initializing,
        Ready,
        Failed
    }
}