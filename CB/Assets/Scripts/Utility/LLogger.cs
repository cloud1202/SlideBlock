using System.Diagnostics;
using System.Linq;
using UnityEngine;

public static class LLogger
{
    public enum LogLevel { Info, Warning, Error }
    
    public static void Log(
        string message,
        LogLevel level = LogLevel.Info,
        string color = Colors.Default,
        bool verbose = false,
        int skipFrames = 1)  // 1 = EditorLogger 자신을 스킵
    {
#if !RELEASE
        var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
        string coloredMessage;

#if UNITY_WEBGL && !UNITY_EDITOR
        // IL2CPP+WebGL에서는 System.Diagnostics.StackTrace가 네이티브 콜스택을
        // 걷다가 메모리 접근 오류(wasm 트랩)로 죽는 경우가 있어 호출 위치 추적을 생략한다.
        var header = $"[{timestamp}]";
        coloredMessage = string.IsNullOrEmpty(color)
            ? $"{header} {message}"
            : $"<color={color}>{header}</color> {message}";
#else
        var stack = new StackTrace(skipFrames, true);
        var frame = stack.GetFrame(0);

        var className = frame.GetMethod()?.DeclaringType?.Name ?? "Unknown";
        var methodName = frame.GetMethod()?.Name ?? "Unknown";
        var lineNumber = frame.GetFileLineNumber();
#if UNITY_EDITOR
        var header = $"[{className}::{methodName}:{lineNumber}]";
#else
        var header = $"[{timestamp}][{className}::{methodName}:{lineNumber}]";
#endif
        coloredMessage = string.IsNullOrEmpty(color)
            ? $"{header} {message}"
            : $"<color={color}>{header}</color> {message}";

        if (verbose)
        {
            var fullStack = string.Join("\n  ",
                stack.GetFrames()
                    .Select(f =>
                    {
                        var method = f.GetMethod();
                        var cls = method?.DeclaringType?.Name ?? "?";
                        var line = f.GetFileLineNumber();
                        return $"{cls}.{method?.Name}  (line {line})";
                    })
            );
            coloredMessage += $"\n  StackTrace:\n  {fullStack}";
        }
#endif

        switch (level)
        {
            case LogLevel.Info: UnityEngine.Debug.Log(coloredMessage); break;
            case LogLevel.Warning: UnityEngine.Debug.LogWarning(coloredMessage); break;
            case LogLevel.Error: UnityEngine.Debug.LogError(coloredMessage); break;
        }
#endif
    }
}
