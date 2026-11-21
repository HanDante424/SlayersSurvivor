using UnityEngine;

/// <summary>
/// 궁극기 연출 중 "플레이어와 궁극기 연출만 정상 속도, 나머지 전부 느리게" (2026-11-21 사용자: 플레이어를 제외한 모든 것 속도 96% 감소, 연출 집중용).
/// 방식: Time.timeScale 을 Factor 로 낮춰 적 · 투사체 · 보스 패턴 · 일반 이펙트 등 모든 게임 시간이 한꺼번에 느려지게 하고,
/// 정상 속도로 남겨야 하는 것 (플레이어 이동 · 애니메이션 · 타이머, 궁극기 연출) 은 PlayerDelta / EffectDelta (실제 시간) 를 씀.
/// DebugMap 이 시간 배속을 덮어쓰지 않도록 Active 를 확인함.
/// </summary>
public static class WorldSlow
{
    /// <summary>느려진 뒤 속도 (0.04 = 96% 감소)</summary>
    public const float Factor = 0.04f;

    public static bool Active { get; private set; }
    static float prevScale = 1f;

    public static void Begin()
    {
        if (Active) return;
        Active = true;
        prevScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        if (Time.timeScale > 0f) Time.timeScale = Factor;   // 일시정지 (0) 중이면 건드리지 않음
    }

    public static void End()
    {
        if (!Active) return;
        Active = false;
        // 느린 동안 일시정지 등으로 상태가 바뀌었으면 (timeScale 가 Factor 가 아님) 그대로 둠
        if (Mathf.Approximately(Time.timeScale, Factor)) Time.timeScale = prevScale;
    }

    /// <summary>플레이어 쪽 시간 (이동 · 애니메이션 · 무적 · 스킬 틱): 느려지는 동안은 실제 시간, 평소에는 게임 시간</summary>
    public static float PlayerDelta => Active && Time.timeScale > 0f ? Time.unscaledDeltaTime : Time.deltaTime;

    /// <summary>궁극기 연출 쪽 시간: 항상 실제 시간 (게임이 일시정지 timeScale = 0 이면 멈춤)</summary>
    public static float EffectDelta => Time.timeScale > 0f ? Time.unscaledDeltaTime : 0f;
}
