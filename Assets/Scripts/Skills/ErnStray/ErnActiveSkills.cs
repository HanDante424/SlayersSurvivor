using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 수동 5종 · 이동 1종 · 궁극기 1종 (기획서 5~7장)
// 방향은 플레이어가 마지막으로 이동한 방향(Facing) 기준.
// 기획서에 없는 값은 각 클래스 맨 위 "[임시]" 상수.

/// <summary>
/// M01 트리플 슬래시 (기획 v0.2): 수동 · 쿨타임 8초 · 1회 사용 시 3타 · 타당 피해 8 (총 24) · 바라보는 방향으로만.
/// 다단히트 → 타마다 floor(8 × (1 + 공격력 증가율 합계)). 판정·간격은 SkillTuning.tripleSlash* 에서 조정.
/// </summary>
public class TripleSlash : ActiveSkill
{
    // 기본 수치 (기획 v0.2 확정 — 임의 변경 금지)
    const float BaseCooldown = 8f;
    const float DamagePerHit = 8f;
    const int Hits = 3;

    const float FxFps = 20f;            // [임시] 10프레임 → 0.5초 (3타 전체를 한 시트로)
    const float FxWidth = 1.8f;         // [임시] 한 칸 가로 = 범위 x 이 값
    const float FxForward = 0.45f;      // [임시] 이펙트 중심 = 플레이어 앞 범위 x 이 값

    static SkillHitbox Hit => T.tripleSlash;
    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        string[] clips = { Player.SlashH, Player.SlashV, Player.Thrust };
        float r0 = RangeOf(Hit.radius);
        // 트리플 슬래시 시트는 3타를 한 번에 그린 10프레임 → 첫 타에 한 번만 띄움
        var frames = ErnSkillFx.TripleSlash;
        if (frames != null)
            ErnSkillFx.SpawnDirected(frames, FxFps, (Vector2)P.transform.position + P.Facing * (r0 * FxForward),
                                     P.Facing, r0 * FxWidth);
        for (int i = 0; i < Hits; i++)
        {
            Vector2 from = P.transform.position;
            Vector2 dir = P.Facing;
            P.PlayAttack(clips[i], dir, baseFx: frames == null);
            float r = RangeOf(Hit.radius);
            if (frames == null) Fx.Arc(from, dir, r, i == Hits - 1 ? Fx.FireBright : Fx.Fire, 0.15f);
            SkillHits.Directed(Hit, from, dir, RangeScale, HitDamage(DamagePerHit));   // 모양은 SkillTuning.tripleSlash
            if (i < Hits - 1) yield return new WaitForSeconds(T.tripleSlashInterval);
        }
    }
}

/// <summary>M02 파이어 차지: 이동기 · 범위 중간 · 피해 매우 낮음 · 쿨 중간 · 무적 · 주변 적 밀치기</summary>
public class FireCharge : ActiveSkill
{
    const float Distance = 4f;          // [임시] 돌진 거리 (기획 미정)
    const float DashTime = 0.25f;       // [임시]
    const float Knock = 8f;             // [임시]

    protected override float CooldownSeconds => Cooldown(Tier.Mid);
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    protected override void Activate()
    {
        hit.Clear();
        float r = Range(Tier.Mid) * 0.5f;   // [임시] 돌진 중 판정은 범위 중간의 절반
        float dmg = Damage(Power.VeryLow);
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

/// <summary>M03 오버 플레임 슬래시: 사거리 김 · 공격 폭 짧음 · 피해 매우 높음 · 쿨 김 · 적 기절</summary>
public class OverFlameSlash : ActiveSkill
{
    const float StunSeconds = 1.5f;     // [임시] 기절 시간 (기획 미정)
    const float WidthRatio = 0.5f;      // [임시] 폭 = 범위 좁음 × 비율

    protected override float CooldownSeconds => Cooldown(Tier.Long);

    protected override void Activate()
    {
        Vector2 from = P.transform.position;
        Vector2 dir = P.Facing;
        float len = Reach(Tier.Long);
        float width = Range(Tier.Short) * WidthRatio;
        P.PlayAttack(Player.Thrust, dir);
        Fx.Box(from, dir, len, width, Fx.FireBright, 0.3f);
        Fx.Box(from, dir, len, width * 2.2f, Fx.FireDeep, 0.2f);
        SkillHits.Box(from, dir, len, width, Damage(Power.VeryHigh), 2f, e => e.Stun(StunSeconds));
        CameraFollow.Shake(0.12f, 0.12f);
    }
}

/// <summary>
/// M04 파이어 브레이커: 군중제어 · 범위 중간 · 피해 중간 · 쿨 중간
/// 최초 발동 범위 피해 + 1초 기절 → 2초 후 같은 위치에서 자동 재발동 (피해 + 1초 기절)
/// </summary>
public class FireBreaker : ActiveSkill
{
    const float StunSeconds = 1f;       // 기획서 확정
    const float RecastDelay = 2f;       // 기획서 확정

    protected override float CooldownSeconds => Cooldown(Tier.Mid);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 at = P.transform.position;   // 같은 위치에서 재발동
        float r = Range(Tier.Mid);
        P.PlayAttack(Player.SlashV);
        Burst(at, r);
        var mark = Fx.Circle(at, r, Fx.FireDeep, RecastDelay, null, 0f);
        yield return new WaitForSeconds(RecastDelay);
        if (mark != null) Destroy(mark.gameObject);
        Burst(at, r);
    }

    void Burst(Vector2 at, float r)
    {
        Fx.Disc(at, r, Fx.Fire, 0.3f);
        SkillHits.Circle(at, r, Damage(Power.Mid), 1f, e => e.Stun(StunSeconds));
        CameraFollow.Shake(0.1f, 0.1f);
    }
}

/// <summary>M05 파이어 엑스큐션: 피해 · 범위 중간 · 전방위 · 피해 매우 높음 · 쿨 김 · 발동 중 이동 불가</summary>
public class FireExecution : ActiveSkill
{
    const float Duration = 1.5f;        // [임시] 공격 지속시간 (기획 미정)
    const int Hits = 5;                 // [임시] 타격 횟수 (기획 미정)

