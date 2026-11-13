using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 가니악 벤데스트라 (레이드 보스, 2026-11-13 사용자 기획 · Docs/Design/Raid_Ganiac_Plan_v0.1.md).
/// 현재 1단계 난이도만. 이동 속도 느림. 체력 50% 아래에서 패턴 강화: 공격 패턴 1 · 2 · 3 의 전방위 베기 뒤 20도 간격 방사 레이저.
///
/// 등장 (첫 스테이지 입장 시):
///   맵 이탈 상태로 시작 → 특수 패턴 1 (맵을 가로지르는 무작위 직선 경고 10개) → 특수 패턴 2 (중앙 레이저 · 등장 · 좌우 레이저).
/// 등장 후: 느리게 추격하며 조건이 맞는 패턴 사용
///   이동 패턴 1 (플레이어가 반경 6 밖) · 공격 패턴 1 (반경 6 안) · 공격 패턴 2 (반경 7 안) · 공격 패턴 3 (반경 6 안) · 공격 패턴 4 (반경 8 안).
///   중간보스와 같은 규칙: 패턴은 한 번에 하나 · 쿨타임은 패턴이 끝난 뒤부터 · 조건 맞는 준비된 패턴 중 무작위 ·
///   기절하면 진행 중인 패턴이 멈췄다가 이어서.
/// 특수 패턴 3 (체력 90 · 60 · 30%): 맵 이탈 · 카메라 맵 전체 · 가니악의 검 3개 · 1분 안에 부수지 못하면 검마다 반경 12 전방위 베기.
/// 피해 · 기획에 없는 값은 [임시].
/// </summary>
public class GaniacAI : MonoBehaviour, IRaidBossAI
{
    // ---------- IRaidBossAI ----------
    /// <summary>첫 등장 전 (맵 이탈 상태) 에는 체력 바 없음. 등장 후 이동 패턴으로 잠깐 이탈할 때는 그대로 표시</summary>
    public bool HideBossBar => intro && e != null && e.Hidden;
    void IRaidBossAI.DrawHud(float W) => DrawSwordHud(W);

    // ---------- 특수 패턴 1: 맵을 가로지르는 무작위 직선 ----------
    const int LineCount = 10;            // "10개 가량" (사용자)
    const float LineInterval = 0.2f;     // 경고 생성 간격 (사용자)
    const float LineWidth = 3f;          // 폭 (사용자: 임시 3)
    const float LineDelay = 3f;          // 생성 3초 뒤 사라지며 피해 (사용자)
    const float LineDamage = 20f;        // [임시]

    // ---------- 특수 패턴 2: 중앙 수직 레이저 → 등장 → 좌우 순차 레이저 ----------
    const float CenterWidth = 9f;        // 중앙 경고 · 레이저 폭 (사용자: 6 → 9)
    const float CenterWarn = 3f;         // 경고 3초 뒤 레이저 (사용자: 5 → 3)
    const float CenterLaserTime = 1.5f;  // 레이저 지속 (사용자: 3 → 1.5초)
    const float RevealBeforeEnd = 0.5f;  // 레이저가 사라지기 0.5초 전 보스가 맵 중앙에 나타남 (사용자)
    const float SweepDelay = 1f;         // 레이저 종료 1초 뒤 좌우 레이저 시작 (사용자: 2 → 1)
    const float SweepWidth = 2f;         // 좌우 레이저 한 줄 폭 (사용자: 1 → 2)
    const float SweepInterval = 0.05f;   // 바깥쪽으로 한 줄씩 생기는 간격 (사용자: 0.2 → 0.05초)
    const float SweepLaserTime = 0.25f;  // 좌우 레이저 한 줄 지속 (사용자: 0.5 → 0.25초)
    const float LaserDamage = 25f;       // [임시] 레이저 안에 있으면 틱마다
    const float LaserTick = 0.5f;        // [임시] 레이저 피해 간격

    // ---------- 이동 패턴 1: 옆으로 빠지며 맵 이탈 → 플레이어 옆에서 다가오며 등장 ----------
    const float BlinkTrigger = 6f;       // 플레이어가 반경 6 안에 없을 때 (사용자)
    const float BlinkCooldown = 30f;     // (사용자)
    const float BlinkExitDistance = 3f, BlinkExitTime = 0.5f;   // 좌 또는 우로 3을 0.5초에 이동하며 이탈 (사용자)
    const float BlinkGone = 0.5f;        // 0.5초 뒤 등장 (사용자)
    const float BlinkAppearOffset = 6f, BlinkEnterDistance = 3f;   // 플레이어 좌 또는 우 6에서 3 이동하며 등장 (사용자)
    const float BlinkEnterTime = 0.5f;   // [임시] 등장 이동 시간 (이탈과 같게)

    // ---------- 공격 패턴 1: 횡베기 × 2 → 전방위 베기 ----------
    const float SlashTrigger = 6f;       // 플레이어가 반경 6 안 (사용자)
    const float SlashCooldown = 10f;     // (사용자)
    const float SweepWarn = 0.5f, SweepRadius = 5f, SweepAdvance = 2f;   // 0.5초 경고 → 반경 5 횡베기 + 2 이동 (사용자: 반경 4 → 7 → 6 → 5)
    const int SweepRepeats = 2;          // 2번 반복 (사용자)
    const float SweepDamage = 20f;       // [임시]

    // ---------- 전방위 베기 (공격 패턴 1 · 2 마무리) ----------
    const float SpinWarn = 1.25f, SpinRadius = 4f, SpinAdvance = 7f;   // 1.25초 경고 → 7 돌진하며 반경 4 전방위 베기 (사용자: 경고 0.75 → 1.75 → 1.25 · 반경 5 → 4 · 이동 2 → 5 → 10 → 7)
    const float SpinDamage = 25f;        // [임시]
    const float AdvanceTime = 0.2f;      // [임시] 횡베기하며 2 이동하는 데 걸리는 시간
    const float SpinAdvanceTime = 0.35f; // [임시] 전방위 베기 돌진 7 이동에 걸리는 시간 (속도 20, 공격 패턴 2 돌진과 같음)

    // ---------- 체력 50% 아래 강화: 전방위 베기 뒤 방사 레이저 (공격 패턴 1 · 2 · 3) ----------
    const float EnrageLaserDelay = 0.2f; // 전방위 베기 이후 0.2초 후 경고 (사용자)
    const float EnrageLaserWarn = 0.5f;  // 경고 0.5초 후 레이저 (사용자)
    const float EnrageLaserStep = 20f;   // 보스 위치 기준 20도 간격 = 18방향 (사용자)
    const float EnrageLaserInterval = 0.02f;   // 각 레이저는 0.02초 간격으로 시계 또는 반시계 방향으로 생김 (사용자: 0.05 → 0.02)
    const float EnrageLaserWidth = 1f;   // 폭 1 (사용자)
    const float EnrageLaserTime = 0.3f;  // [임시] 레이저 지속
    const float EnrageLaserDamage = 20f; // [임시]

    // ---------- 공격 패턴 2: 돌진 → 연속 베기 → 전방위 베기 ----------
    const float RushTrigger = 7f;        // 플레이어가 반경 7 안 (사용자)
    const float RushCooldown = 15f;      // (사용자)
    const float RushWarn = 0.5f, RushDistance = 6f, RushStun = 0.5f;   // 0.5초 경고 → 6 빠르게 돌진, 적중 시 0.5초 기절 (사용자)
    const float RushSpeed = 20f;         // [임시] "빠르게" → 0.3초
    const float RushDamage = 20f;        // [임시]
    const float FlurryWarn = 1f, FlurryRadius = 4f;   // 1초 경고 → 반경 4 반원 연속 베기 (사용자: 5 → 6 → 5 → 4)
    const int FlurryHits = 10;           // 10회 (사용자)
    const float FlurryActive = 0.1f, FlurryGap = 0.05f;   // 0.1초 판정 · 0.05초 딜레이 (사용자)
    const float FlurryDamage = 4f;       // [임시] 1회당. 피격 무적(0.35초)을 무시하고 판정마다 1번씩 맞음

