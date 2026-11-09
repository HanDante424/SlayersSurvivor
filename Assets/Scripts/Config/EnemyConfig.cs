using UnityEngine;

/// <summary>
/// 적 감각 조정 값. (Assets/Resources/Config/EnemyConfig.asset)
/// Play 중에 Inspector 에서 바꾸면 즉시 반영되고, Play 를 멈춰도 값이 유지됩니다.
/// </summary>
[CreateAssetMenu(menuName = "Slayers/Enemy Config", fileName = "EnemyConfig")]
public class EnemyConfig : ScriptableObject
{
    [Header("판정 크기 (빨간 원) = 적 크기 x 비율")]
    [Range(0.1f, 1.5f)] public float batHitbox = 0.5f;
    [Range(0.1f, 1.5f)] public float runnerHitbox = 0.5f;
    [Range(0.1f, 1.5f)] public float tankHitbox = 0.5f;
    [Range(0.1f, 1.5f)] public float bossHitbox = 0.5f;

    public float HitboxRatio(EnemyKind kind)
    {
        switch (kind)
        {
            case EnemyKind.Runner: return runnerHitbox;
            case EnemyKind.Tank:   return tankHitbox;
            case EnemyKind.Boss:   return bossHitbox;
            default:               return batHitbox;
        }
    }
}
