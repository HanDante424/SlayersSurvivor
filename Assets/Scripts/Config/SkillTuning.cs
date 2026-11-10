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

    [Header("기본 강화 1회 효과 (기획 v0.2 확정 · 종류별 합연산)")]
    [Tooltip("공격력 강화 1회: 피해량 +10%")] public float upgradeDamage = 0.10f;
    [Tooltip("범위 강화 1회: 공격 범위 +10% (11/09 5% → 10% 상향)")] public float upgradeRange = 0.10f;
    [Tooltip("쿨타임 강화 1회: 쿨타임 -5%")] public float upgradeCooldown = 0.05f;
    [Tooltip("쿨타임 감소율 합계 최대치 (0.9 = 90%, 최소 쿨타임 = 기본의 10%)")] public float cooldownReductionCap = 0.9f;

    [Header("[임시] 타격 판정 — 플레이 테스트로 조정 (Play 중 수정 즉시 반영)")]
    [Tooltip("A01 파이어 슬래시: 바라보는 방향 (기본 Capsule)")]
    public SkillHitbox fireSlash = new SkillHitbox { shape = HitShape.Capsule, forward = 2.2f, back = 0.9f, width = 1.6f, radius = 1.6f, halfAngle = 70f, knockback = 0.6f };
    [Tooltip("M01 트리플 슬래시: 바라보는 방향, 3타 (기본 Capsule)")]
    public SkillHitbox tripleSlash = new SkillHitbox { shape = HitShape.Capsule, forward = 3.2f, back = 0.9f, width = 2.2f, radius = 2.6f, halfAngle = 75f, knockback = 0.4f };
    [Tooltip("M01 트리플 슬래시: 타 사이 간격(초)")] public float tripleSlashInterval = 0.15f;
    [Tooltip("A02 솔라 슬래시 베기: 플레이어 중심 원 (halfAngle 미사용)")]
    public SkillHitbox solarSlash = new SkillHitbox { radius = 2.6f, halfAngle = 180f, knockback = 0.3f };
    [Tooltip("A02 솔라 슬래시 폭발: 플레이어 중심 원 (halfAngle 미사용)")]
    public SkillHitbox solarExplosion = new SkillHitbox { radius = 2.6f, halfAngle = 180f, knockback = 1.2f };
    [Tooltip("A02 솔라 슬래시: 베기 → 폭발 간격(초). 기획 미정 → 이펙트 폭발 프레임(10번째, 20fps)에 맞춘 값")]
    public float solarExplosionDelay = 0.45f;

    [Header("[임시] 파이어 슬래시 특수 강화 — 기획서에 수치가 없는 값 (플레이 테스트로 조정)")]
    [Tooltip("F5-A 대상 추적: 가장 가까운 적을 찾는 거리(유닛). 범위 안에 적이 없으면 바라보는 방향 (죽음의 메아리는 무작위 방향)")] public float fireTrackRange = 12f;
    [Tooltip("F10-A 검기 발사: 검기 속도(유닛/초)")] public float fireWaveSpeed = 14f;
    [Tooltip("F10-A 검기 발사: 검기 최대 사거리(유닛, 범위 강화 적용)")] public float fireWaveDistance = 12f;
    [Tooltip("F10-A 검기 발사: 검기 판정 폭 = 파이어 슬래시 판정 폭 × 이 값")] public float fireWaveWidth = 1f;
    [Tooltip("F10-A 검기 발사: 근접 참격 후 검기가 나가기까지(초)")] public float fireWaveDelay = 0.05f;
    [Tooltip("F10-B 연속 공격 생성: 추가 참격 생성 위치 = 원래 참격 시작점에서 공격 방향으로 (판정 앞 길이 × 이 값)")] public float fireChainOffset = 0.5f;
    [Tooltip("F10-B 연속 공격 생성: 원래 참격 후 추가 참격까지(초)")] public float fireChainDelay = 0.1f;
    [Tooltip("F15-A 삼연발: 원래 공격 후 좌우 60도 공격까지(초)")] public float fireTripleDelay = 0.08f;
    [Tooltip("F15-C 죽음의 메아리: 적 사망 후 메아리 참격까지(초). 0 이어도 다음 프레임 이후·일시정지 중에는 발동 안 함")] public float fireEchoDelay = 0.1f;
    [Tooltip("F15-C 죽음의 메아리: 한 프레임에 발동하는 메아리 최대 수. 넘치는 메아리는 버리지 않고 다음 프레임으로 미룸 (총량 유지)")] [Min(1)] public int fireEchoMaxPerFrame = 4;
    [Tooltip("F15-C 죽음의 메아리 + 난사: 120° 세 방향 각각에서 ± 이 각도(도) 안 무작위로 3회. 60 = 각 방향이 맡은 120° 칸 전체")] [Range(0f, 60f)] public float fireEchoScatterSpread = 60f;

    [Header("[임시] 화염의 검 — 기획서에 수치가 없는 값 (플레이 테스트로 조정)")]
    [Tooltip("B5-C 확산되는 불길: 화상 상태로 죽은 적 주변에 화상 1스택을 부여하는 반경(유닛). 결정 기록: 임시 3")] public float flameSpreadRadius = 3f;
    [Tooltip("B10-B 살아있는 폭탄: 폭발 반경(유닛)")] public float flameBombRadius = 1.5f;

    [Header("[임시] 솔라 슬래시 특수 강화 — 기획서에 수치가 없는 값 (플레이 테스트로 조정)")]
    [Tooltip("S15-A 연속 폭발: 추가 폭발 간격 = 베기→폭발 간격(solarExplosionDelay) × 이 값")] public float solarMultiIntervalRatio = 1f;
    [Tooltip("S10-A 메아리: 원래 솔라 슬래시 종료 후 재발동까지(초)")] public float solarEchoGap = 0.15f;
    [Tooltip("S10-B 타오름: 지속 공격 판정 (플레이어 중심 원). 실제 넉백 범위 = 이 반경 (넉백 범위 증가 미적용)")]
    public SkillHitbox solarBurn = new SkillHitbox { radius = 4f, halfAngle = 180f, knockback = 0.3f };
    [Tooltip("S10-B 타오름: true = 매 틱 넉백 / false = 첫 틱만 넉백 (기획: 넉백 시점 미정)")] public bool solarBurnKnockEveryTick = true;
    [Tooltip("S5-B 뇌진탕: 폭발에 넉백된 뒤 충돌을 확인하는 시간(초)")] public float concussionWindow = 0.35f;
    [Tooltip("S5-B 뇌진탕: 부딪힌 상대 적도 기절시킬지 (기획: 기절 적용 대상 미정)")] public bool concussionStunsOther = false;

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