    // ---------- 공격 패턴 3: 횡베기 → 전방위 베기, 1회 반복 ----------
    const float CrossTrigger = 6f;       // 플레이어가 반경 6 안 (사용자)
    const float CrossSweepWarn = 0.5f, CrossSweepRadius = 5f;   // 반경 5 반원 경고 0.5초 → 횡베기 1회 (사용자: 6 → 5). 이동 없음
    const int CrossRepeats = 2;          // "1회 반복" = 총 2번 (사용자)
    const float CrossCooldown = 12f;     // [임시] 기획 미정

    // ---------- 공격 패턴 4: 넓은 횡베기 → 같은 범위 연속 베기 ----------
    const float WideTrigger = 8f;        // 플레이어가 반경 8 안 (사용자)
    const float WideWarn = 1.25f, WideRadius = 9f; // 반경 9 반원 경고 1.25초 → 횡베기 1회 → 같은 반원 연속 베기 (사용자: 경고 1 → 1.25 · 반경 10 → 9)
    const float WideFlurryWarn = 0.75f;  // 횡베기 뒤 연속 베기 선딜 0.75초 (같은 반원 경고 표시) (사용자)
    const float WideCooldown = 15f;      // [임시] 기획 미정

    const float PatternGap = 3f;         // 패턴 간 공통 쿨타임: 패턴이 끝난 뒤 3초 동안 다음 패턴 없음 (사용자: 1.5 → 3)

    // ---------- 특수 패턴 3: 가니악의 검 (체력 90 · 60 · 30%) ----------
    static readonly float[] SwordThresholds = { 0.9f, 0.6f, 0.3f };   // 체력 90 · 60 · 30% 에 사용 (사용자)
    const float SwordPhaseGap = 10f;     // 특수 패턴 3 중 패턴 간 공통 쿨타임 10초 (사용자)
    const float SwordExitTime = 0.5f;    // [임시] 맵 이탈 (흐려지며 사라짐)
    const float SwordSpawnDelay = 0.25f; // 맵 이탈 · 카메라 축소 0.25초 뒤 검 3개 생성 (사용자)
    const int SwordCount = 3;            // 검 3개, 삼각형 (사용자)
    const float SwordTriangleRadius = 8f;   // [임시] 맵 중앙에서 삼각형 꼭짓점까지
    const float SwordCountdown = 60f;    // 1분 카운트다운 (사용자)
    const float SwordReturnTime = 0.5f;  // [임시] 검 생성 뒤 보스가 떠난 자리로 돌아옴 (나타남)
    const float SwordStrikeWarn = 0.75f, SwordStrikeRadius = 12f;   // 남은 검으로 순간이동 → 반경 12 경고 0.75초 → 전방위 베기 (사용자)
    const float SwordStrikeDelay = 0.5f; // 순차 이동 0.5초 딜레이 (사용자)
    const float SwordStrikeDamage = 40f; // [임시]

    // ---------- 특수 패턴 4: 구석 레이저 난사 (체력 75 · 50 · 25%) ----------
    static readonly float[] StormThresholds = { 0.75f, 0.5f, 0.25f };   // 체력 75 · 50 · 25% 에 사용 (사용자)
    const float StormInset = 6f;         // 보스 위치 = 완전 가장자리(모서리)에서 가로 · 세로 6 (사용자)
    const float StormWarn = 7f;          // 보스와 가장자리 사이를 뺀 나머지 전부 경고, 7초 (사용자: 5 → 7)
    const float StormTime = 3f;          // 난사 총 3초 (사용자)
    const float StormInterval = 0.05f;   // 0.05초 간격 (사용자)
    const int StormPerVolley = 5;        // 5개씩 (사용자)
    const float StormLaserTime = 0.1f;   // 레이저 하나 0.1초 지속 (사용자)
    const float StormRepeatDelay = 1f;   // 끝나면 1초 뒤 우측 상단에서 1회 반복 (사용자)
    const float StormLaserWidth = 1.2f;  // [임시]
    const float StormDamage = 10f;       // [임시] 레이저 1개당 (피격 무적 0.35초 적용)
    const float StormAngleMargin = 6f;   // [임시] 안전 지대 쪽으로는 쏘지 않음 (가장자리 각도 여유, 도)

    // ---------- 공격 패턴 5: 어둠의 검 지그재그 돌진 (특수 패턴 3 이후 해제) ----------
    const float SummonCooldown = 20f;    // (사용자)
    const int SummonCount = 4;           // 4개 (사용자)
    const float SummonSpacing = 3f;      // 간격 3 (사용자) — 플레이어 방향으로 3씩 앞으로
    const float SummonZigzag = 1.5f;     // [임시] 좌우로 엇갈리는 폭 (지그재그)
    const float SummonWait = 1f;         // 1초 대기 후 돌진 (사용자)
    const float SummonSpinRadius = 6f;   // 검에 닿으면 반경 6 전방위 베기 (사용자)

    // ---------- 공격 패턴 6 · 7 공통: 횡베기 → 맵 가르는 레이저 6개 (특수 패턴 4 이후 해제) ----------
    const float LaserSweepWarn = 0.5f, LaserSweepRadius = 4f;   // 0.5초 경고 → 반경 4 횡베기 (사용자)
    const int CrossLaserCount = 6;       // 무작위 방향에서 플레이어에게 총 6개 (사용자)
    const float CrossLaserWidth = 1f;    // 폭 (사용자: 2 → 1). 공격 패턴 6 (공격 패턴 7 은 SweepLaserWidth)
    const float CrossLaserWarn = 0.5f;   // 0.5초 후 레이저 (사용자)
    const float CrossLaserTime = 0.3f;   // [임시] 레이저 지속
    const float CrossLaserDamage = 20f;  // [임시]

    // ---------- 공격 패턴 6: 돌진 → 횡베기 → 레이저 ----------
    const float ChargeLaserTrigger = 6f; // 플레이어가 반경 6 안 (사용자)
    const float ChargeLaserCooldown = 15f;   // (사용자)
    const float ChargeLaserWarn = 0.5f, ChargeLaserDistance = 8f;   // 0.5초 경고 → 8 빠르게 돌진, 적중 시 0.5초 기절 (사용자)

    // ---------- 공격 패턴 7: 횡베기 → 레이저, 3회 ----------
    const float SweepLaserCooldown = 12f;    // (사용자)
    const int SweepLaserRepeats = 3;     // 3회 반복 (사용자)
    const float SweepLaserWarn = 0.75f;  // 레이저 경고 → 발사 딜레이 (사용자: 0.5 → 0.75, 공격 패턴 7 만)
    const float SweepLaserWidth = 1f;    // 레이저 폭 (사용자: 2 → 1, 공격 패턴 7 만)

    // [임시] 등장 직후 첫 사용까지 (이동 · 공격 1 · 공격 2 · 공격 3 · 공격 4). 공격 5 · 6 · 7 은 해제되는 순간 바로 사용 가능
    static readonly float[] FirstCooldown = { 3f, 1f, 2f, 3f, 4f, 0f, 0f, 0f };
    static readonly float[] Cooldowns = { BlinkCooldown, SlashCooldown, RushCooldown, CrossCooldown, WideCooldown,
                                          SummonCooldown, ChargeLaserCooldown, SweepLaserCooldown };
    enum Pattern { Blink, Slash, Rush, Cross, Wide, Summon, ChargeLaser, SweepLaser }

