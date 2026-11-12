using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 레니아 스킬 9종 (2026-11-11 사용자 전달 스킬 구성) + 자동 스킬 특수 강화 (2026-11-12 프로토타입).
// 기획서에 없는 값은 각 클래스 맨 위 "[임시]" 상수.
// 공통: 피해 = 초기 피해량 (MakeDamage → 피해량 · 최종 피해량 등 전역 규칙 적용). 터렛 공격은 설치(직접 사용)의 간접 사용.
// 투사체 공통 규칙 (투사체 증가 · 도탄 · 관통 · 부메랑 · 적중 시 순서) = LeniaShots.cs
// 능력치 증가는 그 스킬에만 적용 ('모든' 이 붙은 효과만 다른 스킬에도).

/// <summary>
/// 투척 스킬 공통 (스패너 · 망치): 가장 가까운 대상에게 던짐. '투사체 증가'분은 0.2초 간격으로 서로 다른 대상에게.
/// 쿨타임은 마지막 투사체를 던진 뒤부터 (사용 완료 후 쿨타임).
/// </summary>
public abstract class ThrowSkill : AutoSkill
{
    protected const float HandHeight = 0.5f;   // [임시] 던지는 손 높이 (발 위)

    protected abstract float BaseDamage { get; }
    protected abstract float Speed { get; }
    protected abstract float Spin { get; }
    protected abstract float HitRadius { get; }
    protected abstract float Knock { get; }
    protected abstract Sprite Sprite { get; }
    /// <summary>'투사체 증가' 합계</summary>
    protected virtual int ExtraShots => 0;
    /// <summary>난사: 무작위 방향</summary>
    protected virtual bool Scatter => false;

    protected float ThrowRange => Reach(Tier.Long);
    protected bool Blocked => DebugCheats.NoAutoSkills || CheatDisabled;

    /// <summary>투사체 1발의 강화 (관통 · 도탄 · 부메랑 · 적중 시)</summary>
    protected abstract ShotMods Mods();

    /// <summary>투사체 1발의 피해 (조건부 피해 증가 = perTarget 은 여기서)</summary>
    protected virtual DamageInfo ShotDamage(CastRef cast) => MakeDamage(BaseDamage, cast);

    protected override bool Fire()
    {
        var t = LeniaFx.Nearest(P.transform.position, ThrowRange);
        if (t == null) return false;
        P.PlayAttack((Vector2)t.transform.position - (Vector2)P.transform.position);
        Casting = true;   // 마지막 투사체까지 쿨타임 대기
        StartCoroutine(Throw(t, DirectCast, 1 + ExtraShots));
        return true;
    }

    IEnumerator Throw(Enemy first, CastRef use, int n)
    {
        var used = new HashSet<Enemy> { first };
        Vector2 lastDir = Vector2.right;
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                yield return new WaitForSeconds(LeniaShots.Interval);
                if (Blocked) break;
            }
            Vector2 hand = (Vector2)P.transform.position + Vector2.up * HandHeight;
            Enemy t = i == 0 ? first : LeniaShots.NextTarget(P.transform.position, ThrowRange, used);
            Vector2 dir;
            if (Scatter) dir = Random.insideUnitCircle.normalized;
            else if (t != null && t.hp > 0) dir = (Vector2)t.transform.position - hand;   // 손 위치에서 조준
            else dir = lastDir;
            if (dir.sqrMagnitude < 0.0001f) dir = lastDir;
            lastDir = dir;
            // 첫 발 = 직접 사용, '투사체 증가'분 = 간접 사용 (같은 사용 번호)
            Shoot(hand, dir, ShotDamage(i == 0 ? use : use.AsIndirect), Mods());
        }
        Casting = false;
    }

    protected void Shoot(Vector2 from, Vector2 dir, DamageInfo dmg, ShotMods mods) =>
        LeniaProjectile.Spawn(Sprite, from, dir, Speed, -Spin, ThrowRange * 1.2f, HitRadius * RangeScale, dmg, Knock, mods);
}

