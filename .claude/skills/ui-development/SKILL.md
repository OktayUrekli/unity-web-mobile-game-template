---
name: ui-development
description: UI in this template (2D and 3D games) - UIManager, UICanvas prefab, UIScreen/UIPopup and the animated bases, type-based UIManager and UIConfig registration, ButtonClickSound, popup stack and Back handling, DOTween show/hide, UIParticle, safe area and CanvasScaler for WebGL/mobile, TextMeshPro. Use when the task touches Assets/_Core/UI or UI prefabs/scripts in Assets/_Project ("add a menu", "pause popup", "reward window", "buttons don't respond", "UI looks wrong on mobile"). Takes precedence over the plugin skills ui and ui-ugui, which only supplement low-level uGUI mechanics.
---

# UI development

Read `Assets/_Core/UI/UIManager.cs` for the API. Copy an existing screen/popup as the starting point: scripts in `Assets/_Project/Scripts/UI/` (`MainMenuScreen`, `SettingsPopup`, `PausePopup`, `LanguagePopup`, `ConfirmPopup`), prefabs in `Assets/_Project/Prefabs/UI/` (default dark theme, TMP LiberationSans). The look is the game's to design.

## How it fits together

- `UIManager` (created by `Bootstrapper`) instantiates `UICanvas.prefab` (Screen Space Overlay, so independent of the 2D or 3D camera; `CanvasScaler` 1920x1080 match 0.5; `UIRoot` with children `Screens` and `Popups`) and marks it DontDestroyOnLoad. Safe area is per prefab: the screen or popup root keeps the full-screen background (popup: the dim) and holds a stretched `SafeArea` child with `SafeAreaFitter`, and all content goes under that child. New screens and popups copy this structure; the loading screen only has centered content and has none.
- Screens (`UIScreen`, one active) and popups (`UIPopup`, a stack) are instantiated lazily from `UIConfig` and cached. They persist across scenes, so UI scripts hold no serialized references to scene objects (use `EventBus` or singletons) and reset state on show. Every `SceneLoader`/`SceneTransition` load hides all UI (`SceneLoadStartedEvent`).
- Back/Escape is read only in `UIManager`: it closes the top popup, otherwise publishes `UIBackRequestedEvent`; it is ignored during an ad request. Gameplay opens its pause menu on it; on the main menu `MainMenuSceneEntry` asks to quit on Android through `ConfirmPopup.Open(LocalizedString, Action)`, the reusable yes/no popup (the action runs only on confirm). Every popup close publishes `UIPopupClosedEvent` carrying the popup (`gameEvent.Popup is PausePopup`), so react to that rather than to one button; a popup that must act on any close does it in its own `OnHidden` (`PausePopup` releases its pause there).
- `AdBlockerOverlay` blocks UI input during ad requests; `RewardedAdButton` handles "watch ad for reward" buttons.

## Adding a screen or popup

1. Script in `_Project/Scripts/UI/` deriving from `AnimatedUIScreen`/`AnimatedUIPopup` (or the plain bases). Its type is its identity: one prefab per type. Put per-open logic in `OnShown()`/`OnHidden()`; add button listeners in `Awake` (screens toggle active often, so `OnEnable` would stack duplicates). Close a popup from its own buttons with `Close()`.
2. Prefab: stretched root with the script and a `CanvasGroup` if animated, no `Canvas`/`CanvasScaler`. Popups: root is a full-screen dim `Image` (the modal blocker), the window is a `panel` child. Every button that should click gets a `ButtonClickSound` (empty sound = the default click from `AudioConfig`); handlers never play the click themselves.
3. Add the prefab to the list in `Assets/_Project/ScriptableObjects/Config/UIConfig.asset`. No enum, no id.
4. Open with `UIManager.Instance.ShowScreen<MyScreen>()` / `ShowPopup<MyPopup>()` (both return the instance for setup); close with `HidePopup<MyPopup>()` or `Close()`. A scene entry script (e.g. `MainMenuSceneEntry`) shows the scene's first screen in `Start`.

Settings UI reads values from `SettingsManager` properties and sets controls with `SetValueWithoutNotify`, so populating doesn't trigger a save.

## Animation and effects

- UI tweens run during pause and ads (`timeScale = 0`), so they need `SetUpdate(true)`, `SetLink(gameObject)`, and a kill before restarting and in `OnDisable`; the animated bases already do this. DOTween module shortcuts (`DOFade` on `CanvasGroup`/`Image`, `DOAnchorPos`, `DOColor`...) compile into Assembly-CSharp and are unreachable from `_Core`/`_Project`: use `DOTween.To` (see `AnimatedUIScreen.FadeCanvas`); `Transform` shortcuts like `DOScale` are fine. DOTween settings live in `Assets/Resources/DOTweenSettings.asset` (TextMeshPro module off; enabling it is an Editor step).
- `com.coffee.ui-particle`: a plain `ParticleSystem` doesn't render in an Overlay canvas; wrap it in a `UIParticle` and enable unscaled time for effects in pause/reward popups. `com.coffee.ui-effect-snapshot` gives a cheap blurred background instead of a live blur.

## Input and layout

- `Bootstrapper` creates one persistent `EventSystem` with `InputSystemUIInputModule`. Delete the legacy `EventSystem` Unity auto-adds when you create UI in a scene, or it duplicates.
- Portrait mobile: reference 1080x1920 and match toward width; `Tools/Template/New Game Setup` sets this on the UI canvas and loading screen prefabs when Orientation is Portrait (1920x1080, match 0.5 otherwise). Test 16:9, 4:3, 21:9, 9:16, 9:19.5. Backgrounds that should bleed under notches go outside `UIRoot`.
- Text uses `TextMeshProUGUI` (TMP ships in `com.unity.ugui` 2.x). Every visible string comes from a Localization string table (`UI` in `Assets/_Project/Localization`, `Core` for `_Core` code): static labels get a `LocalizeStringEvent` whose Update String targets `TMP_Text.text`; code-driven text uses a serialized `LocalizedString` (+ `Arguments`) and its `StringChanged` event, unsubscribed in `OnDestroy` (see `MainMenuScreen`, `SettingsPopup`). Tables are preloaded at boot, so `StringChanged` fires right away. LiberationSans SDF is static; Turkish ı İ ş ğ come from its dynamic fallback.

## Verify

Play from `00_Bootstrap`, open and close the UI, change scenes and reopen it, and watch the Console for "no prefab with a ... in UIConfig" errors.
