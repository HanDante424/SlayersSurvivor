using System;
using UnityEngine;

/// <summary>스킬 종류 (기획서 2.1)</summary>
public enum SkillKind { Auto, Manual, Movement, Passive, Ultimate }

/// <summary>기본 강화 3종 (기획 v0.2 4장). 1회 선택 효과는 SkillTuning (공격력 +10% / 범위 +10% / 쿨타임 -5%)</summary>
public enum BasicUpgrade { Damage, Range, Cooldown }

/// <summary>특수 강화 선택지 1개 (기획 미설계 → 아직 어떤 스킬도 만들지 않음)</summary>
public class SpecialUpgradeOption
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

    /// <summary>궁극기 강화 (기본 강화 없이 레벨만)</summary>
    public void LevelUp()
    {
        if (level >= LevelCap) return;
        level++;
        OnLevelChanged();
    }

    // ---------- 특수 강화 (Lv.5·10·15, 스킬 레벨 기준) ----------
    readonly System.Collections.Generic.HashSet<int> specialTaken = new System.Collections.Generic.HashSet<int>();

    /// <summary>현재 레벨이 특수 강화 레벨인데 아직 고르지 않았으면 true</summary>
    public bool SpecialPending => IsGeneral && IsMilestone(level) && !specialTaken.Contains(level);

    /// <summary>
    /// 특수 강화 선택지. 기획 미설계 → 기본은 비어 있음 (선택 화면에는 "미설계" 확인 버튼만 표시).
    /// 효과가 확정되면 스킬별로 override.
    /// </summary>
    public virtual SpecialUpgradeOption[] SpecialOptions(int lv) => NoSpecialOptions;

    /// <param name="option">SpecialOptions 의 인덱스, 선택지가 없으면 -1</param>
    public void ApplySpecial(int lv, int option)
    {
        if (!specialTaken.Add(lv)) return;
        OnSpecialUpgrade(lv, option);
    }

    protected virtual void OnSpecialUpgrade(int lv, int option) { }
    protected virtual void OnLevelChanged() { }

    // ---------- 최종 수치 (기본 수치 + 기본 강화 합연산) ----------
    /// <summary>1타 최종 피해량 = floor(기본 × (1 + 공격력 증가율 합계)). 다단히트는 타마다 이 값</summary>
    protected int HitDamage(float baseDamage) =>
        Mathf.FloorToInt(baseDamage * (1f + DamageBonus) * P.damageMul + 1e-4f);   // 1e-4: 10×1.3 같은 부동소수 오차 보정

    /// <summary>최종 쿨타임 = 기본 × (1 - min(감소율 합계, 0.9))</summary>
    protected float CooldownOf(float baseSeconds) =>
        DebugCheats.NoCooldown ? 0f : baseSeconds * (1f - CooldownReduction) * P.cooldownMul;   // 치트 F6: 쿨타임 0

    /// <summary>최종 범위 = 기본 × (1 + 범위 증가율 합계). 정수화 규칙은 미정 → 그대로</summary>
    protected float RangeOf(float baseRange) => baseRange * (1f + RangeBonus) * P.areaMul;
    /// <summary>범위 배율 (판정 도형 전체에 곱함)</summary>
    protected float RangeScale => RangeOf(1f);

    // ---------- 등급 → 숫자 (기본 수치가 아직 없는 스킬용 임시 환산표) ----------
    protected float Damage(Power p) => T.Damage(p) * (1f + DamageBonus) * P.damageMul;
    protected float Cooldown(Tier t) => CooldownOf(T.Cooldown(t));
    protected float Range(Tier t) => RangeOf(T.Range(t));
    protected float Reach(Tier t) => T.Reach(t) * (1f + RangeBonus);

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
        if (DebugCheats.NoCooldown && timer > 0f) timer = 0f;   // 치트 F6
        timer -= dt;
        if (timer > 0) return;
        // 발동할 대상이 없으면 쿨타임을 소모하지 않고 다음 프레임에 다시 확인
        if (!Fire()) { timer = 0f; return; }
        timer = CooldownSeconds;
    }

    /// <summary>발동. 대상이 없어 발동하지 않았으면 false</summary>
    protected abstract bool Fire();
}
