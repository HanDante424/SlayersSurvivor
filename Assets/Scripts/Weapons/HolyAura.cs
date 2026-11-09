using System.Collections.Generic;
using UnityEngine;

/// <summary>플레이어 주변에 지속 피해를 주는 오라</summary>
public class HolyAura : Weapon
{
    public override string DisplayName => "성스러운 오라";

    float Damage => 4f + (level - 1) * 2f;
    float Radius => (1.4f + (level - 1) * 0.18f) * P.areaMul;
    const float TickRate = 0.4f;
    readonly List<Enemy> targets = new List<Enemy>();

    public override string NextDescription(int lv) => lv == 1
        ? "주변 적에게 지속적으로 피해를 줍니다."
        : "피해량 증가, 범위 증가";

    Transform visual;

    void Start()
    {
        var go = new GameObject("AuraVisual");
        go.transform.SetParent(transform, false);
        SpriteFactory.AddRenderer(go, "Aura", SpriteFactory.Shape.Ring, new Color(1f, 0.95f, 0.5f, 0.6f), 1);
        visual = go.transform;
    }

    public override void Tick(float dt)
    {
        if (visual != null)
        {
            float s = Radius * 2f / P.transform.localScale.x;
            visual.localScale = Vector3.one * s * (1f + Mathf.Sin(Time.time * 4f) * 0.03f);
        }

        timer -= dt;
        if (timer > 0) return;
        timer = TickRate * P.cooldownMul;

        Vector2 pp = P.transform.position;
        float r = Radius;
        foreach (var e in Enemy.CopyAll(targets))
        {
            if (e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - pp;
            float rr = r + e.radius;
            if (d.sqrMagnitude < rr * rr) e.Hit(Damage * P.damageMul, d, 1.5f);
        }
    }
}
