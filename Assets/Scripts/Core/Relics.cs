using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유물 (2026-11-24 사용자 요청): 상자(Chest)를 얻으면 유물 3개 이상 중 하나를 고른다.
/// 지금은 3종만 (피해량 · 이동속도 · 쿨타임 감소 +10%). 새 유물은 All 에 한 줄 추가.
/// [해석] 같은 유물을 다시 고르면 중첩 (곱연산: 피해 ×1.1, 이동 ×1.1, 쿨타임 ×0.9) · 최대 개수 제한 없음.
/// [해석] 피해량 +10% = 플레이어 전체 피해 배율(Player.damageMul), 쿨타임 감소 +10% = Player.cooldownMul ×0.9
///        (쿨타임 감소 상한은 기존 규칙 그대로).
/// </summary>
public static class Relics
{
    public class Relic
    {
        public string id, name, desc;
        public Action<Player> apply;
    }

    // [임시] 수치
    public const float DamageBonus = 0.10f, MoveBonus = 0.10f, CooldownBonus = 0.10f;
    /// <summary>한 번에 보여 줄 유물 수 (유물이 이보다 적으면 있는 만큼)</summary>
    public const int ChoiceCount = 3;

    public static readonly Relic[] All =
    {
        new Relic { id = "damage",   name = "불꽃의 인장",   desc = $"피해량 +{DamageBonus * 100:0}%",     apply = p => p.damageMul *= 1f + DamageBonus },
        new Relic { id = "move",     name = "바람의 부적",   desc = $"이동속도 +{MoveBonus * 100:0}%",     apply = p => p.moveSpeedMul *= 1f + MoveBonus },
        new Relic { id = "cooldown", name = "시간의 모래시계", desc = $"쿨타임 감소 +{CooldownBonus * 100:0}%", apply = p => p.cooldownMul *= 1f - CooldownBonus },
    };

    static readonly Dictionary<string, int> owned = new Dictionary<string, int>();

    /// <summary>유물을 얻을 때마다 (불씨 수확 특수 조합: 확률로 추가 획득)</summary>
    public static event Action Obtained;

    public static void Clear() => owned.Clear();
    public static int Count(string id) => owned.TryGetValue(id, out var n) ? n : 0;

    public static void Give(Relic r)
    {
        var p = Player.I;
        if (p == null) return;
        r.apply(p);
        owned[r.id] = Count(r.id) + 1;
        Obtained?.Invoke();
    }

    /// <summary>선택지: 유물 중 무작위로 ChoiceCount 개 (겹치지 않게)</summary>
    public static List<Relic> Roll()
    {
        var pool = new List<Relic>(All);
        var list = new List<Relic>();
        for (int i = 0; i < ChoiceCount && pool.Count > 0; i++)
        {
            int k = UnityEngine.Random.Range(0, pool.Count);
            list.Add(pool[k]);
            pool.RemoveAt(k);
        }
        return list;
    }

    /// <summary>HUD 용 한 줄: "유물  불꽃의 인장 ×2 · 바람의 부적" (없으면 빈 문자열)</summary>
    public static string Summary()
    {
        var parts = new List<string>();
        foreach (var r in All)
        {
            int n = Count(r.id);
            if (n > 0) parts.Add(n > 1 ? $"{r.name} ×{n}" : r.name);
        }
        return parts.Count == 0 ? "" : string.Join(" · ", parts);
    }
}
