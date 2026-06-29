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
        int skipFrames = 1)  // 1 = EditorLogger ÀÚ½ÅÀº ½ºÅµ
    {
        var stack = new StackTrace(skipFrames, true);
        var frame = stack.GetFrame(0);

        var className = frame.GetMethod()?.DeclaringType?.Name ?? "Unknown";
        var methodName = frame.GetMethod()?.Name ?? "Unknown";
        var lineNumber = frame.GetFileLineNumber();
        var timestamp = System.DateTime.Now.ToString("HH:mm:ss");
#if UNITY_EDITOR
        var header = $"[{className}::{methodName}:{lineNumber}]";
#else
        var header = $"[{timestamp}][{className}::{methodName}:{lineNumber}]";
#endif
        var coloredMessage = string.IsNullOrEmpty(color)
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

        switch (level)
        {
            case LogLevel.Info: UnityEngine.Debug.Log(coloredMessage); break;
            case LogLevel.Warning: UnityEngine.Debug.LogWarning(coloredMessage); break;
            case LogLevel.Error: UnityEngine.Debug.LogError(coloredMessage); break;
        }
    }
}
