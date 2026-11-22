using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    Vector2 dir;
    float speed, life, radius;
    DamageInfo damage;
    int pierce;
    /// <summary>적중할 때마다 (맞은 적, 투사체 위치). 피해 적용 직후 · 투사체가 사라지기 전에 호출 (파이어 볼 폭발 등)</summary>
    public System.Action<Enemy, Vector2> onHit;
    /// <summary>종료 시 한 번 (사거리 끝 · 관통을 다 쓴 마지막 적중). 투사체 위치. 파이어 볼 연속 폭발 · 폭죽놀이 등 "종료 시" 효과용</summary>
    public System.Action<Vector2> onEnd;
    bool ended;

    // 파이어 볼 특수 강화용 (도탄 · 관통): 방향 · 남은 관통 · 남은 수명 · 이미 맞은 적 조회
    public Vector2 Direction
    {
        get => dir;
        set { if (value.sqrMagnitude > 0.0001f) { dir = value.normalized; transform.right = dir; } }
    }
    public int Pierce { get => pierce; set => pierce = value; }
    public float Life { get => life; set => life = value; }
    public bool HasHit(Enemy e) => hitSet.Contains(e);

    void End()
    {
        if (ended) return;
        ended = true;
        onEnd?.Invoke(transform.position);
        Destroy(gameObject);
    }
    readonly HashSet<Enemy> hitSet = new HashSet<Enemy>();
    static readonly List<Enemy> targets = new List<Enemy>();   // 투사체끼리 공유 (Update 는 겹쳐 돌지 않음)

    public static Projectile Spawn(Vector2 pos, Vector2 dir, float speed, DamageInfo damage, int pierce, float life, string sprite, Color color, float scale)
    {
        var go = new GameObject("Projectile");
        go.transform.position = pos;
        go.transform.localScale = Vector3.one * scale;
        go.transform.right = dir;
        var p = go.AddComponent<Projectile>();
        p.dir = dir.normalized; p.speed = speed; p.damage = damage; p.pierce = pierce; p.life = life;
        p.radius = scale * 0.5f;
        SpriteFactory.AddRenderer(go, sprite, SpriteFactory.Shape.Circle, color, 8);
        return p;
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        transform.position += (Vector3)(dir * speed * Time.deltaTime);
        life -= Time.deltaTime;
        if (life <= 0) { End(); return; }

        Vector2 pos = transform.position;
        foreach (var e in Enemy.CopyAll(targets))
        {
            if (e.hp <= 0 || hitSet.Contains(e)) continue;
            float r = radius + e.radius;
            if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
            {
                hitSet.Add(e);
                e.Hit(damage, dir);
                onHit?.Invoke(e, pos);
                if (--pierce <= 0) { End(); return; }
            }
        }
    }
}
