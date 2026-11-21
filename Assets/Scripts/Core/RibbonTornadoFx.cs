using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 곧은 리본 그림(불꽃 줄기)을 원뿔 나선 궤도의 타원 호를 따라 휘어 그리고 돌리는 토네이도 이펙트 (2026-11-21).
/// - 리본마다 띠 모양 메시를 매 프레임 다시 만듦: 길이 방향 = 궤도 호, 두께 방향 = 호의 수직
/// - 위에서 본 원을 옆에서 본 타원(세로 납작)으로 그림. 타원 아래쪽(sin &lt; 0) = 앞 → 캐릭터 앞 순서 · 밝게,
///   위쪽 = 뒤 → 캐릭터 뒤 순서 · 어둡고 투명하게
/// - 그림 프레임(시트 칸)을 넘겨 불꽃이 일렁이게 함
/// 값은 Settings 로 받음 (기본값은 모두 [임시])
/// </summary>
public class RibbonTornadoFx : MonoBehaviour
{
    public class Settings
    {
        public Texture2D texture;
        /// <summary>리본 그림 영역 (텍스처 UV 0~1). 여러 개면 fps 로 넘김</summary>
        public Rect[] frames;
        /// <summary>true = 그림 세로가 리본 길이 (아래 = 꼬리, 위 = 머리). false = 가로 (왼쪽 = 꼬리, 오른쪽 = 머리)</summary>
        public bool lengthAlongY;
        public float fps = 20f;

        public int ribbons = 20;
        public float baseY = -1f;            // 토네이도 아래 끝 높이 (기준점 기준)
        public float height = 8f;
        public float bottomRadius = 0.5f, topRadius = 4f;
        public float squash = 0.3f;          // 타원 세로 / 가로 (옆에서 본 기울기)
        public float arcMin = 70f, arcMax = 150f;   // 리본 하나가 덮는 궤도 각도 (도)
        public float thickBottom = 0.25f, thickTop = 0.9f;   // 리본 두께 (월드)
        public float endThickness = 0.35f;   // 리본 양 끝 두께 배율 (가운데 = 1)
        public float riseJitter = 0.35f;     // 리본 양 끝 높이 차 (월드, ±) — 나선처럼 비스듬히
        public float spin = 420f;            // 회전 속도 (도/초, + = 시계 반대)
        public float spinJitter = 0.3f;      // 리본마다 속도 편차 (±비율)
        public int segments = 18;            // 리본 하나를 나누는 토막 수
        public float growTime = 0.3f, fadeTime = 0.35f, life = 1.5f;
        public float growFromRadius = 0.3f;  // 생성 시작 때 반지름 배율
        public float fadeToRadius = 1.2f;    // 사라질 때 반지름 배율
        public int backOrder = 9, frontOrder = 11;   // 플레이어 = 10
        public Color backTint = new Color(0.6f, 0.4f, 0.35f, 0.7f);
        public Color frontTint = Color.white;
    }

    class Ribbon
    {
        public float level, phase, arc, speedMul, rise, frameOffset;
        public Mesh back, front;
    }

    Settings s;
    float t;
    Material mat;
    readonly List<Ribbon> list = new List<Ribbon>();
    readonly List<Vector3> bv = new List<Vector3>(), fv = new List<Vector3>();
    readonly List<Vector2> bu = new List<Vector2>(), fu = new List<Vector2>();
    readonly List<Color> bc = new List<Color>(), fc = new List<Color>();
    readonly List<int> bi = new List<int>(), fi = new List<int>();

    public static RibbonTornadoFx Spawn(Settings settings, Transform parent, Vector2 localPos)
    {
        if (settings == null || settings.texture == null || settings.frames == null || settings.frames.Length == 0) return null;
        var go = new GameObject("FX_RibbonTornado");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var fx = go.AddComponent<RibbonTornadoFx>();
        fx.Init(settings);
        return fx;
    }

