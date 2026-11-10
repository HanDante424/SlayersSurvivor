using System;
using UnityEngine;

/// <summary>스킬 종류 (기획서 2.1)</summary>
public enum SkillKind { Auto, Manual, Movement, Passive, Ultimate }

/// <summary>기본 강화 3종 (기획 v0.2 4장). 1회 선택 효과는 SkillTuning (공격력 +10% / 범위 +10% / 쿨타임 -5%)</summary>
public enum BasicUpgrade { Damage, Range, Cooldown }

/// <summary>특수 강화 선택지 1개. 스킬별 SpecialOptions 가 만들어 줌</summary>
public class SpecialUpgradeOption
{
    /// <summary>스킬 안에서 고유한 식별자 (예: "F5A"). 획득 여부는 Skill.HasSpecial(id)</summary>
    public string id;
    public string title, desc;
    /// <summary>선택 불가 (다른 특수 강화와 충돌). 화면에는 회색으로 표시, lockReason 을 설명으로</summary>
    public bool locked;
    public string lockReason;
    /// <summary>선택 시 확인 창을 띄울 경고문 (null = 바로 적용)</summary>
    public string warning;
}

/// <summary>패시브 고유 기본 강화 선택지 1개</summary>
public class PassiveUpgradeOption
{
    public string title, desc;
}

/// <summary>
/// 모든 스킬의 공통 기반.
/// - 스킬 레벨 1~15 (캐릭터 레벨과 별개)
/// - 일반 스킬 성장 (기획 v0.2): 보유 스킬을 레벨업 후보로 고르면 기본 강화 1개 선택 → 스킬 레벨 +1.
///   스킬 레벨 5·10·15 에 도달하면 이어서 특수 강화 선택 (효과는 기획 미설계 → 훅만 존재).
/// - 강화율은 종류별로 합연산. 피해량 = floor(기본 × (1 + 합계)), 쿨타임 감소 합계는 최대 90%.
/// - 궁극기는 기본 강화 없이 레벨만 오름 (캐릭터 Lv.60 강화, 별도 규칙)
/// </summary>
public abstract class Skill : MonoBehaviour
{
    public const int MaxLevel = 15;
    public static readonly int[] MilestoneLevels = { 5, 10, 15 };
    static readonly SpecialUpgradeOption[] NoSpecialOptions = new SpecialUpgradeOption[0];

    public SkillEntry Entry { get; private set; }
    public int level = 1;

    protected Player P => Player.I;
    protected static SkillTuning T => Tuning.Skills;

    public virtual int LevelCap => Entry.kind == SkillKind.Ultimate ? T.ultimateMaxLevel : MaxLevel;
    /// <summary>일반 스킬 = 기본/특수 강화 대상 (궁극기 제외)</summary>
    public bool IsGeneral => Entry.kind != SkillKind.Ultimate;

    public void Init(SkillEntry entry) => Entry = entry;

    public static bool IsMilestone(int lv) => Array.IndexOf(MilestoneLevels, lv) >= 0;

    // ---------- 기본 강화 (합연산) ----------
    public int DamageUps { get; private set; }
    public int RangeUps { get; private set; }
    public int CooldownUps { get; private set; }

    /// <summary>공격력 증가율 합계 (0.3 = +30%)</summary>
    public float DamageBonus => T.upgradeDamage * DamageUps;
    /// <summary>범위 증가율 합계</summary>
    public float RangeBonus => T.upgradeRange * RangeUps;
    /// <summary>쿨타임 감소율 합계 (최대 cooldownReductionCap = 90%)</summary>
    public float CooldownReduction => Mathf.Min(T.upgradeCooldown * CooldownUps, T.cooldownReductionCap);

