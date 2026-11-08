using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class SceneSetup
{
    const string ScenePath = "Assets/Scenes/Main.unity";

    [MenuItem("SlayersSurvivor/메인 씬 다시 만들기")]
    public static void CreateMainScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        var c = cam.AddComponent<Camera>();
        c.orthographic = true;
        c.orthographicSize = 7f;
        c.clearFlags = CameraClearFlags.SolidColor;
        c.backgroundColor = new Color(0.08f, 0.1f, 0.09f);
        cam.transform.position = new Vector3(0, 0, -10);
        cam.AddComponent<AudioListener>();

        new GameObject("GameManager").AddComponent<GameManager>();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        AssetDatabase.SaveAssets();
        Debug.Log("[SceneSetup] Main scene created: " + ScenePath);
    }
}