    void Init(Settings settings)
    {
        s = settings;
        mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = s.texture };
        for (int k = 0; k < s.ribbons; k++)
        {
            var r = new Ribbon
            {
                // 높이를 고르게 나누고 조금씩 흔듦 (층이 겹쳐 보이게)
                level = Mathf.Clamp01((k + Random.Range(0.1f, 0.9f)) / s.ribbons),
                phase = Random.Range(0f, 360f),
                arc = Random.Range(s.arcMin, s.arcMax),
                speedMul = 1f + Random.Range(-s.spinJitter, s.spinJitter),
                rise = Random.Range(-s.riseJitter, s.riseJitter),
                frameOffset = Random.Range(0f, 100f),
            };
            r.back = MakePart("Back", s.backOrder);
            r.front = MakePart("Front", s.frontOrder);
            list.Add(r);
        }
    }

    Mesh MakePart(string name, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = "RibbonTornado_" + name };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.sortingOrder = order;
        return mesh;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= s.life) { Destroy(gameObject); return; }

        // 생성: 작게 → 원래 크기 (처음 빠르게) · 사라짐: 투명해지며 조금 커짐
        float grow = s.growTime > 0f ? Mathf.Clamp01(t / s.growTime) : 1f;
        grow = 1f - (1f - grow) * (1f - grow);
        float fade = s.fadeTime > 0f ? Mathf.Clamp01((s.life - t) / s.fadeTime) : 1f;
        float radiusMul = Mathf.Lerp(s.growFromRadius, 1f, grow) * Mathf.Lerp(s.fadeToRadius, 1f, fade);
        float alpha = grow * fade;

        foreach (var r in list) Build(r, radiusMul, alpha);
    }

    void Build(Ribbon r, float radiusMul, float alpha)
    {
        bv.Clear(); fv.Clear(); bu.Clear(); fu.Clear(); bc.Clear(); fc.Clear(); bi.Clear(); fi.Clear();

        Rect uv = s.frames[(int)(t * s.fps + r.frameOffset) % s.frames.Length];
        float radius = Mathf.Lerp(s.bottomRadius, s.topRadius, r.level) * radiusMul;
        float y0 = s.baseY + s.height * r.level;
        float thick = Mathf.Lerp(s.thickBottom, s.thickTop, r.level) * radiusMul;
        float a0 = (r.phase + s.spin * r.speedMul * t) * Mathf.Deg2Rad;
        float arc = r.arc * Mathf.Deg2Rad;
        int n = Mathf.Max(2, s.segments);

        Vector2 Point(float u)
        {
            float a = a0 + arc * u;
            return new Vector2(Mathf.Cos(a) * radius, y0 + (u - 0.5f) * r.rise * 2f + Mathf.Sin(a) * radius * s.squash);
        }

        for (int i = 0; i < n; i++)
        {
            float u0 = i / (float)n, u1 = (i + 1) / (float)n;
            float mid = a0 + arc * (u0 + u1) * 0.5f;
            float depth = (1f - Mathf.Sin(mid)) * 0.5f;   // 1 = 가장 앞 (타원 아래)
            bool front = Mathf.Sin(mid) < 0f;
            var col = Color.Lerp(s.backTint, s.frontTint, depth);
            col.a *= alpha;
            float w = thick * 0.5f * Mathf.Lerp(0.8f, 1.1f, depth)
                      * Mathf.Lerp(s.endThickness, 1f, Mathf.Sin(Mathf.PI * (u0 + u1) * 0.5f));   // 양 끝을 가늘게

            Vector2 p0 = Point(u0), p1 = Point(u1);
            Vector2 n0 = Normal(u0, Point), n1 = Normal(u1, Point);

            var v = front ? fv : bv; var uvs = front ? fu : bu; var c = front ? fc : bc; var idx = front ? fi : bi;
            int b = v.Count;
            v.Add(p0 - n0 * w); v.Add(p0 + n0 * w); v.Add(p1 - n1 * w); v.Add(p1 + n1 * w);
            uvs.Add(UV(uv, u0, 0f)); uvs.Add(UV(uv, u0, 1f)); uvs.Add(UV(uv, u1, 0f)); uvs.Add(UV(uv, u1, 1f));
            c.Add(col); c.Add(col); c.Add(col); c.Add(col);
            idx.Add(b); idx.Add(b + 1); idx.Add(b + 2); idx.Add(b + 2); idx.Add(b + 1); idx.Add(b + 3);
        }
        Apply(r.back, bv, bu, bc, bi);
        Apply(r.front, fv, fu, fc, fi);
    }

    static Vector2 Normal(float u, System.Func<float, Vector2> point)
    {
        const float e = 0.01f;
        Vector2 d = point(Mathf.Min(1f, u + e)) - point(Mathf.Max(0f, u - e));
        if (d.sqrMagnitude < 1e-8f) return Vector2.up;
        d.Normalize();
        return new Vector2(-d.y, d.x);
    }

    /// <summary>along = 리본 길이 위치 (0 꼬리 ~ 1 머리), across = 두께 위치 (0 ~ 1)</summary>
    Vector2 UV(Rect r, float along, float across) => s.lengthAlongY
        ? new Vector2(r.x + (1f - across) * r.width, r.y + along * r.height)
        : new Vector2(r.x + along * r.width, r.y + across * r.height);

    static void Apply(Mesh m, List<Vector3> v, List<Vector2> u, List<Color> c, List<int> i)
    {
        m.Clear();
        m.SetVertices(v); m.SetUVs(0, u); m.SetColors(c); m.SetTriangles(i, 0);
    }

    void OnDestroy()
    {
        foreach (var r in list) { if (r.back != null) Destroy(r.back); if (r.front != null) Destroy(r.front); }
        if (mat != null) Destroy(mat);
    }
}
