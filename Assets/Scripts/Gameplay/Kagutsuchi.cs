using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 카구츠치 (레이드 보스, 2026-11-13 사용자 기획 · Docs/Design/Raid_Kagutsuchi_Plan_v0.1.md).
/// 설정 원문 Raid_Kagutsuchi_Setting_v0.1.md — 아메노무라쿠모 · 야타의 거울 권능을 임의로 넣지 않음.
///
/// 지금 구현 = 입장 연출까지:
///   맵 밖 중앙 위에서 천천히 걸어 들어옴 (카메라가 보스를 확대해서 따라감)
///   → 멈추면 카메라가 맵 전체를 비춤
///   → 카구츠치에서 부채꼴 무작위 방향으로 맵 끝까지 레이저 경고 (폭 1.5) 1초 → 레이저 0.2초, 경고는 0.05초마다 총 20회
///   → 난사 끝 1초 뒤 보스가 중앙 조금 아래로 레이저 (폭 0.5) → 0.5초 뒤 반경 14 까지 커지는 경고 → 5초 뒤 원 안 피해
///   → 야타의 거울이 나타나 보스 주변을 돌며 항상 플레이어 쪽에 위치 (체력 = 카구츠치 × 15, 때릴 수 있음 · 방어 판정은 아직 없음)
///   → 입장 끝: 맞을 수 있게 됨. 카메라는 보스전 내내 맵 전체 (입장 때 걸어 들어오는 동안만 확대). 고정형 보스 — 멈춘 자리 (맵 중앙 위) 에서 움직이지 않음 (공격 패턴 미설계)
/// 기획에 없는 값은 [임시].
/// </summary>
public class KagutsuchiAI : MonoBehaviour, IRaidBossAI
{
    // ---------- 입장: 걸어 들어오기 ----------
    const float EnterStartOutside = 3f;  // [임시] 맵 위쪽 경계 밖 이만큼에서 시작
    const float EnterStopInside = 3f;    // [임시] 맵 위쪽 경계에서 이만큼 안쪽에서 멈춤 = 기본 위치 (맵 중심 기준 y = 9, 사용자: 더 위로 — 5 → 3)
    const float EnterSpeed = 1.6f;       // [임시] "천천히" (약 5초)
    const float EnterZoom = 4.5f;        // [임시] 카메라 줌인 시야 (세로 절반, 기본 7)
    const float StopPause = 0.6f;        // [임시] 멈춘 뒤 맵 전체 보기로 바뀌는 동안

    // ---------- 입장: 레이저 ----------
    const int IntroLaserCount = 20;      // 총 20회 (사용자: 10 → 15 → 30 → 20)
    const float IntroLaserInterval = 0.05f;  // 0.05초마다 경고 생성 (사용자: 0.5 → 0.2 → 0.05)
    const float IntroLaserFan = 120f;    // [임시] 카구츠치에서 아래(맵 쪽)로 펼쳐지는 부채꼴 각도 — 이 안에서 무작위 방향
    const float IntroLaserWarn = 1f;     // 경고 1초 (사용자)
    const float IntroLaserTime = 0.2f;   // 레이저 0.2초 (사용자)
    const float IntroLaserWidth = 1.5f;  // 폭 (사용자: 2 → 1.5)
    const float IntroLaserDamage = 20f;  // [임시]

    // ---------- 입장: 난사 뒤 커지는 원 (보스 레이저로 충전) ----------
    const float NovaBeamDelay = 1f;      // 난사가 끝나고 1초 뒤 보스가 경고 중심으로 레이저 (사용자)
    const float NovaBeamWidth = 0.5f;    // 레이저 폭 (사용자: 1 → 0.5)
    const float NovaWarnDelay = 0.5f;    // 레이저 0.5초 뒤부터 경고가 나타나 커짐 (= 난사 끝 1.5초 뒤, 사용자)
    const float NovaGrowTime = 5f;       // 경고가 커지기 시작하고 5초 뒤 피해 (사용자)
    const float NovaRadius = 14f;        // 총 크기 반경 14 (사용자)
    const float NovaCenterDown = 2f;     // [임시] "중앙 조금 아래" = 맵 중심에서 아래로 2
    const float NovaStartRadius = 0.5f;  // [임시] 경고 시작 크기
    const float NovaDamage = 40f;        // [임시]
    const float NovaBeamDamage = 10f;    // [임시] 레이저에 닿으면 (0.5초마다)

    // ---------- 야타의 거울 ----------
    const float MirrorGap = 1f;          // 보스 몸에서 1 밖 (사용자)
    const float MirrorTurnSpeed = 240f;  // [임시] 플레이어 쪽으로 도는 속도 (도/초)
    const float AfterMirror = 0.5f;      // [임시] 거울이 나타난 뒤 입장 끝까지

    // ---------- 공격 패턴 1: 반사 레이저 ----------
    const float ReflectWarn = 2f;        // 경고 2초 (사용자)
    const float ReflectLaserTime = 0.5f; // 레이저 0.5초 (사용자)
    const float ReflectWidth = 1f;       // 폭 1 (사용자)
    const int ReflectBounces = 5;        // 맵 벽에 닿으면 반사, 총 5번 (사용자)
    const float ReflectCooldown = 3f;    // 쿨타임 (사용자: 5 → 3초)
    const float ReflectDamage = 20f;     // [임시]
    const float ReflectFirstUse = 2f;    // [임시] 입장이 끝나고 첫 사용까지

    // ---------- 공격 패턴 2: 거울 반사 부채꼴 레이저 ----------
    const float MirrorFrontGap = 2f;     // 야타의 거울을 보스 정면 거리 2 로 (사용자) — 보스 몸 바깥 2
    const float MirrorMoveMax = 0.6f;    // [임시] 거울이 정면에 도착할 때까지 최대 대기
    const float MirrorBeamTime = 1f;     // 보스가 폭 1 레이저를 1초간 거울에 발사 (사용자)
    const float MirrorBeamWidth = 1f;    // (사용자)
    const float MirrorFanDelay = 0.5f;   // 0.5초 후 부채꼴 경고 (사용자) — [해석] 보스 레이저 시작 0.5초 뒤, 경고는 보스 레이저가 끝날 때 발사
    const int MirrorFanCount = 10;       // 10개 (사용자)
    const float MirrorFanWidth = 2f;     // 폭 2 (사용자)
    const float MirrorFanLaserTime = 0.5f;   // 0.5초 지속, 맵을 꿰뚫음 (사용자)
    const float MirrorFanAngle = 90f;    // [임시] 부채꼴 각도 (거울에서 플레이어 쪽)
    const float MirrorCooldown = 10f;    // 쿨타임 10초 (사용자)
    const float MirrorFirstUse = 4f;     // [임시]
    const float MirrorBeamDamage = 10f, MirrorFanDamage = 20f;   // [임시] 보스 레이저 (0.5초마다) · 부채꼴 레이저 1개당

