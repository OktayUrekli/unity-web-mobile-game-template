mergeInto(LibraryManager.library, {
  // Tells the SaveManager when the page is hidden or unloaded (tab closed or reloaded, browser minimized, app
  // switch on mobile), so it can write pending changes. Unity's own focus events miss a tab closed without a blur.
  SaveManager_ListenForPageHide: function (gameObjectName) {
    var target = UTF8ToString(gameObjectName);
    var notify = function () {
      try {
        SendMessage(target, 'OnPageHidden');
      } catch (e) {
        // The runtime may already be shutting down.
      }
    };

    document.addEventListener('visibilitychange', function () {
      if (document.visibilityState === 'hidden') {
        notify();
      }
    });
    window.addEventListener('pagehide', notify);
  }
});