    protected override float CooldownSeconds => Cooldown(Tier.Long);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        P.Root(Duration);
        float per = Damage(Power.VeryHigh) / Hits;
        string[] clips = { Player.SlashH, Player.SlashV, Player.Thrust };
        for (int i = 0; i < Hits; i++)
        {
            Vector2 at = P.transform.position;
            float r = Range(Tier.Mid);
            P.PlayAttack(clips[i % clips.Length]);
            Fx.Circle(at, r, i == Hits - 1 ? Fx.FireBright : Fx.Fire, 0.25f);
            SkillHits.Circle(at, r, per, i == Hits - 1 ? 6f : 1f);
            if (i == Hits - 1) CameraFollow.Shake(0.15f, 0.15f);
            yield return new WaitForSeconds(Duration / Hits);
        }
    }
}

/// <summary>
/// D01 파이어 스트림: 이동 · 3스택 · 쿨 중간(스택마다) · 거리 짧음 · 속도 매우 빠름 · 피해 낮음 · 무적
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

    float StackCooldown => Cooldown(Tier.Mid);
    float Distance => Reach(Tier.Short);

    public override string Badge => stacks.ToString();
    public override float CooldownFraction =>
        stacks >= MaxStacks ? 0f : stacks > 0 ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));
    /// <summary>다음 스택 회복 진행도 (HUD)</summary>
    public float RechargeFraction => stacks >= MaxStacks ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));

    public override void Tick(float dt)
    {
        if (stacks >= MaxStacks) { recharge = 0f; return; }
        if (recharge <= 0f) recharge = StackCooldown;
        recharge -= dt;
        if (recharge <= 0f) { stacks++; recharge = stacks < MaxStacks ? StackCooldown : 0f; }
    }

    public override bool TryActivate()
    {
        if (stacks <= 0 || P.IsBusy) return false;
        if (stacks == MaxStacks) recharge = StackCooldown;
        stacks--;

        hit.Clear();
        float dmg = Damage(Power.Low);
        float r = 0.6f;                 // [임시] 이동 경로 판정 반경
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
    const int Hits = 3;                 // [임시] 타격 횟수 (기획 미정)
    const float StunSeconds = 2f;       // [임시] 기절 시간 (기획 미정, 보스 규칙 미정 → 동일 적용)

    readonly List<Enemy> targets = new List<Enemy>();
    bool running;

    public override float CooldownFraction => running ? 1f : 1f - GameManager.I.UltimateGaugeFraction;
    public override string Badge => running ? null : $"{Mathf.FloorToInt(GameManager.I.UltimateGaugeFraction * 100)}%";

    public override bool TryActivate()
    {
        if (running || P.IsBusy || !GameManager.I.UltimateReady) return false;
        GameManager.I.ConsumeUltimateGauge();
        StartCoroutine(Run());
        return true;
    }

    IEnumerator Run()
    {
        running = true;
        P.SetInvulnerable(Duration + 0.1f);
        float per = Damage(Power.VeryHigh) / Hits;
        Fx.ScreenFlash(new Color(1f, 0.45f, 0.1f, 0.45f), Duration);
        for (int i = 0; i < Hits; i++)
        {
            CameraFollow.Shake(0.2f, 0.25f);
            foreach (var e in SkillHits.OnScreen(targets))
            {
                Fx.Disc(e.transform.position, 0.6f, Fx.Fire, 0.25f);
                e.Hit(per, (Vector2)(e.transform.position - P.transform.position), 0.5f);
            }
            yield return new WaitForSeconds(Duration / Hits);
        }
        // 피해 이후 기절
        foreach (var e in SkillHits.OnScreen(targets)) e.Stun(StunSeconds);
        running = false;
    }
}
