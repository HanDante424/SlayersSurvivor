using UnityEngine;

/// <summary>
/// 임시 스킬 연출 (도형 기반). 이펙트 리소스가 생기면 스킬별로 SpriteEffect 로 교체.
/// </summary>
public static class Fx
{
    public static readonly Color Fire = new Color(1f, 0.5f, 0.15f, 0.75f);
    public static readonly Color FireBright = new Color(1f, 0.8f, 0.3f, 0.85f);
    public static readonly Color FireDeep = new Color(0.9f, 0.2f, 0.05f, 0.7f);

    /// <summary>원형 범위 표시. follow 가 있으면 따라다님</summary>
    public static FxShape Circle(Vector2 pos, float radius, Color c, float duration, Transform follow = null, float grow = 0.15f)
        => Make("FxCircle", SpriteFactory.Shape.Ring, pos, new Vector2(radius * 2f, radius * 2f), 0f, c, duration, follow, grow);

    /// <summary>꽉 찬 원 (폭발 등)</summary>
    public static FxShape Disc(Vector2 pos, float radius, Color c, float duration, float grow = 0.25f)
        => Make("FxDisc", SpriteFactory.Shape.Circle, pos, new Vector2(radius * 2f, radius * 2f), 0f, c, duration, null, grow);

    /// <summary>origin 에서 dir 방향으로 length × width 직사각형</summary>
    public static FxShape Box(Vector2 origin, Vector2 dir, float length, float width, Color c, float duration)
    {
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return Make("FxBox", SpriteFactory.Shape.Square, origin + dir.normalized * length * 0.5f,
                    new Vector2(length, width), ang, c, duration, null, 0f);
    }

    /// <summary>부채꼴 대신 바라보는 방향으로 납작한 타원</summary>
    public static FxShape Arc(Vector2 center, Vector2 dir, float radius, Color c, float duration)
    {
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        return Make("FxArc", SpriteFactory.Shape.Ring, center + dir.normalized * radius * 0.45f,
                    new Vector2(radius * 1.1f, radius * 1.9f), ang, c, duration, null, 0.1f);
    }

    static FxShape Make(string name, SpriteFactory.Shape shape, Vector2 pos, Vector2 size, float angle,
                        Color c, float duration, Transform follow, float grow)
    {
        var go = new GameObject(name);
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("Fx_" + shape, shape);
        sr.color = c;
        sr.sortingOrder = 15;
        var f = go.AddComponent<FxShape>();
        f.Setup(sr, duration, follow, grow);
        return f;
    }

    /// <summary>화면 전체 번쩍임</summary>
    public static void ScreenFlash(Color c, float duration)
    {
        var go = new GameObject("FxScreenFlash");
        var f = go.AddComponent<FxScreenFlash>();
        f.color = c; f.duration = duration;
    }
}

public class FxShape : MonoBehaviour
{
    SpriteRenderer sr;
    float duration, t, grow;
    Transform follow;
    Color c;
    Vector3 baseScale;

    public void Setup(SpriteRenderer sr, float duration, Transform follow, float grow)
    {
        this.sr = sr; this.duration = Mathf.Max(0.01f, duration); this.follow = follow; this.grow = grow;
        c = sr.color;
        baseScale = transform.localScale;
    }

    /// <summary>지속형 범위가 크기를 바꿀 때</summary>
    public void SetRadius(float r) => baseScale = new Vector3(r * 2f, r * 2f, 1f);

    void Update()
    {
        t += Time.deltaTime;
        float k = t / duration;
        if (follow != null) transform.position = follow.position;
        transform.localScale = baseScale * (1f + grow * k);
        sr.color = new Color(c.r, c.g, c.b, c.a * (1f - k * k));
        if (t >= duration) Destroy(gameObject);
    }
}

public class FxScreenFlash : MonoBehaviour
{
    public Color color;
    public float duration;
    float t;

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t >= duration) Destroy(gameObject);
    }

    void OnGUI()
    {
        GUI.depth = 10;
        var c = color; c.a *= 1f - t / duration;
        GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
