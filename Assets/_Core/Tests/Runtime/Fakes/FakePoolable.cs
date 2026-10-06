using _Core.Pooling;
using UnityEngine;

namespace _Core.Tests.Fakes
{
    /// <summary>
    /// <see cref="IPoolable"/> component that counts the pool's callbacks and records whether its GameObject
    /// was active during each one. Lives in the runtime test assembly (<c>_Core.Tests.Runtime</c>), in its own
    /// file: Unity refuses to attach a MonoBehaviour from an Editor-only assembly (<c>AddComponent</c> returns
    /// null), and <c>Object.Instantiate</c> needs the script asset to clone the component.
    /// </summary>
    public class FakePoolable : MonoBehaviour, IPoolable
    {
        /// <summary>
        /// Number of <see cref="OnSpawn"/> calls.
        /// </summary>
        public int SpawnCount { get; private set; }

        /// <summary>
        /// Number of <see cref="OnDespawn"/> calls.
        /// </summary>
        public int DespawnCount { get; private set; }

        /// <summary>
        /// <c>activeSelf</c> of the GameObject during the last <see cref="OnSpawn"/>.
        /// </summary>
        public bool WasActiveOnLastSpawn { get; private set; }

        /// <summary>
        /// <c>activeSelf</c> of the GameObject during the last <see cref="OnDespawn"/>.
        /// </summary>
        public bool WasActiveOnLastDespawn { get; private set; }

        /// <summary>
        /// Counts the call and records the active state.
        /// </summary>
        public void OnSpawn()
        {
            SpawnCount++;
            WasActiveOnLastSpawn = gameObject.activeSelf;
        }

        /// <summary>
        /// Counts the call and records the active state.
        /// </summary>
        public void OnDespawn()
        {
            DespawnCount++;
            WasActiveOnLastDespawn = gameObject.activeSelf;
        }
    }
}
