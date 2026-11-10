using System.Collections.Generic;
using UnityEngine;

public class Projectile : MonoBehaviour
{
    Vector2 dir;
    float speed, life, radius;
    DamageInfo damage;
    int pierce;
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
        if (life <= 0) { Destroy(gameObject); return; }

        Vector2 pos = transform.position;
        foreach (var e in Enemy.CopyAll(targets))
        {
            if (e.hp <= 0 || hitSet.Contains(e)) continue;
            float r = radius + e.radius;
            if (((Vector2)e.transform.position - pos).sqrMagnitude < r * r)
            {
                hitSet.Add(e);
                e.Hit(damage, dir);
                if (--pierce <= 0) { Destroy(gameObject); return; }
            }
        }
    }
}