    /// <summary>기본 강화 1개 적용 후 스킬 레벨 +1. 특수 강화는 이 다음에 SpecialPending 으로 확인</summary>
    public void ApplyBasicUpgrade(BasicUpgrade u)
    {
        if (!IsGeneral || level >= LevelCap) return;
        switch (u)
        {
            case BasicUpgrade.Damage: DamageUps++; break;
            case BasicUpgrade.Range: RangeUps++; break;
            case BasicUpgrade.Cooldown: CooldownUps++; break;
        }
        level++;
        OnLevelChanged();
    }

    // ---------- 패시브 고유 기본 강화 (공통 기본 강화 대신 패시브마다 3종) ----------
    /// <summary>패시브 고유 기본 강화 선택지. null = 공통 기본 강화(피해량·범위·쿨타임) 사용</summary>
    public virtual PassiveUpgradeOption[] PassiveBasicOptions() => null;

    /// <summary>패시브 고유 기본 강화 1개 적용 후 스킬 레벨 +1</summary>
    public void ApplyPassiveBasic(int index)
    {
        if (!IsGeneral || level >= LevelCap) return;
        OnPassiveBasic(index);
        level++;
        OnLevelChanged();
    }

    protected virtual void OnPassiveBasic(int index) { }

    /// <summary>HUD 표시용 기본 강화 누적 요약 (null = 공통 기본 강화 표시)</summary>
    public virtual string BasicSummary => null;

    /// <summary>궁극기 강화 (기본 강화 없이 레벨만)</summary>
    public void LevelUp()
    {
        if (level >= LevelCap) return;
        level++;
        OnLevelChanged();
    }

    // ---------- 특수 강화 (Lv.5·10·15, 스킬 레벨 기준) ----------
    // 단계별로 1개씩 고르고, 고른 강화는 모두 유지·중첩. 효과는 각 스킬이 HasSpecial(id) 로 확인해서 적용.
    readonly System.Collections.Generic.Dictionary<int, string> specialPicks = new System.Collections.Generic.Dictionary<int, string>();

    /// <summary>현재 레벨이 특수 강화 레벨인데 아직 고르지 않았으면 true</summary>
    public bool SpecialPending => IsGeneral && IsMilestone(level) && !specialPicks.ContainsKey(level);

    /// <summary>이 특수 강화를 획득했는지</summary>
    public bool HasSpecial(string id) => !string.IsNullOrEmpty(id) && specialPicks.ContainsValue(id);

    /// <summary>
    /// 특수 강화 선택지 (스킬별 override). 비어 있으면 선택 화면에 "미설계" 확인 버튼만 표시.
    /// 선택 불가·경고는 이미 고른 강화를 보고 여기서 채움.
    /// </summary>
    public virtual SpecialUpgradeOption[] SpecialOptions(int lv) => NoSpecialOptions;

    /// <param name="option">SpecialOptions 의 인덱스, 선택지가 없으면 -1</param>
    public void ApplySpecial(int lv, int option)
    {
        if (specialPicks.ContainsKey(lv)) return;
        var opts = SpecialOptions(lv);
        string id = "";
        if (option >= 0 && option < opts.Length)
        {
            if (opts[option].locked) return;   // 선택 불가 후보
            id = opts[option].id;
        }
        specialPicks[lv] = id;
        OnSpecialUpgrade(lv, id);
    }

    /// <summary>HUD 표시용: 획득한 특수 강화 이름 (Lv 순)</summary>
    public string SpecialSummary
    {
        get
        {
            var names = new System.Collections.Generic.List<string>();
            foreach (int lv in MilestoneLevels)
            {
                if (!specialPicks.TryGetValue(lv, out var id) || string.IsNullOrEmpty(id)) continue;
                foreach (var o in SpecialOptions(lv)) if (o.id == id) { names.Add(o.title); break; }
            }
            return string.Join(" · ", names);
        }
    }

    protected virtual void OnSpecialUpgrade(int lv, string id) { }

