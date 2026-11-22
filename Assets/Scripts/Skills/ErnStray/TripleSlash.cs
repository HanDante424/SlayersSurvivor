using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// M01 트리플 슬래시 (기획 v0.2 + 통합 기획서 v1.0 6장 + 결정 기록 5장): 수동 · 쿨타임 8초 · 3타 · 타당 피해 8 · 바라보는 방향.
///
/// 1회 사용 = 스킬 발동(SkillCastCount, 기본 1 · 삼연발 3) × 내부 공격(AttackCount, 기본 3 · 난도질 9 · 일섬 1).
///   - 첫 발동 = 직접 발동, 삼연발 추가 2회 = 간접 발동 (각 발동에 모든 강화 적용)
/// 특수 조합 츠바메가에시 (일섬 + 삼연발, 2026-11-20 노션): 최종 피해량 +500% · 쿨타임 감소 무효화 (1%당 최종 피해량 +10%) · 쿨타임 +200%
///   + 전용 이펙트 · 임팩트 프레임. 조합을 사용하지 않으면 일섬 + 삼연발 효과만 (일섬 이펙트).
///   - 난개: 공격마다 바라보는 방향 + 120° 간격 2방향
/// 피해 적용(적 1명 1타)마다: 빠른 준비(남은 쿨타임 2.5%↓) · 강제 발화(그 적의 화상 전체 총 피해를 고정 피해로)
/// 사용이 끝나면: 잔화의 검흔 3개 (최종 피해량 40% 고정 피해, 1초 간격 3초, 지속 피해)
/// </summary>
public class TripleSlash : ActiveSkill
{
    // 기본 수치 (기획 확정 — 임의 변경 금지)
    const float BaseCooldown = 8f;
    const float DamagePerHit = 8f;
    const int BaseHits = 3;

    const float FxForward = 1.2f;       // [임시] 이펙트 중심 = 플레이어 앞 범위 x 이 값

    // 기본 3타 이펙트 (2026-11-21 사용자: 기존 시트 대신 새로 만든 불꽃 참격 시트): 4열 x 3행 칸 500x500 (11프레임 + 빈 칸 1), 참격이 왼쪽 아래 → 오른쪽 위로 휜 호.
    // 한 번 사용 (3타) 에 참격 3개를 FlameInterval 간격으로, 방향을 FlameTurns 만큼 달리해서 같은 자리에 띄움 (이펙트만, 피해 판정은 그대로).
    // 사용 모션 (2026-11-21 사용자): 기존 베기 모션 대신 포즈 3장을 차례로. 이름 = Player.ErnPoses (파일 ErnStray_ 뺀 이름)
    static readonly string[] UsePoses = { "Atk_SlashReady", "Atk_OneHandSlash", "Atk_HorizontalSlashReady" };
    const float UsePoseStep = 0.1f;     // [임시] 포즈 한 장이 보이는 시간 (게임 시간). 3장 = 0.3초
    bool poseBusy;

    const string FlameSheet = "ErnStray_TripleSlashFlame";
    const float FlameFps = 40f;         // [임시] 12칸 = 0.3초
    const float FlameWidth = 3.0f;      // [임시] 한 칸 가로 = 범위 x 이 값 (호 길이는 칸의 약 0.74배)
    const float FlameInterval = 0.05f;  // 참격 사이 간격 (사용자 2026-11-21)
    static readonly float[] FlameTurns = { -40f, 40f, 0f };   // [임시] 화면 수평 기준 참격마다 돌리는 각도 (도). 사용자 2026-11-21: 수평 + ±40
    static readonly float[] FlameSags = { 0.22f, -0.22f, 0.12f };   // [임시] 참격마다 휨 정도 (칸 가로 단위, 양수 = 위로 볼록, 음수 = 아래로 볼록). BentSlashFx 가 그림을 곡선으로 직접 휨

