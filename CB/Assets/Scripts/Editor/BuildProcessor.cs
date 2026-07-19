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

    // GA4 측정 ID (Google Analytics 관리 화면 > 데이터 스트림에서 발급). 실제 ID로 교체 필요.
    private const string GA4MeasurementId = "G-7751YYDKFF";

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

    [MenuItem("Build/BuildWebGL_Debug")]
    public static void BuildWebGL_Debug()
    {
        string[] args = Environment.GetCommandLineArgs();

        _isJenkins = args.Length > 0;
        _newSymbol = "DEVELOP";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, _newSymbol);
        EditorUserBuildSettings.development = true;
        _autoIncrementPatch = false;
        _autoBundleInc = false;

        BuildOptions options = BuildOptions.Development;

        Build(BuildTarget.WebGL, options);
    }

    [MenuItem("Build/BuildWebGL_Release")]
    public static void BuildWebGL_Release()
    {
        string[] args = Environment.GetCommandLineArgs();
        _isJenkins = args.Length > 0;
        _newSymbol = "RELEASE";
        PlayerSettings.SetScriptingDefineSymbols(NamedBuildTarget.WebGL, _newSymbol);
        EditorUserBuildSettings.development = false;
        _autoIncrementPatch = false;
        _autoBundleInc = false;

        BuildOptions options = BuildOptions.None;

        Build(BuildTarget.WebGL, options);
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
        if (buildTarget == BuildTarget.Android && _autoBundleInc) PlayerSettings.Android.bundleVersionCode++;

        string version = $"{_major}.{_minor}.{_patch}";
        PlayerSettings.bundleVersion = version;

        string platformFolder = buildTarget == BuildTarget.WebGL ? "WebGL" : "Android";
        string buildDir = Path.Combine(Application.dataPath, $"../Build/{platformFolder}");
        Directory.CreateDirectory(buildDir);

        string outputPath;
        if (buildTarget == BuildTarget.WebGL)
        {
            // WebGL은 단일 파일이 아닌 폴더(index.html 등)로 빌드됨
            outputPath = Path.Combine(buildDir, $"SlideBlock_{version}_{_newSymbol}");
            Directory.CreateDirectory(outputPath);
        }
        else if (EditorUserBuildSettings.buildAppBundle)
        {
            outputPath = Path.Combine(buildDir, $"SlideBlock_{version}.aab");
        }
        else
        {
            outputPath = Path.Combine(buildDir, $"SlideBlock_{version}_{_newSymbol}.apk");
        }

        if (buildTarget == BuildTarget.Android)
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        if (buildTarget == BuildTarget.WebGL)
            PlayerSettings.WebGL.decompressionFallback = true;

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

            if (buildTarget == BuildTarget.WebGL)
                InjectGtagSnippet(outputPath);

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

    // WebGL 빌드 결과물의 index.html <head>에 GA4(gtag.js) 스니펫을 주입
    private static void InjectGtagSnippet(string webglOutputDir)
    {
        if (GA4MeasurementId == "G-XXXXXXXXXX")
        {
            Debug.LogWarning("GA4MeasurementId가 설정되지 않아 gtag 스니펫 주입을 건너뜁니다. BuildProcessor.GA4MeasurementId를 실제 측정 ID로 교체하세요.");
            return;
        }

        string indexPath = Path.Combine(webglOutputDir, "index.html");
        if (!File.Exists(indexPath))
        {
            Debug.LogWarning($"gtag 스니펫 주입 실패: index.html을 찾을 수 없습니다. ({indexPath})");
            return;
        }

        string html = File.ReadAllText(indexPath);
        if (html.Contains("googletagmanager.com/gtag/js"))
            return; // 이미 삽입됨

        string snippet =
            $"<script async src=\"https://www.googletagmanager.com/gtag/js?id={GA4MeasurementId}\"></script>\n" +
            "<script>\n" +
            "  window.dataLayer = window.dataLayer || [];\n" +
            "  function gtag(){dataLayer.push(arguments);}\n" +
            "  gtag('js', new Date());\n" +
            $"  gtag('config', '{GA4MeasurementId}');\n" +
            "</script>\n";

        html = html.Replace("</head>", snippet + "</head>");
        File.WriteAllText(indexPath, html);
        Debug.Log("gtag(GA4) 스니펫을 index.html에 삽입했습니다.");
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
