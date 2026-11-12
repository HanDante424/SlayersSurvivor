using System.Collections.Generic;
using UnityEngine;

// 레니아 패시브 3종 (2026-11-12 사용자 전달 기획): 공구 벨트 · 자동화 공정 · 충격 역학.
// 패시브는 공통 기본 강화 대신 고유 강화 3종 (Skill.PassiveBasicOptions).
// 결합 규칙 (Docs/Design/Combat_Decisions_v1.0.md):
//   '피해량' % = 합연산 · '최종 피해량' = 효과마다 독립 배율 곱연산 (그 효과를 올리는 기본 강화는 효과 안에서 합)
//   '쿨타임' 효과 = 독립 배율 곱연산 (기본 강화끼리는 패시브 안에서 합) · '지속 시간' % = 합연산
// "대상이 기절 상태일 때"처럼 대상에 따라 달라지는 '최종 피해량'은 대상 단계(ReceivedMul)에서 곱함 (적중 순간 상태 기준).

/// <summary>
/// LP01 공구 벨트: 모든 '투척' 스킬 (스패너 투척 · 망치 투척) 강화.
/// </summary>
public class ToolBelt : Skill
{
    public static ToolBelt I { get; private set; }

    const float BaseDamage = 0.10f;          // 모든 '투척' 스킬 '피해량' +10%
    const float UpDamage = 0.05f;            // 기본 강화: '투척' '피해량' +5%
    const float UpProjectileRange = 0.10f;   // 기본 강화: '투사체 사거리' +10%
    const float UpCooldown = 0.025f;         // 기본 강화: '쿨타임 감소' +2.5% (모든 스킬)

    public const string SpareTools = "TB5A", PierceId = "TB5B", HeavyTools = "TB5C";
    public const string RicochetId = "TB10A", SkullCrack = "TB10B", RecycleId = "TB10C";
    public const string Overcharge = "TB15A", Designate = "TB15B", Master = "TB15C";

    const float HeavyDamage = 0.30f;         // 묵직한 공구: '피해량' +30%
    const float SkullCrackFinal = 1.3f;      // 골통 부수기: 기절 상태 적에게 '최종 피해량' +30%
    const float RecycleRatio = 0.05f;        // 재활용: 처치 시 그 스킬 남은 쿨타임 -5%
    const int OverchargeEvery = 3, OverchargeShots = 5;
    const float DesignateMul = 1.25f, DesignateSeconds = 5f;   // 표적 지시
    const float MasterFinal = 1.5f, MasterRange = 0.5f;        // 투척 마스터

    int dmgUps, rangeUps, cdUps;
    int throwUses;
    bool overcharged;
    readonly Dictionary<Enemy, float> designated = new Dictionary<Enemy, float>();
    Mod mod;

    // ---------- 다른 스킬이 읽는 값 ----------
    /// <summary>'투사체 사거리' % 합 — 투척 사거리 · 터렛 사거리 모두 (2026-11-12 사용자: 터렛 포함)</summary>
    public float ProjectileRangePct => UpProjectileRange * rangeUps;
    public float ProjectileRangeMul => 1f + ProjectileRangePct;
    public int ExtraThrows => HasSpecial(SpareTools) ? 1 : 0;
    public int ExtraPierce => HasSpecial(PierceId) ? 1 : 0;
    public int ExtraBounces => HasSpecial(RicochetId) ? 1 : 0;
    float ThrowDamagePct => BaseDamage + UpDamage * dmgUps + (HasSpecial(HeavyTools) ? HeavyDamage : 0f);
    float CooldownMulAll => 1f - UpCooldown * cdUps;

    /// <summary>
    /// 투척 스킬 '직접 사용' 1회 (ThrowSkill.Fire). 과잉 장전이면 3회마다 다음 사용에 '투사체 증가' +5 → 이번 사용의 추가 투사체 수를 돌려줌.
    /// [해석] 강화된 사용(+5)은 다음 3회 집계에 포함하지 않음. 스패너 · 망치 사용을 함께 셈
    /// </summary>
    public int OnThrowUse()
    {
        if (!HasSpecial(Overcharge)) return 0;
        if (overcharged) { overcharged = false; return OverchargeShots; }
        if (++throwUses >= OverchargeEvery) { throwUses = 0; overcharged = true; }
        return 0;
    }

