using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 수동 5종 · 이동 1종 · 궁극기 1종 (기획서 5~7장, 수치 = 2026-11-11 스킬 설명 중간본)
// 방향은 플레이어가 마지막으로 이동한 방향(Facing) 기준.
// 기획서에 없는 값은 각 클래스 맨 위 "[임시]" 상수.

/// <summary>M02 파이어 차지: 주변 적을 밀치며 돌진 · 사용 중 무적 · 초기 피해량 4 · 쿨타임 15초</summary>
public class FireCharge : ActiveSkill
{
    const float BaseDamage = 4f, BaseCooldown = 15f;
    const float Distance = 2.67f;       // [임시] 돌진 거리 (기획 미정). 카메라 시야 7 복귀에 맞춰 4 × 2/3
    const float DashTime = 0.25f;       // [임시]
    const float Knock = 8f;             // [임시]

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    protected override void Activate()
    {
        hit.Clear();
        float r = Range(Tier.Mid) * 0.5f;   // [임시] 돌진 중 판정은 범위 중간의 절반
        DamageInfo dmg = MakeDamage(BaseDamage);
        Fx.Circle(P.transform.position, r, Fx.Fire, DashTime + 0.1f, P.transform, 0f);
        P.Dash(P.Facing, Distance, DashTime, true,
            onStep: () =>
            {
                Vector2 at = P.transform.position;
                foreach (var e in SkillHits.InCircle(at, r, found))
                {
                    if (!hit.Add(e)) continue;
                    Vector2 d = (Vector2)e.transform.position - at;
                    e.Hit(dmg, d, 0f);
                    e.Push(d, Knock);
                }
            },
            onEnd: () =>
            {
                // 도착 지점에서 주변 적 밀치기
                Vector2 at = P.transform.position;
                Fx.Disc(at, Range(Tier.Mid), Fx.FireDeep, 0.2f);
                foreach (var e in SkillHits.InCircle(at, Range(Tier.Mid), found))
                    e.Push((Vector2)e.transform.position - at, Knock);
            });
    }
}

/// <summary>
/// M03 오버 드라이브 플레임 슬래시 (구 오버 플레임 슬래시): 화염의 검을 내려쳐 기절 → 1초 뒤 같은 자리 폭발.
/// 화염의 검 초기 피해량 20 · 기절 2초, 폭발 딜레이 1초 · 초기 피해량 50, 쿨타임 20초
/// </summary>
public class OverFlameSlash : ActiveSkill
{
    const float BladeDamage = 20f, StunSeconds = 2f;
    const float ExplosionDelay = 1f, ExplosionDamage = 50f;
    const float BaseCooldown = 20f;
    const float WidthRatio = 0.5f;      // [임시] 검 판정 폭 = 범위 좁음 × 비율
    const float ExplosionWidthMul = 2.2f;   // [임시] 폭발 판정 폭 = 검 판정 폭 × 이 값 (같은 길이)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 from = P.transform.position;
        Vector2 dir = P.Facing;
        float len = Reach(Tier.Long);
        float width = Range(Tier.Short) * WidthRatio;
        var cast = DirectCast;
        P.PlayAttack(dir);
        Fx.Box(from, dir, len, width, Fx.FireBright, 0.3f);
        SkillHits.Box(from, dir, len, width, MakeDamage(BladeDamage, cast), 2f, e => e.Stun(StunSeconds));
        CameraFollow.Shake(0.08f, 0.08f);

        // [임시] 폭발 위치 = 검을 내려친 자리 (넓은 직사각형)
        Fx.Box(from, dir, len, width * ExplosionWidthMul, Fx.FireDeep, ExplosionDelay);
        yield return new WaitForSeconds(ExplosionDelay);
        Fx.Box(from, dir, len, width * ExplosionWidthMul, Fx.FireBright, 0.3f);
        SkillHits.Box(from, dir, len, width * ExplosionWidthMul, MakeDamage(ExplosionDamage, cast), 3f);
        CameraFollow.Shake(0.15f, 0.15f);
    }
}

