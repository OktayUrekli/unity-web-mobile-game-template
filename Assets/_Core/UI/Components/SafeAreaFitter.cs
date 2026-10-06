using UnityEngine;

namespace _Core.UI.Components
{
    /// <summary>
    /// Fits this RectTransform to <see cref="Screen.safeArea"/> (notches, rounded corners, home indicator).
    /// Put it on a stretched "SafeArea" child of a screen or popup and place the content under it; the root keeps
    /// the background (or the popup dim) so that still covers the whole screen. Its parents must cover the whole
    /// screen (Screen Space Overlay canvas). Harmless on desktop and WebGL.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SafeAreaFitter : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _appliedSafeArea;
        private Vector2Int _appliedScreenSize;

        private void Awake()
        {
            _rectTransform = (RectTransform)transform;
        }

        private void OnEnable()
        {
            Apply();
        }

        private void Update()
        {
            // Cheap comparison; re-applies after rotation or window resize.
            if (Screen.safeArea != _appliedSafeArea ||
                Screen.width != _appliedScreenSize.x ||
                Screen.height != _appliedScreenSize.y)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_rectTransform == null)
                _rectTransform = (RectTransform)transform;

            Rect safeArea = Screen.safeArea;
            _appliedSafeArea = safeArea;
            _appliedScreenSize = new Vector2Int(Screen.width, Screen.height);

            if (Screen.width <= 0 || Screen.height <= 0)
                return;

            Vector2 anchorMin = safeArea.position;
            Vector2 anchorMax = safeArea.position + safeArea.size;
            anchorMin.x /= Screen.width;
            anchorMin.y /= Screen.height;
            anchorMax.x /= Screen.width;
            anchorMax.y /= Screen.height;

            _rectTransform.anchorMin = anchorMin;
            _rectTransform.anchorMax = anchorMax;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }
    }
}
