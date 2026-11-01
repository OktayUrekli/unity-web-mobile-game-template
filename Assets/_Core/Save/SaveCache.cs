using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Platform.Services.Save;
using UnityEngine;

namespace _Core.Save
{
    /// <summary>
    /// In-memory layer over an <see cref="ISaveStorage"/>.
    /// Reads are served from memory (a key is read from storage once), writes only
    /// mark keys dirty until <see cref="Flush()"/> pushes them to storage. Storages that
    /// also implement <see cref="IAsyncSaveStorage"/> are written asynchronously, one write per key at a time; when
    /// a WebGL page is hidden or closed, <see cref="Flush(bool)"/> also writes keys whose earlier asynchronous write
    /// is still running, and keeps them dirty for one more write.
    /// A key whose storage read failed is never written blindly: it is read again first, and data stored there
    /// that this session never saw wins over the change.
    /// </summary>
    public class SaveCache
    {
        private readonly ISaveStorage _storage;
        private readonly IAsyncSaveStorage _asyncStorage;

        // A null value means "known to have no data" (never saved or deleted).
        private readonly Dictionary<string, string> _values = new();
        private readonly Dictionary<string, int> _sizes = new();
        private readonly HashSet<string> _dirty = new();
        private readonly List<string> _flushBuffer = new();

        // Running asynchronous writes per key. A key has two only after a page-hide flush wrote it while its earlier
        // write was still running.
        private readonly Dictionary<string, int> _writesInFlight = new();

        // Keys read again before a held-back write (see RereadThenWriteAsync). They are never written while that read
        // runs, not even by a page-hide flush.
        private readonly HashSet<string> _rereading = new();

        // Keys whose storage read failed: what is stored there is unknown, so a change to them is written only
        // after a read succeeds (see RereadThenWriteAsync).
        private readonly HashSet<string> _unreadable = new();

        /// <summary>
        /// Creates a cache over <paramref name="storage"/>.
        /// </summary>
        public SaveCache(ISaveStorage storage)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _asyncStorage = storage as IAsyncSaveStorage;
        }

        /// <summary>
        /// Raised with the key when writing it to storage failed, or was held back because the key still cannot be
        /// read. The key stays dirty, so the next <see cref="Flush()"/> retries it; <see cref="SaveManager"/>
        /// schedules that retry.
        /// </summary>
        public event Action<string> WriteFailed;

        /// <summary>
        /// True while some changes have not been written to storage yet. A key that a page-hide flush wrote while an
        /// earlier write of it was still running stays pending until it is written once more.
        /// </summary>
        public bool HasPendingChanges => _dirty.Count > 0;

        /// <summary>
        /// True while asynchronous writes, or reads that come before a held-back write, are still running.
        /// </summary>
        public bool HasWritesInFlight => _writesInFlight.Count > 0 || _rereading.Count > 0;

        /// <summary>
        /// Approximate size in bytes of all keys and values known to the cache, counted as <see cref="MeasureBytes"/>
        /// counts them. Keys never read or written in this session are not counted.
        /// </summary>
        public int TotalBytes { get; private set; }

        /// <summary>
        /// Bytes a key and its value count toward a storage's size limit: UTF-8 inside JSON strings, the way web
        /// storages serialize saves, so quotes, backslashes and control characters count more than once. 0 when
        /// <paramref name="value"/> is null.
        /// </summary>
        public static int MeasureBytes(string key, string value)
        {
            return value == null ? 0 : JsonStringBytes(key) + JsonStringBytes(value);
        }

        /// <summary>
        /// Bytes <paramref name="key"/> counts toward <see cref="TotalBytes"/> now; 0 when it holds no value or was
        /// never read.
        /// </summary>
        internal int GetBytes(string key)
        {
            return _sizes.TryGetValue(key, out int size) ? size : 0;
        }

