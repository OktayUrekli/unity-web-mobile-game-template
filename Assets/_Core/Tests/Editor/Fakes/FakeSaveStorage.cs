using System;
using System.Collections.Generic;
using _Core.Platform.Services.Save;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// In-memory synchronous <see cref="ISaveStorage"/> that counts calls and can be told to fail.
    /// </summary>
    public class FakeSaveStorage : ISaveStorage
    {
        /// <summary>
        /// The stored values by key; tests may seed or inspect it directly.
        /// </summary>
        public Dictionary<string, string> Values { get; } = new();

        /// <summary>
        /// Number of <see cref="Load"/> calls.
        /// </summary>
        public int LoadCount { get; private set; }

        /// <summary>
        /// Number of successful <see cref="Save"/> calls.
        /// </summary>
        public int SaveCount { get; private set; }

        /// <summary>
        /// Number of <see cref="Delete"/> calls.
        /// </summary>
        public int DeleteCount { get; private set; }

        /// <summary>
        /// When true, <see cref="Save"/> throws an <see cref="InvalidOperationException"/>.
        /// </summary>
        public bool ThrowOnSave { get; set; }

        /// <summary>
        /// When true, <see cref="HasSave"/> and <see cref="Load"/> throw an <see cref="InvalidOperationException"/>.
        /// </summary>
        public bool ThrowOnRead { get; set; }

        /// <inheritdoc />
        public bool HasSave(string key)
        {
            if (ThrowOnRead)
                throw new InvalidOperationException("FakeSaveStorage read failed");

            return Values.ContainsKey(key);
        }

        /// <inheritdoc />
        public void Save(string key, string data)
        {
            if (ThrowOnSave)
                throw new InvalidOperationException("FakeSaveStorage save failed");

            SaveCount++;
            Values[key] = data;
        }

        /// <inheritdoc />
        public string Load(string key)
        {
            if (ThrowOnRead)
                throw new InvalidOperationException("FakeSaveStorage read failed");

            LoadCount++;
            return Values.TryGetValue(key, out string value) ? value : null;
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
            DeleteCount++;
            Values.Remove(key);
        }
    }
}
