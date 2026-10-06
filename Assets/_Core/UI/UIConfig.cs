using System;
using System.Collections.Generic;
using UnityEngine;

namespace _Core.UI
{
    /// <summary>
    /// The UI canvas and every screen/popup prefab. A prefab is found by the type of the <see cref="UIScreen"/> or
    /// <see cref="UIPopup"/> on its root, so adding UI means adding a prefab here and nothing in code.
    /// </summary>
    [CreateAssetMenu(fileName = "UIConfig", menuName = "Configuration/UI Config")]
    public class UIConfig : ScriptableObject
    {
        [SerializeField] private GameObject uiCanvasPrefab;
        [SerializeField] private List<UIData> uiList = new();

        private Dictionary<Type, GameObject> _cache;

        /// <summary>
        /// The persistent canvas with the screen and popup roots (<see cref="UICanvasReferences"/>).
        /// </summary>
        public GameObject UICanvasPrefab => uiCanvasPrefab;

        /// <summary>
        /// The prefab whose root carries a component of exactly <paramref name="viewType"/>.
        /// </summary>
        public bool TryGetPrefab(Type viewType, out GameObject prefab)
        {
            Initialize();

            return _cache.TryGetValue(viewType, out prefab);
        }

        private void Initialize()
        {
            if (_cache != null)
                return;

            _cache = new Dictionary<Type, GameObject>();

            foreach (UIData data in uiList)
            {
                if (data == null || data.prefab == null)
                    continue;

                Type viewType = GetViewType(data.prefab);
                if (viewType == null)
                {
                    Debug.LogError($"UIConfig: '{data.prefab.name}' has no UIScreen or UIPopup on its root; skipped.", this);
                    continue;
                }

                if (!_cache.TryAdd(viewType, data.prefab))
                    Debug.LogError($"UIConfig: more than one prefab for {viewType.Name}; '{data.prefab.name}' skipped.", this);
            }
        }

        private static Type GetViewType(GameObject prefab)
        {
            if (prefab.TryGetComponent(out UIScreen screen))
                return screen.GetType();

            if (prefab.TryGetComponent(out UIPopup popup))
                return popup.GetType();

            return null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Rebuilt on the next lookup, so prefabs added in the Inspector are found without a domain reload.
            _cache = null;
        }
#endif
    }
}
