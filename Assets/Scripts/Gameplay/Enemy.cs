using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Bat, Runner, Tank, Boss }

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    public EnemyKind kind;
    public float hp, maxHp, speed, damage, radius;
    public int xp;

    SpriteRenderer sr;
    Color baseColor;
    float flash;
    Vector2 knockback;

    public static Enemy Spawn(EnemyKind kind, Vector2 pos, float hpMul)
    {
        var go = new GameObject("Enemy_" + kind);
        go.transform.position = pos;
        var e = go.AddComponent<Enemy>();
        e.Setup(kind, hpMul);
        return e;
    }

    void Setup(EnemyKind k, float hpMul)
    {
        kind = k;
        Color col; float scale; SpriteFactory.Shape shape = SpriteFactory.Shape.Circle;
        switch (k)
        {
            default:
            case EnemyKind.Bat:    maxHp = 10;  speed = 2.2f; damage = 8;  xp = 1;  scale = 0.6f; col = new Color(0.85f, 0.3f, 0.35f); break;
            case EnemyKind.Runner: maxHp = 7;   speed = 3.6f; damage = 6;  xp = 2;  scale = 0.5f; col = new Color(1f, 0.6f, 0.2f); shape = SpriteFactory.Shape.Diamond; break;
            case EnemyKind.Tank:   maxHp = 60;  speed = 1.4f; damage = 15; xp = 5;  scale = 1.1f; col = new Color(0.55f, 0.3f, 0.7f); shape = SpriteFactory.Shape.Square; break;
            case EnemyKind.Boss:   maxHp = 900; speed = 1.8f; damage = 25; xp = 60; scale = 2.2f; col = new Color(0.9f, 0.1f, 0.15f); break;
        }
        maxHp *= hpMul;
        hp = maxHp;
        radius = scale * 0.5f;
        transform.localScale = Vector3.one * scale;
        sr = SpriteFactory.AddRenderer(gameObject, "Enemy_" + k, shape, col, k == EnemyKind.Boss ? 6 : 5);
        baseColor = sr.color;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>
    /// All 의 사본을 buffer 에 채워 돌려줍니다.
    /// 순회 중 Hit() 으로 적이 죽어 All 이 바뀌어도 안전하게 돌 수 있습니다. (죽은 적은 hp &lt;= 0)
    /// </summary>
    public static List<Enemy> CopyAll(List<Enemy> buffer)
    {
        buffer.Clear();
        buffer.AddRange(All);
        return buffer;
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        var p = Player.I;
        if (p == null) return;

        Vector2 pos = transform.position;
        Vector2 toPlayer = (Vector2)p.transform.position - pos;
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.zero;

        pos += (dir * speed + knockback) * Time.deltaTime;
        knockback = Vector2.Lerp(knockback, Vector2.zero, 10f * Time.deltaTime);
        transform.position = pos;
        sr.flipX = dir.x < 0;

        if (dist < radius + p.radius) p.TakeDamage(damage);

        // 너무 멀어지면 플레이어 근처로 재배치 (뱀서 방식)
        if (dist > 22f) transform.position = (Vector2)p.transform.position + Random.insideUnitCircle.normalized * 14f;

        if (flash > 0)
        {
            flash -= Time.deltaTime;
            sr.color = flash > 0 ? Color.white : baseColor;
        }
    }

    public void Hit(float dmg, Vector2 fromDir, float knock = 3f)
    {
        if (hp <= 0) return;
        hp -= dmg;
        flash = 0.08f;
        if (kind != EnemyKind.Boss) knockback = fromDir.normalized * knock;
        DamageText.Show(transform.position, dmg);
        if (hp <= 0) Die();
    }

    void Die()
    {
        GameManager.I.OnEnemyKilled(this);
        Gem.Spawn(transform.position, xp);
        Destroy(gameObject);
    }

    // 간단한 밀어내기: 적끼리 겹치지 않게 (매 프레임 일부만 처리)
    public static void Separate()
    {
        int n = All.Count;
        if (n < 2) return;
        int start = Time.frameCount % 2;
        for (int i = start; i < n; i += 2)
        {
            var a = All[i];
            Vector2 pa = a.transform.position;
            Vector2 push = Vector2.zero;
            int checks = 0;
            for (int j = 0; j < n && checks < 40; j++)
            {
                if (i == j) continue;
                var b = All[j];
                Vector2 d = pa - (Vector2)b.transform.position;
                float min = a.radius + b.radius;
                float sq = d.sqrMagnitude;
                if (sq < min * min && sq > 0.0001f)
                {
                    float m = Mathf.Sqrt(sq);
                    push += d / m * (min - m);
                    checks++;
                }
            }
            if (push != Vector2.zero) a.transform.position = pa + push * 0.5f;
        }
    }
}
