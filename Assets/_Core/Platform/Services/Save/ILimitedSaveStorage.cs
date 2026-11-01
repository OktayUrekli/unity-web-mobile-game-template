using System.Collections.Generic;

namespace _Core.Platform.Services.Save
{
    /// <summary>
    /// Optional interface for storages that hold a limited amount of save data. Implement it next to
    /// <see cref="ISaveStorage"/>: <see cref="_Core.Save.SaveManager"/> then warns as the saves near
    /// <see cref="LimitBytes"/> and refuses a save that would grow them past it, keeping the last saved data, because
    /// the platform may reject or drop data past its limit. A platform with such a limit also passes it to its
    /// PlayerPrefs fallback (the <see cref="_Core.Save.Local.PlayerPrefsSaveStorage"/> constructor that takes a
    /// limit).
    /// </summary>
    public interface ILimitedSaveStorage
    {
        /// <summary>
        /// The most bytes all keys and values together may take, counted as
        /// <see cref="_Core.Save.SaveCache.MeasureBytes"/> counts them; 0 or less for no limit.
        /// </summary>
        int LimitBytes { get; }

        /// <summary>
        /// Every key the storage holds, so that <see cref="_Core.Save.SaveManager"/> loads them all when it starts and
        /// counts them toward <see cref="LimitBytes"/>. Null when the storage can't list its keys: then only keys read
        /// or written in the session count, and data the session never touched can still push the stored total past
        /// the limit.
        /// </summary>
        IEnumerable<string> StoredKeys { get; }
    }
}
