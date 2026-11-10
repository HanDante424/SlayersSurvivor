using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P02 옥염: '최종 피해량' 패시브 (기획 v1.0 9장 + 결정 기록 7장).
/// - 최종 피해량 효과는 하나하나 독립 배율 → 곱연산. 효과 수치를 올리는 기본 강화(+2%p)는 그 효과 안에서 합연산
///   예) 기본 30% + 2%p × 4 = ×1.38, 극한 연소 ×1.75 → ×2.415
/// - 적용 대상: 고정 피해를 제외한 모든 피해 (직접 · 지속 · 아이템)
/// - 겁화 · 홍련지옥: '화상' 최종 피해량 (화상 태그 피해에만, 서로 곱연산)
/// </summary>
public class Hellfire : Skill
{
    // 기본 수치 (기획 v1.0 확정)
    const float BaseFinal = 0.30f;          // 최종 피해량 +30%

    // 고유 기본 강화
    const float UpFinal = 0.02f;            // 최종 피해 강화: 옥염 자체 최종 피해량 +2%p
    const float UpMoveSpeed = 0.02f;        // 기동력 강화: 이동속도 +2%
    const float UpCooldown = 0.02f;         //               쿨타임 감소 +2% (옥염 안에서 합연산 → 독립 배율 1개)
    const float UpDotDamage = 0.10f;        // 지속 피해 강화: '지속 피해' '피해량' +10%

    // 특수 강화 id
    public const string Inferno = "Y5A", Amplify = "Y5B", Slaughter = "Y5C";
    public const string Crimson = "Y10A", Extreme = "Y10B", Cycle = "Y10C";
    public const string Incinerate = "Y15A", Overburn = "Y15B", Resonance = "Y15C";

    // 특수 강화 확정 수치
    const float InfernoBurn = 0.5f;         // 겁화: 화상 최종 피해량 +50%
    const float AmplifyFinal = 0.3f;        // 화력 증폭: 최종 피해량 +30%
    const float SlaughterPerStack = 0.05f;  // 살육의 열기: 처치 시 최종 피해량 +5%
    const float SlaughterSeconds = 5f;      //               지속 5초 (처치마다 모든 스택 갱신)
    const int SlaughterMax = 10;
    const float CrimsonBurn = 1f;           // 홍련지옥: 화상 최종 피해량 +100%
    const float ExtremeFinal = 0.5f;        // 극화: 최종 피해량 +50%
    const float CycleReduce = 0.05f;        // 불길의 순환: 처치 시 자동·수동·이동 스킬 남은 쿨타임 5% 감소
    const float IncinerateNormal = 0.01f;   // 소각: 지속 피해마다 최대 체력 1% 고정 피해 (보스 0.4%)
    const float IncinerateBoss = 0.004f;
    const float IncinerateMin = 1f;         //       최소 1 (결정 2026-11-10)
    const float OverburnFinal = 0.75f;      // 극한 연소: 최종 피해량 +75%
    const float ResonanceGauge = 0.01f;     // 화염 공명: 직접 발동 적중 시 궁극기 게이지 +1% (발동 1번당 1회)

    int finalUps, mobilityUps, dotUps;
    int slaughterStacks;
    float slaughterLeft;
    float appliedMoveMul = 1f;
    readonly HashSet<int> resonated = new HashSet<int>();
    Mod mod;

    // ---------- 수치 ----------
    /// <summary>옥염 자체 최종 피해 배율 (기본 30% + 강화 2%p 씩, 내부 합연산)</summary>
    float BaseMul => 1f + BaseFinal + UpFinal * finalUps;
    float SlaughterMul => 1f + SlaughterPerStack * slaughterStacks;

    /// <summary>옥염의 최종 피해량 배율 (고정 피해 제외 모든 피해) — 독립 효과끼리 곱연산</summary>
    float FinalMul(in DamageInfo d)
    {
        float m = BaseMul;
        if (HasSpecial(Amplify)) m *= 1f + AmplifyFinal;
        if (HasSpecial(Extreme)) m *= 1f + ExtremeFinal;
        if (HasSpecial(Overburn)) m *= 1f + OverburnFinal;
        if (HasSpecial(Slaughter)) m *= SlaughterMul;
        if (d.IsBurn)
        {
            if (HasSpecial(Inferno)) m *= 1f + InfernoBurn;
            if (HasSpecial(Crimson)) m *= 1f + CrimsonBurn;
        }
        return m;
    }

