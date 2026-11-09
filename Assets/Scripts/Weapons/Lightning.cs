using System.Collections.Generic;
using UnityEngine;

/// <summary>화면 내 무작위 적에게 낙뢰</summary>
public class Lightning : Weapon
{
    public override string DisplayName => "낙뢰";

    float Damage => 25f + (level - 1) * 10f;
    float Cooldown => Mathf.Max(0.8f, 2.2f - (level - 1) * 0.15f);
    int Strikes => 1 + level / 3;
    float Splash => (0.9f + level * 0.05f) * P.areaMul;

    public override string NextDescription(int lv) => lv == 1
        ? "무작위 적에게 번개를 떨어뜨립니다. 주변에도 피해."
        : lv % 3 == 0 ? "낙뢰 횟수 +1" : "피해량 증가, 쿨다운 감소";

    readonly List<Enemy> candidates = new List<Enemy>();
    readonly List<Enemy> targets = new List<Enemy>();

    public override void Tick(float dt)
    {
        timer -= dt;
        if (timer > 0) return;
        timer = Cooldown * P.cooldownMul;

        Vector2 pp = P.transform.position;
        candidates.Clear();
        foreach (var e in Enemy.All)
            if (e.hp > 0 && ((Vector2)e.transform.position - pp).sqrMagnitude < 100f) candidates.Add(e);
        if (candidates.Count == 0) return;

        for (int s = 0; s < Strikes && candidates.Count > 0; s++)
        {
            int idx = Random.Range(0, candidates.Count);
            Vector2 at = candidates[idx].transform.position;
            candidates.RemoveAt(idx);
            Strike(at);
        }
    }

    void Strike(Vector2 at)
    {
        var fx = new GameObject("LightningFx");
        fx.transform.position = at;
        fx.transform.localScale = new Vector3(Splash * 2f, Splash * 2f, 1);
        SpriteFactory.AddRenderer(fx, "Lightning", SpriteFactory.Shape.Circle, new Color(1f, 1f, 0.4f, 0.8f), 20);
        fx.AddComponent<FadeOut>().duration = 0.2f;
        CameraFollow.Shake(0.05f, 0.05f);

        float dmg = Damage * P.damageMul;
        foreach (var e in Enemy.CopyAll(targets))
        {
            if (e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - at;
            float r = Splash + e.radius;
            if (d.sqrMagnitude < r * r) e.Hit(dmg, d, 2f);
        }
    }
}

public class FadeOut : MonoBehaviour
{
    public float duration = 0.3f;
    float t;
    SpriteRenderer sr;
    Color c;
    void Start() { sr = GetComponent<SpriteRenderer>(); c = sr.color; }
    void Update()
    {
        t += Time.deltaTime;
        sr.color = new Color(c.r, c.g, c.b, c.a * (1f - t / duration));
        if (t >= duration) Destroy(gameObject);
    }
}
