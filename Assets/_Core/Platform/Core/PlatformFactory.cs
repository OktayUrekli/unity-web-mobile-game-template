using System;
using System.Collections.Generic;
using _Core.Platform.Platforms;
using UnityEngine;

namespace _Core.Platform.Core
{
    /// <summary>
    /// Creates the configured platform. Platform bridges live outside _Core and register themselves
    /// with <see cref="Register"/> from a <c>[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]</c>,
    /// before the bootstrap scene initializes the platform. Unregistered platforms fall back to
    /// <see cref="NullPlatform"/>.
    /// </summary>
    public static class PlatformFactory
    {
        private static readonly Dictionary<PlatformType, Func<IPlatform>> Factories = new();

        /// <summary>
        /// Registers (or replaces) the factory used for <paramref name="platform"/>.
        /// </summary>
        public static void Register(PlatformType platform, Func<IPlatform> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            Factories[platform] = factory;
        }

        /// <summary>
        /// Test seam: removes the factory registered for <paramref name="platform"/>, so <see cref="Create"/>
        /// returns a <see cref="NullPlatform"/> for it again. The registry is static and outlives a test, so
        /// EditMode tests call this in TearDown; bridges register again on every Play Mode entry.
        /// </summary>
        internal static void Unregister(PlatformType platform)
        {
            Factories.Remove(platform);
        }

        /// <summary>
        /// Creates the registered implementation of <paramref name="platform"/>, or a <see cref="NullPlatform"/>.
        /// </summary>
        public static IPlatform Create(PlatformType platform)
        {
            if (Factories.TryGetValue(platform, out Func<IPlatform> factory))
                return factory();

            if (platform != PlatformType.None)
                Debug.LogWarning($"PlatformFactory: no implementation registered for {platform}. Using NullPlatform.");

            return new NullPlatform();
        }
    }
}