/// <summary>
/// M04 파이어 브레이커: 군중제어 · 범위 중간 · 피해 중간 · 쿨 중간
/// 최초 발동 범위 피해 + 1초 기절 → 2초 후 같은 위치에서 자동 재발동 (피해 + 1초 기절)
/// </summary>
public class FireBreaker : ActiveSkill
{
    const float StunSeconds = 1f;       // 기획서 확정
    const float RecastDelay = 2f;       // 기획서 확정 (폭발 딜레이 2초)
    const float BaseDamage = 10f, BaseCooldown = 10f;   // [해석] 두 번째 폭발도 초기 피해량 10

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 at = P.transform.position;   // 같은 위치에서 재발동
        float r = Range(Tier.Mid);
        var cast = DirectCast;
        P.PlayAttack();
        Burst(at, r, cast);
        var mark = Fx.GroundCircle(at, r, Fx.FireDeep, RecastDelay);   // 재발동 위치 표시 = 바닥 레이어
        yield return new WaitForSeconds(RecastDelay);
        if (mark != null) Destroy(mark.gameObject);
        Burst(at, r, cast);
    }

    void Burst(Vector2 at, float r, CastRef cast)
    {
        Fx.Disc(at, r, Fx.Fire, 0.3f);
        SkillHits.Circle(at, r, MakeDamage(BaseDamage, cast), 1f, e => e.Stun(StunSeconds));
        CameraFollow.Shake(0.1f, 0.1f);
    }
}

/// <summary>M05 파이어 엑스큐션: 빠른 속도로 주변을 베어냄 · 초기 피해량 5 × 공격 횟수 30 · 쿨타임 30초 · 발동 중 이동 불가</summary>
public class FireExecution : ActiveSkill
{
    const float Duration = 1.5f;        // [임시] 공격 지속시간 (기획 미정) → 30회면 0.05초 간격
    const int Hits = 30;
    const float BaseDamage = 5f, BaseCooldown = 30f;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        P.Root(Duration);
        // [해석] 키워드 '고정' '피해' 의 의미 미확정 → 일반 직접 피해로 처리
        DamageInfo per = MakeDamage(BaseDamage, DirectCast);
        for (int i = 0; i < Hits; i++)
        {
            Vector2 at = P.transform.position;
            float r = Range(Tier.Mid);
            P.PlayAttack();
            Fx.Circle(at, r, i == Hits - 1 ? Fx.FireBright : Fx.Fire, 0.25f);
            SkillHits.Circle(at, r, per, i == Hits - 1 ? 6f : 1f);
            if (i == Hits - 1) CameraFollow.Shake(0.15f, 0.15f);
            yield return new WaitForSeconds(Duration / Hits);
        }
    }
}

/// <summary>
/// D01 파이어 스트림: 빠른 돌진 · 이동 경로 피해 · 무적 · 초기 피해량 3 · 스택 3 · 스택마다 쿨타임 15초
/// </summary>
public class FireStream : Skill
{
    const int MaxStacks = 3;            // 기획서 확정
    const float DashTime = 0.12f;       // [임시] 이동 시간 (속도 '매우 빠름')
    // [임시 해석] 스택은 한 번에 하나씩 순서대로 회복

    int stacks = MaxStacks;
    float recharge;
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    const float BaseDamage = 3f, BaseCooldown = 15f;   // 스택마다 쿨타임 15초
    float StackCooldown => CooldownOf(BaseCooldown);
    float Distance => Reach(Tier.Short);

