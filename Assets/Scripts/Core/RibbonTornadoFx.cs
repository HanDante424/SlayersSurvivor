using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 곧은 리본 그림(불꽃 줄기)을 원뿔 나선 궤도의 타원 호를 따라 휘어 그리고 돌리는 토네이도 이펙트 (2026-11-21).
/// - 리본마다 띠 모양 메시를 매 프레임 다시 만듦: 길이 방향 = 궤도 호, 두께 방향 = 호의 수직
/// - 위에서 본 원을 옆에서 본 타원(세로 납작)으로 그림. 타원 아래쪽(sin &lt; 0) = 앞 → 캐릭터 앞 순서 · 밝게,
///   위쪽 = 뒤 → 캐릭터 뒤 순서 · 어둡고 투명하게
/// - 그림 프레임(시트 칸)을 넘겨 불꽃이 일렁이게 함
/// - 리본마다 그림 재료(Source)를 가중치대로 골라 섞어 씀 (2026-11-21: 애니메이션 줄기 · 낱장 줄기 혼합). Source 를 안 주면 아래 texture/frames 한 벌로 동작 (예전 방식)
/// 값은 Settings 로 받음 (기본값은 모두 [임시])
/// </summary>
public class RibbonTornadoFx : MonoBehaviour
{
    /// <summary>리본 그림 재료 한 종류 (시트 하나). 리본마다 하나를 골라 씀</summary>
    public class Source
    {
        public Texture2D texture;
        /// <summary>리본 그림 영역 (텍스처 UV 0~1). 여러 개면 fps 로 넘김, 하나면 정지 그림</summary>
        public Rect[] frames;
        /// <summary>true = 그림 세로가 리본 길이. false = 가로 (왼쪽 = 꼬리, 오른쪽 = 머리)</summary>
        public bool lengthAlongY;
        public float fps = 20f;
        /// <summary>뽑힐 비중 (큰 값일수록 자주)</summary>
        public float weight = 1f;
        /// <summary>리본 두께 배율 (그림 두께 방향 영역 크기가 다른 재료의 굵기를 맞출 때)</summary>
        public float thickMul = 1f;
    }

