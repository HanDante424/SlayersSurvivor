using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M01 트리플 슬래시 (기획 v0.2 + 통합 기획서 v1.0 6장 + 결정 기록 5장): 수동 · 쿨타임 8초 · 3타 · 타당 피해 8 · 바라보는 방향.
///
/// 1회 사용 = 스킬 발동(SkillCastCount, 기본 1 · 츠바메가에시 3) × 내부 공격(AttackCount, 기본 3 · 난도질 9 · 일섬 1).
///   - 첫 발동 = 직접 발동, 츠바메가에시 추가 2회 = 간접 발동 (각 발동에 모든 강화 적용)
///   - 난개: 공격마다 바라보는 방향 + 120° 간격 2방향
/// 피해 적용(적 1명 1타)마다: 빠른 준비(남은 쿨타임 2.5%↓) · 강제 발화(그 적의 화상 전체 총 피해를 고정 피해로)
/// 사용이 끝나면: 잔화의 검흔 3개 (최종 피해량 20% 고정 피해, 1초 간격 3초, 지속 피해)
/// </summary>
public class TripleSlash : ActiveSkill
{
    // 기본 수치 (기획 확정 — 임의 변경 금지)
    const float BaseCooldown = 8f;
    const float DamagePerHit = 8f;
    const int BaseHits = 3;

    const float FxFps = 20f;            // [임시] 10프레임 → 0.5초 (3타 전체를 한 시트로)
    const float FxWidth = 3f;           // [임시] 한 칸 가로 = 범위 x 이 값
    const float FxForward = 1.2f;       // [임시] 이펙트 중심 = 플레이어 앞 범위 x 이 값

    // 일섬 전용 이펙트 (2행 x 3열, 6프레임). 참격이 약 45° 대각선(왼쪽 아래 → 오른쪽 위)으로 그려져 있어 각도 보정
    const string IlseomSheet = "ErnStray_SkillTripleSlashIlseom";
    const float IlseomFps = 18f;        // [임시] 6프레임 → 약 0.33초
    const float IlseomAngle = 45f;      // 시트 안 참격 각도
    const float IlseomWidth = 2.2f;     // [임시] 한 칸 가로 = 범위 x 이 값

    // 특수 강화 id
    public const string Frenzy = "T5A", Spread = "T5B", QuickPrep = "T5C";
    public const string Overload = "T10A", Ilseom = "T10B", Searing = "T10C";
    public const string Tsubame = "T15A", Ignite = "T15B", Embers = "T15C";

    // 특수 강화 확정 수치
    const int FrenzyMul = 3;            // 난도질: 총 공격 횟수 3배 (다른 공격 횟수 배수와는 합연산)
    const float SpreadAngle = 120f;     // 난개: 120° 간격 추가 2방향
    const float QuickPrepReduce = 0.025f;   // 빠른 준비: 피해 적용마다 남은 쿨타임 2.5% 감소
    const float OverloadRange = 0.5f, OverloadDamage = 1f, OverloadCooldown = 0.25f;   // 과부화
    const float IlseomPerHit = 1.25f;   // 일섬: 공격 횟수 1회당 피해량 +125%
    const float SearingPerStack = 0.05f;    // 고열 절삭: 대상 화상 스택당 최종 피해량 +5% (내부 합연산)
    const int TsubameExtra = 2;         // 츠바메가에시: 발동 횟수 +2
    const float EmbersShare = 0.2f;     // 잔화의 검흔: 최종 피해량의 20%
    const float EmbersDuration = 3f, EmbersInterval = 1f;
    const int EmbersCount = 3;

    // [임시] 기획서에 없는 값
    const float TsubameGap = 0.12f;     // 츠바메가에시: 발동 사이 간격(초)
    const float EmbersSpread = 25f;     // 잔화의 검흔: 공격이 1~2방향뿐일 때 검흔을 벌리는 각도

    static SkillHitbox Hit => T.tripleSlash;

