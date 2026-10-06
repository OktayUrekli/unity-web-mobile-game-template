using System.Collections.Generic;
using System.Threading.Tasks;
using _Core.Platform.Services.Save;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IAsyncSaveStorage"/> whose writes stay pending until the test completes them,
    /// so in-flight behaviour can be checked without waiting.
    /// </summary>
    public class FakeAsyncSaveStorage : ISaveStorage, IAsyncSaveStorage
    {
        private readonly List<TaskCompletionSource<bool>> _pendingWrites = new();

        /// <summary>
        /// Number of <see cref="SaveAsync"/> calls.
        /// </summary>
        public int SaveAsyncCount { get; private set; }

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
            return Task.FromResult<string>(null);
        }

        /// <inheritdoc />
        public Task SaveAsync(string key, string data)
        {
            SaveAsyncCount++;
            var completion = new TaskCompletionSource<bool>();
            _pendingWrites.Add(completion);
            return completion.Task;
        }

        /// <inheritdoc />
        public Task DeleteAsync(string key)
        {
            var completion = new TaskCompletionSource<bool>();
            _pendingWrites.Add(completion);
            return completion.Task;
        }

        /// <summary>
        /// Completes every pending write successfully.
        /// </summary>
        public void CompleteAllWrites()
        {
            TaskCompletionSource<bool>[] writes = _pendingWrites.ToArray();
            _pendingWrites.Clear();

            foreach (TaskCompletionSource<bool> write in writes)
                write.TrySetResult(true);
        }
    }
}
