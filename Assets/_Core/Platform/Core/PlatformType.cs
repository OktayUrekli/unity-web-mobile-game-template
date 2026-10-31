namespace _Core.Platform.Core
{
    /// <summary>
    /// Represents supported target platforms. PlatformConfig assets store the value as a number, so the values are
    /// fixed: add a new platform at the end with the next number, and never reorder or renumber.
    /// </summary>
    public enum PlatformType
    {
        None = 0,

        GooglePlay = 1,
        CrazyGames = 2,
        YandexGames = 3
    }
}