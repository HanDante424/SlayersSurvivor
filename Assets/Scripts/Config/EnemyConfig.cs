using UnityEngine;

/// <summary>
/// 적 감각 조정 값. (Assets/Resources/Config/EnemyConfig.asset)
/// Play 중에 Inspector 에서 바꾸면 즉시 반영되고, Play 를 멈춰도 값이 유지됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Slayers/Enemy Config", fileName = "EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("판정 크기 (빨간 원) = 적 크기 x 비율")]
    // 2026-11-12 고블린 그림 6종 기준: 그림 가운데의 몸통을 덮는 원 (무기 · 지팡이 · 늑대 꼬리 끝은 제외)
    [Range(0.1f, 1.5f)] public float goblinHitbox = 0.3f;
    [Range(0.1f, 1.5f)] public float archerHitbox = 0.3f;
    [Range(0.1f, 1.5f)] public float hobgoblinHitbox = 0.33f;
    [Range(0.1f, 1.5f)] public float riderHitbox = 0.3f;
    [Range(0.1f, 1.5f)] public float elderHitbox = 0.28f;
    [Range(0.1f, 1.5f)] public float knightHitbox = 0.32f;
    [Range(0.1f, 1.5f)] public float bossHitbox = 0.5f;
    [Tooltip("[임시] 가니악 (레이드 보스) — 그림이 생기면 몸통에 맞춰 조정")]
    [Range(0.1f, 1.5f)] public float ganiacHitbox = 0.45f;
    [Tooltip("[임시] 가니악의 검 (특수 패턴 3 파괴 대상)")]
    [Range(0.1f, 1.5f)] public float ganiacSwordHitbox = 0.3f;   // 그림 (가는 세로 검) 기준
    [Tooltip("[임시] 카구츠치 (레이드 보스) — 그림이 생기면 몸통에 맞춰 조정")]
    [Range(0.1f, 1.5f)] public float kagutsuchiHitbox = 0.35f;   // 그림 (옥좌 포함 키 4 · 폭 약 2.9) 기준 → 판정 반경 1.4
    [Tooltip("[임시] 야타의 거울 (카구츠치 주변 궤도)")]
    [Range(0.1f, 1.5f)] public float kagutsuchiMirrorHitbox = 0.45f;
    [Tooltip("[임시] 아메노무라쿠모 (카구츠치의 검) — 검 가운데 원 반경 (크기 1 기준)")]
    [Range(0.1f, 1.5f)] public float kagutsuchiSwordHitbox = 0.8f;

    public float HitboxRatio(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.GoblinArcher: return archerHitbox;
            case EnemyKind.Hobgoblin:    return hobgoblinHitbox;
            case EnemyKind.GoblinRider:  return riderHitbox;
            case EnemyKind.GoblinElder:  return elderHitbox;
            case EnemyKind.HobgoblinKnight: return knightHitbox;
            case EnemyKind.Boss:         return bossHitbox;
            case EnemyKind.Ganiac:       return ganiacHitbox;
            case EnemyKind.GaniacSword:  return ganiacSwordHitbox;
            case EnemyKind.Kagutsuchi:   return kagutsuchiHitbox;
            case EnemyKind.KagutsuchiMirror: return kagutsuchiMirrorHitbox;
            case EnemyKind.KagutsuchiSword:  return kagutsuchiSwordHitbox;
            default:                     return goblinHitbox;
        }
    }
}
