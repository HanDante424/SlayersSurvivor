using UnityEngine;

/// <summary>
/// P03 흡수 가열 (통합 기획서 v1.0 10장 + 결정 2026-11-10).
/// - 스택 = 플레이어 주변(absorbDetectRadius) **화상 상태인 적의 수** (화상 스택 수가 아님), 최대 스택까지
/// - 스택당 효과는 흡수 가열 안에서 합산 → 효과마다 배율/수치 1개
///   기본: 최종 피해량 +5% (독립 최종 배율 1개) · 쿨타임 감소 1% (독립 쿨타임 배율 1개)
/// - 최종 효과량 = 기본 효과량 × (1 + 효과량 증가 합) × (1 + 최종 효과량 증가 합). 압축 연료 · 열기 응축 · 임계 과열 각 +100%, 서로 **합연산** (셋 다 ×4)
///   결정: 최종 효과량은 흡수 가열의 **스택당 효과 전부**에 곱해짐 (기본 2종 + 불씨 점화 · 열기 추진 · 열폭주 · 에너지 흡수 · 가속 연소 · 이열치열)
/// - 가속 연소 최소 피해 간격 0.1초 (CombatMods.DotInterval), 임계 과열은 최대 스택을 잃는 즉시 해제
/// </summary>
public class HeatAbsorption : Skill
{
    // 기본 수치 (기획 v1.0 확정)
    const int BaseMaxStacks = 10;
    const float BaseFinalPerStack = 0.05f;      // 스택당 최종 피해량 +5%
    const float BaseCooldownPerStack = 0.01f;   // 스택당 쿨타임 감소 1%

    // 고유 기본 강화
    const float UpFinalPerStack = 0.01f;        // 스택당 최종 피해 증가량 +1%p
    const float UpCooldownPerStack = 0.002f;    // 스택당 쿨타임 감소 +0.2%p
    const int UpMaxStacks = 1;                  // 최대 스택 +1

    // 특수 강화 id
    public const string Kindle = "H5A", Propel = "H5B", Compress = "H5C";
    public const string Runaway = "H10A", Energy = "H10B", Condense = "H10C";
    public const string Accel = "H15A", Leech = "H15B", Critical = "H15C";

    // 특수 강화 확정 수치 (모두 스택당 · 최종 효과량 적용 전)
    const float KindleBurnPct = 0.10f;          // 불씨 점화: 모든 화상 피해량 +10%
    const float PropelMove = 0.05f;             // 열기 추진: 이동속도 +5%
    const float RunawayDotPct = 0.10f;          // 열폭주: 모든 지속 피해 피해량 +10%
    const float EnergyGaugePct = 0.05f;         // 에너지 흡수: 궁극기 게이지 획득량 +5%
    const float CondenseRange = 0.5f;           // 열기 응축: 감지 범위 50% 감소
    const float AccelInterval = 0.05f;          // 가속 연소: 모든 지속 피해 간격 −0.05초 (최소 0.1초)
    const float LeechPct = 0.0005f;             // 이열치열: 모든 피해 흡혈 +0.05%p
    const float FinalEffectBonus = 1f;          // 압축 연료 · 열기 응축 · 임계 과열: 최종 효과량 +100% (서로 합연산)
    const float CriticalHold = 5f;              // 임계 과열: 최대 스택 5초 유지

    int finalUps, cooldownUps, stackUps;
    int stacks;
    float maxHeld;          // 최대 스택을 연속으로 유지한 시간
    bool criticalOn;
    float appliedMoveMul = 1f;
    Mod mod;

    // ---------- 수치 ----------
    /// <summary>최대 스택. 압축 연료면 절반 (올림): 11 → 6</summary>
    public int MaxStacks
    {
        get
        {
            int max = BaseMaxStacks + UpMaxStacks * stackUps;
            return HasSpecial(Compress) ? Mathf.CeilToInt(max * 0.5f) : max;
        }
    }

    public int Stacks => stacks;

    /// <summary>최종 효과량 배율 = (1 + 효과량 증가 합 0) × (1 + 최종 효과량 증가 합)</summary>
    float EffectMul =>
        1f + (HasSpecial(Compress) ? FinalEffectBonus : 0f)
           + (HasSpecial(Condense) ? FinalEffectBonus : 0f)
           + (criticalOn ? FinalEffectBonus : 0f);

    /// <summary>스택당 효과 × 스택 × 최종 효과량</summary>
    float Scaled(float perStack) => perStack * stacks * EffectMul;

    float FinalPerStack => BaseFinalPerStack + UpFinalPerStack * finalUps;
    float CooldownPerStack => BaseCooldownPerStack + UpCooldownPerStack * cooldownUps;
    float DetectRadius => T.absorbDetectRadius * (HasSpecial(Condense) ? 1f - CondenseRange : 1f);

    /// <summary>흡수 가열의 수정자: 최종 피해량 · 화상/지속 피해 피해량 · 쿨타임 · 지속 피해 간격 · 궁극기 게이지</summary>
    class Mod : CombatModifier
    {
        readonly HeatAbsorption h;
        public Mod(HeatAbsorption h) => this.h = h;

        public override float FinalMul(in DamageInfo d) => d.isFixed ? 1f : 1f + h.Scaled(h.FinalPerStack);

