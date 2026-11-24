using UnityEngine;

// 에른 스트레이 새 패시브 3종 — 방어 · 피해 대비 · 자원 획득 (2026-11-24 사용자)
// 기본 성능 + 고유 기본 강화 3종까지 구현. Lv.5/10/15 특수 강화는 미설계 (선택 화면에 "미설계" 확인 버튼만).

/// <summary>
/// P04 잿불 갑주: 최대 체력 +25% · 받는 피해량 -20% · 모든 회복량 +10%
/// 고유 기본 강화: 최대 체력 +5% / 받는 피해 -1% / 모든 회복량 +2%
/// [해석] 설명의 "모든 회복량 +10%" 는 기존 기본 성능(체력 +25% · 받는 피해 -20%)에 추가한 것으로 처리.
/// [해석] 최대 체력 · 회복량 증가는 자기 안에서 합연산, 받는 피해 감소는 기본 -20% 에 곱연산 (강화 1회당 ×0.99).
/// </summary>
public class EmberArmor : Skill
{
    const float BaseMaxHp = 0.25f, BaseTakenCut = 0.20f, BaseHeal = 0.10f;
    const float UpMaxHp = 0.05f, UpTakenCut = 0.01f, UpHeal = 0.02f;

    int hpUps, takenUps, healUps;
    float appliedHpMul = 1f, appliedTakenMul = 1f, appliedHealMul = 1f;

    void OnEnable() => Apply();

    void OnDisable()
    {
        Remove();
    }

    float HpMul => 1f + BaseMaxHp + UpMaxHp * hpUps;
    float TakenMul => (1f - BaseTakenCut) * Mathf.Pow(1f - UpTakenCut, takenUps);
    float HealMul => 1f + BaseHeal + UpHeal * healUps;

    /// <summary>현재 강화 상태로 플레이어 수치를 갱신 (이전 몫을 나누고 새 몫을 곱함)</summary>
    void Apply()
    {
        var p = P;
        if (p == null) return;
        float before = p.maxHp;
        p.maxHp = p.maxHp / appliedHpMul * HpMul;
        p.hp = Mathf.Min(p.maxHp, p.hp + Mathf.Max(0f, p.maxHp - before));   // 늘어난 만큼 현재 체력도 함께
        p.damageTakenMul = p.damageTakenMul / appliedTakenMul * TakenMul;
        p.healMul = p.healMul / appliedHealMul * HealMul;
        appliedHpMul = HpMul; appliedTakenMul = TakenMul; appliedHealMul = HealMul;
    }

    void Remove()
    {
        var p = P;
        if (p == null) return;
        p.maxHp /= appliedHpMul;
        p.hp = Mathf.Min(p.hp, p.maxHp);
        p.damageTakenMul /= appliedTakenMul;
        p.healMul /= appliedHealMul;
        appliedHpMul = appliedTakenMul = appliedHealMul = 1f;
    }

    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"체력 강화  <size=16>최대 체력 +{UpMaxHp * 100:0}%</size>",
            desc = $"최대 체력 +{(BaseMaxHp + UpMaxHp * hpUps) * 100:0}% → +{(BaseMaxHp + UpMaxHp * (hpUps + 1)) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"방어 강화  <size=16>받는 피해 -{UpTakenCut * 100:0}%</size>",
            desc = $"받는 피해 -{(1f - TakenMul) * 100:0.#}% → -{(1f - (1f - BaseTakenCut) * Mathf.Pow(1f - UpTakenCut, takenUps + 1)) * 100:0.#}%",
        },
        new PassiveUpgradeOption
        {
            title = $"회복 강화  <size=16>모든 회복량 +{UpHeal * 100:0}%</size>",
            desc = $"모든 회복량 +{(BaseHeal + UpHeal * healUps) * 100:0}% → +{(BaseHeal + UpHeal * (healUps + 1)) * 100:0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: hpUps++; break;
            case 1: takenUps++; break;
            case 2: healUps++; break;
        }
        Apply();
    }

    public override string BasicSummary =>
        $"체력 +{(HpMul - 1f) * 100:0}% · 받는 피해 -{(1f - TakenMul) * 100:0.#}% · 회복 +{(HealMul - 1f) * 100:0}%";
}

/// <summary>
/// P05 불씨 수확: 경험치 획득 +20% · 경험치 획득 범위 +30%
/// 고유 기본 강화: 경험치 획득 +5% / 획득 범위 +8% / 유물 획득 확률 +10%
/// [해석] 유물 획득 확률 +10% = 일반 몹 상자 드랍 확률(0.4%)의 상대 증가 (0.4% → 0.44%). 의도적으로 낮은 체감 (사용자).
///        기본 성능에는 유물 확률이 없고 강화를 고를 때만 오름. 효과는 자기 안에서 합연산.
/// </summary>
public class EmberHarvest : Skill
{
    const float BaseXp = 0.20f, BasePickup = 0.30f;
    const float UpXp = 0.05f, UpPickup = 0.08f, UpRelic = 0.10f;

    int xpUps, pickupUps, relicUps;
    float appliedXp = 1f, appliedPickup = 1f, appliedRelic = 1f;

    void OnEnable() => Apply();

    void OnDisable()
    {
        var p = P;
        if (p == null) return;
        p.xpGainMul /= appliedXp;
        p.pickupMul /= appliedPickup;
        p.relicChanceMul /= appliedRelic;
        appliedXp = appliedPickup = appliedRelic = 1f;
    }

    float XpMul => 1f + BaseXp + UpXp * xpUps;
    float PickupMul => 1f + BasePickup + UpPickup * pickupUps;
    float RelicMul => 1f + UpRelic * relicUps;