    Enemy e;
    bool intro = true;
    bool busy;
    float gapLeft;   // 패턴 간 공통 쿨타임 남은 시간
    Vector2 faceDir;
    readonly float[] cd = new float[8];
    bool summonUnlocked;                 // 공격 패턴 5: 특수 패턴 3 을 한 번 마친 뒤
    bool laserUnlocked;                  // 공격 패턴 6 · 7: 특수 패턴 4 를 한 번 마친 뒤
    readonly List<GameObject> darkSwords = new List<GameObject>();
    readonly List<int> ready = new List<int>();
    readonly List<BossWarning> warnings = new List<BossWarning>();

    // 특수 패턴 3 상태
    int nextStormThreshold;              // 다음에 확인할 StormThresholds 번호 (특수 패턴 4)
    int nextThreshold;                   // 다음에 확인할 SwordThresholds 번호
    bool swordPhase;
    bool stormPhase;                     // 특수 패턴 4 진행 중 (카메라 축소 유지용)
    float swordTimeLeft;
    readonly List<Enemy> swords = new List<Enemy>();
    int AliveSwords { get { int n = 0; foreach (var s in swords) if (s != null && s.hp > 0) n++; return n; } }

    /// <summary>지금 레이드의 가니악 (화면 표시용)</summary>
    public static GaniacAI Current { get; private set; }

    /// <summary>등장 연출 중 (이동 · 일반 패턴 없음)</summary>
    public bool InIntro => intro;
    bool Enraged => e.hp <= e.maxHp * 0.5f;

    static Player P => Player.I;
    static bool Playing => GameManager.I != null && GameManager.I.State == GameState.Playing;
    static Vector2 Half => Stage.HalfSize;
    static Vector2 Center => Stage.Center;

    void Awake()
    {
        e = GetComponent<Enemy>();
        e.SetHidden(true);   // 첫 등장 = 맵 이탈 상태 (맞지 않음 · 조준 안 됨 · 접촉 피해 없음)
        FirstCooldown.CopyTo(cd, 0);
        Current = this;
    }

    void Start() => StartCoroutine(Intro());

    void OnDisable()
    {
        foreach (var w in warnings) if (w != null) Destroy(w.gameObject);
        warnings.Clear();
        foreach (var s in swords) if (s != null) Destroy(s.gameObject);
        swords.Clear();
        foreach (var d in darkSwords) if (d != null) Destroy(d);
        darkSwords.Clear();
        if (swordPhase || stormPhase) CameraFollow.ResetZoom();
        swordPhase = false;
        stormPhase = false;
        if (Current == this) Current = null;
    }

    Vector2 Pos => transform.position;
    Vector2 PlayerPos => P != null ? P.HurtCenter : Pos;
    Vector2 ToPlayer { get { var d = PlayerPos - Pos; return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down; } }