    /// <summary>전역 수정자: 최종 피해량 · 지속 피해 피해량 · 쿨타임 배율</summary>
    class Mod : CombatModifier
    {
        readonly Hellfire h;
        public Mod(Hellfire h) => this.h = h;
        public override float FinalMul(in DamageInfo d) => d.isFixed ? 1f : h.FinalMul(d);
        public override float DamagePct(in DamageInfo d) => d.type == DamageType.DoT ? UpDotDamage * h.dotUps : 0f;
        public override float CooldownMul(Skill s) => 1f - UpCooldown * h.mobilityUps;
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"최종 피해 강화  <size=16>옥염 최종 피해량 +{UpFinal * 100:0}%p</size>",
            desc = $"옥염 최종 피해량 +{(BaseMul - 1f) * 100:0}% → +{(BaseMul - 1f + UpFinal) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"기동력 강화  <size=16>이동속도 +{UpMoveSpeed * 100:0}% · 쿨타임 감소 +{UpCooldown * 100:0}%</size>",
            desc = $"누적 이동속도 +{UpMoveSpeed * mobilityUps * 100:0}% · 쿨타임 -{UpCooldown * mobilityUps * 100:0}%"
                 + $" → +{UpMoveSpeed * (mobilityUps + 1) * 100:0}% · -{UpCooldown * (mobilityUps + 1) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"지속 피해 강화  <size=16>지속 피해 피해량 +{UpDotDamage * 100:0}%</size>",
            desc = $"누적 +{UpDotDamage * dotUps * 100:0}% → +{UpDotDamage * (dotUps + 1) * 100:0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: finalUps++; break;
            case 1: mobilityUps++; ApplyMoveSpeed(); break;
            case 2: dotUps++; break;
        }
    }

    public override string BasicSummary =>
        $"최종 +{(BaseMul - 1f) * 100:0}% · 이속 +{UpMoveSpeed * mobilityUps * 100:0}% · 쿨 -{UpCooldown * mobilityUps * 100:0}% · 지속 +{UpDotDamage * dotUps * 100:0}%";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Inferno, title = "겁화", desc = "모든 화상 최종 피해량 +50%" },
                new SpecialUpgradeOption { id = Amplify, title = "화력 증폭", desc = "최종 피해량 +30%" },
                new SpecialUpgradeOption { id = Slaughter, title = "살육의 열기", desc = "적 처치 시 최종 피해량 +5% (5초, 최대 10스택, 처치마다 모든 스택 갱신)" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Crimson, title = "홍련지옥", desc = "모든 화상 최종 피해량 +100%" },
                new SpecialUpgradeOption { id = Extreme, title = "극화", desc = "최종 피해량 +50%" },
                new SpecialUpgradeOption { id = Cycle, title = "불길의 순환", desc = "적 처치 시 자동·수동·이동 스킬의 현재 남은 쿨타임 5% 감소" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Incinerate, title = "소각", desc = "지속 피해가 적용될 때마다 대상 최대 체력의 1% (보스 0.4%) 고정 피해 (특수 피해)" },
                new SpecialUpgradeOption { id = Overburn, title = "극한 연소", desc = "최종 피해량 +75%" },
                new SpecialUpgradeOption { id = Resonance, title = "화염 공명", desc = "직접 발동한 스킬이 적중하면 궁극기 게이지 +1% (발동 1번당 1회)" },
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
        Enemy.Killed += OnKilled;
        ApplyMoveSpeed();
    }

    void OnDisable()
    {
        if (mod != null) { CombatMods.Remove(mod); mod = null; }
        Enemy.Damaged -= OnDamaged;
        Enemy.Killed -= OnKilled;
        if (P != null) P.moveSpeedMul /= appliedMoveMul;
        appliedMoveMul = 1f;
        resonated.Clear();
    }

    /// <summary>기동력 강화 이동속도: 옥염 몫만 곱하고 갱신 시 이전 몫을 나눔 (다른 이동속도 효과와 곱연산)</summary>
    void ApplyMoveSpeed()
    {
        if (P == null) return;
        P.moveSpeedMul /= appliedMoveMul;
        appliedMoveMul = 1f + UpMoveSpeed * mobilityUps;
        P.moveSpeedMul *= appliedMoveMul;
    }

    // ---------- 트리거 ----------
    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        // 소각: 지속 피해가 적용될 때마다 (화상 틱 · 폭발 · 일촉즉발 · 타오름 각각). 특수 피해 → 다른 트리거 없음
        if (HasSpecial(Incinerate) && d.type == DamageType.DoT && e.hp > 0)
        {
            float pct = e.kind == EnemyKind.Boss ? IncinerateBoss : IncinerateNormal;
            float amount = Mathf.Max(IncinerateMin, e.maxHp * pct);   // 체력이 낮은 적도 최소 1 (결정 2026-11-10)
            e.Hit(new DamageInfo(amount, DamageType.Special, this, CastKind.Indirect, isFixed: true),
                  Vector2.zero, Enemy.NoKnock);
        }

        // 화염 공명: 직접 발동 1번당 1회, '직접 피해'가 맞았을 때만 (지속 피해 · 고정 피해 · 궁극기 제외)
        if (HasSpecial(Resonance) && d.type == DamageType.Direct && d.cast == CastKind.Direct && d.castId != 0 && !d.isFixed
            && (d.source == null || d.source.Entry == null || d.source.Entry.kind != SkillKind.Ultimate)
            && resonated.Add(d.castId))
        {
            GameManager.I?.AddUltimateGauge(ResonanceGauge);
            if (resonated.Count > 512) { resonated.Clear(); resonated.Add(d.castId); }   // 오래된 발동 번호 정리
        }
    }

    void OnKilled(Enemy e)
    {
        if (HasSpecial(Slaughter))
        {
            slaughterStacks = Mathf.Min(SlaughterMax, slaughterStacks + 1);
            slaughterLeft = SlaughterSeconds;   // 처치마다 모든 스택 갱신
        }
        if (HasSpecial(Cycle) && P != null)
            foreach (var s in P.skills)
            {
                var k = s.Entry.kind;
                if (k == SkillKind.Auto || k == SkillKind.Manual || k == SkillKind.Movement) s.ReduceRemainingCooldown(CycleReduce);
            }
    }

    public override void Tick(float dt)
    {
        if (slaughterStacks > 0)
        {
            slaughterLeft -= dt;
            if (slaughterLeft <= 0f) slaughterStacks = 0;
        }
    }

    public override string Badge => HasSpecial(Slaughter) && slaughterStacks > 0 ? $"살육 {slaughterStacks}" : null;
}
