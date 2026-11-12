using UnityEngine;

/// <summary>
/// 적 감각 조정 값. (Assets/Resources/Config/EnemyConfig.asset)
/// Play 중에 Inspector 에서 바꾸면 즉시 반영되고, Play 를 멈춰도 값이 유지됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Slayers/Enemy Config", fileName = "EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("판정 크기 (빨간 원) = 적 크기 x 비율")]
    // 사람형 그림은 긴 변(키)보다 몸 폭이 좁아 0.4 (그림이 들어오면 F1 판정 표시로 다시 맞출 것)
    [Range(0.1f, 1.5f)] public float goblinHitbox = 0.4f;
    [Range(0.1f, 1.5f)] public float archerHitbox = 0.4f;
    [Range(0.1f, 1.5f)] public float hobgoblinHitbox = 0.4f;
    [Range(0.1f, 1.5f)] public float riderHitbox = 0.4f;
    [Range(0.1f, 1.5f)] public float elderHitbox = 0.4f;
    [Range(0.1f, 1.5f)] public float knightHitbox = 0.4f;
    [Range(0.1f, 1.5f)] public float bossHitbox = 0.5f;

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
            default:                     return goblinHitbox;
        }
    }
}
