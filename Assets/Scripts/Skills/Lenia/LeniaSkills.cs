using System.Collections;
using UnityEngine;

// 레니아 스킬 9종 (2026-11-11 사용자 전달 스킬 구성). 기획서에 없는 값은 각 클래스 맨 위 "[임시]" 상수.
// 공통: 피해 = 초기 피해량 (MakeDamage → 피해량 · 최종 피해량 등 전역 규칙 적용). 터렛 공격은 설치(직접 사용)의 간접 사용.

/// <summary>LA01 스패너 투척: 자동 · 기본 지급 · 초기 피해량 10 · 쿨타임 1.5초 · 가장 가까운 대상에게 발사</summary>
public class SpannerThrow : AutoSkill
{
    const float BaseDamage = 10f, BaseCooldown = 1.5f;
    const float Speed = 10f, Spin = 900f;       // [임시] 투척 속도 · 회전 (도/초)
    const float HitRadius = 0.22f;              // [임시]
    const float HandHeight = 0.5f;              // [임시] 던지는 손 높이 (발 위)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float ThrowRange => Reach(Tier.Long);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        var t = LeniaFx.Nearest(from, ThrowRange);
        if (t == null) return false;
        Vector2 hand = from + Vector2.up * HandHeight;
        Vector2 dir = (Vector2)t.transform.position - hand;   // 손 위치에서 조준 (발밑 기준이면 수평 투척이 작은 적을 빗나감)
        ThrownProjectile.Spawn(LeniaFx.Spanner(LeniaFx.SpannerLength * RangeScale, grip: false), hand, dir,
                               Speed, -Spin, ThrowRange * 1.2f, HitRadius * RangeScale, MakeDamage(BaseDamage), 0.6f);
        return true;
    }
}

/// <summary>LA02 망치 투척: 자동 · 초기 피해량 15 · 쿨타임 4초 · 가장 가까운 대상에게 발사 · 적중 시 1초 기절</summary>
public class HammerThrow : AutoSkill
{
    const float BaseDamage = 15f, BaseCooldown = 4f, StunSeconds = 1f;
    const float Speed = 9f, Spin = 720f;        // [임시]
    const float HitRadius = 0.26f;              // [임시]
    const float HandHeight = 0.5f;              // [임시] 던지는 손 높이 (발 위)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float ThrowRange => Reach(Tier.Long);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        var t = LeniaFx.Nearest(from, ThrowRange);
        if (t == null) return false;
        Vector2 hand = from + Vector2.up * HandHeight;
        Vector2 dir = (Vector2)t.transform.position - hand;
        ThrownProjectile.Spawn(LeniaFx.Hammer(LeniaFx.HammerLength * RangeScale, grip: false), hand, dir,
                               Speed, -Spin, ThrowRange * 1.2f, HitRadius * RangeScale, MakeDamage(BaseDamage), 1.2f,
                               e => { if (e != null && e.hp > 0) e.Stun(StunSeconds); });
        return true;
    }
}

/// <summary>
/// 터렛 소환 3종 공통: 자동 · 설치와 동시에 설치 쿨타임 감소 시작 (AutoSkill 기본 동작).
/// [해석] 플레이어 주변 터렛 사거리 안에 적이 있을 때만 설치. 설치 위치 = 플레이어 발밑 근처 무작위
/// </summary>
public abstract class TurretSkill : AutoSkill
{
    const float PlaceSpread = 0.9f;   // [임시] 설치 위치 흩어짐

    protected abstract TurretKind Kind { get; }
    protected abstract float Duration { get; }
    protected abstract float FireCooldown { get; }
    protected abstract float ShotDamage { get; }
    protected virtual float Stun => 0f;
    protected virtual int Bounces => 0;

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (LeniaFx.Nearest(from, Turret.Range * RangeScale) == null) return false;
        var use = DirectCast;
        Vector2 at = from + Random.insideUnitCircle * PlaceSpread;
        // 기본 강화 '범위' = 터렛 사거리 · 미사일 폭발 반경 · 도탄 거리에 적용 (설치 시점 값)
        Turret.Spawn(Kind, at, Duration, FireCooldown, () => MakeDamage(ShotDamage, use.AsIndirect), Stun, Bounces, RangeScale);
        return true;
    }
}

/// <summary>LA03 표준 터렛 소환: 총알 발사 · 초기 피해량 5 · 공격 쿨타임 1초 · 지속시간 5초 · 설치 쿨타임 8초</summary>
public class StandardTurretSummon : TurretSkill
{
    protected override TurretKind Kind => TurretKind.Standard;
    protected override float Duration => 5f;
    protected override float FireCooldown => 1f;
    protected override float ShotDamage => 5f;
    protected override float CooldownSeconds => CooldownOf(8f);
}

/// <summary>LA04 미사일 터렛 소환: 미사일 발사 · 범위 피해 · 초기 피해량 12 · 공격 쿨타임 1.5초 · 지속시간 8초 · 설치 쿨타임 15초</summary>
public class MissileTurretSummon : TurretSkill
{
    protected override TurretKind Kind => TurretKind.Missile;
    protected override float Duration => 8f;
    protected override float FireCooldown => 1.5f;
    protected override float ShotDamage => 12f;
    protected override float CooldownSeconds => CooldownOf(15f);
}

