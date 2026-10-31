// Bridge between the Yandex Games SDK (YaGames, loaded from /sdk.js by the YandexGames WebGL template) and
// YandexGamesSdk.cs. Results go back to C# through SendMessage on the receiver object as JSON; calls that answer
// later carry the request id C# passed in. Linked only into Yandex Games builds (PlatformBuildAssets).
var YandexGamesBridge = {
  $YandexGames: {
    sdk: null,
    player: null,
    data: {},
    receiver: null,

    // Last gameplay state the game reported through GameplayAPI.
    gameplayRunning: false,

    // setData calls made in the same C# call are sent as one (SaveManager flushes several keys at once).
    pendingSaveIds: [],
    saveScheduled: false,

    // True from pagehide (tab closed or reloaded, page left) until the page is shown again.
    unloading: false,

    // True while the newest data went to the SDK only with flush false (it may still wait in the SDK's queue), or
    // while its resend failed.
    queuedWrite: false,
    listensForHide: false,

    send: function (method, payload) {
      if (!YandexGames.receiver) {
        return;
      }

      try {
        SendMessage(YandexGames.receiver, method, payload);
      } catch (e) {
        console.error('Yandex: SendMessage ' + method + ' failed.', e);
      }
    },

    // Answers a C# request: ok with an optional value, or not ok with an error text.
    reply: function (id, ok, value) {
      YandexGames.send('OnRequestCompleted', JSON.stringify({
        id: id,
        ok: ok,
        value: value === undefined || value === null ? '' : String(value)
      }));
    },

    adEvent: function (id, type, value) {
      YandexGames.send('OnAdEvent', JSON.stringify({ id: id, type: type, value: value || '' }));
    },

    errorText: function (error) {
      if (!error) {
        return 'unknown error';
      }

      if (typeof error === 'string') {
        return error;
      }

      return error.message || error.code || String(error);
    },

    // Runs an SDK call and always returns a Promise. An exception thrown inside a function C# called stops the
    // Unity runtime ("Halting program"), and some SDK methods return nothing in some versions and in the
    // sdk-dev-proxy mocks, so no SDK result is chained on directly.
    promise: function (call) {
      try {
        return Promise.resolve(call());
      } catch (e) {
        return Promise.reject(e);
      }
    },

    // Values this bridge writes are strings; anything else (written by another build or tool) goes to C# as JSON.
    toText: function (value) {
      return typeof value === 'string' ? value : JSON.stringify(value);
    },

    gameplayApi: function () {
      var sdk = YandexGames.sdk;
      return sdk && sdk.features ? sdk.features.GameplayAPI : null;
    },

    flushData: function () {
      var ids = YandexGames.pendingSaveIds;
      YandexGames.pendingSaveIds = [];
      YandexGames.saveScheduled = false;

      if (!YandexGames.player) {
        ids.forEach(function (id) { YandexGames.reply(id, false, 'no player'); });
        return;
      }

      // The whole object is sent every time: it holds every key, so the result is the same whether setData
      // replaces or merges. flush false lets the SDK queue the request (setData allows 100 calls per 5 minutes);
      // a write made while the page is hidden or closing (SaveManager flushes on pagehide/visibilitychange) is sent
      // at once, because the page may be closed or frozen before a queued request goes out. A closing tab fires
      // pagehide while the page still counts as visible, hence the unloading flag. When that event's own save sends
      // nothing, sendQueuedWrite sends the data again with flush true.
      var player = YandexGames.player;
      var sendNow = YandexGames.unloading ||
        (typeof document !== 'undefined' && document.visibilityState === 'hidden');
      YandexGames.queuedWrite = !sendNow;
      YandexGames.promise(function () { return player.setData(YandexGames.data, sendNow); }).then(function () {
        ids.forEach(function (id) { YandexGames.reply(id, true); });
      }, function (error) {
        var text = YandexGames.errorText(error);
        ids.forEach(function (id) { YandexGames.reply(id, false, text); });
      });
    },

    // Runs when the page is hidden or closed, after the SaveManager's own save for that event: when nothing went out
    // at once since a write was queued (flush false), the data is sent again with flush true.
    sendQueuedWrite: function () {
      if (!YandexGames.queuedWrite || !YandexGames.player) {
        return;
      }

      YandexGames.queuedWrite = false;
      var player = YandexGames.player;
      YandexGames.promise(function () { return player.setData(YandexGames.data, true); }).catch(function (error) {
        YandexGames.queuedWrite = true;
        console.warn('Yandex: sending the queued save failed: ' + YandexGames.errorText(error));
      });
    }
  },

  YandexGames_IsSdkLoaded: function () {
    return typeof YaGames !== 'undefined' && YaGames && typeof YaGames.init === 'function' ? 1 : 0;
  },

  // dataTimeoutMs: how long (from now) the player data may take. When it is slower, the SDK still counts as
  // initialized and only the cloud saves fall back to local ones, instead of C# timing out the whole platform.
  YandexGames_Init: function (receiverPtr, dataTimeoutMs) {
    YandexGames.receiver = UTF8ToString(receiverPtr);

    // A monotonic clock like C#'s Time.realtimeSinceStartup: a wall-clock correction can't delay the answer past the
    // C# timeout.
    var dataDeadline = performance.now() + dataTimeoutMs;

    // Registered before SaveManager.jslib's pagehide listener (the save manager starts after the platform) and in
    // the capture phase, so the flag is already set when that listener's save reaches flushData.
    window.addEventListener('pagehide', function () { YandexGames.unloading = true; }, true);
    window.addEventListener('pageshow', function () { YandexGames.unloading = false; }, true);

    YandexGames.promise(function () { return YaGames.init(); }).then(function (sdk) {
      YandexGames.sdk = sdk;

      // Sent around the platform's ads (its start-up ad included), purchase windows, hidden tabs and minimized
      // windows. The SDK itself stops and restarts GameplayAPI around them.
      try {
        sdk.on('game_api_pause', function () {
          YandexGames.send('OnGamePaused', '');
        });
        sdk.on('game_api_resume', function () {
          YandexGames.send('OnGameResumed', '');

          // If the game reported gameplay as stopped (for example the pause menu opened during the pause), undo
          // the SDK's automatic start once its own handler has run.
          setTimeout(function () {
            var api = YandexGames.gameplayApi();
            if (!YandexGames.gameplayRunning && api && typeof api.stop === 'function') {
              api.stop();
            }
          }, 0);
        });
      } catch (e) {
        console.warn('Yandex: pause events unavailable: ' + YandexGames.errorText(e));
      }

      // Cloud saves need the player object; without it the game saves locally. scopes false: the bridge uses no
      // personal data (name, avatar), so no permission is asked for.
      var load = YandexGames.promise(function () { return sdk.getPlayer({ scopes: false }); }).then(function (player) {
        return YandexGames.promise(function () { return player.getData(); }).then(function (data) {
          return { player: player, data: data };
        });
      });
      var timeout = new Promise(function (resolve, reject) {
        setTimeout(function () { reject(new Error('timed out')); }, Math.max(0, dataDeadline - performance.now()));
      });

      // A load that finishes after the deadline is ignored: the game already runs on local saves.
      return Promise.race([load, timeout]).then(function (result) {
        YandexGames.player = result.player;
        YandexGames.data = result.data || {};
        return true;
      }, function (error) {
        console.warn('Yandex: player data unavailable: ' + YandexGames.errorText(error));
        YandexGames.player = null;
        YandexGames.data = {};
        return false;
      });
    }).then(function (hasPlayerData) {
      var sdk = YandexGames.sdk;
      var player = YandexGames.player;
      var keys = Object.keys(YandexGames.data);
      var values = keys.map(function (key) { return YandexGames.toText(YandexGames.data[key]); });

      YandexGames.send('OnInitialized', JSON.stringify({
        language: sdk.environment && sdk.environment.i18n ? sdk.environment.i18n.lang || '' : '',
        deviceType: sdk.deviceInfo ? sdk.deviceInfo.type || '' : '',
        hasPlayerData: hasPlayerData,
        isAuthorized: !!(player && typeof player.isAuthorized === 'function' && player.isAuthorized()),
        dataKeys: keys,
        dataValues: values
      }));
    }).catch(function (error) {
      YandexGames.send('OnInitFailed', YandexGames.errorText(error));
    });
  },

  YandexGames_GameReady: function () {
    var sdk = YandexGames.sdk;
    var api = sdk && sdk.features ? sdk.features.LoadingAPI : null;
    if (api && typeof api.ready === 'function') {
      YandexGames.promise(function () { return api.ready(); }).catch(function (error) {
        console.warn('Yandex: LoadingAPI.ready failed: ' + YandexGames.errorText(error));
      });
    }
  },

  YandexGames_GameplayStart: function () {
    YandexGames.gameplayRunning = true;
    var api = YandexGames.gameplayApi();
    if (api && typeof api.start === 'function') {
      YandexGames.promise(function () { return api.start(); }).catch(function (error) {
        console.warn('Yandex: GameplayAPI.start failed: ' + YandexGames.errorText(error));
      });
    }
  },

  YandexGames_GameplayStop: function () {
    YandexGames.gameplayRunning = false;
    var api = YandexGames.gameplayApi();
    if (api && typeof api.stop === 'function') {
      YandexGames.promise(function () { return api.stop(); }).catch(function (error) {
        console.warn('Yandex: GameplayAPI.stop failed: ' + YandexGames.errorText(error));
      });
    }
  },

  YandexGames_ShowFullscreenAdv: function (id) {
    try {
      YandexGames.sdk.adv.showFullscreenAdv({
        callbacks: {
          onOpen: function () { YandexGames.adEvent(id, 'open'); },
          // wasShown is false when nothing was shown: an error, or a call sooner than Yandex allows.
          onClose: function (wasShown) { YandexGames.adEvent(id, 'close', wasShown ? 'shown' : 'notShown'); },
          onError: function (error) { YandexGames.adEvent(id, 'error', YandexGames.errorText(error)); },
          onOffline: function () { YandexGames.adEvent(id, 'error', 'offline'); }
        }
      });
    } catch (e) {
      YandexGames.adEvent(id, 'error', YandexGames.errorText(e));
    }
  },

  YandexGames_ShowRewardedVideo: function (id) {
    try {
      YandexGames.sdk.adv.showRewardedVideo({
        callbacks: {
          onOpen: function () { YandexGames.adEvent(id, 'open'); },
          onRewarded: function () { YandexGames.adEvent(id, 'rewarded'); },
          onClose: function () { YandexGames.adEvent(id, 'close'); },
          onError: function (error) { YandexGames.adEvent(id, 'error', YandexGames.errorText(error)); }
        }
      });
    } catch (e) {
      YandexGames.adEvent(id, 'error', YandexGames.errorText(e));
    }
  },

  // Sticky banners show only when "Use the API to display a sticky-banner" is on in the Developer Console;
  // otherwise Yandex shows them for the whole session by itself.
  YandexGames_ShowBanner: function () {
    YandexGames.promise(function () { return YandexGames.sdk.adv.showBannerAdv(); }).then(function (result) {
      if (result && result.reason) {
        console.warn('Yandex: sticky banner not shown: ' + result.reason);
      }
    }, function (error) {
      console.warn('Yandex: sticky banner failed: ' + YandexGames.errorText(error));
    });
  },

  YandexGames_HideBanner: function () {
    YandexGames.promise(function () { return YandexGames.sdk.adv.hideBannerAdv(); }).catch(function (error) {
      console.warn('Yandex: hiding the sticky banner failed: ' + YandexGames.errorText(error));
    });
  },

  // A null value deletes the key.
  YandexGames_SetData: function (id, keyPtr, valuePtr) {
    var key = UTF8ToString(keyPtr);
    if (valuePtr) {
      YandexGames.data[key] = UTF8ToString(valuePtr);
    } else {
      delete YandexGames.data[key];
    }

    // Registered at the first save, so after SaveManager.jslib's own listeners (that manager starts before any save):
    // they run once that manager's save for the same event has gone out.
    if (!YandexGames.listensForHide) {
      YandexGames.listensForHide = true;
      window.addEventListener('pagehide', YandexGames.sendQueuedWrite);
      document.addEventListener('visibilitychange', function () {
        if (document.visibilityState === 'hidden') {
          YandexGames.sendQueuedWrite();
        }
      });
    }

    // Batched in a microtask, not a timer: it runs once the current C# call (one SaveManager flush) has made all
    // its SetData calls, still inside the page-hide event that caused it, while a timer may never run on a closing
    // page.
    YandexGames.pendingSaveIds.push(id);
    if (!YandexGames.saveScheduled) {
      YandexGames.saveScheduled = true;
      Promise.resolve().then(YandexGames.flushData);
    }
  },

  YandexGames_SetLeaderboardScore: function (id, namePtr, score) {
    var sdk = YandexGames.sdk;
    var name = UTF8ToString(namePtr);

    // Only signed-in players can submit; guests get false here.
    YandexGames.promise(function () {
      return sdk.isAvailableMethod('leaderboards.setScore');
    }).then(function (available) {
      if (!available) {
        YandexGames.reply(id, false, 'leaderboards.setScore is not available (guest player?)');
        return;
      }

      return YandexGames.promise(function () {
        return sdk.leaderboards.setScore(name, Math.round(score));
      }).then(function () {
        YandexGames.reply(id, true);
      });
    }).catch(function (error) {
      YandexGames.reply(id, false, YandexGames.errorText(error));
    });
  },

  YandexGames_RequestReview: function (id) {
    var feedback = null;

    // Only signed-in players who have not rated the game yet, once per session.
    YandexGames.promise(function () {
      feedback = YandexGames.sdk.feedback;
      return feedback.canReview();
    }).then(function (result) {
      if (!result || !result.value) {
        YandexGames.reply(id, false, result && result.reason ? result.reason : 'canReview returned false');
        return;
      }

      return YandexGames.promise(function () { return feedback.requestReview(); }).then(function (outcome) {
        YandexGames.reply(id, true, outcome && outcome.feedbackSent ? 'sent' : 'closed');
      });
    }).catch(function (error) {
      YandexGames.reply(id, false, YandexGames.errorText(error));
    });
  }
};

autoAddDeps(YandexGamesBridge, '$YandexGames');
mergeInto(LibraryManager.library, YandexGamesBridge);
