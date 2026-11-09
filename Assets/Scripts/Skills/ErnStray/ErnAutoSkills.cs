using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 자동 스킬 6종 (기획서 4.2)
// 등급(Power/Tier)은 기획서 그대로, 숫자는 SkillTuning 임시 환산표에서.
// 기획서에 등급조차 없는 값은 각 클래스 맨 위 "[임시]" 상수로 모아 둠.
// Lv.5·10·15 특수 강화는 기획 미설계 → 구현하지 않음.

/// <summary>A01 파이어 슬래시: 범위 좁음 · 쿨 짧음 · 피해 낮음</summary>
public class FireSlash : AutoSkill
{
    const float HalfAngle = 70f;        // [임시] 베기 각도 (좌우)

    protected override float CooldownSeconds => Cooldown(Tier.Short);
    float Radius => Range(Tier.Short);

    protected override bool Fire()
    {
        // [임시 타겟팅] 범위 안의 가장 가까운 적 방향
        Vector2 from = P.transform.position;
        var target = Nearest(from, Radius + 0.8f);
        if (target == null) return false;
        Vector2 dir = ((Vector2)target.transform.position - from).normalized;

        // 기존 수평베기 모션 + 화염 이펙트 (이미 다른 모션 중이면 판정만)
        if (!P.PlayAttack(Player.SlashH, dir)) Fx.Arc(from, dir, Radius, Fx.Fire, 0.15f);
        SkillHits.Sector(from, dir, Radius, HalfAngle, Damage(Power.Low), 2.5f);
        return true;
    }
}

/// <summary>A02 솔라 슬래시: 전방위 · 범위 중간 · 쿨 중간 · 피해 중간</summary>
public class SolarSlash : AutoSkill
{
    protected override float CooldownSeconds => Cooldown(Tier.Mid);
    float Radius => Range(Tier.Mid);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (Nearest(from, Radius) == null) return false;
        P.PlayAttack(Player.SlashV);
        Fx.Circle(from, Radius, Fx.FireBright, 0.3f, P.transform);
        SkillHits.Circle(from, Radius, Damage(Power.Mid), 3f);
        return true;
    }
}

/// <summary>A03 파이어 볼: 투사체 · 사거리 김 · 범위 좁음 · 쿨 짧음 · 피해 낮음</summary>
public class FireBall : AutoSkill
{
    const float Speed = 10f;            // [임시] 투사체 속도
    const int Pierce = 1;               // [임시] 관통 수 (기획 미정 → 1)

    protected override float CooldownSeconds => Cooldown(Tier.Short);
    float MaxRange => Reach(Tier.Long);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        var target = Nearest(from, MaxRange);
        if (target == null) return false;
        Vector2 dir = ((Vector2)target.transform.position - from).normalized;
        // 크기 = 범위 좁음의 일부, 수명 = 사거리 / 속도
        float size = Mathf.Max(0.3f, Range(Tier.Short) * 0.3f);
        Projectile.Spawn(from, dir, Speed, Damage(Power.Low), Pierce, MaxRange / Speed,
                         "FireBall", new Color(1f, 0.55f, 0.15f), size);
        return true;
    }
}

/// <summary>A04 파이어 버스터: 범위 중간 · 쿨 중간 · 피해 중간 · 주변 적 밀치기</summary>
public class FireBuster : AutoSkill
{
    const float Knock = 9f;             // [임시] 밀치기 세기

    protected override float CooldownSeconds => Cooldown(Tier.Mid);
    float Radius => Range(Tier.Mid);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (Nearest(from, Radius) == null) return false;
        Fx.Disc(from, Radius, Fx.FireDeep, 0.25f);
        SkillHits.Circle(from, Radius, Damage(Power.Mid), Knock);
        CameraFollow.Shake(0.08f, 0.08f);
        return true;
    }
}

