using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Core.Events
{
    /// <summary>
    /// Global event bus.
    /// Allows systems to communicate without direct references.
    /// Each event type has its own static channel, so publishing needs no dictionary lookup, no boxing and no
    /// allocation (handlers are invoked from a cached array that is rebuilt only after a subscription change).
    /// </summary>
    public static class EventBus
    {
        // One clear action per event type that was ever used, so Clear() reaches every channel.
        private static readonly List<Action> ClearActions = new();

        /// <summary>
        /// Subscribe to an event type.
        /// </summary>
        public static void Subscribe<T>(Action<T> callback)
            where T : IGameEvent
        {
            Channel<T>.Subscribe(callback);
        }

        /// <summary>
        /// Unsubscribe from an event type.
        /// </summary>
        public static void Unsubscribe<T>(Action<T> callback)
            where T : IGameEvent
        {
            Channel<T>.Unsubscribe(callback);
        }

        /// <summary>
        /// Publish an event. Exceptions thrown by handlers are logged, not rethrown. Handlers added or removed
        /// while the event is being delivered take effect from the next publish.
        /// </summary>
        public static void Publish<T>(T gameEvent)
            where T : IGameEvent
        {
            Channel<T>.Publish(gameEvent);
        }

        /// <summary>
        /// Removes all registered events.
        /// </summary>
        public static void Clear()
        {
            foreach (Action clear in ClearActions)
                clear();
        }

        /// <summary>
        /// Test seam: number of handlers subscribed to <typeparamref name="T"/>.
        /// </summary>
        internal static int SubscriberCount<T>()
            where T : IGameEvent
        {
            return Channel<T>.Count;
        }

        private static class Channel<T>
            where T : IGameEvent
        {
            private static readonly List<Action<T>> Handlers = new();
            private static Action<T>[] _snapshot = Array.Empty<Action<T>>();
            private static bool _isSnapshotStale;

            static Channel()
            {
                ClearActions.Add(Clear);
            }

            public static int Count => Handlers.Count;

            public static void Subscribe(Action<T> callback)
            {
                if (callback == null)
                    return;

                Handlers.Add(callback);
                _isSnapshotStale = true;
            }

            public static void Unsubscribe(Action<T> callback)
            {
                if (callback == null)
                    return;

                // The last occurrence, like Delegate.Remove, so a handler subscribed twice is removed once per call.
                int index = Handlers.LastIndexOf(callback);
                if (index < 0)
                    return;

                Handlers.RemoveAt(index);
                _isSnapshotStale = true;
            }

            public static void Publish(T gameEvent)
            {
                if (Handlers.Count == 0)
                    return;

                if (_isSnapshotStale)
                {
                    _snapshot = Handlers.ToArray();
                    _isSnapshotStale = false;
                }

                // A local reference: a handler that subscribes or unsubscribes replaces _snapshot on the next
                // publish, never the array being iterated here.
                Action<T>[] handlers = _snapshot;

                // Invoke each handler separately so one throwing handler
                // does not stop the others or the publisher.
                for (int i = 0; i < handlers.Length; i++)
                {
                    try
                    {
                        handlers[i].Invoke(gameEvent);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }

            private static void Clear()
            {
                Handlers.Clear();
                _snapshot = Array.Empty<Action<T>>();
                _isSnapshotStale = false;
            }
        }
    }
}
