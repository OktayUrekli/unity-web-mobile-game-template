using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Platform.Services.Save;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IAsyncSaveStorage"/> whose writes stay pending until the test completes or fails them, and whose
    /// loads can be held the same way, so in-flight behaviour can be checked without waiting. The synchronous
    /// <see cref="HasSave"/> and <see cref="Load"/> answer false and null, like an async storage that answers
    /// synchronous reads only from what it has loaded.
    /// </summary>
    public class FakeAsyncSaveStorage : ISaveStorage, IAsyncSaveStorage
    {
        private sealed class PendingWrite
        {
            public string Key;

            // Null for a delete.
            public string Data;

            public TaskCompletionSource<bool> Completion;
        }

        private sealed class PendingLoad
        {
            public string Key;
            public TaskCompletionSource<string> Completion;
        }

        private readonly List<PendingWrite> _pendingWrites = new();
        private readonly List<PendingLoad> _pendingLoads = new();
        private readonly List<string> _savedData = new();

        /// <summary>
        /// What <see cref="LoadAsync"/> answers; tests may seed it directly. A write that completes successfully
        /// stores its data here, and a completed delete removes the key.
        /// </summary>
        public Dictionary<string, string> Values { get; } = new();

        /// <summary>
        /// Number of <see cref="SaveAsync"/> calls.
        /// </summary>
        public int SaveAsyncCount { get; private set; }

        /// <summary>
        /// The data of every <see cref="SaveAsync"/> call, in call order.
        /// </summary>
        public IReadOnlyList<string> SavedData => _savedData;

        /// <summary>
        /// Number of writes (saves and deletes) that are still pending.
        /// </summary>
        public int PendingWriteCount => _pendingWrites.Count;

        /// <summary>
        /// When true, <see cref="LoadAsync"/> returns a task that fails with an
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        public bool ThrowOnLoad { get; set; }

        /// <summary>
        /// When true, <see cref="LoadAsync"/> returns a task that stays pending until <see cref="CompleteLoads"/>.
        /// </summary>
        public bool HoldLoads { get; set; }

        /// <inheritdoc />
        public bool HasSave(string key)
        {
            return false;
        }

        /// <inheritdoc />
        public void Save(string key, string data)
        {
        }

        /// <inheritdoc />
        public string Load(string key)
        {
            return null;
        }

        /// <inheritdoc />
        public void Delete(string key)
        {
        }

        /// <inheritdoc />
        public Task<string> LoadAsync(string key)
        {
            if (ThrowOnLoad)
                return Task.FromException<string>(new InvalidOperationException("FakeAsyncSaveStorage load failed"));

            if (HoldLoads)
            {
                var completion = new TaskCompletionSource<string>();
                _pendingLoads.Add(new PendingLoad { Key = key, Completion = completion });
                return completion.Task;
            }

            return Task.FromResult(GetValue(key));
        }

        /// <inheritdoc />
        public Task SaveAsync(string key, string data)
        {
            SaveAsyncCount++;
            _savedData.Add(data);
            return AddPendingWrite(key, data);
        }

        /// <inheritdoc />
        public Task DeleteAsync(string key)
        {
            return AddPendingWrite(key, null);
        }

        /// <summary>
        /// Completes one pending write successfully and applies it to <see cref="Values"/>. 0 is the oldest write that
        /// is still pending.
        /// </summary>
        public void CompleteWrite(int index)
        {
            PendingWrite write = TakeWrite(index);
            Apply(write);
            write.Completion.TrySetResult(true);
        }

        /// <summary>
        /// Fails one pending write with an <see cref="InvalidOperationException"/>; <see cref="Values"/> is unchanged.
        /// 0 is the oldest write that is still pending.
        /// </summary>
        public void FailWrite(int index)
        {
            PendingWrite write = TakeWrite(index);
            write.Completion.TrySetException(new InvalidOperationException("FakeAsyncSaveStorage write failed"));
        }

        /// <summary>
        /// Completes every pending write successfully, oldest first, and applies each to <see cref="Values"/>.
        /// </summary>
        public void CompleteAllWrites()
        {
            PendingWrite[] writes = _pendingWrites.ToArray();
            _pendingWrites.Clear();

            foreach (PendingWrite write in writes)
            {
                Apply(write);
                write.Completion.TrySetResult(true);
            }
        }

        /// <summary>
        /// Completes every held load with the current entry of <see cref="Values"/>, or null when it has none.
        /// </summary>
        public void CompleteLoads()
        {
            PendingLoad[] loads = _pendingLoads.ToArray();
            _pendingLoads.Clear();

            foreach (PendingLoad load in loads)
                load.Completion.TrySetResult(GetValue(load.Key));
        }

        private Task AddPendingWrite(string key, string data)
        {
            var completion = new TaskCompletionSource<bool>();
            _pendingWrites.Add(new PendingWrite { Key = key, Data = data, Completion = completion });
            return completion.Task;
        }

        private PendingWrite TakeWrite(int index)
        {
            PendingWrite write = _pendingWrites[index];
            _pendingWrites.RemoveAt(index);
            return write;
        }

        private void Apply(PendingWrite write)
        {
            if (write.Data == null)
                Values.Remove(write.Key);
            else
                Values[write.Key] = write.Data;
        }

        private string GetValue(string key)
        {
            return Values.TryGetValue(key, out string value) ? value : null;
        }
    }
}
