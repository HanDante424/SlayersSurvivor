using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 경험치 = 영혼 조각 (2026-11-12, 경험치 보석 대체). 등급 3종: Soul_Small 청백 (1~4) · Soul_Mid 청록 (5~19) · Soul_Large 붉은 주황 (20 이상).
/// 그림이 없으면 예전처럼 등급 색으로 칠한 도형 원.
/// 표시: 바닥 위에 떠서 위아래로 천천히 흔들리고 은은하게 커졌다 작아짐 (그림 자식 오브젝트만 움직여 줍기 판정 위치는 그대로).
/// </summary>
public class Gem : MonoBehaviour
{
    public static readonly List<Gem> All = new List<Gem>();
    public int value;
    bool attracted;
    float speed;

    // [임시] 떠 있는 연출
    const float BobHeight = 0.05f, BobSpeed = 2.4f;
    const float PulseAmount = 0.06f, PulseSpeed = 3.1f;

    Transform visual;
    float phase;

    public static void Spawn(Vector2 pos, int value)
    {
        var go = new GameObject("Soul");
        go.transform.position = pos;
        var g = go.AddComponent<Gem>();
        g.value = value;
        g.phase = Random.Range(0f, Mathf.PI * 2f);   // 여러 개가 함께 흔들리지 않게

        string tier = value >= 20 ? "Soul_Large" : value >= 5 ? "Soul_Mid" : "Soul_Small";
        Color c = value >= 20 ? new Color(1f, 0.45f, 0.25f) : value >= 5 ? new Color(0.3f, 1f, 0.75f) : new Color(0.6f, 0.85f, 1f);
        var v = new GameObject("Visual");
        v.transform.SetParent(go.transform, false);
        g.visual = v.transform;
        var sprite = SpriteFactory.TryGet(tier);
        if (sprite != null)
        {
            var r = v.AddComponent<SpriteRenderer>();
            r.sprite = sprite;
            r.sortingOrder = 2;
        }
        else SpriteFactory.AddRenderer(v, tier, SpriteFactory.Shape.Circle, c, 2);
        // 화면 지름 (카메라 세로 14유닛 · 1080p 기준 약 22 · 27 · 35px)
        go.transform.localScale = Vector3.one * (value >= 20 ? 0.45f : value >= 5 ? 0.35f : 0.28f);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Attract() => attracted = true;

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        if (visual != null)
        {
            float t = Time.time + phase;
            visual.localPosition = new Vector3(0f, Mathf.Sin(t * BobSpeed) * BobHeight / transform.localScale.y, 0f);
            visual.localScale = Vector3.one * (1f + Mathf.Sin(t * PulseSpeed) * PulseAmount);
        }
        var p = Player.I;
        Vector2 to = p.transform.position - transform.position;
        float d = to.magnitude;
        if (!attracted && d < p.pickupRadius) attracted = true;
        if (attracted)
        {
            speed += 25f * Time.deltaTime;
            transform.position += (Vector3)(to.normalized * Mathf.Min(speed * Time.deltaTime, d));
            if (d < 0.3f)
            {
                GameManager.I.AddXp(value);
                Destroy(gameObject);
            }
        }
    }
}