    // 특수 강화가 더하는 수치 (스킬별 override). 키워드별 결합 규칙은 Docs/Design/Combat_Decisions_v1.0.md
    /// <summary>특수 강화 '피해량' % 합계 (기본 강화 피해량과 합연산)</summary>
    protected virtual float SpecialDamageBonus => 0f;
    /// <summary>이 스킬 몫의 '최종 피해량' 배율 (독립 효과끼리 곱해서 반환. 예: 플레어 ×1.5)</summary>
    protected virtual float SpecialFinalDamageMul => 1f;
    /// <summary>특수 강화 공격 범위 증가율 합계</summary>
    protected virtual float SpecialRangeBonus => 0f;
    /// <summary>특수 강화 쿨타임 독립 배율 (곱연산. 감소 0.75 · 증가 1.25 …)</summary>
    protected virtual float SpecialCooldownMul => 1f;

    /// <summary>'피해량' % 전체 (기본 + 특수)</summary>
    public float TotalDamageBonus => DamageBonus + SpecialDamageBonus;
    /// <summary>공격 범위 증가율 전체 (기본 + 특수)</summary>
    public float TotalRangeBonus => RangeBonus + SpecialRangeBonus;
    protected virtual void OnLevelChanged() { }

    // ---------- 피해 (① 공격자 단계) ----------
    /// <summary>
    /// 이 스킬의 피해 1건 = 초기 피해량 → 피해량(기본+특수+전역) → 추가 피해량 → 최종 피해량(스킬 몫 × 전역).
    /// 내림은 대상 단계(Enemy.Hit)에서. 다단히트는 타마다 이 값.
    /// </summary>
    protected DamageInfo MakeDamage(float initial, CastKind cast = CastKind.Direct, DamageType type = DamageType.Direct)
    {
        var d = new DamageInfo(0f, type, this, cast);
        if (cast == CastKind.Direct) d.castId = CastId;
        d.amount = DamageMath.Attacker(d, initial, TotalDamageBonus, 0f, SpecialFinalDamageMul);
        return d;
    }

    /// <summary>
    /// 발동 시점에 잡아 둔 발동 구분 + 번호. 지연 공격(삼연발 · 검기 · 연속 공격 · 다단히트)이
    /// 다음 발동의 번호를 받지 않도록 발동 시작 때 만들어서 넘김
    /// </summary>
    protected readonly struct CastRef
    {
        public readonly CastKind kind;
        public readonly int id;
        public CastRef(CastKind kind, int id) { this.kind = kind; this.id = id; }
        public static CastRef Indirect => new CastRef(CastKind.Indirect, 0);
    }

    /// <summary>지금 진행 중인 직접 발동</summary>
    protected CastRef DirectCast => new CastRef(CastKind.Direct, CastId);

    /// <summary>발동 시점에 잡아 둔 cast 로 피해 1건 (지연 공격용)</summary>
    protected DamageInfo MakeDamage(float initial, CastRef cast, DamageType type = DamageType.Direct)
    {
        var d = new DamageInfo(0f, type, this, cast.kind);
        d.castId = cast.kind == CastKind.Direct ? cast.id : 0;
        d.amount = DamageMath.Attacker(d, initial, TotalDamageBonus, 0f, SpecialFinalDamageMul);
        return d;
    }

    /// <summary>고정 피해 (모든 증감 무효). 참조값 계산은 호출하는 쪽에서</summary>
    protected DamageInfo FixedDamage(float amount, DamageType type, CastKind cast = CastKind.Direct) =>
        new DamageInfo(amount, type, this, cast, isFixed: true);

    // ---------- 발동 식별 ----------
    static int castCounter;
    /// <summary>가장 최근 직접 발동의 번호. 그 발동에서 나온 피해(지연·지속 포함)가 같은 번호를 가짐</summary>
    public int CastId { get; private set; }
    /// <summary>직접 발동 시작 (쿨타임 자동 발동 · 키 입력) 시 호출</summary>
    protected void BeginCast() => CastId = ++castCounter;

