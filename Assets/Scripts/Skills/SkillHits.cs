using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스킬 판정 공용 함수. 모두 Enemy.CopyAll 사본으로 돌기 때문에 순회 중 적이 죽어도 안전.
/// onHit 은 피해를 준 직후 호출 (기절·슬로우 부여 등).
/// </summary>
public static class SkillHits
{
    static readonly List<Enemy> buf = new List<Enemy>();
    static readonly List<Enemy> result = new List<Enemy>();

    /// <summary>원 안의 살아 있는 적 목록 (피해 없이 조회만)</summary>
    public static List<Enemy> InCircle(Vector2 c, float r, List<Enemy> into)
    {
        into.Clear();
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            float rr = r + e.radius;
            if (((Vector2)e.transform.position - c).sqrMagnitude < rr * rr) into.Add(e);
        }
        return into;
    }

    /// <summary>원형 범위 피해. 밀치기는 중심에서 바깥 방향</summary>
    public static int Circle(Vector2 c, float r, DamageInfo dmg, float knock, Action<Enemy> onHit = null)
    {
        InCircle(c, r, result);
        foreach (var e in result)
        {
            Vector2 d = (Vector2)e.transform.position - c;
            e.Hit(dmg, d.sqrMagnitude > 0.0001f ? d : UnityEngine.Random.insideUnitCircle, knock);
            onHit?.Invoke(e);
        }
        return result.Count;
    }

    /// <summary>
    /// 부채꼴 범위 피해 (dir 기준 좌우 halfAngle 도).
    /// 맞는 조건: ① 적 원이 반경 r 원에 닿고 ② 적 중심이 각도 안. 단, 적 원이 근접 원(near)에 닿으면 각도 무관.
    /// </summary>
    public static int Sector(Vector2 c, Vector2 dir, float r, float halfAngle, DamageInfo dmg, float knock,
                             Action<Enemy> onHit = null, float near = 0.3f)
    {
        dir = dir.normalized;
        float cosLimit = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
        int n = 0;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - c;
            float rr = r + e.radius;
            if (d.sqrMagnitude > rr * rr) continue;
            // 근접 원(near)에 닿은 적은 각도와 관계없이 맞음
            if (d.magnitude > e.radius + near && Vector2.Dot(d.normalized, dir) < cosLimit) continue;
            e.Hit(dmg, d.sqrMagnitude > 0.0001f ? d : dir, knock);
            onHit?.Invoke(e);
            n++;
        }
        return n;
    }

    /// <summary>
    /// 캡슐(둥근 직사각형) 범위 피해. c 에서 dir 방향으로 앞 forward, 뒤 back 까지, 폭 width (양 끝은 반원).
    /// 적 원이 캡슐에 닿으면 맞음.
    /// </summary>
    public static int Capsule(Vector2 c, Vector2 dir, float forward, float back, float width, DamageInfo dmg, float knock,
                              Action<Enemy> onHit = null)
    {
        dir = dir.normalized;
        float hw = width * 0.5f;
        // 캡슐 = 선분(a~b) 에서 hw 이내. 앞뒤 끝이 둥글어지도록 선분은 양쪽에서 hw 만큼 안쪽
        float s0 = -back + hw, s1 = forward - hw;
        if (s1 < s0) s0 = s1 = (forward - back) * 0.5f;
        int n = 0;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - c;
            float along = Mathf.Clamp(Vector2.Dot(d, dir), s0, s1);
            float rr = hw + e.radius;
            if ((d - dir * along).sqrMagnitude > rr * rr) continue;
            e.Hit(dmg, d.sqrMagnitude > 0.0001f ? d : dir, knock);
            onHit?.Invoke(e);
            n++;
        }
        return n;
    }

    /// <summary>
    /// SkillHitbox 설정대로 판정 + F1 표시. scale = 범위 배율 (기본 강화 범위 +10% 등).
    /// Capsule 은 앞·뒤·폭 모두, Sector 는 반경·근접 원에 scale 적용.
    /// </summary>
    public static int Directed(SkillHitbox h, Vector2 c, Vector2 dir, float scale, DamageInfo dmg, Action<Enemy> onHit = null)
    {
        if (h.shape == HitShape.Capsule)
        {
            HitDebug.Capsule(c, dir, h.forward * scale, h.back * scale, h.width * scale, HitDebug.SlashColor);
            return Capsule(c, dir, h.forward * scale, h.back * scale, h.width * scale, dmg, Enemy.KnockSpeed(h.knockback), onHit);
        }
        HitDebug.Sector(c, dir, h.radius * scale, h.halfAngle, h.near * scale, HitDebug.SlashColor);
        return Sector(c, dir, h.radius * scale, h.halfAngle, dmg, Enemy.KnockSpeed(h.knockback), onHit, h.near * scale);
    }

    /// <summary>origin 에서 dir 방향으로 length, 폭 width 인 직사각형 범위 피해</summary>
    public static int Box(Vector2 origin, Vector2 dir, float length, float width, DamageInfo dmg, float knock, Action<Enemy> onHit = null)
    {
        dir = dir.normalized;
        var side = new Vector2(-dir.y, dir.x);
        int n = 0;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - origin;
            float along = Vector2.Dot(d, dir), across = Mathf.Abs(Vector2.Dot(d, side));
            if (along < -e.radius || along > length + e.radius || across > width * 0.5f + e.radius) continue;
            e.Hit(dmg, dir, knock);
            onHit?.Invoke(e);
            n++;
        }
        return n;
    }

    /// <summary>카메라에 보이는 모든 적</summary>
    public static List<Enemy> OnScreen(List<Enemy> into)
    {
        into.Clear();
        var cam = Camera.main;
        if (cam == null) return into;
        float h = cam.orthographicSize, w = h * cam.aspect;
        Vector2 c = cam.transform.position;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - c;
            if (Mathf.Abs(d.x) <= w + e.radius && Mathf.Abs(d.y) <= h + e.radius) into.Add(e);
        }
        return into;
    }
}

