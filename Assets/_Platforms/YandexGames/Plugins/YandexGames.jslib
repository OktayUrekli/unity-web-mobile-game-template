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

    // setData calls made in the same frame are sent as one (SaveManager flushes several keys at once).
    pendingSaveIds: [],
    saveScheduled: false,

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
      // replaces or merges. flush false lets the SDK queue the request (setData allows 100 calls per 5 minutes).
      YandexGames.player.setData(YandexGames.data, false).then(function () {
        ids.forEach(function (id) { YandexGames.reply(id, true); });
      }, function (error) {
        var text = YandexGames.errorText(error);
        ids.forEach(function (id) { YandexGames.reply(id, false, text); });
      });
    }
  },

  YandexGames_IsSdkLoaded: function () {
    return typeof YaGames !== 'undefined' && YaGames && typeof YaGames.init === 'function' ? 1 : 0;
  },

  YandexGames_Init: function (receiverPtr) {
    YandexGames.receiver = UTF8ToString(receiverPtr);

    YaGames.init().then(function (sdk) {
      YandexGames.sdk = sdk;

      // Sent around the platform's ads (its start-up ad included), purchase windows, hidden tabs and minimized
      // windows. The SDK itself stops and restarts GameplayAPI around them.
      sdk.on('game_api_pause', function () {
        YandexGames.send('OnGamePaused', '');
      });
      sdk.on('game_api_resume', function () {
        YandexGames.send('OnGameResumed', '');

        // If the game reported gameplay as stopped (for example the pause menu opened during the pause), undo the
        // SDK's automatic start once its own handler has run.
        setTimeout(function () {
          var api = YandexGames.gameplayApi();
          if (!YandexGames.gameplayRunning && api && typeof api.stop === 'function') {
            api.stop();
          }
        }, 0);
      });

      // Cloud saves need the player object; without it the game saves locally.
      return sdk.getPlayer().then(function (player) {
        YandexGames.player = player;
        return player.getData();
      }).then(function (data) {
        YandexGames.data = data || {};
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
      api.ready();
    }
  },

  YandexGames_GameplayStart: function () {
    YandexGames.gameplayRunning = true;
    var api = YandexGames.gameplayApi();
    if (api && typeof api.start === 'function') {
      api.start();
    }
  },

  YandexGames_GameplayStop: function () {
    YandexGames.gameplayRunning = false;
    var api = YandexGames.gameplayApi();
    if (api && typeof api.stop === 'function') {
      api.stop();
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
    YandexGames.sdk.adv.showBannerAdv().then(function (result) {
      if (result && result.reason) {
        console.warn('Yandex: sticky banner not shown: ' + result.reason);
      }
    }, function (error) {
      console.warn('Yandex: sticky banner failed: ' + YandexGames.errorText(error));
    });
  },

  YandexGames_HideBanner: function () {
    YandexGames.sdk.adv.hideBannerAdv().catch(function (error) {
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

    YandexGames.pendingSaveIds.push(id);
    if (!YandexGames.saveScheduled) {
      YandexGames.saveScheduled = true;
      setTimeout(YandexGames.flushData, 0);
    }
  },

  YandexGames_SetLeaderboardScore: function (id, namePtr, score) {
    var sdk = YandexGames.sdk;
    var name = UTF8ToString(namePtr);

    // Only signed-in players can submit; guests get false here.
    sdk.isAvailableMethod('leaderboards.setScore').then(function (available) {
      if (!available) {
        YandexGames.reply(id, false, 'leaderboards.setScore is not available (guest player?)');
        return;
      }

      return sdk.leaderboards.setScore(name, Math.round(score)).then(function () {
        YandexGames.reply(id, true);
      });
    }).catch(function (error) {
      YandexGames.reply(id, false, YandexGames.errorText(error));
    });
  },

  YandexGames_RequestReview: function (id) {
    var feedback = YandexGames.sdk.feedback;

    // Only signed-in players who have not rated the game yet, once per session.
    feedback.canReview().then(function (result) {
      if (!result || !result.value) {
        YandexGames.reply(id, false, result && result.reason ? result.reason : 'canReview returned false');
        return;
      }

      return feedback.requestReview().then(function (outcome) {
        YandexGames.reply(id, true, outcome && outcome.feedbackSent ? 'sent' : 'closed');
      });
    }).catch(function (error) {
      YandexGames.reply(id, false, YandexGames.errorText(error));
    });
  }
};

autoAddDeps(YandexGamesBridge, '$YandexGames');
mergeInto(LibraryManager.library, YandexGamesBridge);
