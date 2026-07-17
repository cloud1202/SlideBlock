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
    private static bool _autoBundleInc = true;

    private static bool _isJenkins = false;

    [MenuItem("Build/BuildAndroid_Debug")]
    public static void BuildAndroid_Debug()
    {
        string[] args = Environment.GetCommandLineArgs();

        _isJenkins = args.Length > 0;
        _newSymbol = "DEVELOP";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _newSymbol);
        AutoKeystoreFile.ApplyFromDefaultPath();
        EditorUserBuildSettings.development = true;
        EditorUserBuildSettings.buildAppBundle = false;
        _autoIncrementPatch = false;
        _autoBundleInc = false;

        BuildOptions options = BuildOptions.Development | BuildOptions.AllowDebugging;

        Build(BuildTarget.Android, options);
    }

    [MenuItem("Build/BuildAndroid_Release_APK")]
    public static void BuildAndroid_Release_APK()
    {
        string[] args = Environment.GetCommandLineArgs();
        _isJenkins = args.Length > 0;
        _newSymbol = "RELEASE";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _newSymbol);
        AutoKeystoreFile.ApplyFromDefaultPath();
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.buildAppBundle = false;
        _autoIncrementPatch = false;
        _autoBundleInc = false;

        BuildOptions options = BuildOptions.None;
        Build(BuildTarget.Android, options);
    }

    [MenuItem("Build/BuildAndroid_Release_Test")]
    public static void BuildAndroid_Release_Test()
    {
        string[] args = Environment.GetCommandLineArgs();
        _isJenkins = args.Length > 0;
        _newSymbol = "RELEASE";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.Android, _newSymbol);
        AutoKeystoreFile.ApplyFromDefaultPath();
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.buildAppBundle = true;
        _autoIncrementPatch = false;
        _autoBundleInc = true;

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
        AutoKeystoreFile.ApplyFromDefaultPath();
        EditorUserBuildSettings.development = false;
        EditorUserBuildSettings.buildAppBundle = true;
        _autoIncrementPatch = true;
        _autoBundleInc = true;

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
    public static void Build(BuildTarget buildTarget, BuildOptions buildOptions)
    {
        LoadData();
        // Patch 자동 증가
        if (_autoIncrementPatch) _patch++;
        if (_autoBundleInc) PlayerSettings.Android.bundleVersionCode++;

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
            if (!_isJenkins) EditorUtility.RevealInFinder(buildDir);
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
