using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 자동 스킬 6종 (기획서 4.2)
// 등급(Power/Tier)은 기획서 그대로, 숫자는 SkillTuning 임시 환산표에서.
// 기획서에 등급조차 없는 값은 각 클래스 맨 위 "[임시]" 상수로 모아 둠.
// Lv.5·10·15 특수 강화는 기획 미설계 → 구현하지 않음.

/// <summary>
/// 에른 스트레이 스킬 전용 스프라이트 시트 이펙트 (FireForge 제작).
/// 시트가 없으면 Load 가 null → 각 스킬이 기존 도형 이펙트(Fx.*)로 대체.
/// </summary>
public static class ErnSkillFx
{
    public const string FireSlashSheet = "ErnStray_SkillFireSlash";     // 2행 x 4열, 7프레임 (마지막 칸 비어 있음)
    public const string TripleSlashSheet = "ErnStray_SkillTripleSlash"; // 2행 x 5열
    public const string SolarSlashSheet = "ErnStray_SkillSolarSlash";   // 4행 x 3열

    public static Sprite[] FireSlash => SpriteEffect.LoadFrames(FireSlashSheet, 2, 4, 1f, 7);
    public static Sprite[] TripleSlash => SpriteEffect.LoadFrames(TripleSlashSheet, 2, 5, 1f);
    public static Sprite[] SolarSlash => SpriteEffect.LoadFrames(SolarSlashSheet, 4, 3, 1f);

    /// <summary>
    /// 오른쪽을 향해 그려진 시트를 dir 방향으로 띄움. 왼쪽 방향은 뒤집힌 그림이 되지 않도록 좌우 반전.
    /// width = 한 칸의 월드 가로 길이 (LoadFrames 를 1유닛 기준으로 잘랐으므로 scale 로 사용).
    /// </summary>
    public static SpriteEffect SpawnDirected(Sprite[] frames, float fps, Vector2 at, Vector2 dir, float width)
    {
        if (frames == null) return null;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bool flip = dir.x < 0f;
        if (flip) angle -= 180f;
        return SpriteEffect.Spawn(frames, fps, null, at, angle, flip, 11, width);
    }
}

/// <summary>
/// A01 파이어 슬래시 (기획 v0.2): 에른 기본 무장 · 자동 · 쿨타임 3초 · 피해 10 · 바라보는 방향으로만 공격.
/// 대상이 없어도 쿨타임마다 휘두름. 판정 크기는 SkillTuning.fireSlash 에서 플레이 테스트로 조정.
/// </summary>
public class FireSlash : AutoSkill
{
    // 기본 수치 (기획 v0.2 확정 — 임의 변경 금지)
    const float BaseCooldown = 1.5f;
    const float BaseDamage = 10f;

    const float FxFps = 24f;            // [임시] 시트 재생 속도 (7프레임 → 약 0.3초)
    const float FxWidth = 3f;         // [임시] 이펙트 한 칸 가로 = 범위 x 이 값
    const float FxForward = 1.2f;      // [임시] 이펙트 중심 = 플레이어 앞 범위 x 이 값

    static SkillHitbox Hit => T.fireSlash;
    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => RangeOf(Hit.radius);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        Vector2 dir = P.Facing;

        // 수평베기 모션 + 파이어 슬래시 시트 (시트가 없으면 기존 기본 공격 이펙트 / 도형)
        var frames = ErnSkillFx.FireSlash;
        bool played = P.PlayAttack(Player.SlashH, dir, baseFx: frames == null);
        if (frames != null)
            ErnSkillFx.SpawnDirected(frames, FxFps, from + dir * (Radius * FxForward), dir, Radius * FxWidth);
        else if (!played)
            Fx.Arc(from, dir, Radius, Fx.Fire, 0.15f);
        SkillHits.Directed(Hit, from, dir, RangeScale, HitDamage(BaseDamage));   // 모양은 SkillTuning.fireSlash
        return true;
    }
}

/// <summary>
/// A02 솔라 슬래시 (기획 v0.2): 자동 · 쿨타임 5초 · 플레이어 중심 전방위.
/// 베기(피해 5) → 후속 폭발(피해 12). 두 피해는 별개 타격 판정, 순서대로 적용.
/// 베기~폭발 간격은 기획 미정 → SkillTuning.solarExplosionDelay [임시].
/// </summary>
public class SolarSlash : AutoSkill
{
    // 기본 수치 (기획 v0.2 확정 — 임의 변경 금지)
    const float BaseCooldown = 5f;
    const float SlashDamage = 5f;
    const float ExplosionDamage = 12f;

    const float FxFps = 20f;            // [임시] 12프레임 → 0.6초 (원형 베기 → 태양 → 폭발)
    const float FxWidth = 2.6f;         // [임시] 한 칸 가로 = 폭발 범위 x 이 값 (마지막 폭발 고리 ≈ 판정 원)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        float slashR = RangeOf(T.solarSlash.radius), blastR = RangeOf(T.solarExplosion.radius);

        P.PlayAttack(Player.SlashV);
        var frames = ErnSkillFx.SolarSlash;
        if (frames != null)   // 플레이어를 따라다니도록 자식으로
            SpriteEffect.Spawn(frames, FxFps, P.transform, Vector2.zero, 0f, false, 11, blastR * FxWidth);
        else
            Fx.Circle(from, slashR, Fx.FireBright, 0.3f, P.transform);

        // 1) 베기
        SkillHits.Circle(from, slashR, HitDamage(SlashDamage), Enemy.KnockSpeed(T.solarSlash.knockback));
        HitDebug.Circle(from, slashR, HitDebug.SlashColor);
        // 2) 후속 폭발
        StartCoroutine(Explode(frames == null));
        return true;
    }

    IEnumerator Explode(bool drawShape)
    {
        yield return new WaitForSeconds(T.solarExplosionDelay);
        Vector2 at = P.transform.position;   // 이펙트가 플레이어를 따라가므로 폭발도 현재 위치
        float r = RangeOf(T.solarExplosion.radius);
        if (drawShape) Fx.Disc(at, r, Fx.FireBright, 0.25f);
        SkillHits.Circle(at, r, HitDamage(ExplosionDamage), Enemy.KnockSpeed(T.solarExplosion.knockback));
        HitDebug.Circle(at, r, HitDebug.BlastColor);
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
