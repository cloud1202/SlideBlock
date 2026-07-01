using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildProcessor
{
    // 버전 관리
    private static int _major = 1;
    private static int _minor = 0;
    private static int _patch = 0;

    // 빌드 심볼
    private static string _newSymbol;
    private static string[] _currentSymbols = Array.Empty<string>();

    // 빌드 옵션
    private static bool _autoIncrementPatch = true;

    private static bool _isJenkins = false;

    [MenuItem("Build/BuildAndroid_Debug")]
    public static void BuildAndroid_Debug()
    {
        string[] args = Environment.GetCommandLineArgs();

        _isJenkins = args.Length > 0;
        _newSymbol = "DEVELOP";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _newSymbol);
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.buildAppBundle = false;
        _autoIncrementPatch = false;

        BuildOptions options = BuildOptions.Development | BuildOptions.AllowDebugging;

        Build(BuildTarget.Android, options);
    }

    [MenuItem("Build/BuildAndroid_Test_Release")]
    public static void BuildAndroid_Test_Release()
    {
        string[] args = Environment.GetCommandLineArgs();
        _isJenkins = args.Length > 0;
        _newSymbol = "RELEASE";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _newSymbol);
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.buildAppBundle = true;
        _autoIncrementPatch = false;

        BuildOptions options = BuildOptions.None;
        Build(BuildTarget.Android, options);
    }


    [MenuItem("Build/BuildAndroid_Release")]
    public static void BuildAndroid_Release()
    {
        string[] args = Environment.GetCommandLineArgs();
        _isJenkins = args.Length > 0;
        _newSymbol = "RELEASE";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _newSymbol);
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.buildAppBundle = true;
        _autoIncrementPatch = true;

        BuildOptions options = BuildOptions.None;
        Build(BuildTarget.Android, options);
    }

    private static void LoadData()
    {
        string ver = PlayerSettings.bundleVersion;
        var parts = ver.Split('.');
        if (parts.Length == 3)
        {
            int.TryParse(parts[0], out _major);
            int.TryParse(parts[1], out _minor);
            int.TryParse(parts[2], out _patch);
        }
    }

    //private static void RefreshSymbols()
    //{
    //    string raw = PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Android);
    //    _currentSymbols = string.IsNullOrEmpty(raw)
    //        ? Array.Empty<string>()
    //        : raw.Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToArray();
    //}

    //private static void AddSymbol(string symbol)
    //{
    //    if (string.IsNullOrEmpty(symbol)) return;
    //    if (_currentSymbols.Contains(symbol)) return;

    //    var list = _currentSymbols.ToList();
    //    list.Add(symbol);
    //    ApplySymbols(list.ToArray());
    //}

    //private static void RemoveSymbol(string symbol)
    //{
    //    var list = _currentSymbols.Where(s => s != symbol).ToArray();
    //    ApplySymbols(list);
    //}

    //private static void ApplySymbols(string[] symbols)
    //{
    //    string joined = string.Join(";", symbols);
    //    PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, joined);
    //    RefreshSymbols();
    //}

    public static void Build(BuildTarget buildTarget, BuildOptions buildOptions)
    {
        AutoKeystoreFile.ApplyFromDefaultPath();
        LoadData();
        // Patch 자동 증가
        if (_autoIncrementPatch)
        {
            _patch++;
            PlayerSettings.Android.bundleVersionCode++;
        }

        string version = $"{_major}.{_minor}.{_patch}";
        PlayerSettings.bundleVersion = version;

        string buildDir = Path.Combine(Application.dataPath, "../Build/Android");
        Directory.CreateDirectory(buildDir);

        string fileName;
        if (EditorUserBuildSettings.buildAppBundle)
        {
            fileName = $"SlideBlock_{version}.aab";
        }
        else
            fileName = $"SlideBlock_{version}_{_newSymbol}.apk";

        string outputPath = Path.Combine(buildDir, fileName);

        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        try
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new Exception("빌드에 포함된 씬이 없습니다.");

            BuildReport report = BuildPipeline.BuildPlayer(
                scenes,
                outputPath,
                buildTarget,
                buildOptions
            );

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new Exception($"Build Failed: {report.summary.result}\nTotal errors: {report.summary.totalErrors}");
            }

            Debug.Log($"Build Success: {outputPath}");
        }
        catch (Exception e)
        {
            Debug.LogError(e);
           //if(_isJenkins) EditorApplication.Exit(1); // Jenkins 실패 처리
        }

        //if (_isJenkins)  EditorApplication.Exit(0);
    }

    // ------------------------
    // 유틸
    // ------------------------
    private static string GetArg(string[] args, string name)
    {
        int index = Array.IndexOf(args, name);
        if (index >= 0 && index < args.Length - 1)
            return args[index + 1];
        return null;
    }

    private static bool GetBoolArg(string[] args, string name)
    {
        string value = GetArg(args, name);
        return value != null && value.ToLower() == "true";
    }
}