    /// <summary>Enemy.Update 에서 매 프레임 (기절 · 일시정지 중에는 불리지 않음 → 쿨타임도 멈춤)</summary>
    public Vector2 Behave(Vector2 toPlayer, float moveSpeed, ref Vector2 face)
    {
        face = busy && faceDir.sqrMagnitude > 0f ? faceDir : toPlayer;
        if (intro) return Vector2.zero;
        // 특수 패턴 3: 체력 90 · 60 · 30% 아래로 내려가면 진행 중인 패턴이 끝난 뒤 바로 (공통 쿨타임 무시)
        if (!busy && !swordPhase && !DebugCheats.NoGaniacSpecial[2] && nextThreshold < SwordThresholds.Length && e.hp <= e.maxHp * SwordThresholds[nextThreshold])
        {
            StartCoroutine(SwordPhase());
            return Vector2.zero;
        }
        // 특수 패턴 4: 체력 75 · 50 · 25% (특수 패턴 3 카운트다운 중에도 사용 — 카운트다운은 계속 흐름)
        if (!busy && !DebugCheats.NoGaniacSpecial[3] && nextStormThreshold < StormThresholds.Length && e.hp <= e.maxHp * StormThresholds[nextStormThreshold])
        {
            StartCoroutine(StormPhase());
            return Vector2.zero;
        }
        if (!busy)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < cd.Length; i++) if (cd[i] > 0f) cd[i] -= dt;
            if (gapLeft > 0f) gapLeft -= dt;
            else TryStart();
        }
        return busy ? Vector2.zero : toPlayer * moveSpeed;   // 패턴 사이에는 느리게 추격
    }

    /// <summary>쿨타임이 끝났고 거리 조건이 맞는 패턴 중 무작위 1개</summary>
    void TryStart()
    {
        float dist = (PlayerPos - Pos).magnitude;
        ready.Clear();
        if (cd[(int)Pattern.Blink] <= 0f && dist > BlinkTrigger) ready.Add((int)Pattern.Blink);
        if (cd[(int)Pattern.Slash] <= 0f && dist <= SlashTrigger) ready.Add((int)Pattern.Slash);
        if (cd[(int)Pattern.Rush] <= 0f && dist <= RushTrigger) ready.Add((int)Pattern.Rush);
        if (cd[(int)Pattern.Cross] <= 0f && dist <= CrossTrigger) ready.Add((int)Pattern.Cross);
        if (cd[(int)Pattern.Wide] <= 0f && dist <= WideTrigger) ready.Add((int)Pattern.Wide);
        if (summonUnlocked && cd[(int)Pattern.Summon] <= 0f) ready.Add((int)Pattern.Summon);   // 거리 조건 없음
        if (laserUnlocked && cd[(int)Pattern.ChargeLaser] <= 0f && dist <= ChargeLaserTrigger) ready.Add((int)Pattern.ChargeLaser);
        if (laserUnlocked && cd[(int)Pattern.SweepLaser] <= 0f) ready.Add((int)Pattern.SweepLaser);   // 거리 조건 없음
        if (ready.Count == 0) return;
        var p = (Pattern)ready[Random.Range(0, ready.Count)];
        IEnumerator body = p switch
        {
            Pattern.Blink => BlinkRoutine(),
            Pattern.Slash => SlashRoutine(),
            Pattern.Rush => RushRoutine(),
            Pattern.Cross => CrossRoutine(),
            Pattern.Wide => WideRoutine(),
            Pattern.Summon => SummonRoutine(),
            Pattern.ChargeLaser => ChargeLaserRoutine(),
            _ => SweepLaserRoutine(),
        };
        StartCoroutine(Run(p, body));
    }

    IEnumerator Run(Pattern p, IEnumerator body)
    {
        busy = true;
        yield return body;
        busy = false;
        e.alpha = 1f;
        cd[(int)p] = Cooldowns[(int)p];
        gapLeft = swordPhase ? SwordPhaseGap : PatternGap;   // 특수 패턴 3 중에는 10초
    }

    // ---------- 특수 패턴 3 ----------
    // 맵 이탈 → 카메라 맵 전체로 축소 → 0.25초 뒤 삼각형으로 검 3개 → 보스가 돌아와 1분 카운트다운
    // (그동안 일반 패턴, 패턴 간 쿨타임 10초) → 시간 안에 검을 모두 부수지 못하면
    // 남은 검으로 차례로 순간이동 (검은 사라짐) → 반경 12 경고 0.75초 → 전방위 베기, 0.5초 딜레이로 다음 검
    IEnumerator SwordPhase()
    {
        busy = true;
        swordPhase = true;
        while (nextThreshold < SwordThresholds.Length && e.hp <= e.maxHp * SwordThresholds[nextThreshold]) nextThreshold++;   // 한 번에 여러 단계를 넘으면 1번만
        Vector2 leaveAt = Pos;

        // 맵 이탈 (제자리에서 흐려지며 사라짐)
        yield return Move(Vector2.zero, SwordExitTime, k => e.alpha = 1f - k);
        e.SetHidden(true);
        e.alpha = 1f;
        CameraFollow.ZoomToFullMap();
        yield return Wait(SwordSpawnDelay);

        // 검 3개: 맵 중앙 기준 정삼각형 꼭짓점 (방향 무작위)
        swords.Clear();
        float a0 = Random.Range(0f, 360f);
        for (int i = 0; i < SwordCount; i++)
        {
            float a = (a0 + 360f / SwordCount * i) * Mathf.Deg2Rad;
            Vector2 at = Stage.ClampInside(Center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * SwordTriangleRadius, 1.5f);
            swords.Add(Enemy.Spawn(EnemyKind.GaniacSword, at, 1f));
            Fx.GroundCircle(at, 1.5f, new Color(0.7f, 0.4f, 1f, 0.7f), 0.6f);
        }
        CameraFollow.Shake(0.2f, 0.15f);
        swordTimeLeft = SwordCountdown;

        // 보스 복귀 → 카운트다운 동안 일반 패턴 (패턴 간 쿨타임 10초)
        transform.position = leaveAt;
        e.alpha = 0f;
        e.SetHidden(false);
        yield return Move(Vector2.zero, SwordReturnTime, k => e.alpha = k);
        e.alpha = 1f;
        gapLeft = SwordPhaseGap;
        busy = false;

        // 1분 카운트다운 (보스 기절과 무관, 일시정지 중 멈춤). 검을 모두 부수면 바로 끝
        while (swordTimeLeft > 0f && AliveSwords > 0)
        {
            if (Playing) swordTimeLeft = Mathf.Max(0f, swordTimeLeft - Time.deltaTime);
            yield return null;
        }

        if (AliveSwords > 0)
        {
            while (busy) yield return null;   // 진행 중인 일반 패턴이 끝난 뒤
            busy = true;
            var left = new List<Enemy>(swords);
            bool first = true;
            foreach (var s in left)
            {
                if (s == null || s.hp <= 0) continue;
                if (!first) yield return WaitActive(SwordStrikeDelay);
                first = false;
                if (s == null || s.hp <= 0) continue;   // 딜레이 동안 부서짐
                // 검으로 순간이동 (검은 보스가 회수 → 사라짐)
                Fx.Circle(Pos, 1.5f, new Color(0.75f, 0.4f, 1f, 0.7f), 0.3f);
                transform.position = s.transform.position;
                Destroy(s.gameObject);
                Fx.Circle(Pos, 1.5f, new Color(0.75f, 0.4f, 1f, 0.7f), 0.3f);
                Track(BossWarning.Circle(Pos, SwordStrikeRadius, SwordStrikeWarn));
                yield return WaitActive(SwordStrikeWarn);
                Fx.Disc(Pos, SwordStrikeRadius, new Color(0.45f, 0.15f, 0.7f, 0.45f), 0.3f);
                Fx.Circle(Pos, SwordStrikeRadius, new Color(0.8f, 0.55f, 1f, 0.8f), 0.3f);
                CameraFollow.Shake(0.25f, 0.25f);
                if (PlayerInCircle(Pos, SwordStrikeRadius)) P.HitByBoss(SwordStrikeDamage, 0f, Vector2.zero, 0f);
            }
            busy = false;
            gapLeft = PatternGap;
        }

        foreach (var s in swords) if (s != null) Destroy(s.gameObject);
        swords.Clear();
        swordPhase = false;
        gapLeft = Mathf.Min(gapLeft, PatternGap);
        if (!stormPhase) CameraFollow.ResetZoom();   // 특수 패턴 4 진행 중이면 축소 유지
        summonUnlocked = true;   // 공격 패턴 5 해제
    }

    // ---------- 특수 패턴 4 ----------
    // 좌측 하단 모서리에서 가로 · 세로 6 안쪽으로 순간이동 → 보스와 모서리 사이(안전 지대)를 뺀 맵 전체 경고 7초
    // → 경고 자리에 0.1초 레이저를 0.05초 간격으로 5개씩 무차별 난사, 3초 → 1초 뒤 우측 상단에서 1회 반복
    IEnumerator StormPhase()
    {
        busy = true;
        stormPhase = true;
        while (nextStormThreshold < StormThresholds.Length && e.hp <= e.maxHp * StormThresholds[nextStormThreshold]) nextStormThreshold++;   // 여러 단계를 넘으면 1번만
        CameraFollow.ZoomToFullMap();   // 화면 축소 (2026-11-13 사용자)

        Vector2[] corners = { new Vector2(-1f, -1f), new Vector2(1f, 1f) };   // 좌측 하단 → 우측 상단
        for (int c = 0; c < corners.Length; c++)
        {
            if (c > 0) yield return WaitActive(StormRepeatDelay);
            Vector2 k = corners[c];
            Vector2 edge = Center + Vector2.Scale(k, Half);                        // 모서리
            Vector2 at = Center + Vector2.Scale(k, Half - Vector2.one * StormInset);   // 보스 위치

            // 순간이동
            Fx.Circle(Pos, 1.5f, new Color(0.75f, 0.4f, 1f, 0.7f), 0.3f);
            transform.position = at;
            Fx.Circle(at, 1.5f, new Color(0.75f, 0.4f, 1f, 0.7f), 0.3f);
            faceDir = (Center - at).normalized;

            // 안전 지대 = 보스와 모서리 사이 직사각형
            var safe = Rect.MinMaxRect(Mathf.Min(at.x, edge.x), Mathf.Min(at.y, edge.y), Mathf.Max(at.x, edge.x), Mathf.Max(at.y, edge.y));
            // 경고 = 맵에서 안전 지대를 뺀 L자 (직사각형 2장)
            float minX = Center.x - Half.x, maxX = Center.x + Half.x, minY = Center.y - Half.y, maxY = Center.y + Half.y;
            // ① 안전 지대 위/아래 줄을 뺀 가로 띠 전체 ② 안전 지대 옆 나머지
            Rect band = k.y < 0 ? Rect.MinMaxRect(minX, safe.yMax, maxX, maxY) : Rect.MinMaxRect(minX, minY, maxX, safe.yMin);
            Rect side = k.x < 0 ? Rect.MinMaxRect(safe.xMax, safe.yMin, maxX, safe.yMax) : Rect.MinMaxRect(minX, safe.yMin, safe.xMin, safe.yMax);
            WarnRect(band, StormWarn);
            WarnRect(side, StormWarn);
            yield return WaitActive(StormWarn);

            // 난사: 보스에서 안전 지대 반대쪽 270도 범위로 무작위, 맵 끝까지
            float pocket = Mathf.Atan2(k.y, k.x) * Mathf.Rad2Deg;   // 안전 지대 방향 (좌하 = 225, 우상 = 45)
            float aMin = pocket + 45f + StormAngleMargin, aMax = pocket + 315f - StormAngleMargin;
            // 0.05초 간격을 프레임 속도와 무관하게 맞춤 (밀린 발사는 같은 프레임에 몰아서) → 3초에 정확히 60회
            int volleys = Mathf.RoundToInt(StormTime / StormInterval), fired = 0;
            float elapsed = 0f;
            while (fired < volleys)
            {
                if (Playing && !e.IsStunned)
                {
                    elapsed += Time.deltaTime;
                    while (fired < volleys && fired * StormInterval <= elapsed)
                    {
                        for (int i = 0; i < StormPerVolley; i++)
                        {
                            float a = Random.Range(aMin, aMax) * Mathf.Deg2Rad;
                            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                            RaidLaser.Line(at, d, ExitDistance(at, d), StormLaserWidth, StormLaserTime, StormDamage, StormLaserTime + 1f, safe);
                        }
                        if (fired % 5 == 0) CameraFollow.Shake(0.06f, 0.08f);
                        fired++;
                    }
                }
                yield return null;
            }
            yield return Wait(StormLaserTime);   // 마지막 레이저가 사라질 때까지
        }

        busy = false;
        stormPhase = false;
        if (!swordPhase) CameraFollow.ResetZoom();   // 특수 패턴 3 카운트다운 중이면 축소 유지
        gapLeft = swordPhase ? SwordPhaseGap : PatternGap;
        laserUnlocked = true;    // 공격 패턴 6 · 7 해제
    }

    /// <summary>축에 맞춘 직사각형 경고 (seconds 뒤 사라짐)</summary>
    void WarnRect(Rect r, float seconds)
    {
        if (r.width <= 0.01f || r.height <= 0.01f) return;
        Track(BossWarning.Box(new Vector2(r.xMin, r.center.y), Vector2.right, r.width, r.height, seconds));
    }

    /// <summary>맵 안의 점 p 에서 dir 방향으로 맵 경계까지 거리</summary>
    static float ExitDistance(Vector2 p, Vector2 dir)
    {
        float t = float.MaxValue;
        Vector2 min = Center - Half, max = Center + Half;
        if (dir.x > 1e-4f) t = Mathf.Min(t, (max.x - p.x) / dir.x); else if (dir.x < -1e-4f) t = Mathf.Min(t, (min.x - p.x) / dir.x);
        if (dir.y > 1e-4f) t = Mathf.Min(t, (max.y - p.y) / dir.y); else if (dir.y < -1e-4f) t = Mathf.Min(t, (min.y - p.y) / dir.y);
        return t == float.MaxValue ? 0f : Mathf.Max(0f, t);
    }

    /// <summary>특수 패턴 3 카운트다운 · 남은 검 (화면 위 가운데, 보스 체력 바 아래). GameManager.OnGUI 에서</summary>
    static void DrawSwordHud(float W)
    {
        var g = Current;
        if (g == null || !g.swordPhase || g.swords.Count == 0) return;
        int alive = g.AliveSwords;
        if (alive == 0 || g.swordTimeLeft <= 0f) return;
        hudStyle ??= new GUIStyle(UIStyles.Label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
        int sec = Mathf.CeilToInt(g.swordTimeLeft);
        string color = g.swordTimeLeft <= 10f ? "#FF6060" : "#E0C8FF";
        GUI.Label(new Rect(0, 112, W, 34), $"<color={color}>가니악의 검을 파괴하라  {sec / 60}:{sec % 60:00}   남은 검 {alive}</color>", hudStyle);
    }
    static GUIStyle hudStyle;

    /// <summary>게임 진행 중에만 흐르는 대기 (등장 연출은 기절과 무관하게 진행)</summary>
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

    /// <summary>delta 만큼 seconds 동안 이동 (맵 밖으로는 안 나감, 기절 · 일시정지 중 멈춤). onStep(진행도 0~1) 은 매 이동 후</summary>
    IEnumerator Move(Vector2 delta, float seconds, System.Action<float> onStep = null)
    {
        for (float t = 0f; t < seconds;)
        {
            if (Playing && !e.IsStunned)
            {
                float dt = Mathf.Min(Time.deltaTime, seconds - t);
                t += dt;
                transform.position = Stage.ClampInside(Pos + delta * (dt / seconds), e.radius);
                onStep?.Invoke(t / seconds);
            }
            yield return null;
        }
    }

    BossWarning Track(BossWarning w) { warnings.RemoveAll(x => x == null); warnings.Add(w); return w; }

    /// <summary>플레이어 피격 판정 원이 보스 기준 반경 r 원 안</summary>
    bool PlayerInCircle(Vector2 at, float r) => P != null && (P.HurtCenter - at).sqrMagnitude < Sq(r + P.radius);

    /// <summary>플레이어가 at 기준 dir 쪽 반원 (반경 r) 안</summary>
    bool PlayerInHalf(Vector2 at, Vector2 dir, float r)
    {
        if (!PlayerInCircle(at, r)) return false;
        return Vector2.Dot(P.HurtCenter - at, dir) >= -P.radius;
    }

    static float Sq(float v) => v * v;

    // ---------- 이동 패턴 1 ----------
    // 좌 또는 우로 3을 0.5초에 이동하며 맵 이탈 (흐려지며 사라짐) → 0.5초 뒤 플레이어 좌 또는 우 6에서 플레이어 쪽으로 3 이동하며 등장
    IEnumerator BlinkRoutine()
    {
        float side = Random.value < 0.5f ? -1f : 1f;
        faceDir = new Vector2(side, 0f);
        yield return Move(new Vector2(side * BlinkExitDistance, 0f), BlinkExitTime, k => e.alpha = 1f - k);
        e.SetHidden(true);
        e.alpha = 1f;
        yield return Wait(BlinkGone);

        // 등장 위치: 플레이어 좌 또는 우 6 (그쪽이 맵 밖이면 반대쪽)
        Vector2 pp = PlayerPos;
        float appear = Random.value < 0.5f ? -1f : 1f;
        float limit = Half.x - e.radius;
        if (Mathf.Abs(pp.x + appear * BlinkAppearOffset - Center.x) > limit) appear = -appear;
        transform.position = Stage.ClampInside(pp + new Vector2(appear * BlinkAppearOffset, 0f), e.radius);
        faceDir = new Vector2(-appear, 0f);
        e.alpha = 0f;
        e.SetHidden(false);
        yield return Move(new Vector2(-appear * BlinkEnterDistance, 0f), BlinkEnterTime, k => e.alpha = k);
        e.alpha = 1f;
    }

    // ---------- 공격 패턴 1 ----------
    // 0.5초 경고 → 반경 5 횡베기 + 2 이동, 2번 → 1.25초 경고 → 7 돌진하며 반경 4 전방위 베기
    IEnumerator SlashRoutine()
    {
        for (int k = 0; k < SweepRepeats; k++)
            yield return Sweep(ToPlayer, SweepRadius, SweepWarn, SweepAdvance);
        yield return SpinRoutine();
    }

    /// <summary>반원 경고 warn 초 → 반경 r 횡베기 1회 → (advance &gt; 0 이면) 그 방향으로 이동</summary>
    IEnumerator Sweep(Vector2 dir, float r, float warn, float advance)
    {
        faceDir = dir;
        Track(BossWarning.HalfDisc(Pos, dir, r, warn));
        yield return WaitActive(warn);
        Fx.Arc(Pos, dir, r, new Color(0.75f, 0.45f, 1f, 0.85f), 0.2f);
        CameraFollow.Shake(0.08f, 0.08f);
        if (PlayerInHalf(Pos, dir, r)) P.HitByBoss(SweepDamage, 0f, Vector2.zero, 0f);
        if (advance > 0f) yield return Move(dir * advance, AdvanceTime);
    }

    /// <summary>반경 r 반원 연속 베기: 0.1초 판정 · 0.05초 딜레이 × 10 (경고 없음 — 부르는 쪽에서). 판정마다 1번씩, 피격 무적 무시</summary>
    IEnumerator Flurry(Vector2 dir, float r)
    {
        faceDir = dir;
        for (int i = 0; i < FlurryHits; i++)
        {
            // 베기 방향을 조금씩 흔들어 연타 느낌 (판정은 같은 반원)
            float wobble = (i % 2 == 0 ? -1f : 1f) * 18f * Mathf.Deg2Rad;
            Vector2 fxDir = new Vector2(dir.x * Mathf.Cos(wobble) - dir.y * Mathf.Sin(wobble), dir.x * Mathf.Sin(wobble) + dir.y * Mathf.Cos(wobble));
            Fx.Arc(Pos, fxDir, r, new Color(0.75f, 0.45f, 1f, 0.8f), FlurryActive);
            bool hitThis = false;
            for (float t = 0f; t < FlurryActive;)
            {
                if (Playing && !e.IsStunned)
                {
                    t += Time.deltaTime;
                    if (!hitThis && PlayerInHalf(Pos, dir, r))
                    {
                        hitThis = true;
                        P.TakeDamage(FlurryDamage, ignoreHitInvuln: true);
                    }
                }
                yield return null;
            }
            if (i < FlurryHits - 1) yield return WaitActive(FlurryGap);
        }
    }

    // ---------- 공격 패턴 3 ----------
    // 반경 5 반원 경고 0.5초 → 횡베기 1회 → 1.25초 경고 → 7 돌진하며 반경 4 전방위 베기. 1회 반복 (총 2번)
    IEnumerator CrossRoutine()
    {
        for (int k = 0; k < CrossRepeats; k++)
        {
            yield return Sweep(ToPlayer, CrossSweepRadius, CrossSweepWarn, 0f);
            yield return SpinRoutine();
        }
    }

    // ---------- 공격 패턴 4 ----------
    // 반경 9 반원 경고 1.25초 → 횡베기 1회 → 0.75초 선딜 (같은 반원 경고) → 같은 반원에 연속 베기 (0.1초 판정 · 0.05초 딜레이 × 10)
    IEnumerator WideRoutine()
    {
        Vector2 dir = ToPlayer;
        yield return Sweep(dir, WideRadius, WideWarn, 0f);
        Track(BossWarning.HalfDisc(Pos, dir, WideRadius, WideFlurryWarn));   // 연속 베기 선딜
        yield return WaitActive(WideFlurryWarn);
        yield return Flurry(dir, WideRadius);
    }

    /// <summary>1.25초 경고 → 플레이어 쪽으로 7 돌진하며 반경 4 전방위 베기</summary>
    IEnumerator SpinRoutine()
    {
        // 돌진하며 사용 (2026-11-13 사용자): 반경 4 판정이 보스와 함께 7 이동 → 지나가는 길 전체가 범위 (1번만 맞음)
        Vector2 dir = faceDir = ToPlayer;
        Vector2 from = Pos;
        Vector2 to = Stage.ClampInside(from + dir * SpinAdvance, e.radius);
        // 경고 = 시작 원 + 지나가는 길 + 끝 원 (캡슐 모양)
        Track(BossWarning.Circle(from, SpinRadius, SpinWarn));
        if ((to - from).sqrMagnitude > 0.01f) Track(BossWarning.Box(from, (to - from).normalized, (to - from).magnitude, SpinRadius * 2f, SpinWarn));
        Track(BossWarning.Circle(to, SpinRadius, SpinWarn));
        yield return WaitActive(SpinWarn);

        Fx.Circle(Pos, SpinRadius, new Color(0.8f, 0.55f, 1f, 0.8f), SpinAdvanceTime + 0.1f, transform);   // 보스를 따라가는 회전 베기
        Fx.Disc(Pos, SpinRadius, new Color(0.5f, 0.2f, 0.8f, 0.35f), 0.2f);
        CameraFollow.Shake(0.12f, 0.12f);
        bool hit = false;
        void Check()
        {
            if (hit || !PlayerInCircle(Pos, SpinRadius)) return;
            hit = true;
            P.HitByBoss(SpinDamage, 0f, Vector2.zero, 0f);
        }
        Check();
        yield return Move(dir * SpinAdvance, SpinAdvanceTime, _ => Check());
        Fx.Disc(Pos, SpinRadius, new Color(0.5f, 0.2f, 0.8f, 0.35f), 0.2f);

        if (Enraged) yield return SpinLasers();   // 체력 50% 아래 강화
    }

    /// <summary>
    /// 체력 50% 아래 강화 (공격 패턴 1 · 2 · 3 의 전방위 베기 뒤): 0.2초 뒤 보스 위치에서 20도 간격 18방향으로
    /// 0.02초 간격 시계 또는 반시계 방향(무작위)으로 한 줄씩 경고 → 각 줄은 자기 경고 0.5초 뒤 맵 끝까지 레이저 (폭 1).
    /// 보스는 패턴이 끝날 때까지 정지 (패턴 중이라 원래 이동 없음)
    /// </summary>
    IEnumerator SpinLasers()
    {
        yield return WaitActive(EnrageLaserDelay);
        Vector2 at = Pos;
        float baseAng = Mathf.Atan2(ToPlayer.y, ToPlayer.x) * Mathf.Rad2Deg;   // [임시] 첫 줄은 플레이어 쪽
        float turn = Random.value < 0.5f ? -1f : 1f;                             // -1 = 시계 방향, +1 = 반시계 방향
        int n = Mathf.RoundToInt(360f / EnrageLaserStep);
        var dirs = new Vector2[n];
        var lens = new float[n];
        for (int i = 0; i < n; i++)
        {
            float r = (baseAng + turn * EnrageLaserStep * i) * Mathf.Deg2Rad;
            dirs[i] = new Vector2(Mathf.Cos(r), Mathf.Sin(r));
            lens[i] = ExitDistance(at, dirs[i]);
        }

        // i 번째 줄: i × 0.02초에 경고, 그 0.5초 뒤 레이저 (프레임 속도와 무관하게 시간표대로)
        int warned = 0, fired = 0;
        float elapsed = 0f;
        while (fired < n)
        {
            if (Playing && !e.IsStunned)
            {
                elapsed += Time.deltaTime;
                while (warned < n && warned * EnrageLaserInterval <= elapsed)
                {
                    Track(BossWarning.Box(at, dirs[warned], lens[warned], EnrageLaserWidth, EnrageLaserWarn));
                    warned++;
                }
                while (fired < warned && fired * EnrageLaserInterval + EnrageLaserWarn <= elapsed)
                {
                    RaidLaser.Line(at, dirs[fired], lens[fired], EnrageLaserWidth, EnrageLaserTime, EnrageLaserDamage, EnrageLaserTime + 1f);
                    if (fired % 3 == 0) CameraFollow.Shake(0.06f, 0.08f);
                    fired++;
                }
            }
            yield return null;
        }
        yield return Wait(EnrageLaserTime);   // 마지막 레이저가 사라질 때까지
    }

    // ---------- 공격 패턴 2 ----------
    // 0.5초 경고 → 6 빠르게 돌진 (적중 시 0.5초 기절) → 1초 경고 → 반경 4 반원 연속 베기 (0.1초 판정 · 0.05초 딜레이 × 10)
    // → 1.25초 경고 → 7 돌진하며 반경 4 전방위 베기
    IEnumerator RushRoutine()
    {
        yield return Charge(RushDistance, RushWarn);
        Vector2 dir = faceDir = ToPlayer;
        Track(BossWarning.HalfDisc(Pos, dir, FlurryRadius, FlurryWarn));
        yield return WaitActive(FlurryWarn);
        yield return Flurry(dir, FlurryRadius);
        yield return SpinRoutine();
    }

    /// <summary>직선 경고 warn 초 → 플레이어 쪽으로 distance 빠르게 돌진, 몸에 닿으면 피해 + 0.5초 기절 (공격 패턴 2 · 6)</summary>
    IEnumerator Charge(float distance, float warn)
    {
        Vector2 dir = faceDir = ToPlayer;
        Track(BossWarning.Box(Pos, dir, distance, e.radius * 2f, warn));
        yield return WaitActive(warn);
        bool hit = false;
        yield return Move(dir * distance, distance / RushSpeed, _ =>
        {
            if (!hit && PlayerInCircle(Pos, e.radius + 0.2f))
            {
                hit = true;
                P.HitByBoss(RushDamage, RushStun, dir, 0f);
            }
        });
        CameraFollow.Shake(0.1f, 0.1f);
    }

    /// <summary>
    /// 플레이어를 지나 맵을 가르는 레이저 count 개: 방향 무작위 · 모두 그 순간의 플레이어 위치를 지남 →
    /// warn 초 경고 뒤 동시에 발사 (공격 패턴 6 · 7)
    /// </summary>
    IEnumerator CrossLasers(int count, float width, float warn)
    {
        Vector2 through = Stage.ClampInside(PlayerPos, 0f);
        var lines = new List<(Vector2 from, Vector2 dir, float len)>();
        for (int i = 0; i < count; i++)
        {
            float a = Random.Range(0f, 180f) * Mathf.Deg2Rad;
            Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float back = ExitDistance(through, -d), fwd = ExitDistance(through, d);
            Vector2 from = through - d * back;
            lines.Add((from, d, back + fwd));
            Track(BossWarning.Box(from, d, back + fwd, width, warn));
        }
        yield return WaitActive(warn);
        foreach (var l in lines)
            RaidLaser.Line(l.from, l.dir, l.len, width, CrossLaserTime, CrossLaserDamage, CrossLaserTime + 1f);
        CameraFollow.Shake(0.15f, 0.15f);
        yield return Wait(CrossLaserTime);
    }

    // ---------- 공격 패턴 5 (특수 패턴 3 이후) ----------
    // 플레이어 방향으로 어둠의 검 4개를 지그재그로 (간격 3) → 1초 뒤 검마다 차례로 돌진, 닿으면 반경 6 전방위 베기
    IEnumerator SummonRoutine()
    {
        Vector2 dir = faceDir = ToPlayer;
        Vector2 side = new Vector2(-dir.y, dir.x);
        var points = new List<Vector2>();
        var marks = new List<GameObject>();
        var rings = new List<BossWarning>();
        for (int i = 0; i < SummonCount; i++)
        {
            float zig = (i % 2 == 0 ? 1f : -1f) * SummonZigzag;
            Vector2 at = Stage.ClampInside(Pos + dir * SummonSpacing * (i + 1) + side * zig, 1f);
            points.Add(at);
            marks.Add(DarkSword(at));
            rings.Add(Track(BossWarning.Circle(at, SummonSpinRadius, 0f)));   // 그 검에서 터질 범위 (닿을 때까지)
            Fx.Circle(at, 1f, new Color(0.6f, 0.3f, 0.9f, 0.7f), 0.3f);
        }
        yield return WaitActive(SummonWait);

        for (int i = 0; i < points.Count; i++)
        {
            Vector2 to = points[i];
            Vector2 d = to - Pos;
            if (d.sqrMagnitude > 0.0001f) faceDir = d.normalized;
            yield return Move(d, Mathf.Max(0.05f, d.magnitude / RushSpeed));
            // 검에 닿음 → 반경 6 전방위 베기
            if (marks[i] != null) Destroy(marks[i]);
            if (rings[i] != null) Destroy(rings[i].gameObject);
            Fx.Disc(Pos, SummonSpinRadius, new Color(0.45f, 0.15f, 0.7f, 0.45f), 0.25f);
            Fx.Circle(Pos, SummonSpinRadius, new Color(0.8f, 0.55f, 1f, 0.8f), 0.25f);
            CameraFollow.Shake(0.12f, 0.12f);
            if (PlayerInCircle(Pos, SummonSpinRadius)) P.HitByBoss(SpinDamage, 0f, Vector2.zero, 0f);
        }
        foreach (var m in marks) if (m != null) Destroy(m);
    }

    /// <summary>어둠의 검 표시 ([임시] 도형 — 그림 Ganiac_DarkSword 가 있으면 사용)</summary>
    GameObject DarkSword(Vector2 at)
    {
        var go = new GameObject("GaniacDarkSword");
        go.transform.position = at;
        go.transform.localScale = new Vector3(0.7f, 1.6f, 1f);
        SpriteFactory.AddRenderer(go, "Ganiac_DarkSword", SpriteFactory.Shape.Diamond, new Color(0.3f, 0.1f, 0.45f, 0.95f), 7);
        darkSwords.RemoveAll(x => x == null);
        darkSwords.Add(go);
        return go;
    }

    // ---------- 공격 패턴 6 (특수 패턴 4 이후) ----------
    // 0.5초 경고 → 8 빠르게 돌진 (적중 시 0.5초 기절) → 0.5초 경고 → 반경 4 횡베기 → 플레이어를 지나는 맵 가르는 레이저 6개 (폭 1, 0.5초 경고)
    IEnumerator ChargeLaserRoutine()
    {
        yield return Charge(ChargeLaserDistance, ChargeLaserWarn);
        yield return Sweep(ToPlayer, LaserSweepRadius, LaserSweepWarn, 0f);
        yield return CrossLasers(CrossLaserCount, CrossLaserWidth, CrossLaserWarn);
    }

    // ---------- 공격 패턴 7 (특수 패턴 4 이후) ----------
    // 0.5초 경고 → 반경 4 횡베기 → 플레이어를 지나는 맵 가르는 레이저 6개 (폭 1, 0.75초 경고). 3회 반복
    IEnumerator SweepLaserRoutine()
    {
        for (int k = 0; k < SweepLaserRepeats; k++)
        {
            yield return Sweep(ToPlayer, LaserSweepRadius, LaserSweepWarn, 0f);
            yield return CrossLasers(CrossLaserCount, SweepLaserWidth, SweepLaserWarn);
        }
    }

    IEnumerator Intro()
    {
        // 치트 패널에서 특수 패턴 1 · 2 를 끌 수 있음 (판 시작 때 확인). 2 를 끄면 보스가 바로 맵 중앙에 나타남
        if (!DebugCheats.NoGaniacSpecial[0]) yield return SpecialLines();
        if (!DebugCheats.NoGaniacSpecial[1]) yield return SpecialCenterLaser();
        else Reveal();
        intro = false;
    }

    // ---------- 특수 패턴 1 ----------
    // 맵 이탈 상태에서 맵 전체를 가로지르는 직선 경고가 0.2초 간격으로 무작위 10개 → 각자 생성 3초 뒤 사라지며 피해 → 특수 패턴 2
    IEnumerator SpecialLines()
    {
        // 맵을 꿰뚫는 길이 (2026-11-13 사용자): 지나는 점에서 양쪽으로 맵 대각선 길이 이상 → 어느 점 · 방향이든 양쪽 경계 밖까지
        // (밖으로 나간 부분은 경계 밖 어둠에 가려짐)
        float length = Half.magnitude * 4f + LineWidth * 2f;
        for (int i = 0; i < LineCount; i++)
        {
            Vector2 through = Center + new Vector2(Random.Range(-Half.x, Half.x), Random.Range(-Half.y, Half.y));
            float ang = Random.Range(0f, 180f) * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            Vector2 from = through - dir * length * 0.5f;
            BossWarning.Box(from, dir, length, LineWidth, LineDelay);
            StartCoroutine(LineStrike(from, dir, length));
            if (i < LineCount - 1) yield return Wait(LineInterval);
        }
        yield return Wait(LineDelay);   // 마지막 경고가 터질 때까지
    }

    IEnumerator LineStrike(Vector2 from, Vector2 dir, float length)
    {
        yield return Wait(LineDelay);
        var flash = Fx.Box(from, dir, length, LineWidth, new Color(0.25f, 0.05f, 0.35f, 0.85f), 0.2f);
        flash.GetComponent<SpriteRenderer>().sortingOrder = Fx.GroundOrder;   // 경계 밖은 어둠에 가려지게
        CameraFollow.Shake(0.08f, 0.1f);
        if (P != null && InBox(P.HurtCenter, from, dir, length, LineWidth, P.radius)) P.TakeDamage(LineDamage);
    }

    // ---------- 특수 패턴 2 ----------
    // 맵 중앙 수직 경고 (폭 9) 3초 → 검은 레이저 1.5초 (끝나기 0.5초 전 보스가 중앙에 나타남, 레이저에 가려 안 보임)
    // → 레이저 종료 1초 뒤 레이저가 없던 양 옆에 바깥쪽으로 순차 레이저 (폭 2 · 0.05초 간격 · 각 0.25초)
    IEnumerator SpecialCenterLaser()
    {
        Vector2 bottom = Center - new Vector2(0f, Half.y);
        float height = Half.y * 2f;
        BossWarning.Box(bottom, Vector2.up, height, CenterWidth, CenterWarn);
        yield return Wait(CenterWarn);

        RaidLaser.Vertical(Center.x, CenterWidth, CenterLaserTime, LaserDamage, LaserTick);
        CameraFollow.Shake(0.25f, 0.2f);
        yield return Wait(CenterLaserTime - RevealBeforeEnd);
        Reveal();
        yield return Wait(RevealBeforeEnd);

        yield return Wait(SweepDelay);
        // 중앙 레이저 바깥 가장자리부터 폭 2씩 맵 끝까지 (마지막 줄은 남은 폭만큼), 좌우 동시에
        float inner = CenterWidth * 0.5f;
        while (inner < Half.x - 0.01f)
        {
            float outer = Mathf.Min(inner + SweepWidth, Half.x);
            float mid = (inner + outer) * 0.5f, w = outer - inner;
            RaidLaser.Vertical(Center.x - mid, w, SweepLaserTime, LaserDamage, LaserTick);
            RaidLaser.Vertical(Center.x + mid, w, SweepLaserTime, LaserDamage, LaserTick);
            CameraFollow.Shake(0.1f, 0.12f);
            inner = outer;
            if (inner < Half.x - 0.01f) yield return Wait(SweepInterval);
        }
        yield return Wait(SweepLaserTime);   // 마지막 줄이 사라질 때까지
    }

    /// <summary>맵 중앙에 나타남 (맵 이탈 해제)</summary>
    void Reveal()
    {
        transform.position = Center;
        e.SetHidden(false);
    }

    /// <summary>점 p 가 from 에서 dir 방향 length × width 직사각형 안 (pad 만큼 넓혀서)</summary>
    public static bool InBox(Vector2 p, Vector2 from, Vector2 dir, float length, float width, float pad)
    {
        Vector2 d = p - from;
        float along = Vector2.Dot(d, dir), side = Mathf.Abs(d.x * dir.y - d.y * dir.x);
        return along >= -pad && along <= length + pad && side <= width * 0.5f + pad;
    }
}