        /// <summary>
        /// Gets the stored value for <paramref name="key"/>, reading storage on first access.
        /// Returns false when nothing (or an empty string) is stored, or when storage could not be read.
        /// </summary>
        public bool TryGet(string key, out string value)
        {
            if (!_values.TryGetValue(key, out value))
            {
                // A failed read is not cached, so the next access retries it.
                if (!TryReadFromStorage(key, out value))
                {
                    _unreadable.Add(key);
                    return false;
                }

                // An async storage may answer a sync read from its own cache (null for keys it has not loaded),
                // so only its LoadAsync clears a failed read.
                if (_asyncStorage == null)
                    _unreadable.Remove(key);

                SetValue(key, value);
            }

            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Stores <paramref name="value"/> in memory and marks the key for the next flush.
        /// </summary>
        public void Set(string key, string value)
        {
            SetValue(key, value);
            _dirty.Add(key);
        }

        /// <summary>
        /// Removes the key in memory and marks it for deletion on the next flush.
        /// </summary>
        public void Remove(string key)
        {
            SetValue(key, null);
            _dirty.Add(key);
        }

        /// <summary>
        /// Loads the given keys into memory. Uses <see cref="IAsyncSaveStorage.LoadAsync"/>
        /// when available. Keys already in memory are skipped.
        /// </summary>
        public async Task PreloadAsync(IEnumerable<string> keys)
        {
            foreach (string key in keys)
            {
                if (string.IsNullOrEmpty(key) || _values.ContainsKey(key))
                    continue;

                string value;

                if (_asyncStorage != null)
                {
                    try
                    {
                        value = await _asyncStorage.LoadAsync(key);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"SaveCache: failed to preload '{key}'.");
                        Debug.LogException(e);
                        _unreadable.Add(key);
                        continue;
                    }
                }
                else if (!TryReadFromStorage(key, out value))
                {
                    _unreadable.Add(key);
                    continue;
                }

                _unreadable.Remove(key);

                // A Set/Remove issued while awaiting wins over the loaded value.
                if (!_values.ContainsKey(key))
                    SetValue(key, value);
            }
        }

        /// <summary>
        /// Writes every dirty key to storage. Keys whose asynchronous write is still
        /// running stay dirty; failed writes stay dirty and are retried on the next flush.
        /// A key whose read failed is read again before it is written.
        /// <see cref="Flush(bool)"/> is the variant for a page that is hidden or closing.
        /// </summary>
        public void Flush()
        {
            Flush(false);
        }

        /// <summary>
        /// Writes every dirty key to storage. A key whose asynchronous write is still running stays dirty for a later
        /// flush. With <paramref name="includeKeysInFlight"/>, which <see cref="SaveManager"/> passes only when a WebGL
        /// page is hidden or closed, its newest value is also written at once, and the key still stays dirty until a
        /// later flush writes it after its writes have ended. Failed writes stay dirty and are retried on the next
        /// flush. A key whose read failed is read again before it is written, and is never written while that read
        /// runs.
        /// </summary>
        public void Flush(bool includeKeysInFlight)
        {
            if (_dirty.Count == 0)
                return;

            _flushBuffer.Clear();
            _flushBuffer.AddRange(_dirty);

            foreach (string key in _flushBuffer)
            {
                // Being read again (RereadThenWriteAsync): written, or dropped, once that read ends.
                if (_rereading.Contains(key))
                    continue;

                _values.TryGetValue(key, out string value);

                // Never write over data that could not be read; a delete is written as asked.
                bool unreadable = value != null && _unreadable.Contains(key);

                if (_writesInFlight.ContainsKey(key))
                {
                    // The page is hidden or closing, so a later flush may never run: the newest value goes out now.
                    // The key stays dirty, so once its writes end a normal flush writes it once more, and the newest
                    // value lands last even if the storage applies the earlier request after this one.
                    if (includeKeysInFlight && !unreadable)
                        _ = WriteAsync(key, value);

                    continue;
                }

                if (unreadable)
                {
                    _ = RereadThenWriteAsync(key);
                    continue;
                }

                _dirty.Remove(key);

                if (_asyncStorage != null)
                    _ = WriteAsync(key, value);
                else
                    WriteSync(key, value);
            }

            _flushBuffer.Clear();
        }

