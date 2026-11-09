using System;
using UnityEngine;

/// <summary>
/// 판정 범위 표시 (디버그용).
///  - 게임 화면: F1 로 켜고 끔 (에디터 / 개발 빌드)
///  - Scene 창: Play 중 항상 표시 (Gizmos)
/// </summary>
public class HitboxView : MonoBehaviour
{
    /// <summary>게임 화면에 판정 원을 그릴지</summary>
    public static bool Show;

    public static readonly Color HurtColor = new Color(0.3f, 1f, 0.4f);     // 플레이어 피격
    public static readonly Color PickupColor = new Color(0.3f, 0.85f, 1f);  // 획득 범위
    public static readonly Color EnemyColor = new Color(1f, 0.3f, 0.3f);    // 적 판정

    static Sprite ring;

    Func<float> radius;
    Func<Vector2> center;
    Color color;
    SpriteRenderer sr;

    /// <param name="radius">반경(월드 유닛)</param>
    /// <param name="center">판정 중심(월드 좌표). null 이면 owner 위치</param>
    public static HitboxView Attach(Transform owner, Color color, Func<float> radius, Func<Vector2> center = null)
    {
        var go = new GameObject("Hitbox");
        go.transform.SetParent(owner, false);
        var v = go.AddComponent<HitboxView>();
        v.radius = radius;
        v.center = center ?? (() => (Vector2)owner.position);
        v.color = color;
        v.sr = go.AddComponent<SpriteRenderer>();
        v.sr.sprite = Ring();
        v.sr.color = color;
        v.sr.sortingOrder = 100;
        v.sr.enabled = false;
        return v;
    }

    void LateUpdate()
    {
        sr.enabled = Show;
        if (!Show) return;
        float r = radius();
        float parentScale = Mathf.Max(0.0001f, transform.parent.lossyScale.x);
        transform.localScale = Vector3.one * (r * 2f / parentScale);
        transform.position = center();
    }

    void OnDrawGizmos()
    {
        if (radius == null) return;
        Gizmos.color = color;
        Gizmos.DrawWireSphere(center(), radius());
    }

    /// <summary>지름 1유닛짜리 원 테두리 + 옅은 채움</summary>
    static Sprite Ring()
    {
        if (ring != null) return ring;
        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
        var px = new Color[size * size];
        float c = (size - 1) / 2f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)); // 픽셀 단위 거리
            float edge = Mathf.Clamp01(1f - Mathf.Abs(d - (c - 2f)) / 2f); // 두께 약 4px 테두리
            float fill = d < c - 2f ? 0.12f : 0f;
            px[y * size + x] = new Color(1, 1, 1, Mathf.Max(edge, fill));
        }
        tex.SetPixels(px);
        tex.Apply();
        ring = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        return ring;
    }
}