/// <summary>
/// 검은 레이저 (가니악): from 에서 dir 방향 length × width 직사각형. 지속 시간 동안 안에 있는 플레이어에게 일정 간격 피해.
/// 적(5)보다 위 · 플레이어(10)보다 아래에 그려 보스를 가림. [임시] 도형 — 레이저 이펙트 리소스가 생기면 교체
/// </summary>
public class RaidLaser : MonoBehaviour
{
    const int CoreOrder = 8, EdgeOrder = 9;
    const float FadeIn = 0.08f, FadeOut = 0.15f, EdgeWidth = 0.25f;
    static readonly Color Core = new Color(0.03f, 0f, 0.06f, 0.97f);
    static readonly Color EdgeGlow = new Color(0.6f, 0.2f, 0.9f, 0.9f);

    Vector2 from, dir;
    float length, width, life, total, damage, tick, hitTimer, fadeIn, fadeOut;
    Rect safe;
    bool hasSafe;
    readonly List<SpriteRenderer> parts = new List<SpriteRenderer>();
    readonly List<Color> baseColors = new List<Color>();

    /// <summary>맵 높이 전체를 덮는 수직 레이저 (가운데 x, 폭 width)</summary>
    public static RaidLaser Vertical(float x, float width, float seconds, float damage, float tick) =>
        Line(new Vector2(x, Stage.Center.y - Stage.HalfSize.y), Vector2.up, Stage.HalfSize.y * 2f, width, seconds, damage, tick);

