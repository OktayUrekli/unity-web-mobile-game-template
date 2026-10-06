using UnityEngine;

namespace _Core.Bootstrap
{
    /// <summary>
    /// Holds all persistent systems.
    /// Survives scene changes.
    /// </summary>
    public class PersistentRoot : MonoBehaviour
    {
        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }
    }
}