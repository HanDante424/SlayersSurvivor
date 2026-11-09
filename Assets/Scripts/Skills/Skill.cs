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

    // 특수 강화가 더하는 수치 (스킬별 override). 기본 강화와 같은 종류끼리 합연산.
    /// <summary>특수 강화 피해 증가율 합계</summary>
    protected virtual float SpecialDamageBonus => 0f;
    /// <summary>특수 강화 공격 범위 증가율 합계</summary>
    protected virtual float SpecialRangeBonus => 0f;
    /// <summary>특수 강화 쿨타임 감소율 (기본 강화 감소율과 합산 후 90% 상한)</summary>
    protected virtual float SpecialCooldownReduction => 0f;
    /// <summary>특수 강화 쿨타임 증가율 (+25% = 0.25). 감소율과 합연산: 배율 = 1 - 감소 + 증가</summary>
    protected virtual float SpecialCooldownIncrease => 0f;

    /// <summary>피해 증가율 전체 (기본 + 특수)</summary>
    public float TotalDamageBonus => DamageBonus + SpecialDamageBonus;
    /// <summary>공격 범위 증가율 전체 (기본 + 특수)</summary>
    public float TotalRangeBonus => RangeBonus + SpecialRangeBonus;
    protected virtual void OnLevelChanged() { }

    // ---------- 최종 수치 (기본 수치 + 기본 강화 합연산) ----------
    /// <summary>1타 최종 피해량 = floor(기본 × (1 + 공격력 증가율 합계)). 다단히트는 타마다 이 값</summary>
    protected int HitDamage(float baseDamage) =>
        Mathf.FloorToInt(baseDamage * (1f + TotalDamageBonus) * P.damageMul + 1e-4f);   // 1e-4: 10×1.3 같은 부동소수 오차 보정

    /// <summary>
    /// 쿨타임 배율 (치트 제외). 기본 = (1 - min(기본+특수 감소율, 0.9) + 특수 증가율), 최소 0.1.
    /// 계산 순서가 따로 정해진 강화(파이어 슬래시 빠른 공격·묵직한 일격)는 스킬에서 override.
    /// </summary>
    protected virtual float CooldownMultiplier => GeneralCooldownMultiplier;

    /// <summary>일반 감소·증가만 적용한 쿨타임 배율 (최종 10% 미만으로는 안 내려감)</summary>
    protected float GeneralCooldownMultiplier
    {
        get
        {
            float cap = T.cooldownReductionCap;
            float red = Mathf.Min(CooldownReduction + SpecialCooldownReduction, cap);
            return Mathf.Max(1f - cap, (1f - red + SpecialCooldownIncrease) * P.cooldownMul);
        }
    }

    /// <summary>최종 쿨타임 = 기본 × CooldownMultiplier</summary>
    protected float CooldownOf(float baseSeconds) =>
        DebugCheats.NoCooldown ? 0f : baseSeconds * CooldownMultiplier;   // 치트 F6: 쿨타임 0

    /// <summary>최종 범위 = 기본 × (1 + 범위 증가율 합계(기본+특수)). 정수화 규칙은 미정 → 그대로</summary>
    protected float RangeOf(float baseRange) => baseRange * (1f + TotalRangeBonus) * P.areaMul;
    /// <summary>범위 배율 (판정 도형 전체에 곱함)</summary>
    protected float RangeScale => RangeOf(1f);

    // ---------- 등급 → 숫자 (기본 수치가 아직 없는 스킬용 임시 환산표) ----------
    protected float Damage(Power p) => T.Damage(p) * (1f + TotalDamageBonus) * P.damageMul;
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
        Activate();
        return true;
    }

    protected abstract void Activate();
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
}