public enum HitShape { Sector, Capsule }

/// <summary>
/// 스킬 타격 판정 (플레이 테스트 조정용). 범위는 기본 강화(범위 +10%)가 곱해지기 전 값.
/// F1 판정 표시를 켜면 공격 순간 실제 판정 도형이 그려짐 (적의 빨간 원이 도형에 닿으면 맞음).
/// </summary>
[System.Serializable]
public class SkillHitbox
{
    [Tooltip("판정 모양. Capsule = 바라보는 방향으로 길쭉한 둥근 직사각형 / Sector = 부채꼴 + 근접 원. 원형 스킬은 미사용")]
    public HitShape shape = HitShape.Sector;

    [Header("Capsule (둥근 직사각형)")]
    [Tooltip("캐릭터 중심에서 바라보는 방향으로 앞쪽 끝까지 거리(유닛)")] public float forward = 2.2f;
    [Tooltip("캐릭터 중심에서 등 뒤쪽 끝까지 거리(유닛)")] public float back = 0.9f;
    [Tooltip("폭(유닛) = 바라보는 방향의 좌우 두께. 양 끝은 이 폭의 반원으로 둥글게")] public float width = 1.6f;

    [Header("Sector (부채꼴) / 원형 스킬")]
    [Tooltip("판정 반경(유닛). 적 원이 이 원에 닿으면 사거리 안. 이펙트 크기도 이 값 기준")] public float radius = 2f;
    [Tooltip("부채꼴 좌우 각도(도). 70 = 바라보는 방향 기준 좌우 70도씩 (총 140도), 180 = 전방위. 원형 스킬은 미사용")]
    public float halfAngle = 70f;
    [Tooltip("근접 원 반경(유닛). 적 원이 여기 닿으면 각도와 관계없이 맞음 (등 뒤에 붙은 적). 0 = 끔. 원형 스킬은 미사용")]
    public float near = 0.3f;
    [Header("넉백")]
    [Tooltip("기본 넉백 거리(유닛): 맞은 적이 공격 중심 반대쪽으로 밀려나는 거리 (보스 제외). 다단히트는 타마다")]
    public float knockback = 0.5f;
}
