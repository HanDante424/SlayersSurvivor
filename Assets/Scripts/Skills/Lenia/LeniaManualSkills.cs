using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 레니아 수동 스킬 4종 + 특수 강화 (2026-11-12 사용자 프로토타입 스펙).
// 공통: 시전이 끝난 뒤 쿨타임 시작 (ActiveSkill.Casting). 기획에 없는 값은 "[임시]".
// 수치 지정용 반경(Radius)은 화면에 표기하지 않음.

/// <summary>
/// LM01 터렛 강화: 설치된 모든 터렛에 10초간 '피해량' +50% · '공격 간격' -10% 부여. 쿨타임 25초.
/// 같은 터렛에 다시 쓰면 계수 곱 · 지속 시간 합 (Turret.Boost)
/// </summary>
public class TurretBoost : ActiveSkill
{
    public const string Lube = "LB5A", Firepower = "LB5B", Durability = "LB5C";
    public const string FocusAim = "LB10A", ExtraFire = "LB10B", LongMaint = "LB10C";
    public const string SyncId = "LB15A", AutoMaintId = "LB15B", ParallelId = "LB15C";

    public const float SyncRatio = 0.02f;          // 동기화: 직접 피해마다 모든 '설치' 스킬 남은 쿨타임 -2%
    public const float ParallelPerTurret = 0.2f;   // 병렬 출력: 강화된 터렛 1개당 최종 피해량 +20%

