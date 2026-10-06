using UnityEngine;

namespace _Core.Managers
{
    /// <summary>
    /// Generic singleton base class.
    /// Ensures only one instance exists.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        /// <summary>
        /// The active instance, or null when none exists.
        /// </summary>
        public static T Instance { get; private set; }

        /// <summary>
        /// True when this component is a duplicate that is being destroyed.
        /// Subclasses must return from their <c>Awake</c> override when this is set.
        /// </summary>
        protected bool IsDuplicate { get; private set; }

        /// <summary>
        /// Test seam: sets <see cref="Instance"/> directly (null clears it). <c>Awake</c> does not run in Edit Mode,
        /// so EditMode tests register the managers they create here and restore the previous value in TearDown.
        /// Not for game code.
        /// </summary>
        internal static void SetInstanceForTests(T instance)
        {
            Instance = instance;
        }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                IsDuplicate = true;
                Destroy(gameObject);
                return;
            }

            Instance = this as T;
        }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
