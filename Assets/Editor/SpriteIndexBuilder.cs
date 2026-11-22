using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Resources/Sprites 안 그림 (하위 폴더 포함) 이 추가 · 삭제 · 이동되면 Assets/Scripts/Core/SpriteIndex.cs (이름 → 폴더 표) 를 다시 만듦.
/// SpriteLib.Load(이름) 이 이 표로 경로를 찾으므로, 그림을 폴더에 넣기만 하면 코드 수정 없이 불러짐.
/// 같은 이름이 서로 다른 폴더에 있으면 경고 (하나만 쓰임).
/// </summary>
public class SpriteIndexBuilder : AssetPostprocessor
{
    const string SpriteRoot = "Assets/Resources/Sprites";
    const string IndexPath = "Assets/Scripts/Core/SpriteIndex.cs";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (!Touches(imported) && !Touches(deleted) && !Touches(moved) && !Touches(movedFrom)) return;
        Rebuild();
    }

    static bool Touches(string[] paths)
    {
        foreach (var p in paths)
            if (p.StartsWith(SpriteRoot + "/") && p.EndsWith(".png")) return true;
        return false;
    }

    [MenuItem("SlayersSurvivor/스프라이트 폴더 표 다시 만들기")]
    public static void Rebuild()
    {
        var map = new SortedDictionary<string, string>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.GetFiles(SpriteRoot, "*.png", SearchOption.AllDirectories))
        {
            string full = file.Replace('\\', '/');
            string name = Path.GetFileNameWithoutExtension(full);
            string dir = Path.GetDirectoryName(full).Replace('\\', '/');
            string folder = dir.Length > SpriteRoot.Length ? dir.Substring(SpriteRoot.Length + 1) : "";
            if (map.TryGetValue(name, out var other)) { Debug.LogWarning($"스프라이트 이름 중복: {name} ({other} / {folder})"); continue; }
            map[name] = folder;
        }
        var sb = new StringBuilder();
        sb.Append("// 자동 생성: Assets/Editor/SpriteIndexBuilder.cs 가 Assets/Resources/Sprites 안 그림이 바뀔 때마다 다시 만듦. 직접 고치지 말 것.\n");
        sb.Append("// 이름 -> Resources/Sprites 아래 하위 폴더. SpriteLib.Load(이름) 이 이 표로 경로를 찾음.\n");
        sb.Append("public static class SpriteIndex\n{\n");
        sb.Append("    public static readonly System.Collections.Generic.Dictionary<string, string> Folder = new System.Collections.Generic.Dictionary<string, string>\n    {\n");
        foreach (var kv in map) sb.Append($"        {{ \"{kv.Key}\", \"{kv.Value}\" }},\n");
        sb.Append("    };\n}\n");
        string text = sb.ToString();
        if (File.Exists(IndexPath) && File.ReadAllText(IndexPath).Replace("\r\n", "\n") == text) return;
        File.WriteAllText(IndexPath, text, new UTF8Encoding(false));
        AssetDatabase.ImportAsset(IndexPath);
    }
}