/// <summary>LA05 테슬라 터렛 소환: 초기 피해량 10 · 공격 쿨타임 2초 · 적중 시 1초 기절 · 3회 도탄 · 지속시간 10초 · 설치 쿨타임 12초</summary>
public class TeslaTurretSummon : TurretSkill
{
    protected override TurretKind Kind => TurretKind.Tesla;
    protected override float Duration => 10f;
    protected override float FireCooldown => 2f;
    protected override float ShotDamage => 10f;
    protected override float Stun => 1f;
    protected override int Bounces => 3;   // [해석] 첫 대상 + 도탄 3회 = 최대 4명
    protected override float CooldownSeconds => CooldownOf(12f);
}

/// <summary>LM01 터렛 강화: 수동 · 설치된 모든 터렛 10초간 강화 (스패너가 나사를 조이듯 돌아감). 이펙트만 구현</summary>
public class TurretBoost : ActiveSkill
{
    const float Duration = 10f;
    const float BaseCooldown = 0f;    // 2026-11-11 사용자: 수동 스킬 일단 쿨타임 0초

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate()
    {
        foreach (var t in Turret.All.ToArray()) if (t != null) t.Boost(Duration);
        Fx.Circle(P.transform.position, 0.8f, Fx.FireBright, 0.3f, P.transform);
    }
}

/// <summary>LM04 터렛 과부화: 수동 · 설치된 모든 터렛 10초간 강화 (망치로 내려침). 이펙트만 구현</summary>
public class TurretOverload : ActiveSkill
{
    const float Duration = 10f;
    const float BaseCooldown = 0f;    // 2026-11-11 사용자: 수동 스킬 일단 쿨타임 0초

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate()
    {
        foreach (var t in Turret.All.ToArray()) if (t != null) t.Overload(Duration);
        Fx.Circle(P.transform.position, 0.8f, Fx.FireDeep, 0.3f, P.transform);
    }
}

/// <summary>LM02 지면 강타: 수동 · 큰 망치로 내려쳐 주변 적 기절 3초</summary>
public class GroundSlam : ActiveSkill
{
    const float StunSeconds = 3f;
    const float BaseCooldown = 0f;    // 2026-11-11 사용자: 수동 스킬 일단 쿨타임 0초
    const float SwingTime = 0.22f;    // [임시] 내려치는 시간
    // [해석] 기획에 피해량이 없어 기절만 (피해 0)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => Range(Tier.Mid) * 1.4f;   // [임시] 주변 범위

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 at = P.transform.position;
        Vector2 side = P.Facing.x < 0 ? Vector2.left : Vector2.right;
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

        // 망치가 닿은 자리에서 충격파가 퍼지고, 기절 범위는 플레이어 '주변' (플레이어 중심 원)
        Vector2 impact = at + side * (LeniaFx.BigHammerLength * 0.85f);
        LeniaFx.Spark(impact);
        Fx.Disc(at, Radius, new Color(0.9f, 0.75f, 0.45f, 0.45f), 0.35f);
        Fx.Circle(at, Radius, Fx.FireBright, 0.4f);
        HitDebug.Circle(at, Radius, HitDebug.BlastColor);
        CameraFollow.Shake(0.2f, 0.18f);
        foreach (var e in SkillHits.InCircle(at, Radius, new System.Collections.Generic.List<Enemy>()))
        {
            e.Stun(StunSeconds);
            e.Push((Vector2)e.transform.position - at, 4f);   // [임시] 약한 밀치기
        }
        yield return new WaitForSeconds(0.25f);
        Destroy(go);
    }
}

/// <summary>LM03 포격 실시: 수동 · 위에서 미사일 10개가 무작위 위치에 낙하 · 범위 피해 · 미사일당 초기 피해량 10</summary>
public class Bombardment : ActiveSkill
{
    const int Count = 10;
    const float BaseDamage = 10f;
    const float BaseCooldown = 0f;    // 2026-11-11 사용자: 수동 스킬 일단 쿨타임 0초
    const float Area = 5f;             // [임시] 낙하 위치 = 플레이어 주변 반경
    const float Radius = 1.2f;         // [임시] 미사일 1발 범위
    const float Interval = 0.1f;       // [임시] 발사 간격
    const float DropHeight = 7f, DropSpeed = 14f;   // [임시] 낙하 시작 높이 · 속도

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        var use = DirectCast;
        for (int i = 0; i < Count; i++)
        {
            Vector2 target = (Vector2)P.transform.position + Random.insideUnitCircle * Area;
            Fx.Circle(target, Radius * RangeScale, new Color(1f, 0.3f, 0.2f, 0.5f), DropHeight / DropSpeed);   // 낙하 지점 표시
            MissileProjectile.Spawn(target + Vector2.up * DropHeight, null, target, DropSpeed, Radius * RangeScale,
                                    MakeDamage(BaseDamage, i == 0 ? use : use.AsIndirect), fromAbove: true);
            yield return new WaitForSeconds(Interval);
        }
    }
}