    const float BaseDuration = 10f, BaseCooldown = 25f;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    public float BuffDuration => BaseDuration + (HasSpecial(LongMaint) ? 5f : 0f);
    public float DamagePct => 0.5f + (HasSpecial(Firepower) ? 0.5f : 0f);
    /// <summary>'공격 간격' 감소 (효과 하나 안에서는 합산)</summary>
    public float IntervalCut => 0.1f + (HasSpecial(Lube) ? 0.1f : 0f);
    public float TurretLifeAdd => HasSpecial(Durability) ? 5f : 0f;
    public float RangeMul => HasSpecial(FocusAim) ? 1.5f : 1f;
    public int ExtraShots => HasSpecial(ExtraFire) ? 1 : 0;
    public bool Sync => HasSpecial(SyncId);
    public bool AutoMaint => HasSpecial(AutoMaintId);
    public bool Parallel => HasSpecial(ParallelId);

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Lube, title = "윤활유", specs = new[] { "'공격 간격' -10% 부여" } },
                new SpecialUpgradeOption { id = Firepower, title = "화력 증강", specs = new[] { "'피해량' +50% 부여" } },
                new SpecialUpgradeOption { id = Durability, title = "내구성 개선", specs = new[] { "'터렛' '지속 시간' +5초 부여" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = FocusAim, title = "초점 맞추기", specs = new[] { "'터렛' '사거리' +50%" } },
                new SpecialUpgradeOption { id = ExtraFire, title = "추가 발사", specs = new[] { "'터렛' '투사체 증가' +1 부여" } },
                new SpecialUpgradeOption { id = LongMaint, title = "장기 정비", specs = new[] { "'지속 시간' +5초" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = SyncId, title = "동기화",
                    desc = "\"터렛 강화\" 상태의 '터렛'이 '직접 피해'를 입힐 때마다 모든 '터렛' 스킬의 남은 '쿨타임'이 감소합니다.",
                    specs = new[] { "남은 '쿨타임' -2%" } },
                new SpecialUpgradeOption { id = AutoMaintId, title = "자동 관리 프로토콜",
                    desc = "\"터렛 강화\" '지속 시간'만큼 '터렛' '지속 시간'을 부여합니다.\n'터렛'마다 1회만 적용됩니다." },
                new SpecialUpgradeOption { id = ParallelId, title = "병렬 출력",
                    specs = new[] { "\"터렛 강화\"가 적용된 '터렛' 1개당 '최종 피해량' +20%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override void Activate()
    {
        P.PlayAttack();
        foreach (var t in Turret.All.ToArray()) if (t != null) t.Boost(this);
        if (!LeniaFx.CastRing(LeniaFx.BoostCast, P.transform, LeniaFx.BoostCastRadius, 10f, 0.89f))
            Fx.Circle(P.transform.position, 0.8f, Fx.FireBright, 0.3f, P.transform);
    }
}

/// <summary>
/// LM04 터렛 과부화: 설치된 모든 터렛에 10초간 '최종 피해량' +50% · '공격 간격' -30% 부여, 터렛 남은 '지속 시간' -50%. 쿨타임 30초.
/// 남은 지속 시간 증감은 곱연산 (냉각 포함). 같은 터렛에 다시 쓰면 계수 곱 · 지속 시간 합 (Turret.Overload)
/// </summary>
public class TurretOverload : ActiveSkill
{
    public const string Amplify = "LO5A", HighSpeed = "LO5B", Cooling = "LO5C";
    public const string ExtraFire = "LO10A", RecycleId = "LO10B", MeltId = "LO10C";
    public const string OverClock = "LO15A", SelfDestructId = "LO15B", FocusFireId = "LO15C";

    public const float RecycleRatio = 0.025f;        // 재활용: 소멸 시 '설치' 스킬 남은 쿨타임 -2.5%
    public const float SelfDestructRatio = 2f;       // 자폭: 터렛 최종 피해량의 200% 고정 피해
    const float SelfDestructBaseRadius = 3f;         // 수치 지정용
    public const float FocusPerStack = 0.05f, FocusDuration = 5f;   // 집중 포화
    public const int FocusMaxStacks = 10;

    const float BaseDuration = 10f, BaseCooldown = 30f;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialCooldownMul => HasSpecial(Cooling) ? 0.9f : 1f;   // 냉각 '쿨타임 감소' +10%

    public float BuffDuration => BaseDuration;
    /// <summary>'최종 피해량' 배율 (효과 하나 안의 +%p 는 합산해 배율 1개)</summary>
    public float FinalMul => 1f + 0.5f + (HasSpecial(Amplify) ? 0.5f : 0f) + (HasSpecial(OverClock) ? 1f : 0f);
    /// <summary>'공격 간격' 감소 (효과 하나 안에서는 합산, 상한은 터렛에서 90%)</summary>
    public float IntervalCut => 0.3f + (HasSpecial(HighSpeed) ? 0.1f : 0f) + (HasSpecial(OverClock) ? 0.5f : 0f);
    /// <summary>터렛 남은 '지속 시간' 배율 (곱연산: -50% · 오버 클럭 -50% · 냉각 +20%)</summary>
    public float LifeMul => 0.5f * (HasSpecial(OverClock) ? 0.5f : 1f) * (HasSpecial(Cooling) ? 1.2f : 1f);
    public int ExtraShots => HasSpecial(ExtraFire) ? 1 : 0;
    public bool Recycle => HasSpecial(RecycleId);
    public bool MeltOn => HasSpecial(MeltId);
    public bool SelfDestruct => HasSpecial(SelfDestructId);
    public bool FocusFire => HasSpecial(FocusFireId);
    public float SelfDestructRadius => SelfDestructBaseRadius * RangeScale;

    /// <summary>자폭 프로토콜 피해: 간접 사용 · 특수 피해 · 고정 피해</summary>
    public DamageInfo SelfDestructDamage(float amount) => FixedDamage(amount, DamageType.Special, CastKind.Indirect);

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Amplify, title = "출력 증폭", specs = new[] { "'최종 피해량' +50% 부여" } },
                new SpecialUpgradeOption { id = HighSpeed, title = "고속 구동", specs = new[] { "'공격 간격' -10% 부여" } },
                new SpecialUpgradeOption { id = Cooling, title = "냉각", specs = new[] { "'터렛' 남은 '지속 시간' +20%", "'쿨타임 감소' +10%" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = ExtraFire, title = "추가 발사", specs = new[] { "'터렛' '투사체 증가' +1 부여" } },
                new SpecialUpgradeOption { id = RecycleId, title = "재활용",
                    desc = "효과를 받고 있는 '터렛'이 소멸하면 '설치' 스킬의 남은 '쿨타임'이 감소합니다.", specs = new[] { "남은 '쿨타임' -2.5%" } },
                new SpecialUpgradeOption { id = MeltId, title = "융해", keywords = new[] { "상태이상" },
                    specs = new[] { "'적중 시' 적의 '받는 최종 피해량' +5% 부여", "'지속 시간' 3초", "'스택' 최대 10" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = OverClock, title = "오버 클럭",
                    specs = new[] { "'터렛' 남은 '지속 시간' -50%", "'최종 피해량' +100% 부여", "'공격 간격' -50% 부여" } },
                new SpecialUpgradeOption { id = SelfDestructId, title = "자폭 프로토콜", keywords = new[] { "간접 사용", "특수 피해" },
                    desc = "효과를 받고 있는 '터렛'이 소멸하면 폭발합니다.", specs = new[] { "'터렛'의 '최종 피해량'의 200%만큼 '고정 피해'" } },
                new SpecialUpgradeOption { id = FocusFireId, title = "집중 포화",
                    specs = new[] { "'적중 시' 해당 '터렛'의 '최종 피해량' +5% 부여", "'지속 시간' 5초", "'스택' 최대 10" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override void Activate()
    {
        P.PlayAttack();
        foreach (var t in Turret.All.ToArray()) if (t != null) t.Overload(this);
        if (!LeniaFx.CastRing(LeniaFx.OverloadCast, P.transform, LeniaFx.OverloadCastRadius))
            Fx.Circle(P.transform.position, 0.8f, Fx.FireDeep, 0.3f, P.transform);
    }
}

/// <summary>
/// LM02 지면 강타: 큰 망치로 지면을 내려쳐 주변 적에게 피해 + '기절' 5초. 초기 피해량 25, 쿨타임 15초.
/// 쿨타임은 내려친 뒤부터
/// </summary>
public class GroundSlam : ActiveSkill
{
    public const string EarthSlam = "LG5A", QuakeId = "LG5B", CrushId = "LG5C";
    public const string AbsorbId = "LG10A", ArmorBreakId = "LG10B", ExpandId = "LG10C";
    public const string Tectonic = "LG15A", CollapseId = "LG15B", ChainShockId = "LG15C";

    const float BaseDamage = 25f, BaseStun = 5f, BaseCooldown = 15f;
    const float BaseRadius = 6f;              // 수치 지정용
    const float BaseKnock = 4f;               // [임시] 밀치기
    const float SwingTime = 0.22f;            // [임시] 내려치는 시간
    // 지진
    const int QuakeCount = 5;
    const float QuakeInterval = 1f, QuakeRatio = 0.2f, QuakeSlowMul = 0.8f;
    const float QuakeArea = 5f;               // 피해 중심에서 지진 발생 반경 (2026-11-12 사용자)
    const float QuakeRadius = 2f;             // [임시] 지진 1회 피해 반경
    const float QuakeSlowSeconds = 2f;        // [임시] 지진 이동 속도 감소 지속 시간
    // 붕괴의 낙인
    const float MarkSeconds = 3f, MarkRatio = 0.3f;
    const float MarkRadius = 2f;              // [임시] 낙인 충격파 반경
    // 연쇄 충격
    const int ChainCount = 3;
    const float ChainRatio = 0.6f, ChainArea = 6f, ChainRadius = 3f;
    const float ChainDelay = 0.15f;           // [임시] 충격파 사이 간격

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialDamageBonus => (HasSpecial(EarthSlam) ? 0.5f : 0f) + (HasSpecial(Tectonic) ? 1f : 0f);
    protected override float SpecialRangeBonus => (HasSpecial(EarthSlam) ? 0.5f : 0f) + (HasSpecial(ExpandId) ? 0.25f : 0f) + (HasSpecial(Tectonic) ? 1f : 0f);
    protected override float SpecialCooldownMul => HasSpecial(Tectonic) ? 1.5f : 1f;

    float Radius => BaseRadius * RangeScale;

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = EarthSlam, title = "대지 강타", specs = new[] { "'피해량' +50%", "'범위' +50%" } },
                new SpecialUpgradeOption { id = QuakeId, title = "지진", keywords = new[] { "간접 사용", "특수 피해", "상태이상", "이동 속도 감소" },
                    desc = "사용 후 지진이 5회 발생해 피해를 입히고 '이동 속도 감소'시킵니다.",
                    specs = new[] { "\"지면 강타\" '최종 피해량'의 20%만큼 '고정 피해'", "'공격 간격' 1초", "'이동 속도 감소' 20% 부여" } },
                new SpecialUpgradeOption { id = CrushId, title = "분쇄의 일격", specs = new[] { "'기절' 상태의 적 '적중 시' '최종 피해량' +50%" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = AbsorbId, title = "충격 흡수", specs = new[] { "'적중 시' 적 1명당 남은 '쿨타임' 2% 감소" } },
                new SpecialUpgradeOption { id = ArmorBreakId, title = "방어구 파괴", keywords = new[] { "상태이상" },
                    specs = new[] { "'적중 시' '받는 최종 피해량' +20%", "'지속 시간' 5초", "'스택' 최대 5" } },
                new SpecialUpgradeOption { id = ExpandId, title = "충격 확장", specs = new[] { "'넉백' +50%", "'범위' +25%" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Tectonic, title = "지각 변동", specs = new[] { "'피해량' +100%", "'범위' +100%", "'쿨타임' +50%" } },
                new SpecialUpgradeOption { id = CollapseId, title = "붕괴의 낙인", keywords = new[] { "간접 사용", "특수 피해" },
                    desc = "'적중 시' 붕괴의 낙인을 부여합니다.\n붕괴의 낙인 지속 시간이 끝나면 충격파가 생깁니다.",
                    specs = new[] { "\"지면 강타\" '최종 피해량'의 30%만큼 '고정 피해'", "붕괴의 낙인 '지속 시간' 3초" } },
                new SpecialUpgradeOption { id = ChainShockId, title = "연쇄 충격", keywords = new[] { "간접 사용", "특수 피해" },
                    desc = "사용 후 공격 위치 주변에 충격파 3개가 발생합니다.",
                    specs = new[] { "\"지면 강타\" '최종 피해량'의 60%만큼 '고정 피해'" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override void Activate()
    {
        Casting = true;   // 내려칠 때까지 쿨타임 대기
        StartCoroutine(Run(DirectCast));
    }

    IEnumerator Run(CastRef use)
    {
        Vector2 at = P.transform.position;
        Vector2 side = P.Facing.x < 0 ? Vector2.left : Vector2.right;
        P.PlayAttack(side);
        // 큰 망치: 손잡이 끝을 축으로 위에서 앞으로 내려침
        var go = new GameObject("GroundSlamHammer");
        go.transform.position = at + side * 0.2f;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = LeniaFx.Hammer(LeniaFx.BigHammerLength, grip: true);
        s.sortingOrder = 12;
        s.flipX = side.x < 0;
        float from = side.x < 0 ? 20f : -20f, to = side.x < 0 ? 95f : -95f;
        for (float t = 0f; t < SwingTime; t += Time.deltaTime)
        {
            float k = t / SwingTime;
            go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Lerp(from, to, k * k));
            yield return null;
        }
        go.transform.rotation = Quaternion.Euler(0, 0, to);

        // 망치가 닿은 자리에서 충격파가 퍼지고, 피해 범위는 플레이어 '주변' (플레이어 중심 원)
        Vector2 impact = at + side * (LeniaFx.BigHammerLength * 0.85f);
        float r = Radius;
        LeniaFx.Spark(impact);
        if (!LeniaFx.Shockwave(at, r))
        {
            Fx.Disc(at, r, new Color(0.9f, 0.75f, 0.45f, 0.45f), 0.35f);
            Fx.Circle(at, r, Fx.FireBright, 0.4f);
        }
        HitDebug.Circle(at, r, HitDebug.BlastColor);
        CameraFollow.Shake(0.2f, 0.18f);

        var dmg = MakeDamage(BaseDamage, use);
        if (HasSpecial(CrushId)) dmg.perTarget = e => e.IsStunned ? 1.5f : 1f;   // 분쇄의 일격: 기절 판정은 기절 부여 전
        float finalDamage = dmg.amount;                                          // "지면 강타 최종 피해량" 참조값
        float knock = BaseKnock * (HasSpecial(ExpandId) ? 1.5f : 1f);
        int hits = SkillHits.Circle(at, r, dmg, knock, e =>
        {
            if (e == null || e.hp <= 0) return;
            if (HasSpecial(CollapseId)) StartCoroutine(CollapseMark(e, finalDamage * MarkRatio));
            if (HasSpecial(ArmorBreakId)) ArmorBreak.Apply(e);
            e.Stun(BaseStun);   // 상태이상 부여가 가장 늦게
        });

        Casting = false;   // 시전 완료 → 쿨타임 시작
        if (HasSpecial(AbsorbId)) for (int i = 0; i < hits; i++) ReduceRemainingCooldown(0.02f);   // 충격 흡수

        if (HasSpecial(QuakeId)) StartCoroutine(Quakes(at, finalDamage * QuakeRatio));
        if (HasSpecial(ChainShockId)) StartCoroutine(ChainShocks(at, finalDamage * ChainRatio));

        yield return new WaitForSeconds(0.25f);
        Destroy(go);
    }

    DamageInfo SpecialFixed(float amount) => FixedDamage(amount, DamageType.Special, CastKind.Indirect);

    /// <summary>지진: 1초 간격 5회, 피해 중심 주변 무작위 위치. 특수 피해 + 이동 속도 감소 부여</summary>
    IEnumerator Quakes(Vector2 center, float amount)
    {
        for (int i = 0; i < QuakeCount; i++)
        {
            yield return new WaitForSeconds(QuakeInterval);
            Vector2 p = center + Random.insideUnitCircle * QuakeArea;
            float r = QuakeRadius * RangeScale;
            if (!LeniaFx.Shockwave(p, r)) Fx.Disc(p, r, new Color(0.7f, 0.55f, 0.35f, 0.4f), 0.3f);
            HitDebug.Circle(p, r, HitDebug.BlastColor);
            CameraFollow.Shake(0.08f, 0.08f);
            SkillHits.Circle(p, r, SpecialFixed(amount), Enemy.NoKnock, e => { if (e.hp > 0) e.Slow(QuakeSlowMul, QuakeSlowSeconds); });
        }
    }

    /// <summary>붕괴의 낙인: 걸릴 때마다 독립된 낙인 1개. 3초 뒤 그 적 위치에서 충격파 (적이 죽었으면 마지막 위치)</summary>
    IEnumerator CollapseMark(Enemy e, float amount)
    {
        Vector2 last = e.transform.position;
        for (float t = 0f; t < MarkSeconds; t += Time.deltaTime)
        {
            if (e != null && e.hp > 0) last = e.transform.position;
            yield return null;
        }
        if (e != null && e.hp > 0) last = e.transform.position;
        float r = MarkRadius * RangeScale;
        if (!LeniaFx.Shockwave(last, r)) Fx.Disc(last, r, new Color(0.85f, 0.6f, 0.3f, 0.45f), 0.3f);
        HitDebug.Circle(last, r, HitDebug.BlastColor);
        SkillHits.Circle(last, r, SpecialFixed(amount), Enemy.NoKnock);
    }

    /// <summary>연쇄 충격: 공격 위치 주변 무작위 위치에 충격파 3개</summary>
    IEnumerator ChainShocks(Vector2 center, float amount)
    {
        for (int i = 0; i < ChainCount; i++)
        {
            yield return new WaitForSeconds(ChainDelay);
            Vector2 p = center + Random.insideUnitCircle * (ChainArea * RangeScale);
            float r = ChainRadius * RangeScale;
            LeniaFx.Spark(p);
            if (!LeniaFx.Shockwave(p, r)) Fx.Disc(p, r, new Color(0.9f, 0.75f, 0.45f, 0.4f), 0.3f);
            HitDebug.Circle(p, r, HitDebug.BlastColor);
            CameraFollow.Shake(0.1f, 0.1f);
            SkillHits.Circle(p, r, SpecialFixed(amount), 2f);
        }
    }
}

/// <summary>
/// LM03 포격 실시: 미사일 10발이 포격 범위 안 무작위 위치에 떨어짐 · 범위 피해. 초기 피해량 15, 쿨타임 10초.
/// '일제 사격': '투사체 증가'에 0.2초 지연을 적용하지 않고, 늘어난 투사체까지 한 번의 사격 시간(VolleyTime) 안에 모두 쏟아냄.
/// 쿨타임은 마지막 미사일을 쏜 뒤부터
/// </summary>
public class Bombardment : ActiveSkill
{
    public const string Burst = "LP5A", HighExplosive = "LP5B", MagFieldId = "LP5C";
    public const string MultiLaunch = "LP10A", ArmorPiercing = "LP10B", NapalmId = "LP10C";
    public const string HomingId = "LP15A", ClusterId = "LP15B", FocusedId = "LP15C";

    const int BaseCount = 10;
    const float BaseDamage = 15f, BaseCooldown = 10f;
    const float BaseArea = 10f, BaseBlast = 3f;   // 수치 지정용 (포격 범위 · 폭발 범위). 포격 범위 15 → 10 (2026-11-12 사용자: 줄이기)
    const float VolleyTime = 1f;                  // [임시] 일제 사격 1회 길이 (투사체가 늘수록 발사 간격이 짧아짐)
    const float DropHeight = 7f, DropSpeed = 14f; // [임시] 낙하 시작 높이 · 속도
    // 자기장 · 작열 지대
    const float FieldSeconds = 3f;                // 2026-11-12 사용자
    const float MagSlowMul = 0.8f;
    const float NapalmDamage = 5f, NapalmInterval = 1f;   // [임시] 작열 지대 피해 간격
    const float ZoneTick = 0.2f;                  // [임시] 자기장 판정 간격
    // 집속탄
    const int ClusterSplit = 3;
    const float ClusterSplitHeight = 6.5f;        // [임시] 분리 높이 (낙하 시작 7 기준 — 떨어지자마자 거의 바로 분리)
    const float ClusterSpread = 1.5f;             // [임시] 분리된 미사일이 퍼지는 반경

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialDamageBonus => (HasSpecial(HighExplosive) ? 0.5f : 0f) + (HasSpecial(ArmorPiercing) ? 0.5f : 0f);
    protected override float SpecialRangeBonus => (HasSpecial(HighExplosive) ? 0.25f : 0f) + (HasSpecial(ClusterId) ? -0.3f : 0f) + (HasSpecial(FocusedId) ? 0.25f : 0f);
    protected override float SpecialFinalDamageMul =>
        (HasSpecial(HomingId) ? 1.25f : 1f) * (HasSpecial(ClusterId) ? 0.6f : 1f) * (HasSpecial(FocusedId) ? 2f : 1f);

    int ExtraShots => (HasSpecial(Burst) ? 10 : 0) + (HasSpecial(MultiLaunch) ? 10 : 0);
    float Area => BaseArea * (HasSpecial(FocusedId) ? 0.5f : 1f);
    float Blast => BaseBlast * RangeScale;

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Burst, title = "연속 발사", specs = new[] { "'투사체 증가' +10" } },
                new SpecialUpgradeOption { id = HighExplosive, title = "고폭", specs = new[] { "'피해량' +50%", "'범위' +25%" } },
                new SpecialUpgradeOption { id = MagFieldId, title = "자기장 발생", keywords = new[] { "상태이상", "이동 속도 감소" },
                    desc = "폭발 범위에 자기장이 생겨 '이동 속도 감소'를 부여합니다.", specs = new[] { "'이동 속도 감소' 20% 부여", "'지속 시간' 3초" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = MultiLaunch, title = "다연장", specs = new[] { "'투사체 증가' +10" } },
                new SpecialUpgradeOption { id = ArmorPiercing, title = "철갑탄", keywords = new[] { "상태이상", "기절" },
                    specs = new[] { "'피해량' +50%", "'기절' +1초" } },
                new SpecialUpgradeOption { id = NapalmId, title = "네이팜탄", keywords = new[] { "간접 피해", "지속 피해" },
                    desc = "폭발 범위에 작열 지대가 생깁니다.", specs = new[] { "'초기 피해량' 5", "'지속 시간' 3초" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = HomingId, title = "호밍 미사일",
                    desc = "범위 안의 적을 노립니다. 겹치지 않게 노리는 것이 우선입니다.\n범위 안에 적이 없으면 무작위 위치에 떨어집니다.",
                    specs = new[] { "'최종 피해량' +25%" } },
                new SpecialUpgradeOption { id = ClusterId, title = "집속탄", desc = "공중에서 미사일이 여러 개로 나뉘어 공격합니다.",
                    specs = new[] { "'최종 피해량' -40%", "'범위' -30%", "분리 '투사체' 3" } },
                new SpecialUpgradeOption { id = FocusedId, title = "집중 포격",
                    specs = new[] { "포격 범위 -50%", "'최종 피해량' +100%", "'폭발 범위' +25%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override void Activate()
    {
        P.PlayAttack();
        Casting = true;   // 마지막 미사일까지 쿨타임 대기
        StartCoroutine(Run(DirectCast));
    }

    IEnumerator Run(CastRef use)
    {
        int n = BaseCount + ExtraShots;
        float interval = VolleyTime / n;   // 일제 사격: 투사체가 늘어도 사격 시간은 그대로
        var used = new HashSet<Enemy>();
        for (int i = 0; i < n; i++)
        {
            if (i > 0) yield return new WaitForSeconds(interval);
            if (P == null) break;
            Vector2 center = P.transform.position;
            Enemy follow = HasSpecial(HomingId) ? LeniaShots.NextTarget(center, Area, used) : null;
            Vector2 target = follow != null ? (Vector2)follow.transform.position : center + Random.insideUnitCircle * Area;
            // 기본 투사체 = 직접 사용, '투사체 증가'분 = 간접 사용 (같은 사용 번호)
            Drop(target, follow, MakeDamage(BaseDamage, i < BaseCount ? use : use.AsIndirect));
        }
        Casting = false;   // 시전 완료 → 쿨타임 시작
    }

    void Drop(Vector2 target, Enemy follow, DamageInfo dmg)
    {
        float r = Blast;
        if (HasSpecial(ClusterId))
        {
            // 집속탄: 운반체가 분리 높이까지 떨어진 뒤 3발로 나뉨
            Vector2 split = target + Vector2.up * ClusterSplitHeight;
            MissileProjectile.Spawn(target + Vector2.up * DropHeight, null, split, DropSpeed, 0f, dmg, fromAbove: true, noBlast: true,
                onLand: at =>
                {
                    Vector2 c = follow != null && follow.hp > 0 ? (Vector2)follow.transform.position : target;
                    for (int k = 0; k < ClusterSplit; k++)
                    {
                        Vector2 p = c + Random.insideUnitCircle * ClusterSpread;
                        LeniaFx.TargetMarker(p, r, ClusterSplitHeight / DropSpeed);
                        MissileProjectile.Spawn(at, null, p, DropSpeed, r, dmg, fromAbove: true, onHit: OnBlastHit, onLand: OnLand);
                    }
                });
            return;
        }
        LeniaFx.TargetMarker(target, r, DropHeight / DropSpeed);   // 낙하 지점 표시
        MissileProjectile.Spawn(target + Vector2.up * DropHeight, follow, target, DropSpeed, r, dmg, fromAbove: true,
                                onHit: OnBlastHit, onLand: OnLand);
    }

    /// <summary>폭발 적중 1회 뒤: 상태이상 부여 (철갑탄 기절)</summary>
    void OnBlastHit(Enemy e)
    {
        if (e == null || e.hp <= 0) return;
        if (HasSpecial(ArmorPiercing)) e.Stun(1f);
    }

    void OnLand(Vector2 at)
    {
        if (this == null) return;
        if (HasSpecial(MagFieldId)) StartCoroutine(MagField(at, Blast));
        if (HasSpecial(NapalmId)) StartCoroutine(Napalm(at, Blast));
    }

    /// <summary>자기장: 3초 동안 범위 안의 적에게 이동 속도 감소 (범위 안에 있는 동안 유지, 매 판정마다 쌓이지 않음)</summary>
    IEnumerator MagField(Vector2 at, float r)
    {
        Fx.GroundDisc(at, r, new Color(0.45f, 0.65f, 1f, 0.2f), FieldSeconds);
        Fx.GroundCircle(at, r, new Color(0.45f, 0.65f, 1f, 0.6f), FieldSeconds);
        var list = new List<Enemy>();
        for (float t = 0f; t < FieldSeconds; t += ZoneTick)
        {
            foreach (var e in SkillHits.InCircle(at, r, list)) e.SustainSlow(MagSlowMul, ZoneTick + 0.1f);
            yield return new WaitForSeconds(ZoneTick);
        }
    }

    /// <summary>네이팜탄 작열 지대: 3초 동안 지속 피해 (간접 사용)</summary>
    IEnumerator Napalm(Vector2 at, float r)
    {
        Fx.GroundDisc(at, r, new Color(1f, 0.45f, 0.1f, 0.35f), FieldSeconds);
        var dmg = MakeDamage(NapalmDamage, CastRef.Indirect, DamageType.DoT);
        for (float elapsed = 0f; elapsed < FieldSeconds - 1e-4f;)
        {
            float iv = CombatMods.DotInterval(NapalmInterval);
            yield return new WaitForSeconds(iv);
            elapsed += iv;
            SkillHits.Circle(at, r, dmg, Enemy.NoKnock);
        }
    }
}