    // ---------- 수치 ----------
    /// <summary>스킬 내부 공격 횟수 계산용 (일섬 전). 난도질 ×3 (배수끼리 합연산: 1 + (3−1))</summary>
    int AttackCountBeforeIlseom => BaseHits * (1 + (HasSpecial(Frenzy) ? FrenzyMul - 1 : 0));
    /// <summary>실제 내부 공격 횟수 (일섬이면 1)</summary>
    int AttackCount => HasSpecial(Ilseom) ? 1 : AttackCountBeforeIlseom;
    /// <summary>스킬 자체 발동 횟수</summary>
    int SkillCastCount => 1 + (HasSpecial(Tsubame) ? TsubameExtra : 0);

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialCooldownMul => HasSpecial(Overload) ? 1f + OverloadCooldown : 1f;
    protected override float SpecialRangeBonus => HasSpecial(Overload) ? OverloadRange : 0f;
    /// <summary>'피해량' %: 과부화 +100% · 일섬 (일섬 전 공격 횟수 × 125%)</summary>
    protected override float SpecialDamageBonus =>
        (HasSpecial(Overload) ? OverloadDamage : 0f)
        + (HasSpecial(Ilseom) ? IlseomPerHit * AttackCountBeforeIlseom : 0f);

    /// <summary>화상이 필요한 강화 설명 뒤에 붙는 안내 (화염의 검 미보유 시)</summary>
    static string NeedsFlameSword => FlameSword.I == null ? "\n<color=#FF9A50>화염의 검이 없으면 화상이 생기지 않아 효과 없음</color>" : "";