    public class Settings
    {
        /// <summary>여러 재료를 섞을 때 (비어 있으면 아래 texture/frames 한 벌을 씀)</summary>
        public List<Source> sources;

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
        /// <summary>
        /// true = 기 모으기 모드: 리본이 시간차로 하나씩 아래 (level 0) 에서 시작해 riseTime 동안 위 (level 1) 로 타고 올라가며 나타났다 사라짐.
        /// 높이 · 반지름은 bottom/top 값을 그대로 쓰고 (아래 → 위 로 갈수록 반지름이 변함), 전체 growTime/fadeTime 은 안 써도 됨
        /// </summary>
        public bool climb;
        public float riseTime = 0.7f;        // climb: 리본 하나가 아래에서 위까지 오르는 시간
        /// <summary>
        /// > 0 = 밑단부터 차례로 생성: 아래 (level 0) 리본이 먼저, 위로 갈수록 늦게 (level x buildUpTime 초 뒤) 시작해
        /// 각자 buildUpGrow 초 동안 작은 반지름 → 원래 크기로 커지며 나타남. 이 모드에서는 전체 growTime 은 0 으로 둘 것
        /// </summary>
        public float buildUpTime;
        public float buildUpGrow = 0.3f;
        /// <summary>
        /// expandTime > 0 = 밑단 확장: expandStart 초부터 expandTime 동안 아래 반지름이 bottomRadius → bottomRadiusEnd 로 부드럽게 넓어짐 (위쪽 반지름은 그대로 → 깔때기가 아래로 퍼짐)
        /// </summary>
        public float bottomRadiusEnd, expandStart, expandTime;
        /// <summary>
        /// driftDistance > 0 = 떠오르며 퍼지고 사라짐: driftStart 초 (보통 생성이 끝나는 시각) 부터 life 까지 가속하며 위로 driftDistance 만큼 올라가고,
        /// 같은 동안 반지름이 spreadEndScale 배까지 더 퍼짐. 사라짐은 fadeTime 으로 (보통 길게 두어 오르는 동안 서서히 사라지게)
        /// </summary>
        public float driftStart, driftDistance, spreadEndScale = 1f;
        /// <summary>
        /// true = 사라질 때 (끝에서 fadeTime 동안) 위쪽 리본부터 차례로 투명해져 아래쪽이 마지막에 사라짐 (전체가 한꺼번에 흐려지지 않음).
        /// 위 (level 1) 는 fadeTime 시작부터, 아래 (level 0) 는 60% 지점부터 각자 fadeTime 의 40% 동안 사라짐
        /// </summary>
        public bool fadeFromTop;
        /// <summary>떠오르는 가속 지수 (1 = 일정한 속도, 클수록 처음 느리고 끝에 급가속, 1 보다 작으면 처음부터 빠름)</summary>
        public float driftEase = 1.6f;
        /// <summary>토네이도 전체를 기준점 (밑단) 에서 화면 평면으로 기울임 (도, + = 왼쪽으로 눕고 - = 오른쪽으로 눕음). 0 = 곧게</summary>
        public float tiltDegrees;
        /// <summary>세로 길이 배율 (높이 · 오르는 거리 · 나선 기울기에 곱함). 토네이도를 화면 쪽으로 눕힐 때 (보는 각도를 올릴 때) 세로가 짧아 보이게 줄이는 용도. 1 = 그대로</summary>
        public float verticalScale = 1f;
        public Vector2 spawnOffset;          // 생성 직후 기준점에서 이만큼 떨어진 곳에서 시작해 offsetTime 동안 제자리로 올라옴 (기 모으기에서 이어질 때 플레이어 몸에서 솟아오르게)
        public float offsetTime;
        public float climbSpread = 0.8f;     // climb: 리본 시작 시각을 전체 시간 (life) 의 이 비율 안에 고르게 흩뿌림. 끝 무렵 시작한 리본은 오르던 중에 끊김 → 끊김 없이 계속 피어오르는 느낌
        public float radiusJitter = 0f;      // 리본마다 반지름 편차 (±비율) — 같은 높이에서도 안팎으로 여러 겹이 쌓여 빽빽한 폭풍이 됨
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
        public float level, phase, arc, speedMul, rise, frameOffset, radiusMul, delay;
        public Mesh back, front;
        public Source src;
    }

    Settings s;
    float t;
    Vector3 basePos;
    readonly List<Source> srcs = new List<Source>();
    readonly Dictionary<Source, Material> mats = new Dictionary<Source, Material>();
    readonly List<Ribbon> list = new List<Ribbon>();
    readonly List<Vector3> bv = new List<Vector3>(), fv = new List<Vector3>();
    readonly List<Vector2> bu = new List<Vector2>(), fu = new List<Vector2>();
    readonly List<Color> bc = new List<Color>(), fc = new List<Color>();
    readonly List<int> bi = new List<int>(), fi = new List<int>();

    public static RibbonTornadoFx Spawn(Settings settings, Transform parent, Vector2 localPos)
    {
        if (settings == null) return null;
        bool hasSources = settings.sources != null && settings.sources.Exists(x => x != null && x.texture != null && x.frames != null && x.frames.Length > 0);
        bool legacy = settings.texture != null && settings.frames != null && settings.frames.Length > 0;
        if (!hasSources && !legacy) return null;
        var go = new GameObject("FX_RibbonTornado");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var fx = go.AddComponent<RibbonTornadoFx>();
        fx.basePos = go.transform.localPosition;
        fx.Init(settings);
        return fx;
    }

