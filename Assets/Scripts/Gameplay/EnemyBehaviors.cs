using UnityEngine;

// 몬스터 종류별 행동 (2026-11-12 사용자: 고블린 계열 기본 몹 4종). 수치는 모두 [임시] — 기획 대기.
//  고블린 · 홉 고블린 · [임시] 보스: 플레이어에게 곧장 다가가 몸으로 부딪힘 (접촉 피해)
//  고블린 궁수: 다가가다가 사거리 안이면 멈춰서 조준 → 화살 1발 → 잠깐 멈춤 → 다시 이동 (공격할 때만 정지)
//  고블린 라이더: 다가가다가 가까워지면 멈춰서 돌진 준비(경로 표시) → 그 방향으로 일직선 돌진 → 잠깐 멈춤
// 기절하면 준비 중인 공격은 취소되고, 기절이 끝나면 처음부터 다시

public partial class Enemy
{
    // ---------- 고블린 궁수 ----------
    const float ArcherRange = 6.5f;          // 이 거리 안이면 사격 시작
    const float ArcherAimTime = 0.45f;       // 멈춰서 조준 (조준선 표시)
    const float ArcherRecover = 0.25f;       // 쏜 뒤 멈춰 있는 시간
    const float ArcherCooldown = 2.2f;       // 사격 간격 (쏜 순간부터)
    const float ArrowDamage = 7f, ArrowSpeed = 7.5f, ArrowTravel = 9f;

    // ---------- 고블린 라이더 ----------
    const float RiderTrigger = 5f;           // 이 거리 안이면 돌진 준비
    const float RiderWindup = 0.6f;          // 멈춰서 돌진 준비 (경로 표시)
    const float RiderChargeSpeed = 10f;
    const float RiderChargeDistance = 7f;
    const float RiderRecover = 0.5f;         // 돌진 뒤 멈춰 있는 시간
    const float RiderCooldown = 3.5f;        // 돌진 간격 (돌진이 끝난 순간부터)
    const float RiderChargeWidth = 0.5f;     // 경로 표시 폭

    enum AttackState { Move, Windup, Active, Recover }
    AttackState atkState;
    float atkTimer, atkCooldown = 1f;        // 처음 나타난 뒤 1초는 공격하지 않음
    float chargeLeft;
    Vector2 atkDir;
    FxShape telegraph;

    /// <summary>이번 프레임 이동 속도(벡터). face = 바라볼 방향 (그림 좌우 반전용)</summary>
    Vector2 Behave(Vector2 pos, Vector2 toPlayerDir, float dist, float speedMul, ref Vector2 face)
    {
        float dt = Time.deltaTime;
        if (atkCooldown > 0f) atkCooldown -= dt;
        if (IsWall) { face = toPlayerDir; return Vector2.zero; }   // 포위 고블린: 제자리
        switch (kind)
        {
            case EnemyKind.GoblinElder: return elderAI != null ? elderAI.Behave(toPlayerDir, speed * speedMul, ref face) : toPlayerDir * speed * speedMul;
            case EnemyKind.HobgoblinKnight: return knightAI != null ? knightAI.Behave(toPlayerDir, speed * speedMul, ref face) : toPlayerDir * speed * speedMul;
            case EnemyKind.GoblinArcher: return ArcherBehave(pos, toPlayerDir, dist, speedMul, ref face, dt);
            case EnemyKind.GoblinRider: return RiderBehave(pos, toPlayerDir, dist, speedMul, ref face, dt);
            default: return toPlayerDir * speed * speedMul;
        }
    }

    Vector2 ArcherBehave(Vector2 pos, Vector2 dir, float dist, float speedMul, ref Vector2 face, float dt)
    {
        switch (atkState)
        {
            case AttackState.Move:
                if (dist <= ArcherRange && atkCooldown <= 0f)
                {
                    // 조준 시작: 방향은 지금 고정 (피할 수 있게), 조준선 표시
                    atkState = AttackState.Windup;
                    atkTimer = ArcherAimTime;
                    atkDir = Player.I != null ? (Player.I.HurtCenter - pos).normalized : dir;
                    telegraph = Fx.Box(pos, atkDir, ArcherRange, 0.06f, new Color(1f, 0.3f, 0.2f, 0.35f), ArcherAimTime);
                    face = atkDir;
                    return Vector2.zero;
                }
                return dir * speed * speedMul;
            case AttackState.Windup:
                face = atkDir;
                atkTimer -= dt;
                if (atkTimer <= 0f)
                {
                    EnemyShot.Arrow(pos + atkDir * (baseScale * 0.4f), atkDir, ArrowSpeed, ArrowTravel, ArrowDamage);
                    atkState = AttackState.Recover;
                    atkTimer = ArcherRecover;
                    atkCooldown = ArcherCooldown;
                }
                return Vector2.zero;
            default:   // Recover
                face = atkDir;
                atkTimer -= dt;
                if (atkTimer <= 0f) atkState = AttackState.Move;
                return Vector2.zero;
        }
    }

