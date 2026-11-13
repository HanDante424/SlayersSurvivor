using UnityEngine;

/// <summary>
/// 레이드 보스 스테이지 (2026-11-13 사용자: 레이드 보스만 나오는 전용 스테이지 · 맵 제한).
/// - 일반 몹 · 중간보스 없음, 시간 제한 없음
/// - 판 시작 시 플레이어는 맵 아래쪽. 레이드 보스(가니악)는 맵 이탈 상태로 생성 → 등장 패턴(GaniacAI) 중 맵 중앙에 나타남
/// - 레이드 보스를 쓰러뜨리면 승리 ("레이드 성공!")
/// 기획에 없는 값은 [임시].
/// </summary>
public static class RaidStage
{
    const float PlayerStartY = -6f;   // [임시] 맵 중심 기준

    static Enemy boss;
    static bool subscribed;

    /// <summary>살아 있는 레이드 보스 (없으면 null)</summary>
    public static Enemy Boss => boss != null && boss.hp > 0 ? boss : null;

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
        boss = Enemy.Spawn(EnemyKind.Ganiac, Stage.Center, 1f);   // 맵 이탈 상태로 시작 (GaniacAI 가 숨김 · 등장 처리)
    }

    static void OnKilled(Enemy e)
    {
        if (e == null || e != boss) return;
        boss = null;
        if (GameManager.I != null) GameManager.I.StageCleared();
    }
}
