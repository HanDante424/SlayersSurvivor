using System.Collections.Generic;
using UnityEngine;

/// <summary>레이드 보스 1종 (레이드 선택 화면에 보일 정보)</summary>
public class RaidBossInfo
{
    public EnemyKind kind;
    public string name, nameEn;
    public string element;      // 주요 전투 요소
    public string note;         // 상태 안내 (선택 화면)
}

/// <summary>
/// 레이드 보스 목록 (2026-11-13: 가니악 벤데스트라 · 카구츠치). 보스를 추가하려면 이 목록에 한 줄 + EnemyKind + 전용 AI.
/// </summary>
public static class RaidBossRoster
{
    public static readonly List<RaidBossInfo> All = new List<RaidBossInfo>
    {
        new RaidBossInfo
        {
            kind = EnemyKind.Ganiac, name = "가니악 벤데스트라", nameEn = "Ganiac Bendestra",
            element = "어둠 · 절단 · 중력", note = "등장 · 공격 패턴 1~7 · 특수 패턴 1~4",
        },
        new RaidBossInfo
        {
            kind = EnemyKind.Kagutsuchi, name = "카구츠치", nameEn = "Kagutsuchi",
            element = "화염 · 검 · 거울", note = "입장 · 공격 패턴 1~7 · 특수 패턴 1~2",
        },
    };

    public static RaidBossInfo Find(EnemyKind k) => All.Find(b => b.kind == k);
}

/// <summary>레이드 난이도 (2026-11-14 사용자): 난이도가 오를수록 페이즈가 하나씩 더 열림</summary>
public enum RaidDifficulty { Easy, Normal, Hard, Extreme }

/// <summary>난이도 1개 (난이도 선택 화면 · 페이즈 수)</summary>
public class RaidDifficultyInfo
{
    public RaidDifficulty difficulty;
    public string name, nameEn, note;
    public int phases;          // 이 난이도에서 싸우는 페이즈 수 (1페 → … → phases 페)
    public bool locked;         // 준비 중 (고를 수 없음)

    public string PhaseText => phases <= 1 ? "1페이즈" : $"1 ~ {phases}페이즈";

    /// <summary>
    /// 이지 = 1페 · 노말 = 1 · 2페 · 하드 = 1 · 2 · 3페 · 익스트림 = 특수 (사용자). 각 페이즈 체력을 모두 깎으면 체력이 다시 차는 연출과 함께 다음 페이즈.
    /// 하드 · 익스트림은 진입 불가 (2026-11-14 사용자). 난이도별 체력 · 피해 배율 없음 (페이즈 수만 다름)
    /// </summary>
    public static readonly List<RaidDifficultyInfo> All = new List<RaidDifficultyInfo>
    {
        new RaidDifficultyInfo { difficulty = RaidDifficulty.Easy,    name = "이지",     nameEn = "Easy",    phases = 1, note = "1페이즈만" },
        new RaidDifficultyInfo { difficulty = RaidDifficulty.Normal,  name = "노말",     nameEn = "Normal",  phases = 2, note = "2페이즈 해금" },
        new RaidDifficultyInfo { difficulty = RaidDifficulty.Hard,    name = "하드",     nameEn = "Hard",    phases = 3, note = "3페이즈 준비 중", locked = true },
        new RaidDifficultyInfo { difficulty = RaidDifficulty.Extreme, name = "익스트림", nameEn = "Extreme", phases = 1, note = "특수 — 내용 미정", locked = true },
    };

    public static RaidDifficultyInfo Get(RaidDifficulty d) => All.Find(x => x.difficulty == d) ?? All[0];
}

/// <summary>레이드 보스 전용 AI 공통 (GameManager · 치트가 보스 종류를 몰라도 되게)</summary>
public interface IRaidBossAI
{
    /// <summary>페이즈 체력을 모두 깎음 → 진행 중인 패턴 · 경고 · 소환물을 즉시 정리 (체력 회복 연출 동안 아무것도 안 함)</summary>
    void OnPhaseEnd();
    /// <summary>체력 회복 연출이 끝나고 phase 페이즈 시작 (2 · 3)</summary>
    void OnPhaseBegin(int phase);

    /// <summary>보스 체력 바를 숨김 (첫 등장 연출 중 등)</summary>
    bool HideBossBar { get; }
    /// <summary>보스 전용 화면 표시 (카운트다운 등). GameManager.OnGUI 에서 체력 바 다음에</summary>
    void DrawHud(float W);
}

/// <summary>보스 전용 전환 연출. true면 해당 보스가 EndTransition까지 진행을 소유.</summary>
public interface IRaidPhaseTransition
{
    bool TryBeginPhaseTransition(int phase);
}

/// <summary>
/// 레이드 보스 스테이지 (2026-11-13 사용자: 레이드 보스만 나오는 전용 스테이지 · 맵 제한).
/// - 일반 몹 · 중간보스 없음, 시간 제한 없음
/// - 판 시작 시 플레이어는 맵 아래쪽. 레이드 보스는 고른 보스 (Selected) — 등장 방식은 보스 AI 가 처리
/// - 레이드 보스를 쓰러뜨리면 승리 ("레이드 성공!")
/// 기획에 없는 값은 [임시].
/// </summary>
public static class RaidStage
{
    const float PlayerStartY = -6f;   // [임시] 맵 중심 기준

