using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>씬 이름을 한 곳에서 관리</summary>
public static class SceneLoader
{
    public const string Title = "Title";
    public const string Game = "Main";

    public static void Load(string scene)
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(scene);
    }

    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