    // ---------- 쿨타임 ----------
    /// <summary>스킬 기본 강화 쿨타임 감소 → 배율 1개 (기본 강화끼리 합연산)</summary>
    protected float BasicCooldownMul => 1f - CooldownReduction;

    /// <summary>
    /// 쿨타임 배율 (치트 제외) = 기본 강화 배율 × 특수 강화 독립 배율 × 전역 독립 배율 (모두 곱연산), 최종 감소 90% 상한.
    /// 계산 순서가 따로 정해진 강화(파이어 슬래시 묵직한 일격)는 스킬에서 override.
    /// </summary>
    protected virtual float CooldownMultiplier => GeneralCooldownMultiplier;

    /// <summary>일반 규칙만 적용한 쿨타임 배율 (최종 10% 미만으로는 안 내려감, 증가는 상한 없음)</summary>
    protected float GeneralCooldownMultiplier =>
        Mathf.Max(1f - T.cooldownReductionCap, BasicCooldownMul * SpecialCooldownMul * CombatMods.CooldownMul(this) * P.cooldownMul);

    /// <summary>
    /// 남은 쿨타임 감소 (빠른 준비 · 불길의 순환): 지금 남은 시간 × (1 - ratio). 90% 상한과 무관.
    /// 쿨타임이 없는 스킬은 무시
    /// </summary>
    public virtual void ReduceRemainingCooldown(float ratio) { }

    /// <summary>최종 쿨타임 = 기본 × CooldownMultiplier</summary>
    protected float CooldownOf(float baseSeconds) =>
        DebugCheats.NoCooldown ? 0f : baseSeconds * CooldownMultiplier;   // 치트 F6: 쿨타임 0

    /// <summary>최종 범위 = 기본 × (1 + 범위 증가율 합계(기본+특수)). 정수화 규칙은 미정 → 그대로</summary>
    protected float RangeOf(float baseRange) => baseRange * (1f + TotalRangeBonus) * P.areaMul;
    /// <summary>범위 배율 (판정 도형 전체에 곱함)</summary>
    protected float RangeScale => RangeOf(1f);

    // ---------- 등급 → 숫자 (기본 수치가 아직 없는 스킬용 임시 환산표) ----------
    /// <summary>
    /// 임시 스킬용: 등급 피해 × share 를 초기 피해량으로 한 이 스킬의 피해 1건 (출처·유형 유지).
    /// 다단히트·지속 피해는 share 로 나눈 값이 이벤트마다 내림됨 (예: 파이어 스톰 틱 1.25 → 1)
    /// </summary>
    protected DamageInfo PowerDamage(Power p, float share = 1f, DamageType type = DamageType.Direct) =>
        MakeDamage(T.Damage(p) * share, CastKind.Direct, type);
    protected float Cooldown(Tier t) => CooldownOf(T.Cooldown(t));
    protected float Range(Tier t) => RangeOf(T.Range(t));
    protected float Reach(Tier t) => T.Reach(t) * (1f + TotalRangeBonus);

    // ---------- 자동 스킬용 ----------
    /// <summary>매 프레임 (Playing 상태에서만) 호출</summary>
    public virtual void Tick(float dt) { }

    // ---------- 수동 / 이동 / 궁극기용 ----------
    /// <summary>HUD 표시용: 0=사용 가능, 1=방금 사용</summary>
    public virtual float CooldownFraction => 0f;
    /// <summary>HUD 표시용: 남은 쿨타임(초). 시간으로 표시할 수 없는 스킬(궁극기 게이지 등)은 0</summary>
    public virtual float CooldownRemaining => 0f;
    /// <summary>치트 패널에서 이 스킬만 사용 금지됨</summary>
    public bool CheatDisabled => Entry != null && DebugCheats.IsSkillDisabled(Entry.id);
    /// <summary>HUD 표시용 추가 정보 (스택 수 등)</summary>
    public virtual string Badge => null;
    /// <summary>키를 눌렀을 때. 사용했으면 true</summary>
    public virtual bool TryActivate() => false;