    // 일섬 전용 이펙트: 2026-11-20 신규 시트 (1행 x 5열, 칸 448x352, 20fps, 1회 재생).
    // 참격이 왼쪽 아래 → 오른쪽 위로 휜 호 (가장 큰 프레임의 양끝을 이은 각도 약 39°)라 각도 보정.
    // 예전 시트 "ErnStray_SkillTripleSlashIlseom" (2행 x 3열, 45°) 는 되돌릴 수 있게 Resources 에 그대로 둠.
    const string IlseomSheet = "ErnStray_SkillTripleSlashIlseomAnim";
    const float IlseomFps = 20f;        // 신규 시트 제작 사양 (5프레임 → 0.25초)
    const float IlseomAngle = 39f;      // [해석] 시트 안 참격 각도 = 가장 큰 프레임 양끝을 이은 직선 (호가 휘어 있어 시작은 더 완만, 끝은 더 가파름)
    const float IlseomWidth = 2.7f;     // [임시] 한 칸 가로 = 범위 x 이 값 (예전 시트의 최대 참격 길이에 맞춤)

    // 특수 조합 츠바메가에시 전용 이펙트 (2026-11-20 사용자: 15레벨 "츠바메가에시" → "삼연발" 로 이름 변경, 일섬 + 삼연발 = 특수 조합 츠바메가에시 → 이 연출을 조합에 귀속).
    // 1행 x 5열, 칸 448x352, 20fps, 1회 재생. 일섬보다 더 휜 호. 공격 한 번에 참격 3개를 중심 기준 120° 간격으로 즉시 띄움 (이펙트만, 판정 방향은 그대로).
    // 조합을 사용하지 않으면: 일섬 있으면 일섬 이펙트, 삼연발만 있으면 기본 3타 이펙트.
    const string TsubameSheet = "ErnStray_SkillTsubameGaeshi";
    const float TsubameFps = 20f;       // 신규 시트 제작 사양 (5프레임 → 0.25초)
    const float TsubameAngle = 39f;     // [해석] 시트 안 참격 각도 = 가장 큰 프레임 양끝을 이은 직선
    const float TsubameWidth = 2.7f;    // [임시] 한 칸 가로 = 범위 x 이 값 (일섬 이펙트와 같은 길이)
    const float TsubameStep = 120f;     // 참격 사이 회전 각도
    // 시트 가장 큰 프레임 (3번째) 에서 참격 무게중심이 칸 중심에서 떨어진 정도 (칸 가로 단위, 오른쪽 · 위쪽이 +): (283-224)/448, -(229-176)/448
    static readonly Vector2 TsubameCentroid = new Vector2(0.132f, -0.118f);
    const float TsubameGather = 0.9f;   // [임시] 3개를 가운데로 모으는 정도 (0 = 그대로, 1 = 무게중심을 한 점에 겹침). 2026-11-20 "좀더 가운데로"
    const int TsubameSlashes = 3;       // 한 번의 공격에 띄우는 참격 수 (2026-11-20: 발동마다가 아니라 공격 한 번에 3개)
    const float TsubameSlashInterval = 0.05f;  // 참격 3개 사이 간격 (게임 시간, 2026-11-20 사용자 지정: 0.1 → 0.05)
    // 적중 임팩트 프레임 (특수 조합 츠바메가에시): 직접 사용이 적에게 맞으면 시간 정지 + 흑백
    const float ImpactSeconds = 0.25f;  // 시간이 멈추는 길이 (사용자 지정)
    const float ImpactParticleWidth = 6.6f;   // [임시] 적중 파티클 한 칸 가로 = 범위 x 이 값 (임팩트 프레임이 끝난 뒤 맞은 적 위치에 재생). 처음 2.2 의 3배 (사용자 지정)
    const float ImpactDelay = 0.1f;    // [임시] 적중 뒤 이만큼 (게임 시간) 기다렸다가 정지 — 참격이 가장 커지는 프레임 (20fps 의 3번째) 이 화면에 남도록

    // 특수 강화 id
    public const string Frenzy = "T5A", Spread = "T5B", QuickPrep = "T5C";
    public const string Overload = "T10A", Ilseom = "T10B", Searing = "T10C";
    public const string TripleCast = "T15A", Ignite = "T15B", Embers = "T15C";   // T15A = 삼연발 (구 츠바메가에시, id 유지)
    // 특수 조합 id
    public const string Tsubame = "TC1";   // 츠바메가에시 = 일섬 + 삼연발