/// <summary>
/// 공격 판정 표시 (F1 판정 표시가 켜져 있을 때만). SkillHits 의 실제 판정과 같은 모양을 그림:
/// 부채꼴(반경 r, 좌우 halfAngle) + 근접 원(각도 무관) / 원형.
/// 적의 빨간 원이 이 도형에 닿으면 맞음 (부채꼴 각도는 적 중심 기준).
/// </summary>
public static class HitDebug
{
    public static readonly Color SlashColor = new Color(1f, 0.92f, 0.2f);   // 노랑: 베기
    public static readonly Color BlastColor = new Color(1f, 0.45f, 0.15f);  // 주황: 폭발
    public static readonly Color KnockColor = new Color(0.35f, 0.85f, 1f);  // 하늘색: 넉백만 (피해 없음, 솔라 슬래시 넉백 범위)
    public const float Duration = 0.35f;

    public static void Sector(Vector2 c, Vector2 dir, float r, float halfAngle, float near, Color col)
    {
        if (!HitboxView.Show) return;
        halfAngle = Mathf.Clamp(halfAngle, 0f, 180f);
        if (halfAngle >= 179.9f) { Circle(c, r, col); return; }
        float a0 = Mathf.Atan2(dir.y, dir.x) - halfAngle * Mathf.Deg2Rad;
        int seg = Mathf.Max(6, Mathf.CeilToInt(halfAngle / 3f));
        var pts = new List<Vector3> { Vector3.zero };
        for (int i = 0; i <= seg; i++)
        {
            float a = a0 + 2f * halfAngle * Mathf.Deg2Rad * i / seg;
            pts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        HitDebugShape.Make(c, pts, true, col);
        if (near > 0f) Circle(c, near, col);
    }

    /// <summary>캡슐: c 기준 앞 forward · 뒤 back · 폭 width, 양 끝 반원</summary>
    public static void Capsule(Vector2 c, Vector2 dir, float forward, float back, float width, Color col)
    {
        if (!HitboxView.Show) return;
        dir = dir.normalized;
        float hw = width * 0.5f, s0 = -back + hw, s1 = forward - hw;
        if (s1 < s0) s0 = s1 = (forward - back) * 0.5f;
        float baseAng = Mathf.Atan2(dir.y, dir.x);
        Vector3 Pt(float along, float a) =>
            (Vector3)(dir * along) + new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * hw;
        const int seg = 16;
        var pts = new List<Vector3> { (Vector3)(dir * ((s0 + s1) * 0.5f)) };   // 채움용 중심
        for (int i = 0; i <= seg; i++)   // 앞쪽 반원 (-90° → +90°)
            pts.Add(Pt(s1, baseAng - Mathf.PI / 2 + Mathf.PI * i / seg));
        for (int i = 0; i <= seg; i++)   // 뒤쪽 반원 (+90° → +270°)
            pts.Add(Pt(s0, baseAng + Mathf.PI / 2 + Mathf.PI * i / seg));
        pts.Add(pts[1]);
        HitDebugShape.Make(c, pts, false, col);
    }

    public static void Circle(Vector2 c, float r, Color col)
    {
        if (!HitboxView.Show) return;
        const int seg = 48;
        var pts = new List<Vector3> { Vector3.zero };
        for (int i = 0; i <= seg; i++)
        {
            float a = 2f * Mathf.PI * i / seg;
            pts.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * r);
        }
        HitDebugShape.Make(c, pts, false, col);
    }
}

