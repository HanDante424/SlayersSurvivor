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
            element = "어둠 · 절단 · 중력", note = "1단계 난이도 · 등장 · 공격 패턴 1~7 · 특수 패턴 1~4",
        },
        new RaidBossInfo
        {
            kind = EnemyKind.Kagutsuchi, name = "카구츠치", nameEn = "Kagutsuchi",
            element = "화염 · 검 · 거울", note = "제작 중 — 입장 연출까지",
        },
    };

    public static RaidBossInfo Find(EnemyKind k) => All.Find(b => b.kind == k);
}

/// <summary>레이드 보스 전용 AI 공통 (GameManager · 치트가 보스 종류를 몰라도 되게)</summary>
public interface IRaidBossAI
{
    /// <summary>보스 체력 바를 숨김 (첫 등장 연출 중 등)</summary>
    bool HideBossBar { get; }
    /// <summary>보스 전용 화면 표시 (카운트다운 등). GameManager.OnGUI 에서 체력 바 다음에</summary>
    void DrawHud(float W);
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
        if (!subscribed) { Enemy.Killed += OnKilled; subscribed = true; }
    }

    public static void Begin(Player p)
    {
        Reset();
        p.transform.position = Stage.Center + new Vector2(0f, PlayerStartY);
        // 처음 위치 · 등장은 보스 AI 가 정함 (가니악 = 맵 이탈 상태 · 카구츠치 = 맵 밖 위에서 걸어 들어옴)
        boss = Enemy.Spawn(SelectedInfo.kind, Stage.Center, 1f);
    }

    static void OnKilled(Enemy e)
    {
        if (e == null || e != boss) return;
        boss = null;
        if (GameManager.I != null) GameManager.I.StageCleared();
    }
}