    /// <summary>표적 지시: '표적' 상태인지</summary>
    public static bool IsDesignated(Enemy e) =>
        I != null && e != null && I.designated.TryGetValue(e, out var until) && until > Time.time;

    class Mod : CombatModifier
    {
        readonly ToolBelt b;
        public Mod(ToolBelt b) => this.b = b;
        public override float DamagePct(in DamageInfo d) => d.source is ThrowSkill ? b.ThrowDamagePct : 0f;
        public override float FinalMul(in DamageInfo d) => b.HasSpecial(Master) && d.source is ThrowSkill ? MasterFinal : 1f;
        public override float ReceivedMul(Enemy target, in DamageInfo d)
        {
            float m = 1f;
            if (b.HasSpecial(SkullCrack) && d.source is ThrowSkill && target.IsStunned) m *= SkullCrackFinal;
            if (b.HasSpecial(Designate) && d.source is TurretSkill && IsDesignated(target)) m *= DesignateMul;
            return m;
        }
        public override float CooldownMul(Skill s) => b.CooldownMulAll;
        public override float RangePct(Skill s) => b.HasSpecial(Master) && s is ThrowSkill ? MasterRange : 0f;
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption { title = $"투척 피해  <size=16>'투척' '피해량' +{UpDamage * 100:0}%</size>",
            desc = $"투척 피해량 +{ThrowDamagePct * 100:0}% → +{(ThrowDamagePct + UpDamage) * 100:0}%" },
        new PassiveUpgradeOption { title = $"투사체 사거리  <size=16>+{UpProjectileRange * 100:0}%</size>",
            desc = $"투척 · 터렛 사거리 +{ProjectileRangePct * 100:0}% → +{(ProjectileRangePct + UpProjectileRange) * 100:0}%" },
        new PassiveUpgradeOption { title = $"쿨타임 감소  <size=16>모든 스킬 +{UpCooldown * 100:0.#}%</size>",
            desc = $"누적 -{UpCooldown * cdUps * 100:0.#}% → -{UpCooldown * (cdUps + 1) * 100:0.#}%" },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: dmgUps++; break;
            case 1: rangeUps++; break;
            case 2: cdUps++; break;
        }
    }

    public override string BasicSummary =>
        $"투척 피해 +{ThrowDamagePct * 100:0}% · 사거리 ×{ProjectileRangeMul:0.##} · 쿨 -{UpCooldown * cdUps * 100:0.#}%";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = SpareTools, title = "예비 공구", specs = new[] { "'투척' 스킬 '투사체 증가' +1" } },
                new SpecialUpgradeOption { id = PierceId, title = "관통", specs = new[] { "'투척' 스킬 '관통' +1" } },
                new SpecialUpgradeOption { id = HeavyTools, title = "묵직한 공구", specs = new[] { "모든 '투척' 스킬 '피해량' +30%" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = RicochetId, title = "도탄", specs = new[] { "'투척' 스킬 '도탄' +1" } },
                new SpecialUpgradeOption { id = SkullCrack, title = "골통 부수기", specs = new[] { "'기절' 상태인 적에게 '투척' 스킬 '최종 피해량' +30%" } },
                new SpecialUpgradeOption { id = RecycleId, title = "재활용", specs = new[] { "'투척' 스킬로 적 처치 시 해당 스킬 남은 '쿨타임' -5%" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Overcharge, title = "과잉 장전", specs = new[] { "'투척' 스킬 '직접 사용' 3회마다 다음 '투척' 스킬의 '투사체 증가' +5" } },
                new SpecialUpgradeOption { id = Designate, title = "표적 지시",
                    specs = new[] { "'투척' 스킬 '직접 피해' 적용 시 '표적' 부여", "'표적' 상태의 적은 '터렛'에게 받는 '최종 피해량' +25%", "'표적' '지속 시간' 5초" } },
                new SpecialUpgradeOption { id = Master, title = "투척 마스터", specs = new[] { "모든 '투척' 스킬 '최종 피해량' +50%", "모든 '투척' 스킬 '범위' +50%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 수명 ----------
    void OnEnable()
    {
        I = this;
        mod = new Mod(this);
        CombatMods.Add(mod);
        Enemy.Damaged += OnDamaged;
    }

    void OnDisable()
    {
        if (I == this) I = null;
        if (mod != null) { CombatMods.Remove(mod); mod = null; }
        Enemy.Damaged -= OnDamaged;
        designated.Clear();
    }

    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (!(d.source is ThrowSkill throwSkill)) return;
        // 재활용: 이번 피해로 처치 (특수 피해 포함 — 투척 스킬로 처치한 것이면 모두)
        if (HasSpecial(RecycleId) && e.hp <= 0) throwSkill.ReduceRemainingCooldown(RecycleRatio);
        // 표적 지시: '직접 피해'마다 '표적' 5초 ([해석] 다시 걸면 지속 시간 갱신, 효과는 중첩 없음)
        if (HasSpecial(Designate) && d.type == DamageType.Direct && e.hp > 0)
        {
            if (designated.Count > 256) Prune();
            designated[e] = Time.time + DesignateSeconds;
        }
    }

    readonly List<Enemy> dead = new List<Enemy>();
    void Prune()
    {
        dead.Clear();
        foreach (var kv in designated) if (kv.Key == null || kv.Value <= Time.time) dead.Add(kv.Key);
        foreach (var k in dead) designated.Remove(k);
    }
}

/// <summary>
/// LP02 자동화 공정: 모든 '터렛' (표준 · 미사일 · 테슬라) 강화.
/// '지속 시간' · '사거리' · '투사체 증가' · '발동 횟수'는 설치 시점 값, '공격 간격' · '피해량'은 터렛이 매 공격 읽음.
/// </summary>
public class AutomationLine : Skill
{
    public static AutomationLine I { get; private set; }

    const float BaseDuration = 0.20f, BaseRange = 0.10f;   // 모든 '터렛' '지속 시간' +20% · '사거리' +10%
    const float UpDuration = 0.10f;                        // 기본 강화: '지속 시간' +10%
    const float UpCooldown = 0.025f;                       // 기본 강화: '터렛' 스킬 '쿨타임 감소' +2.5%
    const float UpInterval = 0.025f;                       // 기본 강화: '공격 간격' -2.5%

    public const string ChainInstall = "AP5A", QuickStart = "AP5B", Detection = "AP5C";
    public const string Rebuild = "AP10A", SyncId = "AP10B", Fortify = "AP10C";
    public const string MassProduction = "AP15A", LongRun = "AP15B", FullBarrage = "AP15C";

    const float QuickStartCut = 0.15f, QuickStartSeconds = 3f;   // 신속 가동
    const float DetectionRange = 0.30f;                           // 탐지 개선
    const float RebuildRatio = 0.15f;                             // 빠른 재구성
    const float SyncPerTurret = 0.10f;                            // 동기화
    const float FortifyPerSec = 0.10f;                            // 요새화
    const float MassDuration = -0.25f, LongRunDuration = 1.0f;    // 양산 개시 · 장기 가동 '지속 시간'
    const float LongRunCooldown = 1.5f;                           // 장기 가동 '쿨타임 감소' -50% → 쿨타임 ×1.5
    const float FullBarrageInterval = 1.3f;                       // 전탄 발사 '공격 간격' +30%

    int durUps, cdUps, intervalUps;
    Mod mod;

    // ---------- 터렛 · 터렛 스킬이 읽는 값 ----------
    /// <summary>'지속 시간' 배율 = 1 + % 합 (기본 · 기본 강화 · 장기 가동 · 양산 개시)</summary>
    public float DurationMul => Mathf.Max(0.1f, 1f + BaseDuration + UpDuration * durUps
                                          + (HasSpecial(LongRun) ? LongRunDuration : 0f) + (HasSpecial(MassProduction) ? MassDuration : 0f));
    /// <summary>터렛 '사거리' % 합 (기본 · 탐지 개선)</summary>
    public float TurretRangePct => BaseRange + (HasSpecial(Detection) ? DetectionRange : 0f);
    public float TurretRangeMul => 1f + TurretRangePct;
    public int ExtraInstalls => (HasSpecial(ChainInstall) ? 1 : 0) + (HasSpecial(MassProduction) ? 2 : 0);
    public int ExtraShots => HasSpecial(FullBarrage) ? 2 : 0;
    public bool InstantFire => HasSpecial(QuickStart);

    /// <summary>'공격 간격' 배율 (효과끼리 곱: 기본 강화 · 신속 가동(설치 후 3초) · 전탄 발사)</summary>
    public float IntervalMul(Turret t)
    {
        float m = 1f - UpInterval * intervalUps;
        if (HasSpecial(QuickStart) && t.Alive < QuickStartSeconds) m *= 1f - QuickStartCut;
        if (HasSpecial(FullBarrage)) m *= FullBarrageInterval;
        return m;
    }

    /// <summary>터렛 공격 '피해량' % (동기화: 소환된 터렛 수 · 요새화: 이 터렛이 지속된 시간)</summary>
    public float DamagePct(Turret t)
    {
        float p = 0f;
        if (HasSpecial(SyncId)) p += SyncPerTurret * Turret.All.Count;
        if (HasSpecial(Fortify)) p += FortifyPerSec * Mathf.Floor(t.Alive);
        return p;
    }

    public void OnTurretExpired(TurretSkill source)
    {
        if (HasSpecial(Rebuild) && source != null) source.ReduceRemainingCooldown(RebuildRatio);
    }

    class Mod : CombatModifier
    {
        readonly AutomationLine a;
        public Mod(AutomationLine a) => this.a = a;
        public override float CooldownMul(Skill s) =>
            s is TurretSkill ? (1f - UpCooldown * a.cdUps) * (a.HasSpecial(LongRun) ? LongRunCooldown : 1f) : 1f;
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption { title = $"터렛 지속 시간  <size=16>+{UpDuration * 100:0}%</size>",
            desc = $"터렛 지속 시간 ×{DurationMul:0.##} → ×{DurationMul + UpDuration:0.##}" },
        new PassiveUpgradeOption { title = $"터렛 쿨타임 감소  <size=16>+{UpCooldown * 100:0.#}%</size>",
            desc = $"누적 -{UpCooldown * cdUps * 100:0.#}% → -{UpCooldown * (cdUps + 1) * 100:0.#}%" },
        new PassiveUpgradeOption { title = $"터렛 공격 간격  <size=16>-{UpInterval * 100:0.#}%</size>",
            desc = $"누적 -{UpInterval * intervalUps * 100:0.#}% → -{UpInterval * (intervalUps + 1) * 100:0.#}%" },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: durUps++; break;
            case 1: cdUps++; break;
            case 2: intervalUps++; break;
        }
    }

    public override string BasicSummary =>
        $"지속 ×{DurationMul:0.##} · 사거리 ×{TurretRangeMul:0.##} · 쿨 -{UpCooldown * cdUps * 100:0.#}% · 간격 -{UpInterval * intervalUps * 100:0.#}%";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = ChainInstall, title = "연속 설치", specs = new[] { "모든 '설치' 스킬 '발동 횟수' +1" } },
                new SpecialUpgradeOption { id = QuickStart, title = "신속 가동", desc = "'터렛' 소환 즉시 공격합니다.",
                    specs = new[] { "'터렛' 소환 후 '공격 간격' -15%", "'지속 시간' 3초" } },
                new SpecialUpgradeOption { id = Detection, title = "탐지 개선", specs = new[] { "모든 '터렛' '사거리' +30%" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Rebuild, title = "빠른 재구성", specs = new[] { "'터렛' 소멸 시 같은 스킬의 남은 '쿨타임' -15%" } },
                new SpecialUpgradeOption { id = SyncId, title = "동기화", specs = new[] { "소환된 '터렛' 1개당 모든 '터렛' '피해량' +10%" } },
                new SpecialUpgradeOption { id = Fortify, title = "요새화", specs = new[] { "소환된 '터렛'이 지속된 시간 1초당 '피해량' +10%" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = MassProduction, title = "양산 개시", specs = new[] { "모든 '설치' 스킬 '발동 횟수' +2", "'터렛' '지속 시간' -25%" } },
                new SpecialUpgradeOption { id = LongRun, title = "장기 가동", specs = new[] { "'터렛' '지속 시간' +100%", "'설치' 스킬 '쿨타임 감소' -50%" } },
                new SpecialUpgradeOption { id = FullBarrage, title = "전탄 발사", specs = new[] { "모든 '터렛' '투사체 증가' +2", "모든 '터렛' '공격 간격' +30%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    void OnEnable()
    {
        I = this;
        mod = new Mod(this);
        CombatMods.Add(mod);
    }

    void OnDisable()
    {
        if (I == this) I = null;
        if (mod != null) { CombatMods.Remove(mod); mod = null; }
    }
}

/// <summary>
/// LP03 충격 역학: '기절' 강화. 모든 기절(다른 캐릭터 스킬 포함)의 지속 시간에 적용.
/// '기절' 스킬 = 기절을 걸 수 있는 스킬 (망치 투척 · 테슬라 터렛 · 지면 강타 + 기절 특수 강화를 얻은 스패너 투척 · 미사일 터렛 · 포격 실시)
/// </summary>
public class ImpactDynamics : Skill
{
    public static ImpactDynamics I { get; private set; }

    const float BaseStunDuration = 0.10f;    // '기절' '지속 시간' +10%
    const float UpStunDuration = 0.05f;      // 기본 강화: +5%
    const float UpStunnedFinal = 0.025f;     // 기본 강화: '기절' 상태 적에게 '최종 피해량' +2.5% (효과 안에서 합)
    const float UpStunCooldown = 0.025f;     // 기본 강화: '기절' 스킬 '쿨타임 감소' +2.5%

    public const string Migraine = "SD5A", Exposed = "SD5B", AreaShock = "SD5C";
    public const string Skull = "SD10A", Restun = "SD10B", Suppress = "SD10C";
    public const string Overkill = "SD15A", Aftershock = "SD15B", Dominance = "SD15C";

    const float MigraineSlow = 0.7f, MigraineSeconds = 3f;      // 편두통: 이동 속도 감소 30% · 3초
    const float ExposedMul = 1.15f;                              // 약점 노출: 받는 최종 피해량 +15%
    const float AreaShockRadius = 3f, AreaShockStun = 0.5f;      // 범위 충격
    const float SkullPerSec = 0.10f;                             // 부서진 두개골: 기절 지속 시간 1초당 +10%
    const float RestunAdd = 0.1f;                                // 재기절
    const float SuppressRatio = 0.025f;                          // 진압 순환
    const float OverkillMul = 1.3f;                              // 과잉 진압
    const float AftershockRatio = 0.05f;                         // 충격 잔향: 기절 중 받은 최종 피해량의 5%
    const float AftershockRadius = 2f;                           // [임시] 충격 잔향 충격파 반경 (기획에 없음)
    const float DominancePer = 0.05f, DominanceMax = 0.5f;       // 제압우위

    int durUps, finalUps, cdUps;
    readonly Dictionary<Enemy, float> damageWhileStunned = new Dictionary<Enemy, float>();
    Mod mod;

    float StunDurationPct => BaseStunDuration + UpStunDuration * durUps;

    /// <summary>기절을 걸 수 있는 스킬인지 (기본 · 획득한 특수 강화 기준)</summary>
    public static bool IsStunSkill(Skill s) =>
        s is HammerThrow || s is TeslaTurretSummon || s is GroundSlam
        || (s is SpannerThrow sp && sp.HasSpecial(SpannerThrow.HeadBash))
        || (s is MissileTurretSummon mt && mt.HasSpecial(MissileTurretSummon.ArmorPiercing))
        || (s is Bombardment bo && bo.HasSpecial(Bombardment.ArmorPiercing));

    // 제압우위: 기절 상태 적 수 (프레임당 1번만 셈)
    static int countFrame = -1, stunnedCount;
    static int StunnedCount()
    {
        if (countFrame == Time.frameCount) return stunnedCount;
        countFrame = Time.frameCount;
        stunnedCount = 0;
        foreach (var e in Enemy.All) if (e != null && e.hp > 0 && e.IsStunned) stunnedCount++;
        return stunnedCount;
    }

    class Mod : CombatModifier
    {
        readonly ImpactDynamics s;
        public Mod(ImpactDynamics s) => this.s = s;
        public override float StunDurationPct() => s.StunDurationPct;
        public override float StunReapplyAdd() => s.HasSpecial(Restun) ? RestunAdd : 0f;
        public override float CooldownMul(Skill sk) => IsStunSkill(sk) ? 1f - UpStunCooldown * s.cdUps : 1f;

        /// <summary>제압우위: 기절 상태 적 1명당 모든 스킬 '최종 피해량' +5% (최대 +50%)</summary>
        public override float FinalMul(in DamageInfo d) =>
            !d.isFixed && s.HasSpecial(Dominance) ? 1f + Mathf.Min(DominanceMax, DominancePer * StunnedCount()) : 1f;

        /// <summary>기절 상태인 대상에게: 기본 강화 '최종 피해량' · 약점 노출 · 부서진 두개골 · 과잉 진압 (독립 효과 → 곱)</summary>
        public override float ReceivedMul(Enemy target, in DamageInfo d)
        {
            if (!target.IsStunned) return 1f;
            float m = 1f + UpStunnedFinal * s.finalUps;
            if (s.HasSpecial(Exposed)) m *= ExposedMul;
            if (s.HasSpecial(Skull)) m *= 1f + SkullPerSec * target.StunTotal;
            if (s.HasSpecial(Overkill)) m *= OverkillMul;
            return m;
        }
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption { title = $"기절 지속 시간  <size=16>+{UpStunDuration * 100:0}%</size>",
            desc = $"기절 지속 시간 +{StunDurationPct * 100:0}% → +{(StunDurationPct + UpStunDuration) * 100:0}%" },
        new PassiveUpgradeOption { title = $"기절 대상 피해  <size=16>'최종 피해량' +{UpStunnedFinal * 100:0.#}%</size>",
            desc = $"기절 상태 적에게 +{UpStunnedFinal * finalUps * 100:0.#}% → +{UpStunnedFinal * (finalUps + 1) * 100:0.#}%" },
        new PassiveUpgradeOption { title = $"기절 스킬 쿨타임 감소  <size=16>+{UpStunCooldown * 100:0.#}%</size>",
            desc = $"누적 -{UpStunCooldown * cdUps * 100:0.#}% → -{UpStunCooldown * (cdUps + 1) * 100:0.#}%" },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: durUps++; break;
            case 1: finalUps++; break;
            case 2: cdUps++; break;
        }
    }

    public override string BasicSummary =>
        $"기절 +{StunDurationPct * 100:0}% · 기절 대상 +{UpStunnedFinal * finalUps * 100:0.#}% · 쿨 -{UpStunCooldown * cdUps * 100:0.#}%";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Migraine, title = "편두통", keywords = new[] { "상태이상", "이동 속도 감소" },
                    specs = new[] { "'기절'이 끝난 적 '이동 속도 감소' 30% 부여", "'지속 시간' 3초" } },
                new SpecialUpgradeOption { id = Exposed, title = "약점 노출", specs = new[] { "'기절' 상태 적 '받는 최종 피해량' +15%" } },
                new SpecialUpgradeOption { id = AreaShock, title = "범위 충격", keywords = new[] { "간접 사용", "상태이상", "기절" },
                    specs = new[] { "'직접 사용' '기절' 부여 시 주변 가까운 적 1명 '기절' 0.5초" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Skull, title = "부서진 두개골", specs = new[] { "적의 '기절' '지속 시간' 1초당 '받는 최종 피해량' +10%" } },
                new SpecialUpgradeOption { id = Restun, title = "재기절", specs = new[] { "'기절' 상태인 적에게 '기절' 적용 시 '기절' '지속 시간' +0.1초" } },
                new SpecialUpgradeOption { id = Suppress, title = "진압 순환", specs = new[] { "'기절' 상태 적 처치 시 모든 '기절' 스킬의 남은 '쿨타임' -2.5%" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Overkill, title = "과잉 진압", specs = new[] { "'기절' 상태 적 '받는 최종 피해량' +30%" } },
                new SpecialUpgradeOption { id = Aftershock, title = "충격 잔향", keywords = new[] { "간접 사용", "특수 피해" },
                    desc = "'기절' 종료 시 충격파가 발생합니다.", specs = new[] { "'기절' 시간 동안 받은 '최종 피해량'의 5% 만큼 '고정 피해'" } },
                new SpecialUpgradeOption { id = Dominance, title = "제압우위",
                    specs = new[] { "'기절' 상태 적 1명당 모든 스킬 '최종 피해량' +5%", "최대 '최종 피해량' +50%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 수명 ----------
    void OnEnable()
    {
        I = this;
        mod = new Mod(this);
        CombatMods.Add(mod);
        Enemy.Stunned += OnStunned;
        Enemy.StunEnded += OnStunEnded;
        Enemy.Damaged += OnDamaged;
        Enemy.Killed += OnKilled;
    }

    void OnDisable()
    {
        if (I == this) I = null;
        if (mod != null) { CombatMods.Remove(mod); mod = null; }
        Enemy.Stunned -= OnStunned;
        Enemy.StunEnded -= OnStunEnded;
        Enemy.Damaged -= OnDamaged;
        Enemy.Killed -= OnKilled;
        damageWhileStunned.Clear();
    }

    /// <summary>범위 충격: '직접 사용'에서 나온 기절이면 주변 가까운 적 1명 0.5초 (간접 사용 → 다시 퍼지지 않음)</summary>
    void OnStunned(Enemy e, float seconds, bool wasStunned, bool indirect)
    {
        if (indirect || !HasSpecial(AreaShock) || e == null) return;
        var other = LeniaFx.Nearest(e.transform.position, AreaShockRadius, new[] { e });
        if (other != null) other.Stun(AreaShockStun, indirect: true);
    }

    /// <summary>충격 잔향용: 기절 중에 받은 최종 피해량 누적 (실제 들어간 피해)</summary>
    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (!HasSpecial(Aftershock) || dealt <= 0 || !e.IsStunned) return;
        damageWhileStunned.TryGetValue(e, out var acc);
        damageWhileStunned[e] = acc + dealt;
    }

    void OnStunEnded(Enemy e)
    {
        if (e == null || e.hp <= 0) return;
        if (HasSpecial(Migraine)) e.Slow(MigraineSlow, MigraineSeconds);
        if (damageWhileStunned.TryGetValue(e, out var acc))
        {
            damageWhileStunned.Remove(e);
            float amount = acc * AftershockRatio;
            if (HasSpecial(Aftershock) && amount >= 1f)
            {
                Vector2 at = e.transform.position;
                float r = AftershockRadius;
                if (!LeniaFx.Shockwave(at, r)) Fx.Disc(at, r, new Color(0.9f, 0.75f, 0.45f, 0.4f), 0.3f);
                HitDebug.Circle(at, r, HitDebug.BlastColor);
                SkillHits.Circle(at, r, FixedDamage(amount, DamageType.Special, CastKind.Indirect), Enemy.NoKnock);
            }
        }
    }

    void OnKilled(Enemy e)
    {
        bool stunned = e.IsStunned;
        damageWhileStunned.Remove(e);
        if (!stunned || !HasSpecial(Suppress) || P == null) return;
        foreach (var s in P.skills) if (IsStunSkill(s)) s.ReduceRemainingCooldown(SuppressRatio);
    }
}
