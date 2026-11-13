using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Windows 실행 파일 빌드. 메뉴 SlayersSurvivor/빌드 또는 명령줄
///   Unity.exe -batchmode -quit -projectPath . -executeMethod BuildWindows.Development [-buildOutput 폴더]
/// 개발 빌드 = 치트 패널(F2) · 판정 표시(F1) 포함 (DebugCheats 는 UNITY_EDITOR || DEVELOPMENT_BUILD 에서만 붙음).
/// 기본 출력: 프로젝트 폴더/Builds/SlayersSurvivor_Win64/SlayersSurvivor.exe (Builds 는 git 제외)
/// </summary>
public static class BuildWindows
{
    const string DefaultFolder = "Builds/SlayersSurvivor_Win64";
    const string ExeName = "SlayersSurvivor.exe";

    [MenuItem("SlayersSurvivor/빌드/Windows 개발 빌드 (치트 포함)")]
    public static void Development() => Build(true);

    [MenuItem("SlayersSurvivor/빌드/Windows 일반 빌드 (치트 없음)")]
    public static void Release() => Build(false);

    static void Build(bool development)
    {
        string folder = ArgValue("-buildOutput") ?? DefaultFolder;
        Directory.CreateDirectory(folder);
        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(folder, ExeName),
            target = BuildTarget.StandaloneWindows64,
            options = development ? BuildOptions.Development : BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        var s = report.summary;
        Debug.Log($"[BuildWindows] {s.result} · {s.totalSize / (1024f * 1024f):0.0} MB · {s.totalTime} · {options.locationPathName}");
        if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
    }

    static string ArgValue(string name)
    {
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
        return null;
    }
}
