using UnityEngine;

/// <summary>
/// Resources/Sprites 그림을 이름으로 불러옴 (2026-11-22 사용자: 스프라이트를 폴더로 분류).
/// 그림이 캐릭터 · 종류별 하위 폴더 (예: Sprites/ErnStray/Pose/ErnStray_UltCharge) 에 있어도 코드는 이름만 넘기면 됨 —
/// 폴더는 SpriteIndex (Editor 가 자동 생성) 표에서 찾고, 표에 없으면 예전처럼 Sprites/{이름}.
/// 새 그림을 Resources/Sprites 아래 아무 폴더에 넣으면 Editor 가 표를 갱신 (이름은 폴더가 달라도 겹치면 안 됨).
/// </summary>
public static class SpriteLib
{
    public static string Path(string name)
        => SpriteIndex.Folder.TryGetValue(name, out var folder) ? "Sprites/" + folder + "/" + name : "Sprites/" + name;

    public static Texture2D Load(string name) => Resources.Load<Texture2D>(Path(name));

    public static Sprite LoadSprite(string name) => Resources.Load<Sprite>(Path(name));
}