    void Init(Settings settings)
    {
        s = settings;
        transform.localRotation = Quaternion.Euler(0f, 0f, s.tiltDegrees);
        if (s.sources != null)
            foreach (var x in s.sources)
                if (x != null && x.texture != null && x.frames != null && x.frames.Length > 0) srcs.Add(x);
        if (srcs.Count == 0)   // 예전 방식: texture/frames 한 벌
            srcs.Add(new Source { texture = s.texture, frames = s.frames, lengthAlongY = s.lengthAlongY, fps = s.fps });
        foreach (var x in srcs) mats[x] = new Material(Shader.Find("Sprites/Default")) { mainTexture = x.texture };

        float totalWeight = 0f;
        foreach (var x in srcs) totalWeight += Mathf.Max(0f, x.weight);
        for (int k = 0; k < s.ribbons; k++)
        {
            var src = PickSource(totalWeight);
            var r = new Ribbon
            {
                src = src,
                // 높이를 고르게 나누고 조금씩 흔듦 (층이 겹쳐 보이게)
                level = Mathf.Clamp01((k + Random.Range(0.1f, 0.9f)) / s.ribbons),
                phase = Random.Range(0f, 360f),
                arc = Random.Range(s.arcMin, s.arcMax),
                speedMul = 1f + Random.Range(-s.spinJitter, s.spinJitter),
                rise = Random.Range(-s.riseJitter, s.riseJitter),
                frameOffset = Random.Range(0f, 100f),
                radiusMul = 1f + Random.Range(-s.radiusJitter, s.radiusJitter),
                delay = s.climb ? s.life * s.climbSpread * (k + Random.value) / s.ribbons : 0f,   // 시각을 고르게 나눠 (균등 분할 + 흔들림) 계속 새 줄기가 피어오름
            };
            r.back = MakePart("Back", s.backOrder, mats[src]);
            r.front = MakePart("Front", s.frontOrder, mats[src]);
            list.Add(r);
        }
    }

    /// <summary>가중치대로 재료 하나 뽑기</summary>
    Source PickSource(float totalWeight)
    {
        if (srcs.Count == 1 || totalWeight <= 0f) return srcs[Random.Range(0, srcs.Count)];
        float pick = Random.Range(0f, totalWeight);
        foreach (var x in srcs)
        {
            pick -= Mathf.Max(0f, x.weight);
            if (pick <= 0f) return x;
        }
        return srcs[srcs.Count - 1];
    }

    Mesh MakePart(string name, int order, Material mat)
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
        t += WorldSlow.EffectDelta;   // 궁극기 연출 전용이라 WorldSlow 로 느려지지 않음 (일시정지에서는 멈춤)
        if (t >= s.life) { Destroy(gameObject); return; }

        if (s.offsetTime > 0f && t < s.offsetTime + 0.05f)   // 시작 위치에서 제자리로 부드럽게 (처음 빠르게). 끝난 뒤에는 위치를 건드리지 않음 → 밖에서 옮길 수 있음 (낙하)
        {
            float k = Mathf.Clamp01(t / s.offsetTime);
            k = 1f - (1f - k) * (1f - k);
            transform.localPosition = basePos + (Vector3)(s.spawnOffset * (1f - k));
        }

        // 떠오르며 퍼짐 (driftDistance > 0): 생성이 끝난 뒤 가속하며 위로 + 반지름 확대
        float spread = 1f;
        if (s.driftDistance > 0f && t > s.driftStart)
        {
            float k = Mathf.Clamp01((t - s.driftStart) / Mathf.Max(0.01f, s.life - s.driftStart));
            transform.localPosition = basePos + Vector3.up * (s.driftDistance * s.verticalScale * Mathf.Pow(k, s.driftEase));   // driftEase > 1 이면 처음 천천히 → 점점 빨라짐
            spread = Mathf.Lerp(1f, s.spreadEndScale, Mathf.Pow(k, 0.8f));
        }