    protected static Enemy Nearest(Vector2 from, float maxDist)
    {
        Enemy best = null;
        float bestSq = maxDist * maxDist;
        foreach (var e in Enemy.All)
        {
            if (e.hp <= 0) continue;
            float sq = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    /// <summary>
    /// [임시 타겟팅] 기획서상 스킬별 타겟팅은 미정 → 범위 안 가장 가까운 적, 없으면 바라보는 방향
    /// </summary>
    protected Vector2 AutoAimDir(float maxDist)
    {
        Vector2 from = P.transform.position;
        var t = Nearest(from, maxDist);
        return t != null ? ((Vector2)t.transform.position - from).normalized : P.Facing;
    }
}

/// <summary>쿨타임이 있는 수동형 스킬 공통</summary>
public abstract class ActiveSkill : Skill
{
    protected float cdLeft;
    protected abstract float CooldownSeconds { get; }

    public override float CooldownFraction => cdLeft <= 0 ? 0f : Mathf.Clamp01(cdLeft / Mathf.Max(0.01f, CooldownSeconds));
    public override float CooldownRemaining => Mathf.Max(0f, cdLeft);

    public override void Tick(float dt)
    {
        if (DebugCheats.NoCooldown) cdLeft = 0f;   // 이미 돌고 있던 쿨타임도 즉시 해제
        if (cdLeft > 0) cdLeft -= dt;
    }

    public override bool TryActivate()
    {
        if (cdLeft > 0 || P.IsBusy || CheatDisabled) return false;
        cdLeft = CooldownSeconds;
        BeginCast();
        Activate();
        return true;
    }

    protected abstract void Activate();

    public override void ReduceRemainingCooldown(float ratio)
    {
        if (cdLeft > 0f) cdLeft *= Mathf.Clamp01(1f - ratio);
    }
}

/// <summary>쿨타임마다 자동 발동하는 스킬 공통</summary>
public abstract class AutoSkill : Skill
{
    protected float timer;
    protected abstract float CooldownSeconds { get; }

    public override void Tick(float dt)
    {
        if (DebugCheats.NoAutoSkills || CheatDisabled) return; // 치트 F7 / 스킬별 사용 금지
        if (Casting) return;                                   // 공격 진행 중 = 쿨타임 대기 (끝나면 그때부터 쿨타임)
        if (DebugCheats.NoCooldown && timer > 0f) timer = 0f;   // 치트 F6
        timer -= dt;
        if (timer > 0) return;
        // 발동할 대상이 없으면 쿨타임을 소모하지 않고 다음 프레임에 다시 확인
        BeginCast();
        if (!Fire()) { timer = 0f; return; }
        timer = CooldownSeconds + CastDuration;
    }

    /// <summary>발동. 대상이 없어 발동하지 않았으면 false</summary>
    protected abstract bool Fire();

    /// <summary>쿨타임이 공격 종료 후부터 도는 지속형 공격의 지속시간 (솔라 슬래시 타오름 등). 기본 0 = 발동 즉시 쿨타임</summary>
    protected virtual float CastDuration => 0f;

    /// <summary>true 동안은 쿨타임이 줄지 않음. 길이가 정해지지 않은 연속 공격(솔라 슬래시 메아리·연속 폭발 등)이 끝날 때 false 로</summary>
    protected bool Casting;

    protected virtual void OnDisable() => Casting = false;   // 코루틴이 끊겨도 영구 정지하지 않게

    /// <summary>
    /// 남은 쿨타임 감소. 공격 진행 중(Casting)에는 쿨타임이 아직 시작 전이라 무시.
    /// 지속형 공격(타오름)도 Casting 으로 처리 → 지속 중에는 줄지 않고, 끝난 뒤 쿨타임에만 적용
    /// </summary>
    public override void ReduceRemainingCooldown(float ratio)
    {
        if (!Casting && timer > 0f) timer *= Mathf.Clamp01(1f - ratio);
    }
}
