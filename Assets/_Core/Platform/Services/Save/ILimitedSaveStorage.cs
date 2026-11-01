namespace _Core.Platform.Services.Save
{
    /// <summary>
    /// Optional interface for storages that hold a limited amount of save data. Implement it next to
    /// <see cref="ISaveStorage"/>: <see cref="_Core.Save.SaveManager"/> then warns as the saves near
    /// <see cref="LimitBytes"/> and refuses a save that would grow them past it, keeping the last saved data, because
    /// the platform would reject that write. A platform with such a limit also passes it to its PlayerPrefs fallback
    /// (the <see cref="_Core.Save.Local.PlayerPrefsSaveStorage"/> constructor that takes a limit).
    /// </summary>
    public interface ILimitedSaveStorage
    {
        /// <summary>
        /// The most bytes all keys and values together may take, counted as
        /// <see cref="_Core.Save.SaveCache.MeasureBytes"/> counts them; 0 or less for no limit.
        /// </summary>
        int LimitBytes { get; }
    }
}