        // Reads a key whose earlier read failed, then decides. Nothing stored: the change is written. Data stored:
        // this session never saw it and built the change without it, so the stored data wins and the change is
        // dropped. Still unreadable: the change stays pending and WriteFailed schedules a retry.
        // Runs synchronously for synchronous storages.
        private async Task RereadThenWriteAsync(string key)
        {
            string stored = null;
            bool readSucceeded;
            _rereading.Add(key);

            try
            {
                if (_asyncStorage != null)
                {
                    stored = await _asyncStorage.LoadAsync(key);
                    readSucceeded = true;
                }
                else
                {
                    readSucceeded = TryReadFromStorage(key, out stored);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveCache: failed to read '{key}'.");
                Debug.LogException(e);
                readSucceeded = false;
            }
            finally
            {
                _rereading.Remove(key);
            }

            if (!readSucceeded)
            {
                Debug.LogWarning($"SaveCache: '{key}' still cannot be read; its change is kept and written once it can.");
                WriteFailed?.Invoke(key);
                return;
            }

            _unreadable.Remove(key);
            _dirty.Remove(key);

            if (!string.IsNullOrEmpty(stored))
            {
                Debug.LogWarning(
                    $"SaveCache: '{key}' could not be read when it was changed and holds saved data now; " +
                    "keeping the saved data and dropping this session's change.");
                SetValue(key, stored);
                return;
            }

            // The latest value: a Set or Remove may have happened while an async read was running.
            _values.TryGetValue(key, out string value);

            if (_asyncStorage != null)
                await WriteAsync(key, value);
            else
                WriteSync(key, value);
        }

        private void WriteSync(string key, string value)
        {
            try
            {
                if (value == null)
                    _storage.Delete(key);
                else
                    _storage.Save(key, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveCache: failed to write '{key}'. It will be retried on the next flush.");
                Debug.LogException(e);
                _dirty.Add(key);
                WriteFailed?.Invoke(key);
            }
        }

        private async Task WriteAsync(string key, string value)
        {
            _writesInFlight.TryGetValue(key, out int running);
            _writesInFlight[key] = running + 1;
            bool failed = false;

            try
            {
                if (value == null)
                    await _asyncStorage.DeleteAsync(key);
                else
                    await _asyncStorage.SaveAsync(key, value);
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveCache: failed to write '{key}'. It will be retried on the next flush.");
                Debug.LogException(e);
                _dirty.Add(key);
                failed = true;
            }
            finally
            {
                // The key counts as in flight until its last write ends.
                if (_writesInFlight.TryGetValue(key, out running) && running > 1)
                    _writesInFlight[key] = running - 1;
                else
                    _writesInFlight.Remove(key);
            }

            if (failed)
                WriteFailed?.Invoke(key);
        }

        private bool TryReadFromStorage(string key, out string value)
        {
            try
            {
                value = _storage.HasSave(key) ? _storage.Load(key) : null;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"SaveCache: failed to read '{key}'.");
                Debug.LogException(e);
                value = null;
                return false;
            }
        }

        private void SetValue(string key, string value)
        {
            if (_sizes.TryGetValue(key, out int oldSize))
                TotalBytes -= oldSize;

            int newSize = MeasureBytes(key, value);

            _sizes[key] = newSize;
            _values[key] = value;
            TotalBytes += newSize;
        }

        // UTF-8 bytes of text written as a JSON string, without its surrounding quotes. Like JSON.stringify, this
        // escapes quotes, backslashes, control characters and lone surrogates.
        private static int JsonStringBytes(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;

            int bytes = 0;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                // A character outside the Basic Multilingual Plane: 4 bytes for the surrogate pair.
                if (char.IsHighSurrogate(c) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    bytes += 4;
                    i++;
                    continue;
                }

                if (c == '"' || c == '\\' || c == '\b' || c == '\f' || c == '\n' || c == '\r' || c == '\t')
                    bytes += 2;
                else if (c < 0x20 || char.IsSurrogate(c))
                    bytes += 6;
                else if (c < 0x80)
                    bytes += 1;
                else if (c < 0x800)
                    bytes += 2;
                else
                    bytes += 3;
            }

            return bytes;
        }
    }
}