/// <summary>판정 도형 1개: 반투명 채움 + 외곽선, Duration 동안 서서히 사라짐</summary>
public class HitDebugShape : MonoBehaviour
{
    static Material mat;
    MeshRenderer fill;
    LineRenderer line;
    Color col;
    float t;

    /// <param name="pts">pts[0] = 중심, 나머지 = 가장자리 (부채꼴이면 외곽선이 중심을 거쳐 닫힘)</param>
    public static void Make(Vector2 c, List<Vector3> pts, bool throughCenter, Color col)
    {
        if (mat == null) mat = new Material(Shader.Find("Sprites/Default"));
        var go = new GameObject("HitDebug");
        go.transform.position = c;

        var mesh = new Mesh();
        mesh.SetVertices(pts);
        var tris = new List<int>();
        for (int i = 1; i < pts.Count - 1; i++) { tris.Add(0); tris.Add(i + 1); tris.Add(i); }
        mesh.SetTriangles(tris, 0);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var s = go.AddComponent<HitDebugShape>();
        s.col = col;
        s.fill = go.AddComponent<MeshRenderer>();
        s.fill.sharedMaterial = mat;
        s.fill.sortingOrder = 99;

        // 한 오브젝트에 Renderer 는 하나만 → 외곽선은 자식에
        var lgo = new GameObject("Outline");
        lgo.transform.SetParent(go.transform, false);
        s.line = lgo.AddComponent<LineRenderer>();
        s.line.sharedMaterial = mat;
        s.line.useWorldSpace = false;
        s.line.widthMultiplier = 0.05f;
        s.line.sortingOrder = 100;
        var outline = throughCenter ? pts : pts.GetRange(1, pts.Count - 1);
        s.line.positionCount = outline.Count;
        s.line.SetPositions(outline.ToArray());
        s.line.loop = true;
        s.Apply(1f);
    }

    void Apply(float k)
    {
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", new Color(col.r, col.g, col.b, 0.22f * k));
        fill.SetPropertyBlock(mpb);
        var lc = new Color(col.r, col.g, col.b, 0.95f * k);
        line.startColor = line.endColor = lc;
    }

    void Update()
    {
        t += Time.deltaTime;
        Apply(1f - t / HitDebug.Duration);
        if (t >= HitDebug.Duration) { Destroy(GetComponent<MeshFilter>().sharedMesh); Destroy(gameObject); }
    }
}