    Vector2 RiderBehave(Vector2 pos, Vector2 dir, float dist, float speedMul, ref Vector2 face, float dt)
    {
        switch (atkState)
        {
            case AttackState.Move:
                if (dist <= RiderTrigger && atkCooldown <= 0f)
                {
                    atkState = AttackState.Windup;
                    atkTimer = RiderWindup;
                    atkDir = dir.sqrMagnitude > 0.0001f ? dir : Vector2.right;
                    telegraph = Fx.Box(pos, atkDir, RiderChargeDistance, RiderChargeWidth, new Color(1f, 0.35f, 0.15f, 0.3f), RiderWindup);
                    face = atkDir;
                    return Vector2.zero;
                }
                return dir * speed * speedMul;
            case AttackState.Windup:
                face = atkDir;
                atkTimer -= dt;
                if (atkTimer <= 0f) { atkState = AttackState.Active; chargeLeft = RiderChargeDistance; }
                return Vector2.zero;
            case AttackState.Active:
                face = atkDir;
                float v = RiderChargeSpeed * speedMul;
                chargeLeft -= v * dt;
                if (chargeLeft <= 0f) { atkState = AttackState.Recover; atkTimer = RiderRecover; atkCooldown = RiderCooldown; }
                return atkDir * v;
            default:   // Recover
                face = atkDir;
                atkTimer -= dt;
                if (atkTimer <= 0f) atkState = AttackState.Move;
                return Vector2.zero;
        }
    }

    /// <summary>기절 등으로 공격 취소: 준비 · 돌진 중이던 것을 멈추고 다음 공격은 짧은 대기 후</summary>
    void CancelAttack()
    {
        if (atkState == AttackState.Move) return;
        if (atkState == AttackState.Windup || atkState == AttackState.Active) atkCooldown = Mathf.Max(atkCooldown, 1f);
        atkState = AttackState.Move;
        if (telegraph != null) Destroy(telegraph.gameObject);
        telegraph = null;
    }
}

/// <summary>
/// 적 투사체 (궁수 화살 · 장로 마력탄): 직진, 플레이어 피격 판정에 닿으면 피해 후 사라짐. 사거리 끝에서 사라짐.
/// 그림: Resources/Sprites/Enemy_Arrow (오른쪽을 향한 가로 그림) · Enemy_MagicBolt. 없으면 도형
/// </summary>
public class EnemyShot : MonoBehaviour
{
    // [임시] 화면 크기 (유닛)
    const float ArrowLength = 0.5f, BoltSize = 0.32f;

    Vector2 dir;
    float speed, left, damage, hitRadius, tipOffset;

    public static void Arrow(Vector2 from, Vector2 dir, float speed, float travel, float damage) =>
        Spawn("Enemy_Arrow", SpriteFactory.Shape.Square, new Color(0.85f, 0.7f, 0.45f), new Vector3(ArrowLength, 0.06f, 1f), ArrowLength,
              from, dir, speed, travel, damage, 0.12f, ArrowLength * 0.4f);

    public static void MagicBolt(Vector2 from, Vector2 dir, float speed, float travel, float damage) =>
        Spawn("Enemy_MagicBolt", SpriteFactory.Shape.Circle, new Color(0.75f, 0.4f, 1f), Vector3.one * BoltSize, BoltSize,
              from, dir, speed, travel, damage, BoltSize * 0.45f, 0f);

    static void Spawn(string sprite, SpriteFactory.Shape shape, Color color, Vector3 fallbackScale, float imageSize,
                      Vector2 from, Vector2 dir, float speed, float travel, float damage, float hitRadius, float tipOffset)
    {
        var go = new GameObject(sprite);
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        var s = go.AddComponent<EnemyShot>();
        s.dir = dir.normalized; s.speed = speed; s.left = travel; s.damage = damage; s.hitRadius = hitRadius; s.tipOffset = tipOffset;
        bool custom = SpriteFactory.TryGet(sprite) != null;
        SpriteFactory.AddRenderer(go, sprite, shape, color, 9);
        go.transform.localScale = custom ? Vector3.one * imageSize : fallbackScale;
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        var p = Player.I;
        if (p == null) { Destroy(gameObject); return; }
        float step = speed * Time.deltaTime;
        transform.position += (Vector3)(dir * step);
        left -= step;
        Vector2 tip = (Vector2)transform.position + dir * tipOffset;
        float rr = hitRadius + p.radius;
        if ((p.HurtCenter - tip).sqrMagnitude < rr * rr)
        {
            p.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }
        if (left <= 0f) Destroy(gameObject);
    }
}
