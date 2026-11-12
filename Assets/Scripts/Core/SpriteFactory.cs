using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 임시 그래픽 생성기.
/// Assets/Resources/Sprites/ 에 같은 이름의 스프라이트를 넣으면 자동으로 그 이미지를 사용합니다.
/// 이름 목록: Player, Enemy_Goblin, Enemy_GoblinArcher, Enemy_Hobgoblin, Enemy_GoblinRider, Enemy_Boss, Enemy_Arrow, Soul_Small, Soul_Mid, Soul_Large, FireBall, Ground
///
/// 크기: 그림의 긴 변이 1유닛이 되도록 맞춘 뒤 각 오브젝트의 scale 로 키움 (도형 대체 이미지와 같은 기준).
///       투명 여백도 크기에 포함되므로 그림은 여백 없이 꽉 채워 자를 것.
/// 바닥(Ground)만 예외: 한 장이 GroundTileSize 유닛인 반복 타일.
/// </summary>
public static class SpriteFactory
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly HashSet<string> custom = new HashSet<string>();

    /// <summary>바닥 타일 한 장의 월드 크기 (유닛). 원본 1024px = 캐릭터 키(1.62)의 약 3.7배 (2026-11-12 Dark_Moss_Wasteland_Tile_v1)</summary>
    public const float GroundTileSize = 6f;

    public static Sprite Get(string name, Shape fallbackShape)
    {
        if (cache.TryGetValue(name, out var s) && s != null) return s;
        s = LoadCustom(name);
        if (s != null) custom.Add(name);
        else s = Make(fallbackShape);
        cache[name] = s;
        return s;
    }

    /// <summary>Resources/Sprites/{name} 이미지가 있으면 그 스프라이트, 없으면 null (도형 대체 안 함)</summary>
    public static Sprite TryGet(string name)
    {
        if (cache.TryGetValue(name, out var s) && s != null) return custom.Contains(name) ? s : null;
        s = LoadCustom(name);
        if (s == null) return null;
        custom.Add(name);
        cache[name] = s;
        return s;
    }

    /// <summary>
    /// SpriteImportSettings 가 텍스처를 Default 타입으로 가져오므로 Load&lt;Sprite&gt; 는 비어 있음 →
    /// 텍스처를 읽어 직접 스프라이트를 만듦 (Sprite 타입으로 따로 임포트한 경우도 그대로 사용)
    /// </summary>
    static Sprite LoadCustom(string name)
    {
        var s = Resources.Load<Sprite>("Sprites/" + name);
        if (s != null) return s;
        var tex = Resources.Load<Texture2D>("Sprites/" + name);
        if (tex == null) return null;
        float world = name == "Ground" ? GroundTileSize : 1f;
        float ppu = Mathf.Max(tex.width, tex.height) / world;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
        s.name = name;
        return s;
    }

    public enum Shape { Circle, Square, Diamond, Ring, Ground, HalfDisc }

    static Sprite Make(Shape shape)
    {
        int size = shape == Shape.Ground ? 64 : 64;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = shape == Shape.Ground ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx = (x - c) / c, dy = (y - c) / c;
            float a = 0f;
            switch (shape)
            {
                case Shape.Circle:
                    a = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * c);
                    break;
                case Shape.HalfDisc:   // 오른쪽 반원 (회전시켜 방향을 맞춤)
                    a = dx >= 0f ? Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * c) * Mathf.Clamp01(dx * c + 1f) : 0f;
                    break;
                case Shape.Square:
                    a = 1f;
                    break;
                case Shape.Diamond:
                    a = Mathf.Clamp01((1f - (Mathf.Abs(dx) + Mathf.Abs(dy))) * c);
                    break;
                case Shape.Ring:
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    a = Mathf.Clamp01((1f - r) * c) * Mathf.Lerp(0.15f, 0.6f, r);
                    break;
                case Shape.Ground:
                    bool line = x == 0 || y == 0;
                    px[y * size + x] = line ? new Color(0.16f, 0.2f, 0.17f) : new Color(0.11f, 0.14f, 0.12f);
                    continue;
            }
            px[y * size + x] = new Color(1, 1, 1, a);
        }
        tex.SetPixels(px);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
    }

    public static SpriteRenderer AddRenderer(GameObject go, string name, Shape shape, Color color, int order)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = Get(name, shape);
        // 커스텀 스프라이트가 있으면 원래 색 그대로 사용
        sr.color = custom.Contains(name) ? Color.white : color;
        sr.sortingOrder = order;
        return sr;
    }
}
