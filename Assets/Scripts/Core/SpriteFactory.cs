using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 임시 그래픽 생성기.
/// Assets/Resources/Sprites/ 에 같은 이름의 스프라이트를 넣으면 자동으로 그 이미지를 사용합니다.
/// 이름 목록: Player, Enemy_Bat, Enemy_Runner, Enemy_Tank, Enemy_Boss, Gem, Bolt, Blade, Aura, Lightning, Ground
/// </summary>
public static class SpriteFactory
{
    static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
    static readonly HashSet<string> custom = new HashSet<string>();

    public static Sprite Get(string name, Shape fallbackShape)
    {
        if (cache.TryGetValue(name, out var s)) return s;
        s = Resources.Load<Sprite>("Sprites/" + name);
        if (s != null) custom.Add(name);
        else s = Make(fallbackShape);
        cache[name] = s;
        return s;
    }

    public enum Shape { Circle, Square, Diamond, Ring, Ground }

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
