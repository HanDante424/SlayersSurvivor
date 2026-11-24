using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 유물 상자 (2026-11-24). 적이 죽을 때 일정 확률로 떨어지고, 플레이어가 닿으면 유물 선택 화면이 열림.
/// 그림 Item_Chest 가 있으면 사용, 없으면 금색 사각형. 위아래로 흔들리며 떠 있음 (Gem 과 같은 방식).
/// [임시] 확률 · 크기 · 닿는 거리 — 중간보스(고블린 장로 · 홉 고블린 기사)는 반드시 드랍.
/// </summary>
public class Chest : MonoBehaviour
{
    public static readonly List<Chest> All = new List<Chest>();

    // [임시] 수치
    const float NormalDropChance = 0.004f;   // 일반 몹 0.4%
    const float Size = 0.7f;                 // 화면 크기 (유닛)
    const float TouchRadius = 0.7f;          // 이 거리 안에 들어오면 획득 (플레이어 중심 기준)
    const float BobHeight = 0.08f, BobSpeed = 2.2f;

    Transform visual;
    float phase;

    /// <summary>적 사망 때 호출: 확률에 따라 상자 생성 (레이드 · 디버그 맵 제외)</summary>
    public static void TryDrop(Vector2 pos, bool guaranteed)
    {
        if (Stage.IsRaid || Stage.IsDebug) return;
        float chance = NormalDropChance * (Player.I != null ? Player.I.relicChanceMul : 1f);   // 불씨 수확: 상대 증가
        if (!guaranteed && Random.value >= chance) return;
        Spawn(pos);
    }

    public static Chest Spawn(Vector2 pos)
    {
        var go = new GameObject("Chest");
        go.transform.position = pos;
        var c = go.AddComponent<Chest>();
        c.phase = Random.Range(0f, Mathf.PI * 2f);
        var v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        c.visual = v.transform;
        var sprite = SpriteFactory.TryGet("Item_Chest");
        if (sprite != null)
        {
            var r = v.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = 3;
        }
        else SpriteFactory.AddRenderer(v, "Item_Chest", SpriteFactory.Shape.Square, new Color(1f, 0.8f, 0.25f), 3);
        go.transform.localScale = Vector3.one * Size;
        return c;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        var gm = GameManager.I;
        if (gm == null || gm.State != GameState.Playing) return;
        if (visual != null)
            visual.localPosition = new Vector3(0f, Mathf.Sin((Time.time + phase) * BobSpeed) * BobHeight / transform.localScale.y, 0f);
        var p = Player.I;
        if (p == null) return;
        if (((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude > TouchRadius * TouchRadius) return;
        Destroy(gameObject);
        gm.OpenRelicChest();
    }
}