    /// <summary>
    /// from 에서 dir 방향 레이저. safe 가 있으면 그 직사각형 안의 플레이어는 맞지 않음 (가니악 특수 패턴 4 안전 지대).
    /// 짧은 레이저(0.1초)도 진하게 보이도록 나타남 · 사라짐 시간은 지속 시간에 맞춰 줄어듦
    /// </summary>
    public static RaidLaser Line(Vector2 from, Vector2 dir, float length, float width, float seconds, float damage, float tick, Rect? safe = null,
                                 Color? core = null, Color? edge = null)
    {
        dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
        var go = new GameObject("RaidLaser");
        go.transform.position = from + dir * length * 0.5f;
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var l = go.AddComponent<RaidLaser>();
        l.from = from; l.dir = dir; l.length = length; l.width = width;
        l.life = l.total = seconds; l.damage = damage; l.tick = tick;
        l.fadeIn = Mathf.Min(FadeIn, seconds * 0.25f);
        l.fadeOut = Mathf.Min(FadeOut, seconds * 0.35f);
        if (safe.HasValue) { l.safe = safe.Value; l.hasSafe = true; }
        // 로컬 x = 레이저 방향, 로컬 y = 폭
        // 색: 기본 = 가니악 (검정 · 보라), 카구츠치 등은 지정
        Color c = core ?? Core, g = edge ?? EdgeGlow;
        l.Part(Vector2.zero, new Vector2(length, width), c, CoreOrder);
        // 테두리 빛은 폭 안쪽에 (보이는 폭 = 판정 폭 width 그대로)
        float ew = Mathf.Min(EdgeWidth, width * 0.25f), eo = width * 0.5f - ew * 0.5f;
        l.Part(new Vector2(0f, -eo), new Vector2(length, ew), g, EdgeOrder);
        l.Part(new Vector2(0f, eo), new Vector2(length, ew), g, EdgeOrder);
        l.Apply(0f);
        return l;
    }

