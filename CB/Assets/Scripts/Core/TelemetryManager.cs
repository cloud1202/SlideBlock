using System;
#if UNITY_ANDROID || UNITY_EDITOR
using Firebase.Analytics;
using Firebase.Crashlytics;
#endif

/// <summary>
/// 앱 전역의 로그/애널리틱스 창구. 무상태이며 실패해도 게임 흐름에 영향을 주지 않는다.
/// Firebase SDK 초기화 전에는 모든 호출이 no-op으로 빠진다.
/// </summary>
public class TelemetryManager : BaseManager
{
    private readonly FirebaseManager m_firebase;

    public TelemetryManager(ManagerInitTracker tracker, FirebaseManager firebase) : base(tracker)
    {
        LLogger.Log("TelemetryManager");
        m_firebase = firebase;
        CompleteInit(ManagerType.Telemetry);
    }

#if UNITY_ANDROID || UNITY_EDITOR

    #region Crashlytics

    public void Log(string message)
    {
        if (!m_firebase.IsInitialized)
            return;
        Crashlytics.Log(message);
    }

    public void LogError(Exception e)
    {
        if (!m_firebase.IsInitialized)
            return;
        Crashlytics.LogException(e);
    }

    public void SetCustomKey(string key, string value)
    {
        if (!m_firebase.IsInitialized)
            return;
        Crashlytics.SetCustomKey(key, value);
    }

    #endregion

    #region Analytics

    public void LogEvent(string eventName)
    {
        if (!m_firebase.IsInitialized)
            return;
        FirebaseAnalytics.LogEvent(eventName);
    }

    public void LogEvent(string eventName, string paramName, string paramValue)
    {
        if (!m_firebase.IsInitialized)
            return;
        FirebaseAnalytics.LogEvent(eventName, new Parameter(paramName, paramValue));
    }

    public void LogEvent(string eventName, params Parameter[] parameters)
    {
        if (!m_firebase.IsInitialized)
            return;
        FirebaseAnalytics.LogEvent(eventName, parameters);
    }

    public void LogModeStart(string mode)
    {
        LogEvent("game_start", new Parameter("mode", mode));
    }

    public void LogModeQuit(string mode, float playDurationSec, int currentScore)
    {
        LogEvent("game_quit",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
    }

    public void LogModePause(string mode, float playDurationSec, int currentScore)
    {
        LogEvent("game_pause",
            new Parameter("mode", mode),
            new Parameter("play_duration_sec", playDurationSec),
            new Parameter("score", currentScore));
    }

    public void LogGameOver(string mode, int finalScore, int maxCombo)
    {
        LogEvent("game_over",
            new Parameter("mode", mode),
            new Parameter("final_score", finalScore),
            new Parameter("max_combo", maxCombo));
    }

    #endregion

#else

    #region Crashlytics (WebGL no-op)

    public void Log(string message) { }
    public void LogError(Exception e) { }
    public void SetCustomKey(string key, string value) { }

    #endregion

    #region Analytics (WebGL)

    public void LogEvent(string eventName)
        => WebAnalyticsBridge.LogEvent(eventName);

    public void LogEvent(string eventName, string paramName, string paramValue)
        => WebAnalyticsBridge.LogEvent(eventName, paramName, paramValue);

    public void LogModeStart(string mode)
        => WebAnalyticsBridge.LogModeStart(mode);

    public void LogModeQuit(string mode, float playDurationSec, int currentScore)
        => WebAnalyticsBridge.LogModeQuit(mode, playDurationSec, currentScore);

    public void LogModePause(string mode, float playDurationSec, int currentScore)
        => WebAnalyticsBridge.LogModePause(mode, playDurationSec, currentScore);

    public void LogGameOver(string mode, int finalScore, int maxCombo)
        => WebAnalyticsBridge.LogGameOver(mode, finalScore, maxCombo);

    #endregion

#endif
}