    // ---------- 특수 강화 목록 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Frenzy, title = "난도질", desc = "총 공격 횟수 3배 (3타 → 9타)" },
                new SpecialUpgradeOption { id = Spread, title = "난개", desc = "바라보는 방향 + 120° 간격 2방향, 총 3방향 공격" },
                new SpecialUpgradeOption { id = QuickPrep, title = "빠른 준비", desc = "트리플 슬래시 피해가 적에게 적용될 때마다 남은 쿨타임 2.5% 감소" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Overload, title = "과부화", desc = "범위 +50% · 피해량 +100% · 쿨타임 +25%" },
                new SpecialUpgradeOption { id = Ilseom, title = "일섬", desc = "공격을 1회로 합침 · 원래 공격 횟수 1회당 피해량 +125%" },
                new SpecialUpgradeOption { id = Searing, title = "고열 절삭", desc = "대상의 화상 스택 1당 최종 피해량 +5%" + NeedsFlameSword },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Tsubame, title = "츠바메가에시", desc = "스킬 발동 횟수 +2 (총 3회, 각 발동에 모든 강화 적용)" },
                new SpecialUpgradeOption { id = Ignite, title = "강제 발화", desc = "피해 적용 시 대상 화상의 전체 지속시간 총 피해를 고정 피해로 추가 (스택 소모 없음)" + NeedsFlameSword },
                new SpecialUpgradeOption { id = Embers, title = "잔화의 검흔", desc = "사용이 끝나면 참격 3개가 3초간 남아 매초 최종 피해량의 20% 고정 피해 (지속 피해)" },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 발동 ----------
    protected override void Activate() => StartCoroutine(Run());

    struct Strike { public Vector2 from, dir; }

    /// <summary>사용 1번의 기록 (재사용이 겹쳐도 서로 덮어쓰지 않게 사용마다 따로)</summary>
    class UseState
    {
        public readonly List<Strike> strikes = new List<Strike>();   // 검흔 위치용 공격 위치
        public float lastHitAmount;   // 잔화의 검흔 참조값 = 마지막 공격 1타의 스킬 최종 피해량 (대상 의존 효과 제외)
    }

    IEnumerator Run()
    {
        var use = new UseState();
        var first = DirectCast;   // 첫 발동 = 직접 발동 (3타 모두 이 번호)
        int casts = SkillCastCount;
        for (int c = 0; c < casts; c++)
        {
            if (c > 0)
            {
                if (TsubameGap > 0) yield return new WaitForSeconds(TsubameGap);
                if (CheatDisabled) break;
            }
            yield return CastOnce(c == 0 ? first : CastRef.Indirect, use);   // 츠바메가에시 추가분 = 간접 발동
        }
        if (HasSpecial(Embers) && !CheatDisabled) SpawnEmbers(use);
    }

    /// <summary>스킬 1회 발동 = 내부 공격 AttackCount 회 (난개면 공격마다 3방향)</summary>
    IEnumerator CastOnce(CastRef cast, UseState use)
    {
        string[] clips = { Player.SlashH, Player.SlashV, Player.Thrust };
        int hits = AttackCount;
        // [임시] 난도질로 공격이 늘어도 한 번 사용 시간은 기본(3타)과 비슷하게: 간격 = 기본 간격 × 2 / (횟수 − 1)
        float interval = hits > BaseHits ? T.tripleSlashInterval * (BaseHits - 1) / (hits - 1) : T.tripleSlashInterval;
        bool ilseom = HasSpecial(Ilseom);
        var sheet = ilseom ? null : ErnSkillFx.TripleSlash;
        var ilseomFrames = ilseom ? SpriteEffect.LoadFrames(IlseomSheet, 2, 3, 1f) : null;

        for (int i = 0; i < hits; i++)
        {
            Vector2 from = P.transform.position;
            Vector2 facing = P.Facing;
            float r = RangeOf(Hit.radius);
            P.PlayAttack(clips[i % clips.Length], facing, baseFx: sheet == null && ilseomFrames == null);

            foreach (var dir in Directions(facing))
            {
                // 이펙트: 일섬 = 전용 시트, 그 외 = 3타 시트를 3타마다 한 번 (시트 1장 = 3타 연출)
                if (ilseomFrames != null) SpawnIlseomFx(ilseomFrames, from, dir, r);
                else if (sheet != null && i % BaseHits == 0)
                    ErnSkillFx.SpawnDirected(sheet, FxFps, from + dir * (r * FxForward), dir, r * FxWidth);
                else if (sheet == null) Fx.Arc(from, dir, r, i == hits - 1 ? Fx.FireBright : Fx.Fire, 0.15f);

                var dmg = MakeDamage(DamagePerHit, cast);
                if (HasSpecial(Searing)) dmg.perTarget = SearingMul;   // 고열 절삭: 맞은 적마다 (대상 단계)
                use.lastHitAmount = dmg.amount;
                SkillHits.Directed(Hit, from, dir, RangeScale, dmg);   // 모양은 SkillTuning.tripleSlash
                use.strikes.Add(new Strike { from = from, dir = dir });
            }
            if (i < hits - 1) yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>난개: 바라보는 방향 + 120° 간격 2방향</summary>
    IEnumerable<Vector2> Directions(Vector2 facing)
    {
        yield return facing;
        if (!HasSpecial(Spread)) yield break;
        yield return Rotate(facing, SpreadAngle);
        yield return Rotate(facing, -SpreadAngle);
    }

    /// <summary>고열 절삭: 대상 화상 스택 합계 1당 +5% (내부 합연산 → 배율 1개)</summary>
    static float SearingMul(Enemy e) => 1f + SearingPerStack * FlameSword.StacksOn(e);

    void SpawnIlseomFx(Sprite[] frames, Vector2 from, Vector2 dir, float r)
    {
        if (frames == null) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - IlseomAngle;   // 시트의 45° 대각선을 공격 방향에 맞춤
        SpriteEffect.Spawn(frames, IlseomFps, null, from + dir * (r * FxForward), angle, false, 11, r * IlseomWidth);
    }

    // ---------- 피해 적용 트리거 ----------
    void OnEnable() { Enemy.Damaging += OnDamaging; Enemy.AfterDamaged += OnAfterDamaged; }
    protected void OnDisable() { Enemy.Damaging -= OnDamaging; Enemy.AfterDamaged -= OnAfterDamaged; }

    /// <summary>
    /// 강제 발화: 이번 타격의 화상이 쌓이기 **전** 스택 기준 (Damaging = Damaged 보다 먼저, 결정 2026-11-10).
    /// 그 적 화상의 전체 지속시간 총 피해를 고정 피해로. [해석] 피해 유형 = 특수 피해 (트리거 없음 → 무한 반복 차단)
    /// </summary>
    void OnDamaging(Enemy e, DamageInfo d, int dealt)
    {
        if (d.source != this || d.type != DamageType.Direct || !HasSpecial(Ignite) || e.hp <= 0) return;
        float total = FlameSword.TotalBurnDamage(e);
        if (total > 0f) e.Hit(FixedDamage(total, DamageType.Special, CastKind.Indirect), Vector2.zero, Enemy.NoKnock);
    }

    /// <summary>
    /// 빠른 준비: 트리플 슬래시의 공격(직접 피해)이 적 1명에게 적용될 때마다 남은 쿨타임 2.5% 감소 (90% 상한 무관).
    /// 잔화의 검흔(지속 피해) · 강제 발화(특수 피해)는 제외 (결정 2026-11-10)
    /// </summary>
    void OnAfterDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (d.source != this || d.type != DamageType.Direct) return;
        if (HasSpecial(QuickPrep)) ReduceRemainingCooldown(QuickPrepReduce);
    }

    // ---------- 잔화의 검흔 ----------
    /// <summary>
    /// [임시 해석] 검흔 3개 위치 = 이번 사용의 마지막 공격부터 거슬러 올라가며 **서로 다른 위치·방향** 3개.
    /// 서로 다른 것이 3개가 안 되면(제자리 기본 3타 · 일섬 등) 마지막 공격 방향을 ±25° 로 벌려 채움 → 같은 자리에 겹치지 않음
    /// </summary>
    void SpawnEmbers(UseState use)
    {
        var strikes = use.strikes;
        if (strikes.Count == 0) return;
        float perTick = use.lastHitAmount * EmbersShare;   // 참조값 = 스킬 최종 피해량 (대상 의존 효과 제외)
        var picks = new List<Strike>();
        for (int i = strikes.Count - 1; i >= 0 && picks.Count < EmbersCount; i--)
        {
            var s = strikes[i];
            bool dup = false;
            foreach (var p in picks)
                if ((p.from - s.from).sqrMagnitude < 0.04f && Vector2.Dot(p.dir, s.dir) > 0.98f) { dup = true; break; }
            if (!dup) picks.Add(s);
        }
        if (picks.Count < EmbersCount)
        {
            var last = strikes[strikes.Count - 1];
            picks.Clear();
            for (int k = 0; k < EmbersCount; k++)
                picks.Add(new Strike { from = last.from, dir = Rotate(last.dir, (k - 1) * EmbersSpread) });
        }
        foreach (var s in picks) StartCoroutine(EmberRoutine(s, perTick));
    }

    /// <summary>검흔 1개: 첫 피해 = 생성 후 피해 간격, 이후 간격마다 (3초 → 3회). 고정 피해 · 지속 피해 → 화상 미부여</summary>
    IEnumerator EmberRoutine(Strike s, float perTick)
    {
        float scale = RangeScale;
        if (ErnSkillFx.TripleSlash is { } frames)
            ErnSkillFx.SpawnDirected(frames, FxFps * 0.5f, s.from + s.dir * (RangeOf(Hit.radius) * FxForward), s.dir, RangeOf(Hit.radius) * FxWidth * 0.8f);
        // 피해 간격마다 지속시간 끝까지 (기본 1초 → 1·2·3초 3회, 가속 연소면 더 자주)
        float elapsed = 0f;
        while (true)
        {
            float iv = CombatMods.DotInterval(EmbersInterval);
            if (elapsed + iv > EmbersDuration + 1e-4f) break;
            yield return new WaitForSeconds(iv);
            elapsed += iv;
            Fx.Arc(s.from, s.dir, RangeOf(Hit.radius), Fx.FireDeep, 0.25f);
            SkillHits.Directed(Hit, s.from, s.dir, scale, FixedDamage(perTick, DamageType.DoT, CastKind.Indirect));
        }
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * sn, v.x * sn + v.y * c);
    }
}
