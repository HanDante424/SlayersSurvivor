using System;
using System.Collections.Generic;
using UnityEngine;

// 피해 계산 · 전달 공통 (전투 결정 기록 v1.0 — Docs/Design/Combat_Decisions_v1.0.md 1~2장)
//
// ① 공격자 단계 (스킬에서 1회, 대상과 무관) — Skill.MakeDamage
//    초기 피해량 → × (1 + 피해량 합) → + 추가 피해량 → × 최종 피해량 배율들   = DamageInfo.amount (내림 전)
// ② 대상 단계 (맞은 적마다) — Enemy.Hit
//    × 대상 의존 최종 피해량 (perTarget) × 받는 최종 피해량 → 내림 → 체력 감소   (고정 피해는 전부 건너뜀)
// ③ 전달 후 트리거 — Enemy.Damaged 이벤트 (특수 피해는 트리거 없음)

/// <summary>피해 유형 (원인). 트리거 조건에 사용</summary>
public enum DamageType
{
    /// <summary>직접 피해: 플레이어 공격의 직접 적중 (화상 부여 · 살아있는 폭탄 · 화염 낙인 트리거)</summary>
    Direct,
    /// <summary>지속 피해: 디버프 · 장판 · 지속형 공격 (화상, 타오름, 잔화의 검흔 …). 소각 트리거</summary>
    DoT,
    /// <summary>아이템 피해</summary>
    Item,
    /// <summary>특수 피해: 어떤 트리거도 발동시키지 않음 (소각 등)</summary>
    Special,
}

/// <summary>발동 구분: 직접 = 플레이어가 최초 발동 (쿨타임 자동 발동 · 키 입력), 간접 = 연쇄로 발동 (죽음의 메아리 · 솔라 메아리 · 츠바메가에시 추가분)</summary>
public enum CastKind { Direct, Indirect }

/// <summary>피해 1건. amount 는 공격자 단계까지 끝난 값 (내림 전)</summary>
public struct DamageInfo
{
    public float amount;
    public DamageType type;
    /// <summary>고정 피해: 피해량 · 최종 피해량 · 받는 최종 피해량 등 모든 증감 무효</summary>
    public bool isFixed;
    /// <summary>출처 스킬 (없으면 null)</summary>
    public Skill source;
    public CastKind cast;
    /// <summary>화상처럼 특정 상태에 묶인 피해의 태그 (null = 일반). '화상' 대상 효과 판정용</summary>
    public string tag;
    /// <summary>대상 의존 최종 피해량 배율 (고열 절삭 등). null = 없음</summary>
    public Func<Enemy, float> perTarget;

    public DamageInfo(float amount, DamageType type = DamageType.Direct, Skill source = null,
                      CastKind cast = CastKind.Direct, bool isFixed = false, string tag = null)
    {
        this.amount = amount; this.type = type; this.source = source; this.cast = cast;
        this.isFixed = isFixed; this.tag = tag; perTarget = null;
    }

    /// <summary>예전 호출(숫자만)은 출처 없는 직접 피해로 취급 — 아직 새 구조로 옮기지 않은 임시 스킬용</summary>
    public static implicit operator DamageInfo(float amount) => new DamageInfo(amount);

    public DamageInfo WithAmount(float a) { var d = this; d.amount = a; return d; }
}

/// <summary>
/// 패시브 등이 피해·쿨타임 계산에 끼어드는 지점. 필요한 것만 override (기본값 = 영향 없음).
/// 등록: CombatMods.Add / Remove
/// </summary>
public abstract class CombatModifier
{
    /// <summary>'초기 피해량' 고정 증가 (예: 화상 초당 초기 피해량 +2)</summary>
    public virtual float InitialAdd(in DamageInfo d) => 0f;
    /// <summary>'피해량' % (합연산 단계, 감소는 음수)</summary>
    public virtual float DamagePct(in DamageInfo d) => 0f;
    /// <summary>'추가 피해량' 고정 수치 (피해량 % 이후)</summary>
    public virtual float FlatAdd(in DamageInfo d) => 0f;
    /// <summary>'최종 피해량' 배율 (독립 효과 → 곱연산). 효과 하나 안의 +%p 는 여기서 미리 합산해 배율 1개로</summary>
    public virtual float FinalMul(in DamageInfo d) => 1f;
    /// <summary>'받는 최종 피해량' 배율 (대상 단계)</summary>
    public virtual float ReceivedMul(Enemy target, in DamageInfo d) => 1f;
    /// <summary>쿨타임 독립 배율 (감소 0.9 · 증가 1.25 …). 곱연산</summary>
    public virtual float CooldownMul(Skill s) => 1f;
}

/// <summary>현재 적용 중인 전투 수정자 모음 (패시브 획득 시 등록)</summary>
public static class CombatMods
{
    static readonly List<CombatModifier> mods = new List<CombatModifier>();

    public static void Add(CombatModifier m) { if (m != null && !mods.Contains(m)) mods.Add(m); }
    public static void Remove(CombatModifier m) => mods.Remove(m);
    public static void Clear() => mods.Clear();

    public static float InitialAdd(in DamageInfo d) { float s = 0f; foreach (var m in mods) s += m.InitialAdd(d); return s; }
    public static float DamagePct(in DamageInfo d) { float s = 0f; foreach (var m in mods) s += m.DamagePct(d); return s; }
    public static float FlatAdd(in DamageInfo d) { float s = 0f; foreach (var m in mods) s += m.FlatAdd(d); return s; }
    public static float FinalMul(in DamageInfo d) { float p = 1f; foreach (var m in mods) p *= m.FinalMul(d); return p; }
    public static float ReceivedMul(Enemy e, in DamageInfo d) { float p = 1f; foreach (var m in mods) p *= m.ReceivedMul(e, d); return p; }
    public static float CooldownMul(Skill s) { float p = 1f; foreach (var m in mods) p *= m.CooldownMul(s); return p; }
}

/// <summary>피해 계산 공식 (한 곳에서만)</summary>
public static class DamageMath
{
    /// <summary>부동소수 오차 보정 (10 × 1.3 = 12.999… → 13)</summary>
    const float Eps = 1e-4f;

    /// <summary>
    /// ① 공격자 단계. initial = 초기 피해량, pct = 피해량 % 합 (출처 스킬 몫), flat = 추가 피해량 합, finalMul = 출처 스킬 몫 최종 배율.
    /// 전역 수정자(CombatMods)·플레이어 배율은 여기서 함께 적용. 고정 피해는 그대로 반환.
    /// </summary>
    public static float Attacker(in DamageInfo info, float initial, float pct, float flat, float finalMul)
    {
        if (info.isFixed) return initial;
        float v = (initial + CombatMods.InitialAdd(info)) * (1f + pct + CombatMods.DamagePct(info));
        v += flat + CombatMods.FlatAdd(info);
        v *= finalMul * CombatMods.FinalMul(info) * (Player.I != null ? Player.I.damageMul : 1f);
        return Mathf.Max(0f, v);
    }

    /// <summary>② 대상 단계 → 내림한 실제 피해</summary>
    public static int Target(Enemy e, in DamageInfo d)
    {
        float v = d.amount;
        if (!d.isFixed)
        {
            if (d.perTarget != null) v *= d.perTarget(e);
            v *= CombatMods.ReceivedMul(e, d);
        }
        return Mathf.Max(0, Mathf.FloorToInt(v + Eps));
    }
}