    /// <summary>이번 판의 레이드 보스 (레이드 선택 화면에서 고름, 재시작해도 유지)</summary>
    public static EnemyKind Selected = EnemyKind.Ganiac;
    /// <summary>이번 판의 난이도 (난이도 선택 화면에서 고름, 재시작해도 유지)</summary>
    public static RaidDifficulty Difficulty = RaidDifficulty.Normal;
    public static RaidDifficultyInfo DifficultyInfo => RaidDifficultyInfo.Get(Difficulty);
    /// <summary>이번 난이도의 마지막 페이즈</summary>
    public static int MaxPhase => Mathf.Max(1, DifficultyInfo.phases);
    /// <summary>지금 페이즈 (1부터)</summary>
    public static int Phase { get; private set; } = 1;
    /// <summary>페이즈 전환 중 (체력 회복 연출 — 보스는 맞지 않고 패턴 없음)</summary>
    public static bool InTransition { get; private set; }
    public static RaidBossInfo SelectedInfo => RaidBossRoster.Find(Selected) ?? RaidBossRoster.All[0];

    static Enemy boss;
    static bool subscribed;

    /// <summary>살아 있는 레이드 보스 (없으면 null)</summary>
    public static Enemy Boss => boss != null && boss.hp > 0 ? boss : null;

    /// <summary>지금 레이드 보스의 AI (없으면 null)</summary>
    public static IRaidBossAI BossAI => Boss != null ? Boss.GetComponent<IRaidBossAI>() : null;

    /// <summary>새 판 시작 (static 이라 씬을 다시 불러와도 남음 → GameManager.Awake 에서)</summary>
    public static void Reset()
    {
        boss = null;
        Phase = 1;
        InTransition = false;
        if (!subscribed) { Enemy.Killed += OnKilled; subscribed = true; }
    }

    public static void Begin(Player p)
    {
        Reset();
        p.transform.position = Stage.Center + new Vector2(0f, PlayerStartY);
        // 처음 위치 · 등장은 보스 AI 가 정함 (가니악 = 맵 이탈 상태 · 카구츠치 = 맵 밖 위에서 걸어 들어옴)
        boss = Enemy.Spawn(SelectedInfo.kind, Stage.Center, 1f);
    }

    /// <summary>
    /// 레이드 보스 체력이 0 이 된 순간 (Enemy.Die). 남은 페이즈가 있으면 죽지 않고 다음 페이즈로 → true.
    /// 체력 0 에서 최대까지 차는 연출 (RaidPhaseTransition) 동안 맞지 않음 · 접촉 피해 없음 · 패턴 없음
    /// </summary>
    public static bool TryNextPhase(Enemy e)
    {
        if (e == null || e != boss || InTransition || Phase >= MaxPhase) return false;
        InTransition = true;
        Phase++;
        e.hp = 0.01f;            // 살아 있음 (Boss != null) · 체력 바는 0 에서 다시 참
        e.SetScripted(true);
        e.GetComponent<IRaidBossAI>()?.OnPhaseEnd();
        var transition = e.GetComponent<IRaidPhaseTransition>();
        if (transition == null || !transition.TryBeginPhaseTransition(Phase)) RaidPhaseTransition.Begin(e);
        return true;
    }

    /// <summary>체력 회복 연출 끝 (RaidPhaseTransition)</summary>
    public static void EndTransition(Enemy e)
    {
        if (e == null || e != boss || !InTransition) return;
        InTransition = false;
        e.SetScripted(false);
        e.GetComponent<IRaidBossAI>()?.OnPhaseBegin(Phase);
    }

    static void OnKilled(Enemy e)
    {
        if (e == null || e != boss) return;
        boss = null;
        if (GameManager.I != null) GameManager.I.StageCleared();
    }
}

/// <summary>
/// 페이즈 전환 연출 (2026-11-14 사용자: 페이즈 체력을 모두 깎으면 체력이 다시 회복되는 연출과 함께 다음 페이즈).
/// 보스 체력이 HealTime 동안 0 → 최대로 차오르고, 보스 주변에 퍼지는 고리 · 화면 흔들림. 일시정지 중에는 멈춤
/// </summary>
public class RaidPhaseTransition : MonoBehaviour
{
    const float HealTime = 2.5f;          // [임시] 체력이 다 차는 시간
    const float RingInterval = 0.3f;      // [임시] 고리 간격
    static readonly Color RingColor = new Color(1f, 0.35f, 0.3f, 0.85f);

    Enemy e;
    float t, ring;

    public static void Begin(Enemy e)
    {
        var c = e.gameObject.AddComponent<RaidPhaseTransition>();
        c.e = e;
        CameraFollow.Shake(0.35f, 0.3f);
        Fx.Disc(e.transform.position, e.radius + 2f, new Color(1f, 0.9f, 0.85f, 0.6f), 0.4f);
    }

    void Update()
    {
        if (e == null) { Destroy(this); return; }
        if (GameManager.I != null && GameManager.I.State != GameState.Playing) return;
        t += Time.deltaTime;
        float k = Mathf.Clamp01(t / HealTime);
        e.hp = Mathf.Max(0.01f, e.maxHp * Mathf.SmoothStep(0f, 1f, k));
        ring -= Time.deltaTime;
        if (ring <= 0f && k < 1f)
        {
            ring = RingInterval;
            Fx.Circle(e.transform.position, e.radius + 0.6f, RingColor, 0.5f, e.transform, 1.2f);
        }
        if (k >= 1f)
        {
            e.hp = e.maxHp;
            CameraFollow.Shake(0.25f, 0.2f);
            Fx.Disc(e.transform.position, e.radius + 3f, new Color(1f, 0.45f, 0.35f, 0.5f), 0.35f);
            RaidStage.EndTransition(e);
            Destroy(this);
        }
    }
}
