using System.Runtime.InteropServices;

/// <summary>
/// WebGL 빌드에서 브라우저의 gtag(GA4)로 이벤트를 보내는 브릿지.
/// FirebaseManager의 Analytics 메서드(LogEvent/LogModeStart/LogModeQuit/LogModePause/LogGameOver)와
/// 동일한 시그니처를 유지해 웹 분기 연결 시 그대로 대체할 수 있게 함.
/// WebGL 이외 플랫폼에서는 전부 no-op.
/// </summary>
public static class WebAnalyticsBridge
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void GA_LogEvent(string eventName);
    [DllImport("__Internal")] private static extern void GA_LogEventParam(string eventName, string paramName, string paramValue);
    [DllImport("__Internal")] private static extern void GA_LogModeStart(string mode);
    [DllImport("__Internal")] private static extern void GA_LogModeQuit(string mode, float playDurationSec, int currentScore);
    [DllImport("__Internal")] private static extern void GA_LogModePause(string mode, float playDurationSec, int currentScore);
    [DllImport("__Internal")] private static extern void GA_LogGameOver(string mode, int finalScore, int maxCombo);
#endif

    public static void LogEvent(string eventName)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GA_LogEvent(eventName);
#endif
    }

    public static void LogEvent(string eventName, string paramName, string paramValue)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GA_LogEventParam(eventName, paramName, paramValue);
#endif
    }

    public static void LogModeStart(string mode)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GA_LogModeStart(mode);
#endif
    }

    public static void LogModeQuit(string mode, float playDurationSec, int currentScore)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GA_LogModeQuit(mode, playDurationSec, currentScore);
#endif
    }

    public static void LogModePause(string mode, float playDurationSec, int currentScore)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GA_LogModePause(mode, playDurationSec, currentScore);
#endif
    }

    public static void LogGameOver(string mode, int finalScore, int maxCombo)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        GA_LogGameOver(mode, finalScore, maxCombo);
#endif
    }
}