    // ---------- 공격 패턴 3: 검 횡베기 → 맵 가르는 베기 10회 ----------
    const float SwordSweepRadius = 10f;  // 반원 반경 10 (사용자)
    const float SwordSweepWarn = 2f;     // 2초 뒤 횡베기 (사용자)
    const float SwordCutDelay = 0.5f;    // 횡베기 0.5초 뒤 베기 경고 시작 (사용자)
    const int SwordCutCount = 10;        // 10회 (사용자)
    const float SwordCutWidth = 2f;      // radius 1 의 검 = 폭 2 (사용자)
    const float SwordCutWarn = 0.8f;     // [임시] 경고 → 베기 (10개 한번에, 사용자)
    const float SwordCutFlash = 0.15f;   // [임시] 베기 표시 시간
    const float SwordCooldown = 12f;     // [임시] 기획 미정
    const float SwordFirstUse = 6f;      // [임시]
    const float SwordSweepDamage = 25f, SwordCutDamage = 20f;   // [임시]
    public const float SwordLength = 5f;    // 검 길이 (사용자: 10 → 5)
    public const float SwordThick = 0.45f;  // [임시] 검 두께
    /// <summary>검이 회전하는 공격의 원 반경 = 검 크기 기준 (사용자). 회전축 = 검 아래쪽 (손잡이 끝, 사용자) → 반경 = 검 길이</summary>
    const float SwordSpinRadius = SwordLength;
    const float SwordHpMul = 1f;         // [임시] 검 체력 = 카구츠치 최대 체력 × 1 (기획 미정, 사용자: 검도 독립 체력)

    // ---------- 공격 패턴 4: 찌르기 → 그 자리에서 회전 ----------
    const float ThrustWarn = 1f;         // 1초 경고 (사용자)
    const float ThrustWidth = 1.5f;      // 폭 1.5 (사용자)
    const float ThrustDistance = 10f;    // 거리 10 (사용자) — 검이 10 앞으로 날아감
    const float ThrustTime = 0.2f;       // [임시] 찌르기에 걸리는 시간
    const float ThrustSpinWarn = 1.5f;   // 그 자리에서 검 길이에 맞는 원으로 1.5초 경고 (사용자)
    const float SpinTime = 0.4f;         // [임시] 회전 공격 시간 (한 바퀴 반)
    const float ThrustCooldown = 12f;    // [임시] 기획 미정
    const float ThrustFirstUse = 8f;     // [임시]
    const float ThrustDamage = 25f, SpinDamage = 25f;   // [임시]

    // ---------- 공격 패턴 5: 아래로 내려와 회전하며 플레이어 쪽 5 이동 × 2 ----------
    const float MarchFirstWarn = 1f;     // 1초 경고 (사용자)
    const float MarchSecondWarn = 1.5f;  // 1.5초 경고 (사용자)
    const float MarchDistance = 5f;      // 플레이어 위치로 5 이동 (사용자)
    const float MarchTime = 0.5f;        // [임시] 5 이동에 걸리는 시간
    const float MarchCooldown = 12f;     // [임시] 기획 미정
    const float MarchFirstUse = 10f;     // [임시]
    const float MarchDamage = 25f;       // [임시] 한 번 이동에 1번

    // 검 색 (사용자: 흰색, 은색 언저리)
    static readonly Color SwordCore = new Color(0.9f, 0.93f, 0.98f, 0.95f);
    static readonly Color SwordEdge = new Color(1f, 1f, 1f, 1f);

    // [임시] 화염 색 (설정 미정) — 레이저 · 연출용
    static readonly Color FireCore = new Color(1f, 0.45f, 0.1f, 0.95f);
    static readonly Color FireEdge = new Color(1f, 0.9f, 0.55f, 0.95f);
    static readonly Color FireFx = new Color(1f, 0.55f, 0.2f, 0.75f);

    Enemy e;
    bool intro = true;
    KagutsuchiMirror mirror;
    KagutsuchiBlade blade;
    readonly List<BossWarning> warnings = new List<BossWarning>();

    static Player P => Player.I;
    static bool Playing => GameManager.I != null && GameManager.I.State == GameState.Playing;
    static Vector2 Half => Stage.HalfSize;
    static Vector2 Center => Stage.Center;

    /// <summary>입장 연출 중 (맞지 않음 · 이동 없음)</summary>
    public bool InIntro => intro;

    // ---------- IRaidBossAI ----------
    /// <summary>걸어 들어오는 동안은 체력 바 없음 (멈춘 뒤부터 표시)</summary>
    public bool HideBossBar => intro && entering;
    /// <summary>야타의 거울 · 검 체력 바 (보스 체력 바 아래, 살아 있는 것만 차례로)</summary>
    public void DrawHud(float W)
    {
        float by = 108f;
        if (mirror != null && mirror.Body != null && mirror.Body.hp > 0) { SubBar(W, by, "야타의 거울", mirror.Body, new Color(0.75f, 0.85f, 1f)); by += 38f; }
        if (blade != null && blade.Body != null && blade.Body.hp > 0) SubBar(W, by, "아메노무라쿠모", blade.Body, new Color(0.88f, 0.9f, 0.95f));
    }

