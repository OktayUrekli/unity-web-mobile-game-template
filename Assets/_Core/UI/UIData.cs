using System;
using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// One screen or popup prefab in <see cref="UIConfig"/>. Its root carries the <see cref="UIScreen"/> or
    /// <see cref="UIPopup"/> subclass that identifies it.
    /// </summary>
    [Serializable]
    public class UIData
    {
        public GameObject prefab;
    }
}