        public override float DamagePct(in DamageInfo d)
        {
            float p = 0f;
            if (d.IsBurn && h.HasSpecial(Kindle)) p += h.Scaled(KindleBurnPct);
            if (d.type == DamageType.DoT && h.HasSpecial(Runaway)) p += h.Scaled(RunawayDotPct);
            return p;
        }

        public override float CooldownMul(Skill s) => Mathf.Max(0f, 1f - h.Scaled(h.CooldownPerStack));
        public override float DotIntervalReduce() => h.HasSpecial(Accel) ? h.Scaled(AccelInterval) : 0f;
        public override float UltimateGaugePct() => h.HasSpecial(Energy) ? h.Scaled(EnergyGaugePct) : 0f;
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"최종 피해 강화  <size=16>스택당 최종 피해량 +{UpFinalPerStack * 100:0}%p</size>",
            desc = $"스택당 +{FinalPerStack * 100:0}% → +{(FinalPerStack + UpFinalPerStack) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"쿨타임 강화  <size=16>스택당 쿨타임 감소 +{UpCooldownPerStack * 100:0.0}%p</size>",
            desc = $"스택당 -{CooldownPerStack * 100:0.0}% → -{(CooldownPerStack + UpCooldownPerStack) * 100:0.0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"최대 스택 강화  <size=16>+{UpMaxStacks}</size>",
            desc = $"최대 {MaxStacks} → {(HasSpecial(Compress) ? Mathf.CeilToInt((BaseMaxStacks + UpMaxStacks * (stackUps + 1)) * 0.5f) : MaxStacks + UpMaxStacks)}스택",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: finalUps++; break;
            case 1: cooldownUps++; break;
            case 2: stackUps++; break;
        }
    }

    public override string BasicSummary =>
        $"스택당 최종 +{FinalPerStack * 100:0}% · 쿨 -{CooldownPerStack * 100:0.0}% · 최대 {MaxStacks}스택";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Kindle, title = "불씨 점화", desc = "스택당 모든 화상 피해량 +10%" },
                new SpecialUpgradeOption { id = Propel, title = "열기 추진", desc = "스택당 이동속도 +5%" },
                new SpecialUpgradeOption { id = Compress, title = "압축 연료", desc = "최대 스택 절반 (올림) · 흡수 가열의 모든 최종 효과량 +100%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Runaway, title = "열폭주", desc = "스택당 모든 지속 피해의 피해량 +10%" },
                new SpecialUpgradeOption { id = Energy, title = "에너지 흡수", desc = "스택당 궁극기 게이지 획득량 +5%" },
                new SpecialUpgradeOption { id = Condense, title = "열기 응축", desc = "화상 대상 감지 범위 50% 감소 · 흡수 가열의 모든 최종 효과량 +100%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Accel, title = "가속 연소", desc = "스택당 모든 지속 피해의 피해 간격 0.05초 감소 (최소 0.1초)" },
                new SpecialUpgradeOption { id = Leech, title = "이열치열", desc = "스택당 모든 피해 흡혈 +0.05%p" },
                new SpecialUpgradeOption { id = Critical, title = "임계 과열", desc = "최대 스택을 5초 이상 유지하면 흡수 가열의 모든 최종 효과량 +100% (최대 스택을 잃으면 즉시 해제)" },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 수명 ----------
    void OnEnable()
    {
        mod = new Mod(this);
        CombatMods.Add(mod);
        Enemy.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (mod != null) { CombatMods.Remove(mod); mod = null; }
        Enemy.Damaged -= OnDamaged;
        if (P != null) P.moveSpeedMul /= appliedMoveMul;
        appliedMoveMul = 1f;
    }

    /// <summary>이열치열: 적에게 적용된 피해(내림 후)에 비례해 체력 회복 (특수 피해는 이벤트가 없어 제외)</summary>
    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (!HasSpecial(Leech) || dealt <= 0 || P == null) return;
        P.hp = Mathf.Min(P.maxHp, P.hp + dealt * Scaled(LeechPct));
    }

    // ---------- 진행 ----------
    public override void Tick(float dt)
    {
        // 스택 = 주변의 화상 상태인 적 수
        int count = 0;
        if (FlameSword.I != null && P != null)
        {
            Vector2 c = P.transform.position;
            float r = DetectRadius;
            foreach (var e in Enemy.All)
            {
                if (e == null || e.hp <= 0 || !FlameSword.IsBurning(e)) continue;
                float rr = r + e.radius;
                if (((Vector2)e.transform.position - c).sqrMagnitude < rr * rr) count++;
            }
        }
        int max = MaxStacks;
        stacks = Mathf.Min(count, max);

        // 임계 과열: 최대 스택을 5초 이상 유지 → 켜짐, 최대 스택을 잃는 즉시 해제 (결정 2026-11-10)
        if (HasSpecial(Critical) && max > 0 && stacks >= max)
        {
            maxHeld += dt;
            if (maxHeld >= CriticalHold) criticalOn = true;
        }
        else { maxHeld = 0f; criticalOn = false; }

        // 열기 추진: 이동속도 (흡수 가열 몫만 곱하고 갱신 시 이전 몫을 나눔)
        float move = HasSpecial(Propel) ? 1f + Scaled(PropelMove) : 1f;
        if (P != null && !Mathf.Approximately(move, appliedMoveMul))
        {
            P.moveSpeedMul = P.moveSpeedMul / appliedMoveMul * move;
            appliedMoveMul = move;
        }
    }

    public override string Badge => criticalOn ? $"{stacks}★" : $"{stacks}/{MaxStacks}";
}