    static void SubBar(float W, float by, string label, Enemy b, Color fill)
    {
        subStyle ??= new GUIStyle(UIStyles.Label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
        float bw = Mathf.Clamp(W * 0.3f, 260f, 520f), bh = 12f;
        float bx = (W - bw) / 2f;
        GUI.Label(new Rect(bx, by - 2, bw, 18), label, subStyle);
        GUI.color = new Color(0, 0, 0, 0.8f);
        GUI.DrawTexture(new Rect(bx - 2, by + 16, bw + 4, bh + 4), UIStyles.White);
        GUI.color = Color.white;
        UIStyles.Bar(new Rect(bx, by + 18, bw, bh), b.maxHp > 0f ? b.hp / b.maxHp : 0f, fill);
        GUI.Label(new Rect(bx, by + 14, bw, 20), $"{Mathf.CeilToInt(Mathf.Max(0f, b.hp))} / {Mathf.CeilToInt(b.maxHp)}", subStyle);
    }
    static GUIStyle subStyle;

    bool entering = true;

    void Awake()
    {
        e = GetComponent<Enemy>();
        e.SetScripted(true);   // 입장 중: 맞지 않음 · 접촉 피해 없음 · 맵 밖에 있어도 됨
        transform.position = Center + new Vector2(0f, Half.y + EnterStartOutside);
    }

    void Start() => StartCoroutine(Intro());

    void OnDisable()
    {
        foreach (var w in warnings) if (w != null) Destroy(w.gameObject);
        warnings.Clear();
        if (mirror != null) Destroy(mirror.gameObject);
        if (blade != null) Destroy(blade.gameObject);
        if (intro) { CameraFollow.ClearFocus(); CameraFollow.ResetZoom(); }
    }

    // ---------- 패턴 진행 ----------
    // [임시] 규칙: 패턴은 한 번에 하나 · 쿨타임은 패턴이 끝난 뒤부터 · 기절하면 진행 중 패턴이 멈췄다가 이어서 (가니악과 같은 방식, 공통 쿨타임 없음)
    //         준비된 패턴이 여러 개면 무작위
    enum Pattern { Reflect, MirrorBeam, SwordCut, Thrust, March }
    static readonly float[] Cooldowns = { ReflectCooldown, MirrorCooldown, SwordCooldown, ThrustCooldown, MarchCooldown };
    readonly float[] cd = { ReflectFirstUse, MirrorFirstUse, SwordFirstUse, ThrustFirstUse, MarchFirstUse };
    readonly List<int> ready = new List<int>();
    bool busy;

    /// <summary>Enemy.Update 에서 매 프레임 (기절 · 일시정지 중에는 불리지 않음 → 쿨타임도 멈춤)</summary>
    public Vector2 Behave(Vector2 toPlayer, float moveSpeed, ref Vector2 face)
    {
        face = intro ? Vector2.down : toPlayer;
        if (!intro && !busy)
        {
            for (int i = 0; i < cd.Length; i++) if (cd[i] > 0f) cd[i] -= Time.deltaTime;
            ready.Clear();
            if (cd[(int)Pattern.Reflect] <= 0f) ready.Add((int)Pattern.Reflect);
            if (cd[(int)Pattern.MirrorBeam] <= 0f && MirrorAlive) ready.Add((int)Pattern.MirrorBeam);   // [임시] 거울이 부서지면 사용 안 함
            // 검 사용 패턴 (3 · 4 · 5): 검 체력이 모두 소모되면 사용 불가 (사용자)
            if (cd[(int)Pattern.SwordCut] <= 0f && BladeAlive) ready.Add((int)Pattern.SwordCut);
            if (cd[(int)Pattern.Thrust] <= 0f && BladeAlive) ready.Add((int)Pattern.Thrust);
            if (cd[(int)Pattern.March] <= 0f && BladeAlive) ready.Add((int)Pattern.March);
            if (ready.Count > 0)
            {
                var p = (Pattern)ready[Random.Range(0, ready.Count)];
                IEnumerator body = p switch
                {
                    Pattern.Reflect => ReflectRoutine(),
                    Pattern.MirrorBeam => MirrorBeamRoutine(),
                    Pattern.SwordCut => SwordCutRoutine(),
                    Pattern.Thrust => ThrustRoutine(),
                    _ => MarchRoutine(),
                };
                StartCoroutine(Run(body, () => cd[(int)p] = Cooldowns[(int)p]));
            }
        }
        return Vector2.zero;   // 고정형 보스 (2026-11-13 사용자): 처음 등장해 멈춘 자리에서 움직이지 않음 (바라보는 방향만 플레이어 쪽)
    }

    bool MirrorAlive => mirror != null && mirror.Alive;
    bool BladeAlive => blade != null && blade.Alive;
    Vector2 Pos => transform.position;
    Vector2 ToPlayer { get { var d = (P != null ? P.HurtCenter : Pos + Vector2.down) - Pos; return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down; } }

    bool PlayerInHalf(Vector2 at, Vector2 dir, float r)
    {
        var p = P;
        if (p == null) return false;
        Vector2 d = p.HurtCenter - at;
        float rr = r + p.radius;
        return d.sqrMagnitude < rr * rr && Vector2.Dot(d, dir) >= -p.radius;
    }

    IEnumerator Run(IEnumerator body, System.Action onEnd)
    {
        busy = true;
        yield return body;
        busy = false;
        onEnd?.Invoke();
    }

    static IEnumerator Wait(float t)
    {
        while (t > 0f)
        {
            if (Playing) t -= Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>패턴 대기: 게임 진행 중 + 기절이 아닐 때만 흐름</summary>
    IEnumerator WaitActive(float t)
    {
        while (t > 0f)
        {
            if (Playing && !e.IsStunned) t -= Time.deltaTime;
            yield return null;
        }
    }

    BossWarning Track(BossWarning w) { warnings.RemoveAll(x => x == null); warnings.Add(w); return w; }

    // ---------- 공격 패턴 1: 반사 레이저 ----------
    // 카구츠치가 폭 1 레이저 준비 → 맵 벽에 닿을 때마다 반사되는 경로 (총 5번 반사) 를 경고 2초 → 경로 전체에 레이저 0.5초. 쿨타임 3초
    // [임시] 첫 방향 = 경고가 뜰 때의 플레이어 쪽
    IEnumerator ReflectRoutine()
    {
        Vector2 from = transform.position;
        Vector2 aim = P != null ? P.HurtCenter - from : Vector2.down;
        yield return ReflectLaser(from, aim, ReflectBounces, ReflectWidth, ReflectWarn, ReflectLaserTime, ReflectDamage);
    }

    // ---------- 공격 패턴 2: 거울 반사 부채꼴 레이저 ----------
    // 야타의 거울을 보스 정면 (플레이어 쪽) 거리 2 로 이동 → 보스가 폭 1 레이저를 1초간 거울에 발사
    // → 0.5초 뒤 거울에서 부채꼴 무작위 방향으로 폭 2 경고 10개 → (보스 레이저가 끝나는 순간) 0.5초 레이저가 맵을 꿰뚫음
    // → 거울 복귀. 쿨타임 10초
    IEnumerator MirrorBeamRoutine()
    {
        if (!MirrorAlive) yield break;
        Vector2 dir = ToPlayer;
        mirror.HoldFront(dir, MirrorFrontGap);
        for (float t = 0f; t < MirrorMoveMax && MirrorAlive && !mirror.Arrived;)
        {
            if (Playing && !e.IsStunned) t += Time.deltaTime;
            yield return null;
        }
        if (!MirrorAlive) yield break;   // 옮기는 중에 부서짐

        Vector2 mp = mirror.transform.position;
        Vector2 toMirror = mp - Pos;
        float beamLen = toMirror.magnitude;
        RaidLaser.Line(Pos, toMirror.normalized, beamLen, MirrorBeamWidth, MirrorBeamTime, MirrorBeamDamage, 0.5f, null, FireCore, FireEdge);
        Fx.Circle(mp, 1f, FireFx, MirrorBeamTime, mirror.transform);
        yield return WaitActive(MirrorFanDelay);

        // 거울에서 부채꼴 (플레이어 쪽) 무작위 방향 10개 경고 → 보스 레이저가 끝날 때 발사
        float baseAng = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float fanWarn = MirrorBeamTime - MirrorFanDelay;
        Vector2 origin = Stage.ClampInside(mp, 0f);
        var rays = new List<(Vector2 d, float len)>();
        for (int i = 0; i < MirrorFanCount; i++)
        {
            float a = (baseAng + Random.Range(-MirrorFanAngle * 0.5f, MirrorFanAngle * 0.5f)) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float len = Stage.ExitDistance(origin, d);
            rays.Add((d, len));
            Track(BossWarning.Box(origin, d, len, MirrorFanWidth, fanWarn));
        }
        yield return WaitActive(fanWarn);
        foreach (var r in rays)
            RaidLaser.Line(origin, r.d, r.len, MirrorFanWidth, MirrorFanLaserTime, MirrorFanDamage, MirrorFanLaserTime + 1f, null, FireCore, FireEdge);
        CameraFollow.Shake(0.2f, 0.15f);
        yield return Wait(MirrorFanLaserTime);
        if (mirror != null) mirror.Release();   // 거울 복귀
    }

    // ---------- 검 공통 (아메노무라쿠모) ----------
    // 검은 기본적으로 카구츠치 옆 (KagutsuchiBlade). 검 패턴은 검을 붙잡아 (Control) 자세를 직접 정하고, 끝나면 놓아서 (Release) 옆으로 복귀.
    // 검이 패턴 도중 부서지면 남은 검 동작은 건너뜀 (경고 · 레이저는 그대로)

    /// <summary>검을 pos · 각도로 seconds 동안 부드럽게 옮김 (붙잡은 상태)</summary>
    IEnumerator BladeTo(Vector2 pos, float angle, float seconds)
    {
        if (!BladeAlive) yield break;
        Vector2 p0 = blade.Pos; float a0 = blade.Angle;
        for (float t = 0f; t < seconds && BladeAlive;)
        {
            if (Playing && !e.IsStunned) t = Mathf.Min(seconds, t + Time.deltaTime);
            float k = seconds > 0f ? t / seconds : 1f;
            blade.SetPose(Vector2.Lerp(p0, pos, k), Mathf.LerpAngle(a0, angle, k));
            yield return null;
        }
        if (BladeAlive) blade.SetPose(pos, angle);
    }

    /// <summary>손잡이 끝 (검 아래쪽) pivot 에서 angle 방향으로 뻗은 검의 가운데</summary>
    static Vector2 CenterFromHilt(Vector2 pivot, float angle)
    {
        float r = angle * Mathf.Deg2Rad;
        return pivot + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * (SwordLength * 0.5f);
    }

    /// <summary>
    /// 회전베기: 검 아래쪽 (손잡이 끝) 을 축으로 돌면서 축이 from → to 로 seconds 동안 이동 (2026-11-13 사용자: 검 아래부분 기준 회전).
    /// 범위 = 축 중심 반경 SwordSpinRadius (= 검 길이) 원, 닿으면 1번 피해
    /// </summary>
    IEnumerator BladeSpinMove(Vector2 from, Vector2 to, float seconds, float damage)
    {
        bool hit = false;
        float a = BladeAlive ? blade.Angle : 0f;
        var ring = Fx.Circle(from, SwordSpinRadius, SwordEdge, seconds + 0.1f);
        for (float t = 0f; t < seconds;)
        {
            if (Playing && !e.IsStunned)
            {
                t = Mathf.Min(seconds, t + Time.deltaTime);
                a -= SpinSpeed * Time.deltaTime;
                Vector2 pivot = Vector2.Lerp(from, to, t / seconds);
                if (ring != null) ring.transform.position = pivot;
                if (BladeAlive) blade.SetPose(CenterFromHilt(pivot, a), a);
                var p = P;
                if (!hit && p != null && (p.HurtCenter - pivot).sqrMagnitude < Sq(SwordSpinRadius + p.radius)) { hit = true; p.HitByBoss(damage, 0f, Vector2.zero, 0f); }
            }
            yield return null;
        }
        CameraFollow.Shake(0.1f, 0.1f);
    }
    const float SpinSpeed = 1080f;   // [임시] 회전 속도 (도/초)
    static float Sq(float v) => v * v;

    /// <summary>회전 공격 경고: 시작 원 + 지나갈 길 + 끝 원 (캡슐). 제자리 회전이면 원 하나</summary>
    void SpinWarning(Vector2 from, Vector2 to, float seconds)
    {
        Track(BossWarning.Circle(from, SwordSpinRadius, seconds));
        Vector2 d = to - from;
        if (d.sqrMagnitude > 0.01f)
        {
            Track(BossWarning.Box(from, d.normalized, d.magnitude, SwordSpinRadius * 2f, seconds));
            Track(BossWarning.Circle(to, SwordSpinRadius, seconds));
        }
    }

    // ---------- 공격 패턴 3: 검 횡베기 → 맵 가르는 베기 10회 ----------
    // 검을 반원 베기의 시작점 (반원 한쪽 끝, 2026-11-13 사용자) 에 준비 + 반원 (반경 10) 경고 → 2초 뒤 횡베기
    // → 0.5초 뒤 맵의 무작위 방향 · 각도로 맵을 가로지르는 경고 10개 한번에 → 폭 2 (radius 1 검) 베기 10개 한번에 → 검 복귀
    // [해석] 검은 반원 바깥쪽에 — 검 끝이 반경 10 경계에 닿게 (검 가운데 = 보스에서 10 - 길이/2)
    IEnumerator SwordCutRoutine()
    {
        Vector2 dir = ToPlayer;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        float sweepR = SwordSweepRadius - SwordLength * 0.5f;
        if (BladeAlive) blade.Control();
        float startA = (ang + 90f) * Mathf.Deg2Rad;
        StartCoroutine(BladeTo(Pos + new Vector2(Mathf.Cos(startA), Mathf.Sin(startA)) * sweepR, ang + 90f, 0.3f));
        Track(BossWarning.HalfDisc(Pos, dir, SwordSweepRadius, SwordSweepWarn));
        yield return WaitActive(SwordSweepWarn);

        // 횡베기: 검을 보스 중심 축으로 반원 휘두름
        Fx.Arc(Pos, dir, SwordSweepRadius, SwordEdge, 0.25f);
        CameraFollow.Shake(0.15f, 0.15f);
        if (PlayerInHalf(Pos, dir, SwordSweepRadius)) P.HitByBoss(SwordSweepDamage, 0f, Vector2.zero, 0f);
        const float swing = 0.18f;
        for (float t = 0f; t < swing && BladeAlive;)
        {
            if (Playing) t += Time.deltaTime;
            float a = ang + 90f - 180f * Mathf.Clamp01(t / swing);
            float r = a * Mathf.Deg2Rad;
            blade.SetPose(Pos + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * sweepR, a);
            yield return null;
        }
        yield return WaitActive(SwordCutDelay);

        // 맵을 가로지르는 베기 10개: 한번에 경고 → 경고 뒤 한번에 베기 (2026-11-13 사용자)
        var cuts = new List<(Vector2 from, Vector2 d, float len)>();
        for (int i = 0; i < SwordCutCount; i++)
        {
            Vector2 through = Center + new Vector2(Random.Range(-Half.x, Half.x), Random.Range(-Half.y, Half.y));
            float a = Random.Range(0f, 180f) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float back = Stage.ExitDistance(through, -d), fwd = Stage.ExitDistance(through, d);
            var c = (through - d * back, d, back + fwd);
            cuts.Add(c);
            Track(BossWarning.Box(c.Item1, d, c.Item3, SwordCutWidth, SwordCutWarn));
        }
        yield return WaitActive(SwordCutWarn);
        foreach (var c in cuts)
            RaidLaser.Line(c.from, c.d, c.len, SwordCutWidth, SwordCutFlash, SwordCutDamage, SwordCutFlash + 1f, null, SwordCore, SwordEdge);
        CameraFollow.Shake(0.2f, 0.18f);
        yield return Wait(SwordCutFlash);
        if (BladeAlive) blade.Release();   // 검 복귀
    }

    // ---------- 공격 패턴 4: 찌르기 → 그 자리에서 회전 ----------
    // 검을 보스 앞에 준비 → 1초 경고 (폭 1.5) → 10 앞으로 찌르기 → 그 자리에서 검 길이에 맞는 원으로 1.5초 경고 → 검이 회전하며 공격 → 검 복귀
    IEnumerator ThrustRoutine()
    {
        if (!BladeAlive) yield break;
        Vector2 dir = ToPlayer;
        float ang = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        blade.Control();
        Vector2 start = Pos + dir * (SwordLength * 0.5f);
        // 찌르기 끝: 검 가운데가 10 앞으로 (맵 밖으로는 안 나감)
        float reach = Mathf.Min(ThrustDistance, Stage.ExitDistance(Stage.ClampInside(start, 0f), dir));
        Vector2 end = start + dir * reach;
        StartCoroutine(BladeTo(start, ang, 0.3f));
        // 경고: 검이 지나가는 직사각형 (검 뒤끝 ~ 찌른 뒤 검 끝)
        Vector2 back = start - dir * (SwordLength * 0.5f);
        Track(BossWarning.Box(back, dir, reach + SwordLength, ThrustWidth, ThrustWarn));
        yield return WaitActive(ThrustWarn);

        // 찌르기
        bool hit = false;
        for (float t = 0f; t < ThrustTime && BladeAlive;)
        {
            if (Playing && !e.IsStunned)
            {
                t = Mathf.Min(ThrustTime, t + Time.deltaTime);
                Vector2 c = Vector2.Lerp(start, end, t / ThrustTime);
                blade.SetPose(c, ang);
                var p = P;
                if (!hit && p != null && GaniacAI.InBox(p.HurtCenter, back, dir, (c - back).magnitude + SwordLength * 0.5f, ThrustWidth, p.radius))
                { hit = true; p.HitByBoss(ThrustDamage, 0f, Vector2.zero, 0f); }
            }
            yield return null;
        }
        CameraFollow.Shake(0.1f, 0.1f);

        // 그 자리에서 회전: 축 = 찌른 검의 아래쪽 (손잡이 끝), 반경 = 검 길이
        Vector2 hilt = end - dir * (SwordLength * 0.5f);
        SpinWarning(hilt, hilt, ThrustSpinWarn);
        yield return WaitActive(ThrustSpinWarn);
        yield return BladeSpinMove(hilt, hilt, SpinTime, SpinDamage);
        if (BladeAlive) blade.Release();   // 검 복귀
    }

    // ---------- 공격 패턴 5: 아래에서 회전하며 플레이어 쪽 5 이동 × 2 ----------
    // 검이 카구츠치 아래로 → 1초 경고 → 회전하며 플레이어 위치로 5 이동 → 1.5초 경고 → 다시 회전하며 5 이동 → 검 복귀
    IEnumerator MarchRoutine()
    {
        if (!BladeAlive) yield break;
        blade.Control();
        // 검 아래쪽 (손잡이 끝) = 회전축. 보스 아래에 축을 두고 검은 아래로 늘어뜨림
        Vector2 below = Stage.ClampInside(Pos + Vector2.down * (e.radius + 0.5f), 0.5f);
        yield return BladeTo(CenterFromHilt(below, -90f), -90f, 0.3f);

        Vector2 cur = below;
        float[] warns = { MarchFirstWarn, MarchSecondWarn };
        foreach (float w in warns)
        {
            if (!BladeAlive) break;
            Vector2 to = (P != null ? P.HurtCenter : cur + Vector2.down) - cur;
            Vector2 next = Stage.ClampInside(cur + (to.sqrMagnitude > 0.0001f ? to.normalized : Vector2.down) * Mathf.Min(MarchDistance, to.magnitude), 0.5f);
            SpinWarning(cur, next, w);
            yield return WaitActive(w);
            yield return BladeSpinMove(cur, next, MarchTime, MarchDamage);
            cur = next;
        }
        if (BladeAlive) blade.Release();   // 검 복귀
    }

    /// <summary>
    /// 반사 레이저 (공용 — 앞으로 여러 패턴에서 사용): from 에서 dir 로, 맵 벽에 닿을 때마다 반사 (bounces 번) →
    /// 경로 전체에 경고 warn 초 → 레이저 laserTime 초 (구간마다 1번 피해)
    /// </summary>
    IEnumerator ReflectLaser(Vector2 from, Vector2 dir, int bounces, float width, float warn, float laserTime, float damage)
    {
        var pts = Stage.ReflectPath(from, dir, bounces);
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 seg = pts[i + 1] - pts[i];
            if (seg.sqrMagnitude < 1e-4f) continue;
            Track(BossWarning.Box(pts[i], seg.normalized, seg.magnitude, width, warn));
        }
        Fx.Circle(from, 1f, FireFx, warn, transform);   // 준비 표시
        yield return WaitActive(warn);
        for (int i = 0; i + 1 < pts.Count; i++)
        {
            Vector2 seg = pts[i + 1] - pts[i];
            if (seg.sqrMagnitude < 1e-4f) continue;
            RaidLaser.Line(pts[i], seg.normalized, seg.magnitude, width, laserTime, damage, laserTime + 1f, null, FireCore, FireEdge);
        }
        CameraFollow.Shake(0.15f, 0.12f);
        yield return Wait(laserTime);
    }

    IEnumerator Intro()
    {
        // 1) 맵 밖 중앙 위에서 천천히 걸어 들어옴 — 카메라가 보스를 확대해서 따라감
        CameraFollow.Focus(transform, EnterZoom);
        Vector2 stop = Center + new Vector2(0f, Half.y - EnterStopInside);
        while (((Vector2)transform.position - stop).sqrMagnitude > 0.0001f)
        {
            if (Playing) transform.position = Vector2.MoveTowards(transform.position, stop, EnterSpeed * Time.deltaTime);
            yield return null;
        }
        entering = false;
        CameraFollow.Shake(0.2f, 0.15f);
        Fx.GroundCircle(stop, 2.5f, FireFx, 0.6f);

        // 2) 멈추면 카메라가 맵 전체를 비춤
        CameraFollow.ClearFocus();
        CameraFollow.ZoomToFullMap();
        yield return Wait(StopPause);

        // 3) 보스에서 부채꼴 무작위 방향 레이저: 0.05초마다 경고 (1초) → 레이저 0.2초, 총 20회
        //    간격이 프레임보다 짧을 수 있어 시간표대로 (밀린 경고는 같은 프레임에 몰아서)
        int spawned = 0;
        float elapsed = 0f;
        while (spawned < IntroLaserCount)
        {
            if (Playing)
            {
                elapsed += Time.deltaTime;
                while (spawned < IntroLaserCount && spawned * IntroLaserInterval <= elapsed)
                {
                    StartCoroutine(IntroLaser());
                    spawned++;
                }
            }
            yield return null;
        }
        yield return Wait(IntroLaserWarn + IntroLaserTime);   // 마지막 레이저가 끝날 때까지

        // 4) 난사 뒤: 1초 뒤 보스가 중앙 조금 아래로 레이저 (폭 0.5) → 0.5초 뒤부터 반경 14 까지 커지는 경고 → 5초 뒤 피해
        yield return IntroNova();

        // 5) 야타의 거울 등장
        mirror = KagutsuchiMirror.Spawn(e, MirrorGap, MirrorTurnSpeed);   // 체력 = 카구츠치 최대 체력 × 15
        blade = KagutsuchiBlade.Spawn(e, SwordHpMul);                     // 검: 보스 옆 기본 위치, 독립 체력 [임시] 보스 × 1
        yield return Wait(AfterMirror);

        // 6) 입장 끝 → 이후부터 보스 행동. 카메라는 보스전 내내 맵 전체 (2026-11-13 사용자) — 시야를 되돌리지 않음
        e.SetScripted(false);
        intro = false;
    }

    /// <summary>
    /// 난사 뒤 커지는 원 (2026-11-13 사용자): 난사 끝 1초 뒤 보스가 경고 중심 (중앙 조금 아래) 으로 폭 0.5 레이저 →
    /// 0.5초 뒤 경고가 나타나 점점 커짐 (반경 14 까지) → 커지기 시작하고 5초 뒤 원 안 피해.
    /// [임시] 레이저는 피해가 날 때까지 유지 (원을 충전하는 연출), 레이저에 닿아도 피해
    /// </summary>
    IEnumerator IntroNova()
    {
        yield return Wait(NovaBeamDelay);
        Vector2 at = Center + new Vector2(0f, -NovaCenterDown);
        Vector2 from = transform.position;
        Vector2 d = at - from;
        float len = d.magnitude;
        d = len > 0.001f ? d / len : Vector2.down;
        RaidLaser.Line(from, d, len, NovaBeamWidth, NovaWarnDelay + NovaGrowTime, NovaBeamDamage, 0.5f, null, FireCore, FireEdge);
        Fx.Circle(at, 1f, FireFx, 0.4f);
        yield return Wait(NovaWarnDelay);

        // 점점 커지는 경고 (반경 0.5 → 14, 5초)
        var w = BossWarning.Circle(at, NovaStartRadius, 0f);
        warnings.RemoveAll(x => x == null);
        warnings.Add(w);
        for (float t = 0f; t < NovaGrowTime;)
        {
            if (Playing)
            {
                t = Mathf.Min(NovaGrowTime, t + Time.deltaTime);
                if (w != null) w.Set(at, Mathf.Lerp(NovaStartRadius, NovaRadius, t / NovaGrowTime));
            }
            yield return null;
        }
        if (w != null) Destroy(w.gameObject);

        // 피해
        Fx.Disc(at, NovaRadius, new Color(1f, 0.45f, 0.1f, 0.5f), 0.4f);
        Fx.Circle(at, NovaRadius, FireEdge, 0.4f);
        CameraFollow.Shake(0.35f, 0.3f);
        var p = P;
        if (p != null && (p.HurtCenter - at).sqrMagnitude < (NovaRadius + p.radius) * (NovaRadius + p.radius)) p.TakeDamage(NovaDamage);
    }

    /// <summary>
    /// 카구츠치에서 부채꼴 안 무작위 방향으로 맵 끝까지 (폭 2) 경고 1초 → 레이저 0.2초 (2026-11-13 사용자: 보스에서 부채꼴로 발사).
    /// 부채꼴 중심 = 보스에서 맵 중앙 쪽 (입장 위치에서는 아래)
    /// </summary>
    IEnumerator IntroLaser()
    {
        Vector2 from = transform.position;
        Vector2 toCenter = Center - from;
        float baseAng = toCenter.sqrMagnitude > 0.01f ? Mathf.Atan2(toCenter.y, toCenter.x) * Mathf.Rad2Deg : -90f;
        float a = (baseAng + Random.Range(-IntroLaserFan * 0.5f, IntroLaserFan * 0.5f)) * Mathf.Deg2Rad;
        Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        float len = Stage.ExitDistance(Stage.ClampInside(from, 0f), d);
        var w = BossWarning.Box(from, d, len, IntroLaserWidth, IntroLaserWarn);
        warnings.RemoveAll(x => x == null);
        warnings.Add(w);
        yield return Wait(IntroLaserWarn);
        RaidLaser.Line(from, d, len, IntroLaserWidth, IntroLaserTime, IntroLaserDamage, IntroLaserTime + 1f, null, FireCore, FireEdge);
        CameraFollow.Shake(0.06f, 0.08f);
    }
}


/// <summary>
/// 야타의 거울 (2026-11-13 사용자): 보스 몸 바깥 (gap) 에서 보스 주변을 돌며 항상 플레이어 쪽으로 향함 (도는 속도 제한).
/// 적(Enemy, EnemyKind.KagutsuchiMirror) 이라 플레이어 스킬로 때릴 수 있음. 체력 = 카구츠치 최대 체력 × 15 (사용자).
/// 움직이지 않음 (궤도만) · 밀리지 않음 · 접촉 피해 없음 · 경험치 없음. 체력이 0 이 되면 사라짐 (그 뒤 효과는 미설계).
/// 방어 · 흡수 판정은 아직 없음 (설정: 흡수한 공격을 반사 · 재발사하지 않음).
/// 그림: Resources/Sprites/Enemy_KagutsuchiMirror (없으면 밝은 원 + 금색 테두리)
/// </summary>
public class KagutsuchiMirror : MonoBehaviour
{
    public const float HpMul = 15f;      // 카구츠치 최대 체력 × 15 (사용자)

    Enemy boss;
    float gap, turnSpeed, angle, appear;
    float curRadius = -1f;

    // 공격 패턴 2: 보스 정면에 붙잡아 둠 (끝나면 Release → 다시 궤도)
    bool hold;
    float holdAngle, holdGap;
    const float HoldTurnSpeed = 720f, MoveSpeed = 15f;   // [임시] 정면으로 옮겨 가는 회전 · 거리 속도

    /// <summary>거울 본체 (적). 부서지면 null</summary>
    public Enemy Body { get; private set; }
    public bool Alive => Body != null && Body.hp > 0;

    /// <summary>보스 기준 dir 쪽, 보스 몸 바깥 gap 거리로 옮겨 붙잡아 둠 (공격 패턴 2)</summary>
    public void HoldFront(Vector2 dir, float gap)
    {
        hold = true;
        holdAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        holdGap = gap;
    }

    /// <summary>다시 플레이어 쪽 궤도로</summary>
    public void Release() => hold = false;

    /// <summary>정면 자리에 도착했는지</summary>
    public bool Arrived => hold && Mathf.Abs(Mathf.DeltaAngle(angle, holdAngle)) < 1f && Mathf.Abs(curRadius - RadiusFor(holdGap)) < 0.05f;

    public static KagutsuchiMirror Spawn(Enemy boss, float gap, float turnSpeed)
    {
        var body = Enemy.Spawn(EnemyKind.KagutsuchiMirror, boss.transform.position, 1f);
        body.maxHp = body.hp = Mathf.Max(1f, boss.maxHp * HpMul);
        var m = body.gameObject.AddComponent<KagutsuchiMirror>();
        m.Body = body;
        m.boss = boss; m.gap = gap; m.turnSpeed = turnSpeed;
        var p = Player.I;
        Vector2 to = p != null ? p.HurtCenter - (Vector2)boss.transform.position : Vector2.down;
        m.angle = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
        body.alpha = 0f;
        if (SpriteFactory.TryGet("Enemy_KagutsuchiMirror") == null)
        {
            // 도형 대체: 금색 테두리 링
            var ring = new GameObject("Rim");
            ring.transform.SetParent(body.transform, false);
            ring.transform.localScale = Vector3.one * 1.15f;
            SpriteFactory.AddRenderer(ring, "Fx_Ring", SpriteFactory.Shape.Ring, new Color(1f, 0.85f, 0.4f, 0.9f), 8);
        }
        m.Place();
        Fx.Circle(body.transform.position, 1.2f, new Color(1f, 0.95f, 0.7f, 0.8f), 0.4f);
        return m;
    }

    /// <summary>보스 중심에서 거울 중심까지 = 보스 몸 반경 + 간격 + 거울 몸 반경</summary>
    float RadiusFor(float g) => (boss != null ? boss.radius : 1f) + g + (Body != null ? Body.radius : 0.5f);

    void Place()
    {
        if (boss == null) return;
        if (curRadius < 0f) curRadius = RadiusFor(gap);
        float r = angle * Mathf.Deg2Rad;
        transform.position = (Vector2)boss.transform.position + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * curRadius;
    }

    // Enemy.Update (이동) 다음에 궤도 위치로 덮어씀
    void LateUpdate()
    {
        if (boss == null || boss.hp <= 0) { Destroy(gameObject); return; }
        if (GameManager.I.State == GameState.Playing)
        {
            var p = Player.I;
            if (hold)
            {
                angle = Mathf.MoveTowardsAngle(angle, holdAngle, HoldTurnSpeed * Time.deltaTime);
            }
            else if (p != null)
            {
                Vector2 to = p.HurtCenter - (Vector2)boss.transform.position;
                float want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
                angle = Mathf.MoveTowardsAngle(angle, want, turnSpeed * Time.deltaTime);
            }
            if (curRadius < 0f) curRadius = RadiusFor(gap);
            curRadius = Mathf.MoveTowards(curRadius, RadiusFor(hold ? holdGap : gap), MoveSpeed * Time.deltaTime);
            appear = Mathf.Min(1f, appear + Time.deltaTime / 0.3f);
            Body.alpha = appear;
        }
        Place();
        if (Body.Hidden != boss.Hidden) Body.SetHidden(boss.Hidden);
    }
}

/// <summary>
/// 아메노무라쿠모 (카구츠치의 검, 2026-11-13 사용자): 기본적으로 카구츠치 옆에 위치, 독립 체력.
/// 적(Enemy, EnemyKind.KagutsuchiSword) 이라 플레이어 스킬로 때릴 수 있음. 체력이 0 이 되면 사라지고 검 사용 패턴 (3 · 4 · 5) 은 사용 불가.
/// 검 패턴이 Control() 로 붙잡아 SetPose 로 자세를 정하고, Release() 하면 옆 자리로 부드럽게 복귀.
/// 설정: 카구츠치는 이 검을 '매우 강력한 검' 으로만 사용 (권능 없음).
/// [임시] 피격 판정은 검 가운데의 원 하나 (긴 검 전체가 아님). 색 = 흰색 · 은색 테두리 (사용자). 그림 Enemy_KagutsuchiSword
/// </summary>
public class KagutsuchiBlade : MonoBehaviour
{
    const float RestGap = 0.6f;              // [임시] 보스 몸과 검 사이
    const float RestAngle = 90f;             // 옆 자리: 수직으로 세움 (2026-11-13 사용자)
    const float ReturnSpeed = 20f, ReturnTurn = 540f;   // [임시] 복귀 속도 (유닛/초 · 도/초)

    Enemy boss;
    bool controlled;
    Vector2 pos;
    float angle;

    public Enemy Body { get; private set; }
    public bool Alive => Body != null && Body.hp > 0;
    public Vector2 Pos => pos;
    public float Angle => angle;

    public static KagutsuchiBlade Spawn(Enemy boss, float hpMul)
    {
        var body = Enemy.Spawn(EnemyKind.KagutsuchiSword, boss.transform.position, 1f);
        body.maxHp = body.hp = Mathf.Max(1f, boss.maxHp * hpMul);
        var b = body.gameObject.AddComponent<KagutsuchiBlade>();
        b.Body = body;
        b.boss = boss;
        b.pos = b.RestPos; b.angle = RestAngle;
        body.transform.localScale = new Vector3(KagutsuchiAI.SwordLength, KagutsuchiAI.SwordThick, 1f);
        // 은색 테두리 (도형일 때)
        if (SpriteFactory.TryGet("Enemy_KagutsuchiSword") == null)
        {
            var rim = new GameObject("Rim");
            rim.transform.SetParent(body.transform, false);
            rim.transform.localScale = new Vector3(1.03f, 1.4f, 1f);
            SpriteFactory.AddRenderer(rim, "Fx_Square", SpriteFactory.Shape.Square, new Color(0.72f, 0.75f, 0.82f, 0.95f), 6);
        }
        b.Apply();
        Fx.Circle(b.pos, 1.5f, new Color(1f, 1f, 1f, 0.8f), 0.4f);
        return b;
    }

    /// <summary>옆 자리 (검 가운데 위치): 보스 오른쪽, 보스와 같은 높이에 수직으로 세움</summary>
    Vector2 RestPos => (Vector2)boss.transform.position + Vector2.right * (boss.radius + RestGap + KagutsuchiAI.SwordThick * 0.5f);

    public void Control() => controlled = true;
    public void Release() => controlled = false;

    /// <summary>검 가운데 위치 · 각도 (붙잡은 상태에서 패턴이 매 프레임 지정)</summary>
    public void SetPose(Vector2 p, float a) { pos = p; angle = a; Apply(); }

    void Apply()
    {
        transform.position = pos;
        transform.rotation = Quaternion.Euler(0, 0, angle);
    }

    // Enemy.Update (이동 · 맵 경계) 다음에 덮어씀
    void LateUpdate()
    {
        if (boss == null || boss.hp <= 0) { Destroy(gameObject); return; }
        if (!controlled && GameManager.I.State == GameState.Playing)
        {
            pos = Vector2.MoveTowards(pos, RestPos, ReturnSpeed * Time.deltaTime);
            angle = Mathf.MoveTowardsAngle(angle, RestAngle, ReturnTurn * Time.deltaTime);
        }
        Apply();
        if (Body.Hidden != boss.Hidden) Body.SetHidden(boss.Hidden);
    }
}