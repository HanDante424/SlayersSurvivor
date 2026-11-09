using UnityEngine;

/// <summary>피해량 등급 (기획서: 매우 낮음 ~ 매우 높음)</summary>
public enum Power { VeryLow, Low, Mid, High, VeryHigh }

/// <summary>범위·쿨타임 등급 (기획서: 좁음/짧음 · 중간 · 넓음/김)</summary>
public enum Tier { Short, Mid, Long }

/// <summary>
/// 기획서의 정성적 등급을 숫자로 바꾸는 "임시 환산표".
/// ⚠ 여기 있는 숫자는 모두 기획 확정 전 임시값입니다. (Docs/Design/GDD_v0.1.md 10장)
///   - 개별 스킬 코드에는 숫자를 직접 쓰지 않고 등급만 씁니다.
///   - 기획 수치가 확정되면 이 파일(Assets/Resources/Config/SkillTuning.asset)만 고치면 됩니다.
/// Play 중 Inspector 에서 바꾸면 즉시 반영됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Slayers/Skill Tuning", fileName = "SkillTuning")]
public class SkillTuning : ScriptableObject
{
    [Header("[임시] 피해량: 매우 낮음 / 낮음 / 중간 / 높음 / 매우 높음")]
    public float[] damage = { 4f, 8f, 15f, 30f, 60f };

    [Header("[임시] 쿨타임(초): 짧음 / 중간 / 김")]
    public float[] cooldown = { 1.0f, 4.0f, 10.0f };

    [Header("[임시] 공격 범위 반경(유닛): 좁음 / 중간 / 넓음")]
    public float[] range = { 1.6f, 2.6f, 4.0f };

    [Header("[임시] 사거리(유닛): 짧음 / 중간 / 김")]
    public float[] reach = { 2.5f, 4.5f, 7.0f };

    [Header("[임시] 스킬 레벨당 성장 (Lv.1 기준 배율에 더해짐)")]
    [Tooltip("레벨당 피해량 증가율 (0.1 = +10%)")] public float damagePerLevel = 0.10f;
    [Tooltip("레벨당 쿨타임 감소율 (0.03 = -3%)")] public float cooldownPerLevel = 0.03f;
    [Tooltip("쿨타임 최소 배율")] public float cooldownFloor = 0.4f;
    [Tooltip("레벨당 범위 증가율")] public float rangePerLevel = 0.02f;

    [Header("슬롯 (기획서: 자동 6, 패시브 6 확정 / 나머지 임시)")]
    public int autoSlots = 6;
    public int passiveSlots = 6;
    [Tooltip("[임시] 수동 역할군 미확정 → J/K/L 3칸")] public int manualSlots = 3;
    [Tooltip("[임시] 이동 스킬 슬롯 구조 미확정 → 독립 1칸 (Space)")] public int movementSlots = 1;

    [Header("궁극기 (기획서 잠정: 획득 Lv.45 / 강화 Lv.60, 캐릭터 레벨 기준)")]
    public int ultimateUnlockLevel = 45;
    public int ultimateUpgradeLevel = 60;
    [Tooltip("[임시] 최대 강화 단계 미확정 → 획득 1 + 강화 1")] public int ultimateMaxLevel = 2;
    [Tooltip("[임시] 게이지 최대치")] public float gaugeMax = 100f;
    [Tooltip("[임시] 적 1마리 처치당 게이지")] public float gaugePerKill = 1f;

    [Header("조작키 [임시]")]
    public KeyCode[] manualKeys = { KeyCode.J, KeyCode.K, KeyCode.L };
    public KeyCode movementKey = KeyCode.Space;
    public KeyCode ultimateKey = KeyCode.Q;

    public float Damage(Power p) => damage[(int)p];
    public float Cooldown(Tier t) => cooldown[(int)t];
    public float Range(Tier t) => range[(int)t];
    public float Reach(Tier t) => reach[(int)t];
}
