mergeInto(LibraryManager.library, {

  GA_LogEvent: function (eventNamePtr) {
    if (typeof gtag !== 'function') return;
    gtag('event', UTF8ToString(eventNamePtr));
  },

  GA_LogEventParam: function (eventNamePtr, paramNamePtr, paramValuePtr) {
    if (typeof gtag !== 'function') return;
    var params = {};
    params[UTF8ToString(paramNamePtr)] = UTF8ToString(paramValuePtr);
    gtag('event', UTF8ToString(eventNamePtr), params);
  },

  GA_LogModeStart: function (modePtr) {
    if (typeof gtag !== 'function') return;
    gtag('event', 'game_start', { mode: UTF8ToString(modePtr) });
  },

  GA_LogModeQuit: function (modePtr, playDurationSec, currentScore) {
    if (typeof gtag !== 'function') return;
    gtag('event', 'game_quit', {
      mode: UTF8ToString(modePtr),
      play_duration_sec: playDurationSec,
      score: currentScore
    });
  },

  GA_LogModePause: function (modePtr, playDurationSec, currentScore) {
    if (typeof gtag !== 'function') return;
    gtag('event', 'game_pause', {
      mode: UTF8ToString(modePtr),
      play_duration_sec: playDurationSec,
      score: currentScore
    });
  },

  GA_LogGameOver: function (modePtr, finalScore, maxCombo) {
    if (typeof gtag !== 'function') return;
    gtag('event', 'game_over', {
      mode: UTF8ToString(modePtr),
      final_score: finalScore,
      max_combo: maxCombo
    });
  }

});