        // 생성: 작게 → 원래 크기 (처음 빠르게) · 사라짐: 투명해지며 조금 커짐
        float grow = s.growTime > 0f ? Mathf.Clamp01(t / s.growTime) : 1f;
        grow = 1f - (1f - grow) * (1f - grow);
        float fade = (s.fadeTime > 0f && !s.fadeFromTop) ? Mathf.Clamp01((s.life - t) / s.fadeTime) : 1f;   // fadeFromTop 이면 리본마다 따로 (Build)
        float radiusMul = Mathf.Lerp(s.growFromRadius, 1f, grow) * Mathf.Lerp(s.fadeToRadius, 1f, fade) * spread;
        float alpha = grow * fade;

        foreach (var r in list) Build(r, radiusMul, alpha);
    }

    void Build(Ribbon r, float radiusMul, float alpha)
    {
        bv.Clear(); fv.Clear(); bu.Clear(); fu.Clear(); bc.Clear(); fc.Clear(); bi.Clear(); fi.Clear();

        // 기 모으기 모드: 이 리본이 올라가는 중이 아니면 그리지 않음. 오르는 동안 level = 진행도, 양 끝에서 투명 → 중간에서 가장 진함
        float lvl = r.level;
        if (s.climb)
        {
            float prog = (t - r.delay) / Mathf.Max(0.01f, s.riseTime);
            if (prog < 0f || prog > 1f) { r.back.Clear(); r.front.Clear(); return; }
            lvl = prog;
            alpha *= Mathf.Sin(Mathf.PI * prog);
        }
        if (s.fadeFromTop && s.fadeTime > 0f)   // 위쪽부터 차례로 사라짐
        {
            float p = Mathf.Clamp01(1f - (s.life - t) / s.fadeTime);   // 0 → 1 (사라지는 구간 진행도)
            float start = (1f - r.level) * 0.6f;                       // 위 = 0, 아래 = 0.6
            alpha *= 1f - Mathf.Clamp01((p - start) / 0.4f);
            if (alpha <= 0.002f) { r.back.Clear(); r.front.Clear(); return; }
        }
        var src = r.src;
        Rect uv = src.frames.Length == 1 ? src.frames[0] : src.frames[(int)(t * src.fps + r.frameOffset) % src.frames.Length];
        if (s.buildUpTime > 0f && !s.climb)   // 밑단부터: 이 리본의 차례가 올 때까지 그리지 않고, 차례가 되면 작게 → 크게 + 투명 → 진하게
        {
            float k = Mathf.Clamp01((t - r.level * s.buildUpTime) / Mathf.Max(0.01f, s.buildUpGrow));
            if (k <= 0f) { r.back.Clear(); r.front.Clear(); return; }
            k = 1f - (1f - k) * (1f - k);
            radiusMul *= Mathf.Lerp(s.growFromRadius, 1f, k);
            alpha *= k;
        }
        float bottomR = s.bottomRadius;
        if (s.expandTime > 0f)   // 밑단 확장 (부드럽게 가속 → 감속)
        {
            float k = Mathf.Clamp01((t - s.expandStart) / s.expandTime);
            k = k * k * (3f - 2f * k);
            bottomR = Mathf.Lerp(s.bottomRadius, s.bottomRadiusEnd, k);
        }
        float radius = Mathf.Lerp(bottomR, s.topRadius, lvl) * radiusMul * r.radiusMul;
        float y0 = s.baseY + s.height * lvl * s.verticalScale;
        float thick = Mathf.Lerp(s.thickBottom, s.thickTop, lvl) * radiusMul * src.thickMul;
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
            uvs.Add(UV(src, uv, u0, 0f)); uvs.Add(UV(src, uv, u0, 1f)); uvs.Add(UV(src, uv, u1, 0f)); uvs.Add(UV(src, uv, u1, 1f));
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
    static Vector2 UV(Source src, Rect r, float along, float across) => src.lengthAlongY
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
        foreach (var m in mats.Values) if (m != null) Destroy(m);
    }
}