    void Apply()
    {
        var p = P;
        if (p == null) return;
        p.xpGainMul = p.xpGainMul / appliedXp * XpMul;
        p.pickupMul = p.pickupMul / appliedPickup * PickupMul;
        p.relicChanceMul = p.relicChanceMul / appliedRelic * RelicMul;
        appliedXp = XpMul; appliedPickup = PickupMul; appliedRelic = RelicMul;
    }

    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"경험치 강화  <size=16>경험치 획득 +{UpXp * 100:0}%</size>",
            desc = $"경험치 획득 +{(BaseXp + UpXp * xpUps) * 100:0}% → +{(BaseXp + UpXp * (xpUps + 1)) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"범위 강화  <size=16>획득 범위 +{UpPickup * 100:0}%</size>",
            desc = $"획득 범위 +{(BasePickup + UpPickup * pickupUps) * 100:0}% → +{(BasePickup + UpPickup * (pickupUps + 1)) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"유물 강화  <size=16>유물 획득 확률 +{UpRelic * 100:0}%</size>",
            desc = $"유물 획득 확률 +{UpRelic * relicUps * 100:0}% → +{UpRelic * (relicUps + 1) * 100:0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: xpUps++; break;
            case 1: pickupUps++; break;
            case 2: relicUps++; break;
        }
        Apply();
    }

    public override string BasicSummary =>
        $"경험치 +{(XpMul - 1f) * 100:0}% · 범위 +{(PickupMul - 1f) * 100:0}% · 유물 +{(RelicMul - 1f) * 100:0}%";
}

/// <summary>
/// P06 화염 보호막 (2026-11-24 사용자): 화상 피해를 적용할 때 그 피해의 10% 만큼 보호막 획득.
/// 한 번에 얻는 양은 최대 체력의 5% 까지, 획득 쿨타임 0.5초, 보호막은 매초 5% 소모, 총량 기본 상한 = 최대 체력의 100%.
/// 고유 기본 강화: 최대 보호막 상한 +5% / 보호막 획득량 +10% / 보호막 감소량 -0.2%p
/// [해석] "피해" = 화상 피해 1회(틱)에 실제 적용된 피해량. "매초 5% 소모" = 현재 보호막의 5% (지수 감소).
/// [해석] 상한 +5% = 최대 체력 대비 +5%p (100% → 105%), 획득량 +10% = 한 번에 얻는 양(상한 포함)에 곱, 감소량 -0.2%p = 5% → 4.8% (0 아래로는 안 내려감).
/// </summary>
public class FlameBarrier : Skill
{
    const float ShieldPerBurn = 0.10f;      // 화상 피해의 10%
    const float MaxGainOfMaxHp = 0.05f;     // 한 번에 얻는 보호막 상한 = 최대 체력의 5%
    const float BaseCapOfMaxHp = 1.0f;      // 기본 총량 상한 = 최대 체력의 100%
    const float GainCooldown = 0.5f;        // 획득 쿨타임
    const float BaseDecay = 0.05f;          // 매초 5% 소모

    const float UpCap = 0.05f, UpGain = 0.10f, UpDecay = 0.002f;

    int capUps, gainUps, decayUps;
    float readyAt;   // 다음 획득이 가능한 시각 (Time.time 기준)

    float CapOfMaxHp => BaseCapOfMaxHp + UpCap * capUps;
    float GainMul => 1f + UpGain * gainUps;
    float Decay => Mathf.Max(0f, BaseDecay - UpDecay * decayUps);

    void OnEnable() { Enemy.Damaged += OnDamaged; readyAt = 0f; }

    void OnDisable()
    {
        Enemy.Damaged -= OnDamaged;
        if (P != null) P.shield = 0f;
    }

    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (!d.IsBurn || dealt <= 0 || P == null || Time.time < readyAt) return;
        readyAt = Time.time + GainCooldown;
        float gain = Mathf.Min(dealt * ShieldPerBurn, P.maxHp * MaxGainOfMaxHp) * GainMul;
        P.shield = Mathf.Min(P.maxHp * CapOfMaxHp, P.shield + gain);
    }

    public override void Tick(float dt)
    {
        if (P == null || P.shield <= 0f) return;
        P.shield *= Mathf.Pow(1f - Decay, dt);
        if (P.shield < 0.01f) P.shield = 0f;
    }

    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"상한 강화  <size=16>최대 보호막 상한 +{UpCap * 100:0}%</size>",
            desc = $"상한 최대 체력의 {CapOfMaxHp * 100:0}% → {(CapOfMaxHp + UpCap) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"획득 강화  <size=16>보호막 획득량 +{UpGain * 100:0}%</size>",
            desc = $"획득량 +{UpGain * gainUps * 100:0}% → +{UpGain * (gainUps + 1) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"유지 강화  <size=16>보호막 감소량 -{UpDecay * 100:0.0}%p</size>",
            desc = $"매초 감소 {Decay * 100:0.0}% → {Mathf.Max(0f, BaseDecay - UpDecay * (decayUps + 1)) * 100:0.0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: capUps++; break;
            case 1: gainUps++; break;
            case 2: decayUps++; break;
        }
    }

    public override string BasicSummary =>
        $"상한 {CapOfMaxHp * 100:0}% · 획득 +{UpGain * gainUps * 100:0}% · 감소 {Decay * 100:0.0}%/초";

    public override string Badge => P != null && P.shield > 0.5f ? Mathf.CeilToInt(P.shield).ToString() : null;
}