/// <summary>
/// A05 파이어 블레이드: 범위 중간 · 쿨 김 · 총 피해 높음
/// 범위 내 대상 타겟팅 → 각 대상에 다수 참격 → 참격 종료 후 각 대상 위치 폭발 범위 피해
/// </summary>
public class FireBlade : AutoSkill
{
    const int MaxTargets = 8;           // [임시] 타겟 수 제한 (기획 미정)
    const int SlashCount = 3;           // [임시] 참격 횟수 (기획 미정)
    const float SlashInterval = 0.12f;  // [임시]
    const float SlashShare = 0.6f;      // [임시] 총 피해 중 참격 비중 (나머지는 폭발)
    const float ExplosionRatio = 0.35f; // [임시] 폭발 반경 = 범위 좁음 × 비율 (기획 미정)

    protected override float CooldownSeconds => Cooldown(Tier.Long);
    float Radius => Range(Tier.Mid);

    readonly List<Enemy> found = new List<Enemy>();

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        SkillHits.InCircle(from, Radius, found);
        if (found.Count == 0) return false;

        // 가까운 순으로 제한
        found.Sort((a, b) => ((Vector2)a.transform.position - from).sqrMagnitude
                       .CompareTo(((Vector2)b.transform.position - from).sqrMagnitude));
        var targets = found.GetRange(0, Mathf.Min(MaxTargets, found.Count));
        StartCoroutine(Run(targets));
        return true;
    }

    IEnumerator Run(List<Enemy> targets)
    {
        float total = Damage(Power.High);
        float perSlash = total * SlashShare / SlashCount;
        float explosion = total * (1f - SlashShare);
        float expR = Range(Tier.Short) * ExplosionRatio;

        var lastPos = new Vector2[targets.Count];
        for (int s = 0; s < SlashCount; s++)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                if (e == null || e.hp <= 0) continue;
                lastPos[i] = e.transform.position;
                var d = Random.insideUnitCircle.normalized;
                Fx.Box(lastPos[i] - d * 0.5f, d, 1f, 0.12f, Fx.FireBright, 0.12f);
                e.Hit(perSlash, d, 0.5f);
            }
            yield return new WaitForSeconds(SlashInterval);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            var e = targets[i];
            Vector2 at = e != null ? (Vector2)e.transform.position : lastPos[i];
            if (at == Vector2.zero && e == null) continue;
            Fx.Disc(at, expR, Fx.Fire, 0.25f);
            SkillHits.Circle(at, expR, explosion, 2f);
        }
    }
}

/// <summary>
/// A06 파이어 스톰: 쿨 중간 · 피해 중간 · 최초 범위 좁음 · 최종 밀치기 범위 중간
/// 주변 지속 피해 + 슬로우 → 일정 시간 뒤 폭발하며 밀치기
/// </summary>
public class FireStorm : AutoSkill
{
    const float Duration = 3f;          // [임시] 지속시간 (기획 미정)
    const float TickInterval = 0.5f;    // [임시] 피해 간격 (기획 미정)
    const float SlowMul = 0.5f;         // [임시] 슬로우 비율 (기획 미정)
    const float DotShare = 0.5f;        // [임시] 총 피해 중 지속 피해 비중
    const float Knock = 9f;             // [임시]

    protected override float CooldownSeconds => Cooldown(Tier.Mid);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (Nearest(from, Range(Tier.Short)) == null) return false;
        StartCoroutine(Run());
        return true;
    }

    IEnumerator Run()
    {
        float total = Damage(Power.Mid);
        int ticks = Mathf.Max(1, Mathf.RoundToInt(Duration / TickInterval));
        float perTick = total * DotShare / ticks;
        float rSmall = Range(Tier.Short);

        // [임시 해석] '주변' = 플레이어를 따라다니는 범위
        var ring = Fx.Circle(P.transform.position, rSmall, Fx.FireDeep, Duration, P.transform, 0f);
        for (int i = 0; i < ticks; i++)
        {
            SkillHits.Circle(P.transform.position, rSmall, perTick, 0.3f,
                             e => e.Slow(SlowMul, TickInterval + 0.1f));
            yield return new WaitForSeconds(TickInterval);
        }
        if (ring != null) Destroy(ring.gameObject);

        Vector2 at = P.transform.position;
        float rBig = Range(Tier.Mid);
        Fx.Disc(at, rBig, Fx.Fire, 0.3f);
        SkillHits.Circle(at, rBig, total * (1f - DotShare), Knock);
        CameraFollow.Shake(0.1f, 0.1f);
    }
}
