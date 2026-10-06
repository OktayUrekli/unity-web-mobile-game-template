using System;
using System.Collections.Generic;
using _Core.Configuration;
using _Core.Events;
using _Core.Events.Scenes;
using _Core.Events.UI;
using _Core.Managers;
using _Core.Platform.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace _Core.UI
{
    /// <summary>
    /// Manages all UI instances, addressed by their component type: <c>ShowScreen&lt;MainMenuScreen&gt;()</c>,
    /// <c>ShowPopup&lt;PausePopup&gt;()</c>. Screens are mutually exclusive; popups stack on top of the current screen.
    /// Instances are created lazily from the prefabs in <see cref="UIConfig"/> and cached under the persistent canvas.
    /// </summary>
    public class UIManager : Singleton<UIManager>
    {
        private readonly Dictionary<Type, Component> _instances = new();
        private readonly List<UIPopup> _popupStack = new();

        private Transform _screensRoot;
        private Transform _popupsRoot;

        /// <summary>
        /// The popup currently on top, or null.
        /// </summary>
        public UIPopup TopPopup => _popupStack.Count > 0 ? _popupStack[_popupStack.Count - 1] : null;

        /// <summary>
        /// Number of open popups.
        /// </summary>
        public int PopupCount => _popupStack.Count;

        /// <summary>
        /// The visible screen, or null.
        /// </summary>
        public UIScreen CurrentScreen { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (IsDuplicate)
                return;

            CreateCanvas();

            // Screens and popups belong to the scene that showed them.
            EventBus.Subscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
        }

        protected override void OnDestroy()
        {
            EventBus.Unsubscribe<SceneLoadStartedEvent>(OnSceneLoadStarted);
            base.OnDestroy();
        }

        private void OnSceneLoadStarted(SceneLoadStartedEvent gameEvent)
        {
            HideAll();
        }

        private void Update()
        {
            // Back/Escape is handled here only, so popups never each react to it.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
                return;

            // The ad blocker overlay blocks pointers only; ignore Back while an ad request runs.
            PlatformManager platform = PlatformManager.Instance;
            if (platform != null && platform.Ads != null && platform.Ads.IsAdShowing)
                return;

            UIPopup top = TopPopup;
            if (top != null)
            {
                if (top.CanCloseWithBack)
                    HideTopPopup();
                return;
            }

            EventBus.Publish(new UIBackRequestedEvent());
        }

        /// <summary>
        /// Shows the <typeparamref name="T"/> screen, hides the previous one and returns it, or null when no prefab
        /// in <see cref="UIConfig"/> has it.
        /// </summary>
        public T ShowScreen<T>() where T : UIScreen
        {
            if (!TryGet(_screensRoot, out T screen))
                return null;

            if (CurrentScreen != null && CurrentScreen != screen)
                CurrentScreen.Hide();

            CurrentScreen = screen;
            screen.Show();
            return screen;
        }

        /// <summary>
        /// Hides the <typeparamref name="T"/> screen if it exists.
        /// </summary>
        public void HideScreen<T>() where T : UIScreen
        {
            // Do not instantiate just to hide.
            if (!TryGetExisting(out T screen))
                return;

            screen.Hide();

            if (CurrentScreen == screen)
                CurrentScreen = null;
        }

        /// <summary>
        /// Shows the <typeparamref name="T"/> popup on top of all others and returns it, or null when no prefab in
        /// <see cref="UIConfig"/> has it.
        /// </summary>
        public T ShowPopup<T>() where T : UIPopup
        {
            if (!TryGet(_popupsRoot, out T popup))
                return null;

            _popupStack.Remove(popup);
            _popupStack.Add(popup);

            // Last sibling renders and receives raycasts on top.
            popup.transform.SetAsLastSibling();
            popup.Show();

            return popup;
        }

        /// <summary>
        /// Hides the <typeparamref name="T"/> popup if it is open.
        /// </summary>
        public void HidePopup<T>() where T : UIPopup
        {
            if (TryGetExisting(out T popup))
                HidePopup(popup);
        }

        /// <summary>
        /// Hides <paramref name="popup"/> if it is open. Returns false otherwise.
        /// </summary>
        public bool HidePopup(UIPopup popup)
        {
            if (popup == null || !_popupStack.Contains(popup))
                return false;

            ClosePopup(popup);
            return true;
        }

        /// <summary>
        /// Hides the top-most popup. Returns false if none was open.
        /// </summary>
        public bool HideTopPopup()
        {
            return HidePopup(TopPopup);
        }

        /// <summary>
        /// True when the <typeparamref name="T"/> popup is open.
        /// </summary>
        public bool IsPopupOpen<T>() where T : UIPopup
        {
            return TryGetExisting(out T popup) && _popupStack.Contains(popup);
        }

        /// <summary>
        /// Hides the current screen and every popup. Runs automatically when SceneLoader starts loading a scene.
        /// </summary>
        public void HideAll()
        {
            while (HideTopPopup())
            {
            }

            if (CurrentScreen != null)
                CurrentScreen.Hide();

            CurrentScreen = null;
        }

        private void ClosePopup(UIPopup popup)
        {
            // Removed from the stack immediately, even if its hide animation is still running.
            _popupStack.Remove(popup);
            popup.Hide();

            EventBus.Publish(new UIPopupClosedEvent(popup));
        }

        // The cached instance of T, without creating one.
        private bool TryGetExisting<T>(out T component) where T : Component
        {
            if (_instances.TryGetValue(typeof(T), out Component instance) && instance != null)
            {
                component = (T)instance;
                return true;
            }

            component = null;
            return false;
        }

        /// <summary>
        /// Returns the cached instance of <typeparamref name="T"/>, instantiating its prefab from UIConfig on first use.
        /// </summary>
        private bool TryGet<T>(Transform parent, out T component) where T : Component
        {
            if (TryGetExisting(out component))
                return true;

            if (parent == null)
            {
                Debug.LogError($"UIManager: No canvas root for {typeof(T).Name}; check the UI canvas prefab.");
                return false;
            }

            UIConfig config = GetConfig();
            if (config == null || !config.TryGetPrefab(typeof(T), out GameObject prefab) || prefab == null)
            {
                Debug.LogError($"UIManager: no prefab with a {typeof(T).Name} on its root in UIConfig.");
                return false;
            }

            GameObject instance = Instantiate(prefab, parent, false);
            instance.SetActive(false);

            // UIConfig keyed the prefab by this exact type, so the root has it.
            component = instance.GetComponent<T>();
            _instances[typeof(T)] = component;
            return true;
        }

        private void CreateCanvas()
        {
            UIConfig config = GetConfig();
            GameObject canvasPrefab = config != null ? config.UICanvasPrefab : null;

            if (canvasPrefab == null)
            {
                Debug.LogError("UIManager: UIConfig has no UI canvas prefab assigned.");
                return;
            }

            GameObject canvas = Instantiate(canvasPrefab);

            DontDestroyOnLoad(canvas);

            if (!canvas.TryGetComponent(out UICanvasReferences references))
            {
                Debug.LogError("UIManager: UI canvas prefab is missing UICanvasReferences.");
                return;
            }

            _screensRoot = references.ScreensRoot;
            _popupsRoot = references.PopupsRoot;
        }

        private static UIConfig GetConfig()
        {
            GameConfig gameConfig = ConfigurationManager.GameConfig;
            return gameConfig != null ? gameConfig.UIConfig : null;
        }
    }
}
