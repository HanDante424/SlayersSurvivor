using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어 주위를 도는 칼날</summary>
public class OrbitBlades : Weapon
{
    public override string DisplayName => "회전 칼날";

    int Count => 2 + (level - 1) / 2;
    float Damage => 8f + (level - 1) * 3f;
    float Radius => (1.6f + level * 0.08f) * P.areaMul;
    float Speed => 180f + level * 15f;   // 초당 각도

    public override string NextDescription(int lv) => lv == 1
        ? "주위를 도는 칼날 2개를 소환합니다."
        : lv % 2 == 1 ? "칼날 +1, 피해량 증가" : "피해량 증가, 회전 속도 증가";

    readonly List<Transform> blades = new List<Transform>();
    readonly Dictionary<Enemy, float> hitCooldown = new Dictionary<Enemy, float>();
    readonly List<Enemy> expired = new List<Enemy>();
    float angle;

    void Start() => Rebuild();
    public override void LevelUp() { base.LevelUp(); Rebuild(); }

    void Rebuild()
    {
        foreach (var b in blades) Destroy(b.gameObject);
        blades.Clear();
        for (int i = 0; i < Count; i++)
        {
            var go = new GameObject("Blade");
            go.transform.SetParent(transform, false);
            go.transform.localScale = Vector3.one * 0.5f;
            SpriteFactory.AddRenderer(go, "Blade", SpriteFactory.Shape.Diamond, new Color(0.9f, 0.9f, 1f), 9);
            blades.Add(go.transform);
        }
    }

    public override void Tick(float dt)
    {
        angle += Speed * dt;
        int n = blades.Count;
        for (int i = 0; i < n; i++)
        {
            float a = (angle + 360f * i / n) * Mathf.Deg2Rad;
            blades[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a)) * Radius / P.transform.localScale.x;
            blades[i].localRotation = Quaternion.Euler(0, 0, angle * 2f);
        }

        expired.Clear();
        var keys = new List<Enemy>(hitCooldown.Keys);
        foreach (var k in keys)
        {
            hitCooldown[k] -= dt;
            if (k == null || hitCooldown[k] <= 0) expired.Add(k);
        }
        foreach (var k in expired) hitCooldown.Remove(k);

        foreach (var b in blades)
        {
            Vector2 bp = b.position;
            foreach (var e in Enemy.All)
            {
                if (e.hp <= 0 || hitCooldown.ContainsKey(e)) continue;
                float r = 0.3f + e.radius;
                Vector2 d = (Vector2)e.transform.position - bp;
                if (d.sqrMagnitude < r * r)
                {
                    hitCooldown[e] = 0.5f;
                    e.Hit(Damage * P.damageMul, (Vector2)e.transform.position - (Vector2)P.transform.position, 4f);
                }
            }
        }
    }
}