    void Part(Vector2 offset, Vector2 size, Color c, int order)
    {
        var go = new GameObject("LaserPart");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = offset;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("Fx_Square", SpriteFactory.Shape.Square);
        sr.sortingOrder = order;
        parts.Add(sr);
        baseColors.Add(c);
    }

    /// <summary>투명도 + 폭 (켜질 때 얇게 시작해 펼쳐짐)</summary>
    void Apply(float k)
    {
        transform.localScale = new Vector3(1f, Mathf.Lerp(0.3f, 1f, k), 1f);
        for (int i = 0; i < parts.Count; i++)
        {
            var c = baseColors[i];
            parts[i].color = new Color(c.r, c.g, c.b, c.a * k);
        }
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Destroy(gameObject); return; }
        float age = total - life;
        Apply(Mathf.Min(fadeIn > 0f ? Mathf.Clamp01(age / fadeIn) : 1f, fadeOut > 0f ? Mathf.Clamp01(life / fadeOut) : 1f));

        // 피해: 들어온 순간 1번, 머무르면 tick 마다 (안전 지대 안은 제외)
        hitTimer -= dt;
        var p = Player.I;
        if (p == null) return;
        bool inside = GaniacAI.InBox(p.HurtCenter, from, dir, length, width, p.radius)
                      && !(hasSafe && safe.Contains(p.HurtCenter));
        if (!inside) { hitTimer = 0f; return; }
        if (hitTimer <= 0f)
        {
            p.TakeDamage(damage);
            hitTimer = tick;
        }
    }
}