    // 특수 강화 확정 수치
    const int FrenzyMul = 3;            // 난도질: 총 공격 횟수 3배 (다른 공격 횟수 배수와는 합연산)
    const float SpreadAngle = 120f;     // 난개: 120° 간격 추가 2방향
    const float QuickPrepReduce = 0.025f;   // 빠른 준비: 피해 적용마다 남은 쿨타임 2.5% 감소
    const float OverloadRange = 0.5f, OverloadDamage = 1f, OverloadCooldown = 0.25f;   // 과부화
    const float IlseomPerHit = 1.25f;   // 일섬: 공격 횟수 1회당 피해량 +125%
    const float SearingPerStack = 0.05f;    // 고열 절삭: 대상 화상 스택당 최종 피해량 +5% (내부 합연산)
    const float SearingCap = 0.75f;         // 고열 절삭: 효과 상한 75% (노션)
    const int TripleCastExtra = 2;      // 삼연발: 발동 횟수 +2
    // 특수 조합 츠바메가에시 (노션 2026-11-20)
    const float TsubameFinal = 5f;          // 최종 피해량 +500%
    const float TsubamePerReduction = 10f;  // 무효화된 쿨타임 감소 1%당 최종 피해량 +10% (+500% 와 합쳐 최종 배율 1개 — 묵직한 일격 방식)
    const float TsubameCooldown = 2f;       // 쿨타임 +200%
    const float EmbersShare = 0.4f;     // 잔화의 검흔: 최종 피해량의 40% (노션)
    const float EmbersDuration = 3f, EmbersInterval = 1f;
    const int EmbersCount = 3;

    // [임시] 기획서에 없는 값
    const float TripleCastGap = 0.12f;  // 삼연발: 발동 사이 간격(초)
    const float EmbersSpread = 25f;     // 잔화의 검흔: 공격이 1~2방향뿐일 때 검흔을 벌리는 각도

    static SkillHitbox Hit => T.tripleSlash;

    // ---------- 수치 ----------
    /// <summary>스킬 내부 공격 횟수 계산용 (일섬 전). 난도질 ×3 (배수끼리 합연산: 1 + (3−1))</summary>
    int AttackCountBeforeIlseom => BaseHits * (1 + (HasSpecial(Frenzy) ? FrenzyMul - 1 : 0));
    /// <summary>실제 내부 공격 횟수 (일섬이면 1)</summary>
    int AttackCount => HasSpecial(Ilseom) ? 1 : AttackCountBeforeIlseom;
    /// <summary>스킬 자체 발동 횟수</summary>
    int SkillCastCount => 1 + (HasSpecial(TripleCast) ? TripleCastExtra : 0);
    bool TsubameOn => HasCombo(Tsubame);

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    protected override float SpecialCooldownMul => HasSpecial(Overload) ? 1f + OverloadCooldown : 1f;
    protected override float SpecialRangeBonus => HasSpecial(Overload) ? OverloadRange : 0f;

    /// <summary>
    /// 츠바메가에시가 무효화한 쿨타임 감소율 (0.3 = 30%): 기본 강화 · 전역(옥염 · 흡수 가열 등) · 플레이어 쿨타임 감소. 90% 상한 그대로.
    /// [해석] '쿨타임 감소'만 무효화 — 과부화의 쿨타임 +25% (증가)는 무효화 대상이 아니라 그대로 곱함.
    /// </summary>
    public float TsubameConvertedReduction
    {
        get
        {
            float m = Mathf.Max(1f - T.cooldownReductionCap, BasicCooldownMul * CombatMods.CooldownMul(this) * P.cooldownMul);
            return Mathf.Round(Mathf.Max(0f, 1f - m) * 10000f) / 10000f;
        }
    }

    /// <summary>츠바메가에시: 쿨타임 감소 무효화 → 과부화 배율 × (1 + 200%)</summary>
    protected override float CooldownMultiplier => TsubameOn ? SpecialCooldownMul * (1f + TsubameCooldown) : GeneralCooldownMultiplier;
    /// <summary>츠바메가에시: '최종 피해량' +500% 와 전환분 (1%당 +10%) 을 합쳐 최종 배율 1개 (예: 30% → 1 + 5 + 3 = ×9)</summary>
    protected override float SpecialFinalDamageMul => TsubameOn ? 1f + TsubameFinal + TsubameConvertedReduction * TsubamePerReduction : 1f;