/// <summary>LA01 스패너 투척: 자동 · 기본 지급 · 초기 피해량 10 · 쿨타임 1.5초 · 가장 가까운 대상에게 발사</summary>
public class SpannerThrow : ThrowSkill
{
    const float BaseCooldown = 1.5f;
    protected override float BaseDamage => 10f;
    protected override float Speed => 10f;         // [임시]
    protected override float Spin => 900f;         // [임시] 회전 (도/초)
    protected override float HitRadius => 0.22f;   // [임시]
    protected override float Knock => 0.6f;
    protected override Sprite Sprite => LeniaFx.Spanner(LeniaFx.SpannerLength * RangeScale, grip: false);

    public const string Giant = "LS5A", Ready = "LS5B", ScatterId = "LS5C";
    public const string Ricochet = "LS10A", HeadBash = "LS10B", Pierce = "LS10C";
    public const string Boomerang = "LS15A", BruiseId = "LS15B", Echo = "LS15C";

    const float EchoRatio = 0.6f;   // 죽음의 메아리: 스패너 투척 최종 피해량의 60% 고정 피해
    const int EchoCount = 3;
    const int EchoPerFrame = 4;     // [임시] 한 프레임 최대 메아리 수 (나머지는 다음 프레임)

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Giant, title = "초대형", specs = new[] { "'피해량' +100%", "'범위' +50%", "'쿨타임 감소' -25%" } },
                new SpecialUpgradeOption { id = Ready, title = "준비된 연장", specs = new[] { "'투사체 증가' +2", "'쿨타임 감소' +15%" } },
                new SpecialUpgradeOption { id = ScatterId, title = "난사", desc = "무작위 방향으로 발사합니다.", specs = new[] { "'투사체 증가' +4" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Ricochet, title = "도탄", specs = new[] { "'도탄' +2" } },
                new SpecialUpgradeOption { id = HeadBash, title = "머리 가격", keywords = new[] { "상태이상" }, specs = new[] { "'피해량' +50%", "'적중 시' '기절' +1초" } },
                new SpecialUpgradeOption { id = Pierce, title = "관통", specs = new[] { "'관통' +2" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Boomerang, title = "부메랑", desc = "투사체가 돌아오며 같은 피해를 입힙니다." },
                new SpecialUpgradeOption { id = BruiseId, title = "타박상", keywords = new[] { "상태이상" },
                    specs = new[] { "'적중 시' 적의 '받는 최종 피해량' +5%", "'지속 시간' 5초", "'스택' 최대 10" } },
                new SpecialUpgradeOption { id = Echo, title = "죽음의 메아리", keywords = new[] { "간접 사용", "직접 피해", "원거리", "투사체" },
                    desc = "적 처치 시 무작위 방향으로 투사체 3개를 발사합니다.", specs = new[] { "\"스패너 투척\" '최종 피해량'의 60% 만큼 '고정 피해'" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialDamageBonus => (HasSpecial(Giant) ? 1f : 0f) + (HasSpecial(HeadBash) ? 0.5f : 0f);
    protected override float SpecialRangeBonus => HasSpecial(Giant) ? 0.5f : 0f;
    protected override float SpecialCooldownMul => (HasSpecial(Giant) ? 1.25f : 1f) * (HasSpecial(Ready) ? 0.85f : 1f);
    protected override int ExtraShots => HasSpecial(Ready) ? 2 : HasSpecial(ScatterId) ? 4 : 0;
    protected override bool Scatter => HasSpecial(ScatterId);

    protected override ShotMods Mods() => new ShotMods
    {
        pierce = HasSpecial(Pierce) ? 2 : 0,
        bounces = HasSpecial(Ricochet) ? 2 : 0,
        bounceRange = LeniaShots.BounceRadius * RangeScale,
        boomerang = HasSpecial(Boomerang),
        onHit = OnShotHit,
    };

    /// <summary>적중 시 상태이상 (피해 뒤, 가장 늦게)</summary>
    void OnShotHit(Enemy e, bool wasStunned, DamageInfo d)
    {
        if (e == null || e.hp <= 0) return;
        if (HasSpecial(BruiseId)) Bruise.Apply(e);
        if (HasSpecial(HeadBash)) e.Stun(1f);
    }

    // ---------- 죽음의 메아리 (처치 조건 연쇄) ----------
    struct PendingEcho { public Vector2 at; public int frame; }
    readonly Queue<PendingEcho> echoQueue = new Queue<PendingEcho>();

    void OnEnable() => Enemy.Killed += OnEnemyKilled;
    protected override void OnDisable() { base.OnDisable(); Enemy.Killed -= OnEnemyKilled; echoQueue.Clear(); }

    void OnEnemyKilled(Enemy e)
    {
        if (!HasSpecial(Echo) || Blocked) return;
        echoQueue.Enqueue(new PendingEcho { at = e.transform.position, frame = Time.frameCount });
    }

    public override void Tick(float dt)
    {
        int budget = EchoPerFrame;
        while (budget > 0 && echoQueue.Count > 0 && echoQueue.Peek().frame < Time.frameCount)   // 처치 다음 프레임부터
        {
            var next = echoQueue.Dequeue();
            if (Blocked || !HasSpecial(Echo)) continue;
            // 간접 사용 · 직접 피해 · 고정 피해 (관통 · 도탄 · 적중 시 효과는 유지, 부메랑은 제외)
            var dmg = FixedDamage(MakeDamage(BaseDamage, CastRef.Indirect).amount * EchoRatio, DamageType.Direct, CastKind.Indirect);
            var m = Mods();
            m.boomerang = false;
            for (int i = 0; i < EchoCount; i++) Shoot(next.at, Random.insideUnitCircle.normalized, dmg, m);
            budget--;
        }
        base.Tick(dt);
    }
}

/// <summary>LA02 망치 투척: 자동 · 초기 피해량 15 · 쿨타임 4초 · 가장 가까운 대상에게 발사 · 적중 시 1초 기절</summary>
public class HammerThrow : ThrowSkill
{
    const float BaseCooldown = 4f, BaseStun = 1f;
    protected override float BaseDamage => 15f;
    protected override float Speed => 9f;          // [임시]
    protected override float Spin => 720f;         // [임시]
    protected override float HitRadius => 0.26f;   // [임시]
    protected override float Knock => 1.2f;
    protected override Sprite Sprite => LeniaFx.Hammer(LeniaFx.HammerLength * RangeScale, grip: false);

    public const string Skull = "LH5A", Ready = "LH5B", ScatterId = "LH5C";
    public const string BruiseId = "LH10A", Crush = "LH10B", ChainShock = "LH10C";
    public const string Reinforced = "LH15A", Barrage = "LH15B", Maintenance = "LH15C";

    // 수치 지정용 (화면에 표기하지 않음)
    const float ChainShockRadius = 3f;
    const float ShockwaveRadius = 4f;
    const float ShockwaveRatio = 0.75f, BarrageRatio = 0.6f;
    const int BarrageCount = 2;
    const float BarrageDelay = 0.15f;   // [임시] 추가 망치 간격

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Skull, title = "두개골 함몰", keywords = new[] { "상태이상" }, specs = new[] { "'피해량' +25%", "'기절' +2초" } },
                new SpecialUpgradeOption { id = Ready, title = "준비된 연장", keywords = new[] { "간접 사용", "직접 피해", "원거리", "투사체" },
                    specs = new[] { "'투사체 증가' +1", "'쿨타임 감소' +25%" } },
                new SpecialUpgradeOption { id = ScatterId, title = "난사", keywords = new[] { "간접 사용", "직접 피해", "원거리", "투사체" },
                    desc = "무작위 방향으로 발사합니다.", specs = new[] { "'투사체 증가' +2" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = BruiseId, title = "타박상", keywords = new[] { "상태이상" },
                    specs = new[] { "'적중 시' 적의 '받는 최종 피해량' +5%", "'지속 시간' 5초", "'스택' 최대 10" } },
                new SpecialUpgradeOption { id = Crush, title = "분쇄 타격", specs = new[] { "'기절' 상태 적에게 '적중 시' '최종 피해량' +100%" } },
                new SpecialUpgradeOption { id = ChainShock, title = "연쇄 충격", keywords = new[] { "상태이상" }, specs = new[] { "'적중 시' 주변 적의 '기절' +1초" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Reinforced, title = "강화 타격", keywords = new[] { "간접 사용", "특수 피해", "상태이상", "기절" },
                    desc = "'적중 시' 충격파가 발생합니다.",
                    specs = new[] { "\"망치 투척\" '최종 피해량'의 75% 만큼 '고정 피해'", "충격파 '적중 시' '기절' +1초" } },
                new SpecialUpgradeOption { id = Barrage, title = "연속 강타", keywords = new[] { "간접 사용", "특수 피해" },
                    desc = "'적중 시' 추가로 망치 2개가 내려찍습니다.", specs = new[] { "\"망치 투척\" '최종 피해량'의 60% 만큼 '고정 피해'" } },
                new SpecialUpgradeOption { id = Maintenance, title = "긴급 정비",
                    specs = new[] { "'적중 시' 모든 '설치' 스킬의 남은 '쿨타임' -5%", "-> '기절' 상태 적에게 '적중 시' 남은 '쿨타임' -10%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialDamageBonus => HasSpecial(Skull) ? 0.25f : 0f;
    protected override float SpecialCooldownMul => HasSpecial(Ready) ? 0.75f : 1f;
    protected override int ExtraShots => HasSpecial(Ready) ? 1 : HasSpecial(ScatterId) ? 2 : 0;
    protected override bool Scatter => HasSpecial(ScatterId);
    float StunSeconds => BaseStun + (HasSpecial(Skull) ? 2f : 0f);

    /// <summary>분쇄 타격: 피해 계산 시점에 대상이 기절 상태면 최종 피해량 ×2 (이번 망치의 기절 부여보다 먼저)</summary>
    protected override DamageInfo ShotDamage(CastRef cast)
    {
        var d = MakeDamage(BaseDamage, cast);
        if (HasSpecial(Crush)) d.perTarget = e => e.IsStunned ? 2f : 1f;
        return d;
    }

    protected override ShotMods Mods() => new ShotMods { onHit = OnShotHit };

    void OnShotHit(Enemy e, bool wasStunned, DamageInfo d)
    {
        if (e == null) return;
        Vector2 at = e.transform.position;

        // 긴급 정비: 모든 '설치' 스킬의 남은 쿨타임 감소 (기절 상태 적이면 대신 -10%)
        if (HasSpecial(Maintenance) && P != null)
            foreach (var s in P.skills)
                if (s is TurretSkill) s.ReduceRemainingCooldown(wasStunned ? 0.10f : 0.05f);

        // 강화 타격: 충격파 = 특수 피해 (적중 시 효과를 다시 발동시키지 않음), 충격파 자체의 기절 +1초
        if (HasSpecial(Reinforced))
        {
            float r = ShockwaveRadius * RangeScale;
            if (!LeniaFx.Shockwave(at, r)) Fx.Circle(at, r, new Color(0.9f, 0.75f, 0.45f, 0.8f), 0.3f);
            LeniaFx.Spark(at);
            HitDebug.Circle(at, r, HitDebug.BlastColor);
            CameraFollow.Shake(0.08f, 0.08f);
            SkillHits.Circle(at, r, LeniaShots.SameAsFixed(d, ShockwaveRatio, DamageType.Special), 1.5f, x => x.Stun(1f));
        }

        // 연속 강타: 같은 대상에게 망치 2개 (단일 · 특수 피해)
        if (HasSpecial(Barrage) && e.hp > 0) StartCoroutine(Smash(e, LeniaShots.SameAsFixed(d, BarrageRatio, DamageType.Special)));

        // 상태이상 부여 (가장 늦게)
        if (e.hp > 0)
        {
            if (HasSpecial(BruiseId)) Bruise.Apply(e);
            e.Stun(StunSeconds);
        }
        if (HasSpecial(ChainShock))
            foreach (var o in SkillHits.InCircle(at, ChainShockRadius * RangeScale, new List<Enemy>()))
                if (o != e) o.Stun(1f);
    }

    IEnumerator Smash(Enemy e, DamageInfo dmg)
    {
        for (int i = 0; i < BarrageCount; i++)
        {
            yield return new WaitForSeconds(BarrageDelay);
            if (e == null || e.hp <= 0) yield break;
            Vector2 at = e.transform.position;
            StartCoroutine(DropHammer(at));
            e.Hit(dmg, Vector2.down, Enemy.NoKnock);
        }
    }

    /// <summary>위에서 망치가 내려찍는 연출 (피해 없음)</summary>
    IEnumerator DropHammer(Vector2 at)
    {
        var go = new GameObject("BarrageHammer");
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = LeniaFx.Hammer(LeniaFx.HammerLength * 1.4f, grip: false);
        s.sortingOrder = 12;
        go.transform.rotation = Quaternion.Euler(0, 0, 180f);   // 머리가 아래
        for (float t = 0f; t < 0.1f; t += Time.deltaTime)
        {
            go.transform.position = at + Vector2.up * Mathf.Lerp(1.2f, 0.2f, t / 0.1f);
            yield return null;
        }
        LeniaFx.Spark(at);
        Destroy(go, 0.08f);
    }
}

/// <summary>
/// 터렛 설치 3종 공통: 자동 · 설치와 동시에 설치 쿨타임 감소 시작 (AutoSkill 기본 동작).
/// 플레이어 주변 터렛 사거리 안에 적이 있을 때만 설치. 설치 위치 = 플레이어 주변 PlaceArea 안 무작위.
/// '발동 횟수' = 한 번에 설치하는 터렛 수.
/// 공통 특수 강화: Lv.10 추가 설치 · 급속 전개, Lv.15 최대한 많이 (각 터렛 SpecialOptions 에 포함)
/// </summary>
public abstract class TurretSkill : AutoSkill
{
    public const float PlaceArea = 6f;   // 수치 지정용 (화면에 표기하지 않음)

    public const string ExtraInstall = "LT10A", RapidDeploy = "LT10B", AsManyAsPossible = "LT15A";

    protected abstract TurretKind Kind { get; }
    protected abstract float BaseDuration { get; }
    protected abstract float BaseFireCooldown { get; }
    protected abstract float BaseShotDamage { get; }
    protected abstract float BaseInstallCooldown { get; }
    protected virtual float Stun => 0f;
    protected virtual int Bounces => 0;
    protected virtual int Pierce => 0;
    protected virtual int ExtraShots => 0;
    protected virtual bool Scatter => false;
    protected virtual bool Orbital => false;
    protected virtual float FireCooldownMul => 1f;

    protected override float CooldownSeconds => CooldownOf(BaseInstallCooldown);

    /// <summary>'발동 횟수' 증가 합계</summary>
    int ExtraInstalls => (HasSpecial(ExtraInstall) ? 1 : 0) + (HasSpecial(RapidDeploy) ? 2 : 0) + (HasSpecial(AsManyAsPossible) ? 2 : 0);
    float Duration => BaseDuration * (HasSpecial(RapidDeploy) ? 0.5f : 1f);

    protected static SpecialUpgradeOption ExtraInstallOption => new SpecialUpgradeOption { id = ExtraInstall, title = "추가 설치", specs = new[] { "'발동 횟수' +1" } };
    protected static SpecialUpgradeOption RapidDeployOption => new SpecialUpgradeOption { id = RapidDeploy, title = "급속 전개", specs = new[] { "'발동 횟수' +2", "'지속 시간' -50%" } };
    protected static SpecialUpgradeOption AsManyOption => new SpecialUpgradeOption { id = AsManyAsPossible, title = "최대한 많이", specs = new[] { "'발동 횟수' +2" } };

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (LeniaFx.Nearest(from, Turret.Range * RangeScale) == null) return false;
        P.PlayAttack();
        var use = DirectCast;
        // 기본 강화 '범위' = 터렛 사거리 · 미사일 폭발 반경 · 도탄 거리에 적용 (설치 시점 값)
        var spec = new TurretSpec
        {
            kind = Kind, duration = Duration, fireCooldown = BaseFireCooldown * FireCooldownMul, baseFireCooldown = BaseFireCooldown,
            rangeScale = RangeScale,
            shot = (pct, fin) => MakeDamage(BaseShotDamage, use.AsIndirect, pct, fin),
            shots = 1 + ExtraShots, scatter = Scatter, pierce = Pierce, bounces = Orbital ? 0 : Bounces, stun = Stun, orbital = Orbital,
        };
        for (int i = 0; i < 1 + ExtraInstalls; i++)
            Turret.Spawn(spec, from + Random.insideUnitCircle * PlaceArea);
        return true;
    }
}

/// <summary>LA03 표준 터렛 설치: 총알 발사 · 초기 피해량 5 · 공격 간격 1초 · 지속시간 5초 · 설치 쿨타임 8초</summary>
public class StandardTurretSummon : TurretSkill
{
    public const string Burst = "LST5A", Ricochet = "LST5B", ScatterId = "LST5C";
    public const string MachineGun = "LST10C";
    public const string Minigun = "LST15B", PierceRound = "LST15C";

    protected override TurretKind Kind => TurretKind.Standard;
    protected override float BaseDuration => 5f;
    protected override float BaseFireCooldown => 1f;
    protected override float BaseShotDamage => 5f;
    protected override float BaseInstallCooldown => 8f;

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Burst, title = "연속 발사", specs = new[] { "'투사체 증가' +2" } },
                new SpecialUpgradeOption { id = Ricochet, title = "도탄", specs = new[] { "'도탄' +2" } },
                new SpecialUpgradeOption { id = ScatterId, title = "난사", desc = "무작위 방향으로 발사합니다.", specs = new[] { "'투사체 증가' +4" } },
            };
            case 10: return new[]
            {
                ExtraInstallOption, RapidDeployOption,
                new SpecialUpgradeOption { id = MachineGun, title = "기관총", specs = new[] { "'투사체 증가' +2" } },
            };
            case 15: return new[]
            {
                AsManyOption,
                new SpecialUpgradeOption { id = Minigun, title = "미니건", specs = new[] { "'투사체 증가' +3" } },
                new SpecialUpgradeOption { id = PierceRound, title = "관통탄", specs = new[] { "'관통' +2", "'피해량' +25%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override int ExtraShots => (HasSpecial(Burst) ? 2 : 0) + (HasSpecial(ScatterId) ? 4 : 0)
                                       + (HasSpecial(MachineGun) ? 2 : 0) + (HasSpecial(Minigun) ? 3 : 0);
    protected override bool Scatter => HasSpecial(ScatterId);
    protected override int Bounces => HasSpecial(Ricochet) ? 2 : 0;
    protected override int Pierce => HasSpecial(PierceRound) ? 2 : 0;
    protected override float SpecialDamageBonus => HasSpecial(PierceRound) ? 0.25f : 0f;
}

/// <summary>LA04 미사일 터렛 설치: 미사일 발사 · 범위 피해 · 초기 피해량 12 · 공격 간격 1.5초 · 지속시간 8초 · 설치 쿨타임 15초</summary>
public class MissileTurretSummon : TurretSkill
{
    public const string Burst = "LMT5A", ArmorPiercing = "LMT5B", ScatterId = "LMT5C";
    public const string MultiLaunch = "LMT10C";
    public const string FullSalvo = "LMT15B", Orbit = "LMT15C";

    protected override TurretKind Kind => TurretKind.Missile;
    protected override float BaseDuration => 8f;
    protected override float BaseFireCooldown => 1.5f;
    protected override float BaseShotDamage => 12f;
    protected override float BaseInstallCooldown => 15f;

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Burst, title = "연속 발사", specs = new[] { "'투사체 증가' +1" } },
                new SpecialUpgradeOption { id = ArmorPiercing, title = "철갑탄", keywords = new[] { "상태이상" }, specs = new[] { "'피해량' +25%", "'기절' +1초" } },
                new SpecialUpgradeOption { id = ScatterId, title = "난사", desc = "무작위 방향으로 발사합니다.", specs = new[] { "'투사체 증가' +2" } },
            };
            case 10: return new[]
            {
                ExtraInstallOption, RapidDeployOption,
                new SpecialUpgradeOption { id = MultiLaunch, title = "다연장", specs = new[] { "'투사체 증가' +2" } },
            };
            case 15: return new[]
            {
                AsManyOption,
                new SpecialUpgradeOption { id = FullSalvo, title = "전탄발사", specs = new[] { "'투사체 증가' +3" } },
                new SpecialUpgradeOption { id = Orbit, title = "궤도 폭격", desc = "미사일을 위로 발사합니다.\n플레이어 주변에 미사일이 무작위로 떨어집니다.",
                    specs = new[] { "'피해량' +50%", "'범위' +50%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override int ExtraShots => (HasSpecial(Burst) ? 1 : 0) + (HasSpecial(ScatterId) ? 2 : 0)
                                       + (HasSpecial(MultiLaunch) ? 2 : 0) + (HasSpecial(FullSalvo) ? 3 : 0);
    protected override bool Scatter => HasSpecial(ScatterId);
    protected override bool Orbital => HasSpecial(Orbit);
    protected override float Stun => HasSpecial(ArmorPiercing) ? 1f : 0f;
    protected override float SpecialDamageBonus => (HasSpecial(ArmorPiercing) ? 0.25f : 0f) + (HasSpecial(Orbit) ? 0.5f : 0f);
    protected override float SpecialRangeBonus => HasSpecial(Orbit) ? 0.5f : 0f;
}

/// <summary>LA05 테슬라 터렛 설치: 초기 피해량 10 · 공격 간격 2초 · 적중 시 1초 기절 · 3회 도탄 · 지속시간 10초 · 설치 쿨타임 12초</summary>
public class TeslaTurretSummon : TurretSkill
{
    public const string Chain = "LTT5A", No2 = "LTT5B", ExtraCurrent = "LTT5C";
    public const string No3 = "LTT10C";
    public const string No2Plus = "LTT15B", No3Sky = "LTT15C";

    const int BaseBounces = 3;   // 첫 대상 + 도탄 3회

    protected override TurretKind Kind => TurretKind.Tesla;
    protected override float BaseDuration => 10f;
    protected override float BaseFireCooldown => 2f;
    protected override float BaseShotDamage => 10f;
    protected override float BaseInstallCooldown => 12f;

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Chain, title = "연쇄 작용", specs = new[] { "'도탄' +2" } },
                new SpecialUpgradeOption { id = No2, title = "고압 전류", keywords = new[] { "상태이상" }, specs = new[] { "'피해량' +25%", "'기절' +1초" } },
                new SpecialUpgradeOption { id = ExtraCurrent, title = "추가 전류", specs = new[] { "'투사체 증가' +2" } },
            };
            case 10: return new[]
            {
                ExtraInstallOption, RapidDeployOption,
                new SpecialUpgradeOption { id = No3, title = "전류 증폭", specs = new[] { "'투사체 증가' +2" } },
            };
            case 15: return new[]
            {
                AsManyOption,
                new SpecialUpgradeOption { id = No2Plus, title = "고속 방전", specs = new[] { "'투사체 증가' +2", "'공격 간격' -20%" } },
                new SpecialUpgradeOption { id = No3Sky, title = "낙뢰 발생", desc = "전류를 위로 발사합니다.\n플레이어 주변에 번개가 떨어져 주변 적에게 피해를 입힙니다.\n더 이상 '도탄' 되지 않습니다.",
                    specs = new[] { "'피해량' +25%", "'범위' +25%", "'도탄' 1당 '최종 피해량', '범위' +25%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    /// <summary>도탄 수 (연쇄 작용 포함). 3번(Lv.15)은 도탄 대신 이 수만큼 최종 피해량 · 범위 증가</summary>
    int BounceCount => BaseBounces + (HasSpecial(Chain) ? 2 : 0);

    protected override int Bounces => BounceCount;
    protected override bool Orbital => HasSpecial(No3Sky);
    protected override int ExtraShots => (HasSpecial(ExtraCurrent) ? 2 : 0) + (HasSpecial(No3) ? 2 : 0) + (HasSpecial(No2Plus) ? 2 : 0);
    protected override float Stun => 1f + (HasSpecial(No2) ? 1f : 0f);
    protected override float FireCooldownMul => HasSpecial(No2Plus) ? 0.8f : 1f;
    protected override float SpecialDamageBonus => (HasSpecial(No2) ? 0.25f : 0f) + (HasSpecial(No3Sky) ? 0.25f : 0f);
    protected override float SpecialRangeBonus => HasSpecial(No3Sky) ? 0.25f + 0.25f * BounceCount : 0f;
    protected override float SpecialFinalDamageMul => HasSpecial(No3Sky) ? 1f + 0.25f * BounceCount : 1f;
}
