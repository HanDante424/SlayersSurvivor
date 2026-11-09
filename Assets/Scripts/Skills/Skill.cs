using System;
using UnityEngine;

/// <summary>스킬 종류 (기획서 2.1)</summary>
public enum SkillKind { Auto, Manual, Movement, Passive, Ultimate }

/// <summary>
/// 모든 스킬의 공통 기반.
/// - 스킬 레벨 1~15 (캐릭터 레벨과 별개). Lv.5·10·15는 특수 강화 시점 (효과는 기획 미설계 → 훅만 존재)
/// - 수치는 등급(Power/Tier)으로만 지정하고, 숫자는 SkillTuning 임시 환산표에서 가져옴
/// </summary>
public abstract class Skill : MonoBehaviour
{
    public const int MaxLevel = 15;
    public static readonly int[] MilestoneLevels = { 5, 10, 15 };

    public SkillEntry Entry { get; private set; }
    public int level = 1;

    protected Player P => Player.I;
    protected static SkillTuning T => Tuning.Skills;

    public virtual int LevelCap => Entry.kind == SkillKind.Ultimate ? T.ultimateMaxLevel : MaxLevel;

    public void Init(SkillEntry entry) => Entry = entry;

    public static bool IsMilestone(int lv) => Array.IndexOf(MilestoneLevels, lv) >= 0;

    public void LevelUp()
    {
        if (level >= LevelCap) return;
        level++;
        // 특수 강화 효과는 스킬별 기획 확정 후 OnMilestone 에서 구현
        if (Entry.kind != SkillKind.Ultimate && IsMilestone(level)) OnMilestone(level);
        OnLevelChanged();
    }

    /// <summary>Lv.5·10·15 특수 강화 (기획 미설계 — 현재 아무 효과 없음)</summary>
    protected virtual void OnMilestone(int lv) { }
    protected virtual void OnLevelChanged() { }

    // ---------- 등급 → 숫자 (임시 환산표 + 스킬 레벨 성장) ----------
    protected float Damage(Power p) =>
        T.Damage(p) * (1f + T.damagePerLevel * (level - 1)) * P.damageMul;

    protected float Cooldown(Tier t) =>
        T.Cooldown(t) * Mathf.Max(T.cooldownFloor, 1f - T.cooldownPerLevel * (level - 1)) * P.cooldownMul;

    protected float Range(Tier t) =>
        T.Range(t) * (1f + T.rangePerLevel * (level - 1)) * P.areaMul;

    protected float Reach(Tier t) => T.Reach(t) * (1f + T.rangePerLevel * (level - 1));

    // ---------- 자동 스킬용 ----------
    /// <summary>매 프레임 (Playing 상태에서만) 호출</summary>
    public virtual void Tick(float dt) { }

    // ---------- 수동 / 이동 / 궁극기용 ----------
    /// <summary>HUD 표시용: 0=사용 가능, 1=방금 사용</summary>
    public virtual float CooldownFraction => 0f;
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

    public override void Tick(float dt) { if (cdLeft > 0) cdLeft -= dt; }

    public override bool TryActivate()
    {
        if (cdLeft > 0 || P.IsBusy) return false;
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
        timer -= dt;
        if (timer > 0) return;
        // 발동할 대상이 없으면 쿨타임을 소모하지 않고 다음 프레임에 다시 확인
        if (!Fire()) { timer = 0f; return; }
        timer = CooldownSeconds;
    }

    /// <summary>발동. 대상이 없어 발동하지 않았으면 false</summary>
    protected abstract bool Fire();
}