    /// <summary>[해석] 츠바메가에시 = 쿨타임 감소 무효화 → '남은 쿨타임 감소' (빠른 준비 · 불길의 순환)도 받지 않음</summary>
    public override void ReduceRemainingCooldown(float ratio)
    {
        if (TsubameOn) return;
        base.ReduceRemainingCooldown(ratio);
    }
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
                new SpecialUpgradeOption { id = Searing, title = "고열 절삭", desc = "대상의 화상 스택 1당 최종 피해량 +5% (상한 75%)" + NeedsFlameSword },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = TripleCast, title = "삼연발", desc = "스킬 발동 횟수 +2 (총 3회, 각 발동에 모든 강화 적용)" },
                new SpecialUpgradeOption { id = Ignite, title = "강제 발화", keywords = new[] { "간접 사용", "지속 피해", "고정 피해" }, desc = "피해 적용 시 대상 화상의 전체 지속시간 총 피해를 고정 피해로 추가 (스택 소모 없음)" + NeedsFlameSword },
                new SpecialUpgradeOption { id = Embers, title = "잔화의 검흔", keywords = new[] { "간접 사용", "지속 피해", "고정 피해" }, desc = "사용이 끝나면 참격 3개가 3초간 남아 매초 최종 피해량의 40% 고정 피해 (지속 피해)" },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 특수 조합 ----------
    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = Tsubame, title = "츠바메가에시", recipe = "일섬 + 삼연발",
            requires = new[] { Ilseom, TripleCast },
            keywords = new[] { "수동", "피해 스킬", "근거리", "직접 사용", "직접 피해" },
            desc = "한순간에 3번의 베기를 사용하는 궁극의 비기",
            specs = new[] { "'최종 피해량' +500%", "'쿨타임 감소' '무효화'", "'쿨타임 감소' 1% 당 '최종 피해량' +10%", "'쿨타임' +200%" },
        },
    };

    // ---------- 발동 ----------
    protected override void Activate() => StartCoroutine(Run());

    struct Strike { public Vector2 from, dir; }

    /// <summary>사용 1번의 기록 (재사용이 겹쳐도 서로 덮어쓰지 않게 사용마다 따로)</summary>
    class UseState
    {
        public readonly List<Strike> strikes = new List<Strike>();   // 검흔 위치용 공격 위치
        public float lastHitAmount;   // 잔화의 검흔 참조값 = 마지막 공격 1타의 스킬 최종 피해량 (대상 의존 효과 제외)
        public bool impacted;         // 임팩트 프레임을 이미 걸었는지 (사용 1번에 한 번)
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
                if (TripleCastGap > 0) yield return new WaitForSeconds(TripleCastGap);
                if (CheatDisabled) break;
            }
            yield return CastOnce(c == 0 ? first : first.AsIndirect, use, c);   // 삼연발 추가분 = 간접 사용 (같은 사용 번호)
        }
        if (HasSpecial(Embers) && !CheatDisabled) SpawnEmbers(use);
    }

    /// <summary>스킬 1회 발동 = 내부 공격 AttackCount 회 (난개면 공격마다 3방향)</summary>
    IEnumerator CastOnce(CastRef cast, UseState use, int castIndex)
    {
        int hits = AttackCount;
        // [임시] 난도질로 공격이 늘어도 한 번 사용 시간은 기본(3타)과 비슷하게: 간격 = 기본 간격 × 2 / (횟수 − 1)
        float interval = hits > BaseHits ? T.tripleSlashInterval * (BaseHits - 1) / (hits - 1) : T.tripleSlashInterval;
        bool ilseom = HasSpecial(Ilseom);
        var ilseomFrames = ilseom ? SpriteEffect.LoadFrames(IlseomSheet, 1, 5, 1f) : null;
        var flameTex = (!ilseom && !TsubameOn) ? SpriteLib.Load(FlameSheet) : null;   // 기본 3타: 새 불꽃 참격 (없으면 예전 시트)
        // 특수 조합 츠바메가에시: 일섬 이펙트 대신 전용 이펙트 (시트가 없으면 일섬 이펙트로)
        var tsubameFrames = TsubameOn ? SpriteEffect.LoadFrames(TsubameSheet, 1, 5, 1f) : null;

        for (int i = 0; i < hits; i++)
        {
            Vector2 from = P.transform.position;
            Vector2 facing = P.Facing;
            float r = RangeOf(Hit.radius);
            if (!poseBusy && castIndex == 0 && i == 0 && P.ShowPose(UsePoses[0])) StartCoroutine(UsePoseRest());   // 포즈 3장 (그림이 없으면 아래 기존 모션)
            else if (!poseBusy) P.PlayAttack(facing);   // 첫 타에만 모션 (재생 중이면 무시)

            foreach (var dir in Directions(facing))
            {
                // 이펙트: 일섬 = 전용 시트, 그 외 = 불꽃 참격 3개를 3타마다 한 번
                if (tsubameFrames != null)
                {
                    // 츠바메가에시: 첫 발동에서 참격 3개를 0.1초 간격으로 (이후 발동 · 추가 사용은 이펙트 없음)
                    if (castIndex == 0 && i == 0) StartCoroutine(SpawnTsubameSequence(tsubameFrames, from, dir, r));
                }
                else if (ilseomFrames != null) SpawnIlseomFx(ilseomFrames, from, dir, r);
                else if (flameTex != null && i % BaseHits == 0) StartCoroutine(SpawnFlameSlashes(flameTex, from + dir * (r * FxForward), dir, r));
                else if (flameTex == null) Fx.Arc(from, dir, r, i == hits - 1 ? Fx.FireBright : Fx.Fire, 0.15f);   // 새 시트를 못 읽을 때만 간단한 호

                var dmg = MakeDamage(DamagePerHit, cast);
                if (HasSpecial(Searing)) dmg.perTarget = SearingMul;   // 고열 절삭: 맞은 적마다 (대상 단계)
                use.lastHitAmount = dmg.amount;
                Enemy firstHit = null;
                int hitCount = SkillHits.Directed(Hit, from, dir, RangeScale, dmg, e => { if (firstHit == null) firstHit = e; });   // 모양은 SkillTuning.tripleSlash
                // 적중 시 (= 직접 사용 + 직접 피해 + 피해 적용): 첫 발동만 직접 사용 → 임팩트 프레임 (사용 1번에 1회)
                if (hitCount > 0 && tsubameFrames != null && castIndex == 0 && !use.impacted)
                {
                    use.impacted = true;
                    Vector2 hitAt = firstHit != null ? (Vector2)firstHit.transform.position : from + dir * (r * FxForward);
                    ImpactFrame.Play(ImpactDelay, ImpactSeconds, firstHit != null ? firstHit.transform : null, hitAt, r * ImpactParticleWidth);
                }
                use.strikes.Add(new Strike { from = from, dir = dir });
            }
            if (i < hits - 1) yield return new WaitForSeconds(interval);
        }
    }

    /// <summary>사용 모션: 첫 포즈는 이미 보이는 중 → 나머지 포즈를 차례로 → 끝나면 풀어서 원래 모습으로</summary>
    IEnumerator UsePoseRest()
    {
        poseBusy = true;
        yield return new WaitForSeconds(UsePoseStep);
        for (int k = 1; k < UsePoses.Length; k++)
        {
            P.ShowPose(UsePoses[k]);
            yield return new WaitForSeconds(UsePoseStep);
        }
        P.ReleasePose();
        poseBusy = false;
    }

    /// <summary>기본 3타: 불꽃 참격 3개를 FlameInterval 간격으로, 방향 · 휨을 달리해 같은 자리에 띄움 (BentSlashFx 로 그림을 곡선으로 휘어 그림). 위치 · 방향은 시작 때 값 고정</summary>
    IEnumerator SpawnFlameSlashes(Texture2D tex, Vector2 at, Vector2 dir, float r)
    {
        float baseAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;   // 기준 = 보는 방향 (위 · 아래도 그 방향). 참격은 이 방향 기준으로 FlameTurns 만큼 돌림 (사용자 2026-11-22: 보는 방향에 맞춰 바뀌게)
        // 휨 부호는 뒤집지 않음: 참격 묶음 전체를 보는 방향만큼 그대로 돌린 모양이라야 모임 (왼쪽을 볼 때 부호를 뒤집었더니 한곳에 모이지 않았음, 사용자 2026-11-22)
        float width = r * FlameWidth;
        for (int k = 0; k < FlameTurns.Length; k++)
        {
            if (k > 0) yield return new WaitForSeconds(FlameInterval);
            float angle = baseAngle + FlameTurns[k];
            BentSlashFx.Spawn(tex, 4, 3, 11, FlameFps, at, angle, width, FlameSags[k], 11);
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

    /// <summary>고열 절삭: 대상 화상 스택 합계 1당 +5%, 상한 +75% (내부 합연산 → 배율 1개)</summary>
    static float SearingMul(Enemy e) => 1f + Mathf.Min(SearingCap, SearingPerStack * FlameSword.StacksOn(e));

    /// <summary>
    /// 츠바메가에시 참격 3개를 TsubameSlashInterval 간격으로 띄움 (이펙트만, 피해 판정은 그대로).
    /// [해석] 위치 · 방향은 첫 참격 때 잡은 값 고정 (그사이 플레이어가 움직여도 같은 자리에 모임)
    /// </summary>
    IEnumerator SpawnTsubameSequence(Sprite[] frames, Vector2 from, Vector2 dir, float r)
    {
        for (int k = 0; k < TsubameSlashes; k++)
        {
            if (k > 0) yield return new WaitForSeconds(TsubameSlashInterval);
            SpawnTsubameFx(frames, from, dir, r, k);
        }
    }

    /// <summary>츠바메가에시 (특수 조합) 이펙트: 일섬과 같은 방식으로 방향을 맞춘 뒤, 순서 x 120° 만큼 중심 기준으로 더 돌림</summary>
    void SpawnTsubameFx(Sprite[] frames, Vector2 from, Vector2 dir, float r, int slashIndex)
    {
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - TsubameAngle + TsubameStep * slashIndex;
        float width = r * TsubameWidth;
        // 칸 중심을 기준으로 돌리면 호의 무게중심이 중심에서 떨어져 있어 3개가 삼각형 고리로 퍼져 보임 →
        // 각 참격을 (돌린 뒤의) 무게중심 방향 반대로 옮겨 가운데로 모음. TsubameGather 0 = 옮기지 않음, 1 = 무게중심을 중심에 겹침
        Vector2 gather = -(Vector2)(Quaternion.Euler(0f, 0f, angle) * TsubameCentroid) * (width * TsubameGather);
        SpriteEffect.Spawn(frames, TsubameFps, null, from + dir * (r * FxForward) + gather, angle, false, 11, width);
    }

    void SpawnIlseomFx(Sprite[] frames, Vector2 from, Vector2 dir, float r)
    {
        if (frames == null) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - IlseomAngle;   // 시트의 45° 대각선을 공격 방향에 맞춤
        SpriteEffect.Spawn(frames, IlseomFps, null, from + dir * (r * FxForward), angle, false, 11, r * IlseomWidth);
    }

    // ---------- 피해 적용 트리거 ----------
    void OnEnable() { Enemy.Damaging += OnDamaging; Enemy.AfterDamaged += OnAfterDamaged; }
    protected override void OnDisable() { base.OnDisable(); if (poseBusy) { poseBusy = false; if (P != null) P.ReleasePose(); } Enemy.Damaging -= OnDamaging; Enemy.AfterDamaged -= OnAfterDamaged; }

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
        var flameTex = SpriteLib.Load(FlameSheet);   // 새 불꽃 참격 (수평, 반 속도)
        if (flameTex != null)
            BentSlashFx.Spawn(flameTex, 4, 3, 11, FlameFps * 0.5f, s.from + s.dir * (RangeOf(Hit.radius) * FxForward),
                              s.dir.x >= 0f ? 0f : 180f, RangeOf(Hit.radius) * FlameWidth * 0.8f, FlameSags[2], 11);
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
