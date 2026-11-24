using UnityEngine;

// 에른 스트레이 새 패시브 3종 — 기반 단계 (2026-11-24 사용자: "일단 기반은 이정도로")
// 기본 성능만 구현. 고유 기본 강화 · Lv.5/10/15 특수 강화는 미설계라 [임시] 로 최대 레벨 1 (레벨업 후보에서 한 번만 등장).
// 설계가 나오면 LevelCap 덮어쓰기를 지우고 PassiveBasicOptions / SpecialOptions 를 채울 것.

/// <summary>P04 잿불 갑주: 최대 체력 +25%, 받는 피해량 -20%</summary>
public class EmberArmor : Skill
{
    const float MaxHpBonus = 0.25f;     // 최대 체력 +25%
    const float DamageTaken = 0.20f;    // 받는 피해량 -20%

    public override int LevelCap => 1;  // [임시] 강화 미설계

    float appliedHpMul = 1f, appliedTakenMul = 1f;

    void OnEnable()
    {
        var p = P;
        if (p == null) return;
        appliedHpMul = 1f + MaxHpBonus;
        float before = p.maxHp;
        p.maxHp *= appliedHpMul;
        p.hp += p.maxHp - before;       // 늘어난 만큼 현재 체력도 함께
        appliedTakenMul = 1f - DamageTaken;
        p.damageTakenMul *= appliedTakenMul;
    }

    void OnDisable()
    {
        var p = P;
        if (p == null) return;
        p.maxHp /= appliedHpMul;
        p.hp = Mathf.Min(p.hp, p.maxHp);
        p.damageTakenMul /= appliedTakenMul;
        appliedHpMul = appliedTakenMul = 1f;
    }
}

/// <summary>P05 불씨 수확: 경험치 획득 +20%, 경험치 획득 범위 +30%</summary>
public class EmberHarvest : Skill
{
    const float XpBonus = 0.20f;        // 경험치 획득 +20%
    const float PickupBonus = 0.30f;    // 경험치 획득 범위 +30%

    public override int LevelCap => 1;  // [임시] 강화 미설계

    float appliedXpMul = 1f, appliedPickupMul = 1f;

    void OnEnable()
    {
        var p = P;
        if (p == null) return;
        appliedXpMul = 1f + XpBonus;
        appliedPickupMul = 1f + PickupBonus;
        p.xpGainMul *= appliedXpMul;
        p.pickupMul *= appliedPickupMul;
    }

    void OnDisable()
    {
        var p = P;
        if (p == null) return;
        p.xpGainMul /= appliedXpMul;
        p.pickupMul /= appliedPickupMul;
        appliedXpMul = appliedPickupMul = 1f;
    }
}

/// <summary>
/// P06 화염 보호막: 화상 피해가 적용될 때 그 피해량의 10% 만큼 보호막 획득, 보호막은 매초 5% 감소.
/// [해석] "총 피해량" = 그 화상 피해 1회(틱)에 실제 적용된 피해량. "매초 5% 감소" = 현재 보호막의 5% (지수 감소).
/// [임시] 보호막 상한 = 최대 체력의 50% (기획 미정 — 화상 피해가 커지면 무한히 쌓이는 것을 막는 안전장치).
/// </summary>
public class FlameBarrier : Skill
{
    const float ShieldPerBurn = 0.10f;      // 화상 피해의 10%
    const float DecayPerSecond = 0.05f;     // 매초 5% 감소
    const float CapOfMaxHp = 0.5f;          // [임시] 최대 체력 대비 상한

    public override int LevelCap => 1;      // [임시] 강화 미설계

    void OnEnable() => Enemy.Damaged += OnDamaged;

    void OnDisable()
    {
        Enemy.Damaged -= OnDamaged;
        if (P != null) P.shield = 0f;
    }

    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (!d.IsBurn || dealt <= 0 || P == null) return;
        P.shield = Mathf.Min(P.maxHp * CapOfMaxHp, P.shield + dealt * ShieldPerBurn);
    }

    public override void Tick(float dt)
    {
        if (P == null || P.shield <= 0f) return;
        P.shield *= Mathf.Pow(1f - DecayPerSecond, dt);
        if (P.shield < 0.01f) P.shield = 0f;
    }

    public override string Badge => P != null && P.shield > 0.5f ? Mathf.CeilToInt(P.shield).ToString() : null;
}
