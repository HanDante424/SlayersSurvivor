using UnityEngine;

/// <summary>
/// 보스 붉은 테두리 (2026-11-13 플레이 테스트 피드백: 몹 사이에서 보스가 잘 안 보임).
/// 보스 그림을 단색 실루엣(Slayers/SpriteSilhouette 셰이더)으로 8방향 살짝 밀어 뒤에 그려 외곽선처럼 보이게 함.
/// 보스 그림의 모양 · 좌우 반전 · 숨김 · 투명도를 매 프레임 따라감. 도형 대체 그림에도 적용됨.
/// 대상: Enemy.IsBoss (중간보스 · 레이드 보스)
/// </summary>
public class BossOutline : MonoBehaviour
{
    const float Thickness = 0.07f;                                   // [임시] 테두리 두께 (월드 유닛)
    static readonly Color OutlineColor = new Color(1f, 0.12f, 0.08f, 0.95f);
    const float PulseSpeed = 4f, PulseMin = 0.65f;                   // [임시] 은은하게 깜빡임

    static Material mat;
    SpriteRenderer src;
    readonly SpriteRenderer[] parts = new SpriteRenderer[8];
    readonly Vector2[] dirs = new Vector2[8];

    public static void Attach(GameObject go, SpriteRenderer src)
    {
        if (mat == null)
        {
            var shader = Resources.Load<Shader>("Shaders/SpriteSilhouette");
            if (shader == null) return;   // 셰이더가 없으면 테두리 없이
            mat = new Material(shader);
        }
        var o = go.AddComponent<BossOutline>();
        o.src = src;
        for (int i = 0; i < 8; i++)
        {
            float a = i * 45f * Mathf.Deg2Rad;
            o.dirs[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            var child = new GameObject("Outline");
            child.transform.SetParent(go.transform, false);
            var sr = child.AddComponent<SpriteRenderer>();
            sr.sharedMaterial = mat;
            sr.sortingOrder = src.sortingOrder - 1;   // 보스 그림 바로 뒤
            o.parts[i] = sr;
        }
        o.Sync();
    }

    void LateUpdate() => Sync();

    void Sync()
    {
        if (src == null) return;
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Abs(ls.x) > 1e-4f ? Thickness / Mathf.Abs(ls.x) : 0f;
        float sy = Mathf.Abs(ls.y) > 1e-4f ? Thickness / Mathf.Abs(ls.y) : 0f;
        float pulse = Mathf.Lerp(PulseMin, 1f, 0.5f + 0.5f * Mathf.Sin(Time.time * PulseSpeed));
        var c = OutlineColor;
        c.a *= src.color.a * pulse;
        for (int i = 0; i < parts.Length; i++)
        {
            var p = parts[i];
            if (p == null) continue;
            p.enabled = src.enabled;
            p.sprite = src.sprite;
            p.flipX = src.flipX;
            p.flipY = src.flipY;
            p.color = c;
            p.transform.localPosition = new Vector3(dirs[i].x * sx, dirs[i].y * sy, 0f);
        }
    }
}