    public override string Badge => stacks.ToString();
    public override float CooldownFraction =>
        stacks >= MaxStacks ? 0f : stacks > 0 ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));
    public override float CooldownRemaining => stacks > 0 ? 0f : Mathf.Max(0f, recharge);
    /// <summary>다음 스택 회복 진행도 (HUD)</summary>
    public float RechargeFraction => stacks >= MaxStacks ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));

    /// <summary>남은 쿨타임 감소 (불길의 순환): 지금 충전 중인 스택 기준</summary>
    public override void ReduceRemainingCooldown(float ratio)
    {
        if (stacks < MaxStacks && recharge > 0f) recharge *= Mathf.Clamp01(1f - ratio);
    }

    public override void Tick(float dt)
    {
        if (stacks >= MaxStacks) { recharge = 0f; return; }
        if (recharge <= 0f) recharge = StackCooldown;
        recharge -= dt;
        if (recharge <= 0f) { stacks++; recharge = stacks < MaxStacks ? StackCooldown : 0f; }
    }

    public override bool TryActivate()
    {
        if (CheatDisabled) return false;   // 치트: 스킬별 사용 금지
        if (stacks <= 0 || P.IsBusy) return false;
        if (stacks == MaxStacks) recharge = StackCooldown;
        stacks--;
        BeginCast();

        hit.Clear();
        DamageInfo dmg = MakeDamage(BaseDamage);
        float r = 0.4f;                 // [임시] 이동 경로 판정 반경 (카메라 시야 7 복귀에 맞춰 0.6 × 2/3)
        Fx.Circle(P.transform.position, r, Fx.FireBright, 0.2f);
        P.Dash(P.Facing, Distance, DashTime, true, onStep: () =>
        {
            Vector2 at = P.transform.position;
            Fx.Disc(at, r * 0.6f, Fx.Fire, 0.18f, 0f);
            foreach (var e in SkillHits.InCircle(at, r, found))
                if (hit.Add(e)) e.Hit(dmg, (Vector2)e.transform.position - at, 1f);
        });
        return true;
    }
}

/// <summary>
/// U01 블레이징 스톰: 궁극기 · 카메라 전체 · 피해 매우 높음 · 사용 종료까지 무적 · 피해 후 모든 적 기절
/// 게이지(적 처치 수 기반)가 가득 찼을 때 수동 발동
/// </summary>
public class BlazingStorm : Skill
{
    const float Duration = 1.5f;        // [임시] 연출 시간 (기획 미정)
    const int Hits = 1;                 // 초기 피해량 500 을 한 번에
    const float BaseDamage = 500f;
    const float StunSeconds = 2f;       // [임시] 기절 시간 (기획 미정, 보스 규칙 미정 → 동일 적용)

    readonly List<Enemy> targets = new List<Enemy>();
    bool running;

    public override float CooldownFraction => running ? 1f : 1f - GameManager.I.UltimateGaugeFraction;
    public override string Badge => running ? null : $"{Mathf.FloorToInt(GameManager.I.UltimateGaugeFraction * 100)}%";

    public override bool TryActivate()
    {
        if (CheatDisabled) return false;   // 치트: 스킬별 사용 금지
        if (running || P.IsBusy || !GameManager.I.UltimateReady) return false;
        GameManager.I.ConsumeUltimateGauge();
        BeginCast();
        StartCoroutine(Run());
        return true;
    }

    IEnumerator Run()
    {
        running = true;
        P.SetInvulnerable(Duration + 0.1f);
        DamageInfo per = MakeDamage(BaseDamage);
        Fx.ScreenFlash(new Color(1f, 0.45f, 0.1f, 0.45f), Duration);
        for (int i = 0; i < Hits; i++)
        {
            CameraFollow.Shake(0.2f, 0.25f);
            foreach (var e in SkillHits.OnScreen(targets))
            {
                Fx.Disc(e.transform.position, 0.4f, Fx.Fire, 0.25f);   // 카메라 시야 7 복귀에 맞춰 0.6 × 2/3
                e.Hit(per, (Vector2)(e.transform.position - P.transform.position), 0.5f);
            }
            // 마지막 피해 직후 모든 적 기절 (연출 시간이 끝날 때까지 기다리지 않음)
            if (i == Hits - 1) foreach (var e in SkillHits.OnScreen(targets)) e.Stun(StunSeconds);
            yield return new WaitForSeconds(Duration / Hits);
        }
        running = false;
    }
}
