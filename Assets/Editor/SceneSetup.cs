using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 씬 자동 구성.
/// - Title 씬이 없으면 에디터가 스크립트를 불러올 때 자동 생성
/// - 빌드 순서: Title(0) → Main(1)
/// - Play 버튼은 항상 Title 씬부터 시작 (메뉴에서 끄고 켤 수 있음)
/// </summary>
[InitializeOnLoad]
public static class SceneSetup
{
    const string MainPath = "Assets/Scenes/Main.unity";
    const string TitlePath = "Assets/Scenes/Title.unity";
    const string StartFromTitlePref = "SlayersSurvivor.StartFromTitle";
    const string StartFromTitleMenu = "SlayersSurvivor/Play 시 타이틀부터 시작";

    static SceneSetup()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (!System.IO.File.Exists(TitlePath)) CreateTitleScene();
            EnsureConfig<SkillTuning>("Assets/Resources/Config/SkillTuning.asset");
            ApplyBuildSettings();
            ApplyPlayModeStartScene();
        };
    }

    /// <summary>설정 파일이 없으면 기본값으로 만들어 Inspector 에서 조정할 수 있게 함</summary>
    public static void EnsureConfig<T>(string path) where T : ScriptableObject
    {
        if (System.IO.File.Exists(path)) return;
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
        AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<T>(), path);
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneSetup] Config created: " + path);
    }

    /// <summary>batchmode용: 설정 파일 생성</summary>
    public static void CreateConfigs() => EnsureConfig<SkillTuning>("Assets/Resources/Config/SkillTuning.asset");

    [MenuItem("SlayersSurvivor/메인 씬 다시 만들기")]
    public static void CreateMainScene()
    {
        BuildScene(MainPath, () => new GameObject("GameManager").AddComponent<GameManager>());
        ApplyBuildSettings();
    }

    [MenuItem("SlayersSurvivor/타이틀 씬 다시 만들기")]
    public static void CreateTitleScene()
    {
        BuildScene(TitlePath, () => new GameObject("TitleScreen").AddComponent<TitleScreen>());
        ApplyBuildSettings();
    }

    /// <summary>batchmode용: 두 씬 모두 생성</summary>
    public static void CreateAllScenes()
    {
        CreateMainScene();
        CreateTitleScene();
    }

    static void BuildScene(string path, System.Action populate)
    {
        // 현재 열린 씬을 건드리지 않도록 추가 씬으로 만든 뒤 저장하고 닫음
        bool batch = Application.isBatchMode;
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, batch ? NewSceneMode.Single : NewSceneMode.Additive);
        var prevActive = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(scene);

        var cam = new GameObject("Main Camera") { tag = "MainCamera" };
        var c = cam.AddComponent<Camera>();
        c.orthographic = true;
        c.orthographicSize = 7f;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.08f, 0.1f, 0.09f);
        cam.transform.position = new Vector3(0, 0, -10);
        cam.AddComponent<AudioListener>();
        populate();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, path);
        if (!batch)
        {
            if (prevActive.IsValid() && prevActive != scene) SceneManager.SetActiveScene(prevActive);
            if (SceneManager.sceneCount > 1) EditorSceneManager.CloseScene(scene, true);
        }
        AssetDatabase.Refresh();
        Debug.Log("[SceneSetup] Scene created: " + path);
    }

    static void ApplyBuildSettings()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
        if (System.IO.File.Exists(TitlePath)) list.Add(new EditorBuildSettingsScene(TitlePath, true));
        if (System.IO.File.Exists(MainPath)) list.Add(new EditorBuildSettingsScene(MainPath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static bool StartFromTitle
    {
        get => EditorPrefs.GetBool(StartFromTitlePref, true);
        set => EditorPrefs.SetBool(StartFromTitlePref, value);
    }

    static void ApplyPlayModeStartScene()
    {
        EditorSceneManager.playModeStartScene = StartFromTitle
            ? AssetDatabase.LoadAssetAtPath<SceneAsset>(TitlePath)
            : null;
    }

    [MenuItem(StartFromTitleMenu)]
    static void ToggleStartFromTitle()
    {
        StartFromTitle = !StartFromTitle;
        ApplyPlayModeStartScene();
    }

    [MenuItem(StartFromTitleMenu, true)]
    static bool ToggleStartFromTitleValidate()
    {
        Menu.SetChecked(StartFromTitleMenu, StartFromTitle);
        return true;
    }
}
