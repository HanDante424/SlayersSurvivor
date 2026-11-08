using UnityEngine;

/// <summary>가장 가까운 적에게 마력탄 발사</summary>
public class MagicBolt : Weapon
{
    public override string DisplayName => "마력탄";

    float Damage => 10f + (level - 1) * 4f;
    float Cooldown => Mathf.Max(0.25f, 1.0f - (level - 1) * 0.08f);
    int Count => 1 + (level >= 3 ? 1 : 0) + (level >= 6 ? 1 : 0);
    int Pierce => level >= 8 ? 3 : level >= 5 ? 2 : 1;

    public override string NextDescription(int lv) => lv == 1
        ? "가장 가까운 적에게 마력탄을 발사합니다."
        : lv == 3 || lv == 6 ? "발사 수 +1" : lv == 5 ? "관통 +1" : "피해량 증가, 쿨다운 감소";

    public override void Tick(float dt)
    {
        timer -= dt;
        if (timer > 0) return;
        Vector2 from = P.transform.position;
        var target = Nearest(from, 12f);
        Vector2 dir = target != null ? ((Vector2)target.transform.position - from).normalized : P.Facing;
        timer = Cooldown * P.cooldownMul;

        int n = Count;
        for (int i = 0; i < n; i++)
        {
            float angle = (i - (n - 1) / 2f) * 12f;
            Vector2 d = Quaternion.Euler(0, 0, angle) * dir;
            Projectile.Spawn(from, d, 11f, Damage * P.damageMul, Pierce, 2f, "Bolt", new Color(0.6f, 0.9f, 1f), 0.3f * P.areaMul);
        }
    }
}
