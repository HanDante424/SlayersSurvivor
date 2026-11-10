using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화상 상태 표시 파티클 (표시 전용 — 화상 피해·지속시간 규칙은 기획 미정).
/// 대상 주변 무작위 위치에 작은 불꽃을 한 번에 5~6개씩 피워 올리고, 각 불꽃은 커졌다 작아지며 사라짐.
/// 시트: Resources/Sprites/FX_BurnFlame.png (4행 x 4열, 16프레임, FireForge guides/burn-flame-fire.json 으로 제작)
///   0~11 = 피어남 → 최대 → 줄어듦 (한 불꽃의 일생으로 사용), 12~15 = 다시 피어나는 구간 (반복 재생용, 지금은 미사용)
///
/// 사용: BurnFx.Show(대상 Transform, () => 반경, 초). 같은 대상에 다시 부르면 남은 시간만 늘어남.
/// </summary>
public class BurnFx : MonoBehaviour
{
    public const string Sheet = "FX_BurnFlame";
    static Sprite[] Frames => SpriteEffect.LoadFrames(Sheet, 4, 4, 1f);

    // [임시] 연출 값 (플레이 보면서 조정)
    const int LifeFrames = 12;          // 불꽃 하나가 쓰는 프레임 (0~11)
    const float Fps = 20f;              // → 불꽃 하나 0.6초
    const float BurstInterval = 0.3f;   // 무리 간격
    const int BurstMin = 5, BurstMax = 6;
    const float SpreadRatio = 0.85f;    // 생성 위치 = 대상 반경 x 이 값 안의 무작위 점
    const float SizeMin = 0.8f, SizeMax = 1.3f;   // 불꽃 한 칸 크기 = 대상 반경 x (최소~최대)
    const float MinSize = 0.3f;         // 아주 작은 대상도 이 크기(유닛) 아래로는 안 줄임
    const float Rise = 0.35f;           // 초당 위로 떠오름 (유닛)
    const float FadeFrom = 0.65f;       // 일생의 이 비율부터 투명해짐
    const int SortingOrder = 12;        // 적·플레이어(10) 위

    static readonly Dictionary<Transform, BurnFx> active = new Dictionary<Transform, BurnFx>();

    Transform target;
    Func<float> radius;
    float left, burstTimer;

    /// <summary>대상에 화상 표시. seconds 동안 무리를 계속 생성 (이미 표시 중이면 더 긴 쪽으로 연장)</summary>
    public static BurnFx Show(Transform target, Func<float> radius, float seconds)
    {
        if (target == null || Frames == null) return null;
        if (active.TryGetValue(target, out var fx) && fx != null)
        {
            fx.left = Mathf.Max(fx.left, seconds);
            return fx;
        }
        var go = new GameObject("BurnFx");
        go.transform.position = target.position;
        fx = go.AddComponent<BurnFx>();
        fx.target = target; fx.radius = radius; fx.left = seconds;
        active[target] = fx;
        return fx;
    }

    /// <summary>표시 중지 (이미 피어난 불꽃은 끝까지 재생 후 사라짐)</summary>
    public static void Stop(Transform target)
    {
        if (target != null && active.TryGetValue(target, out var fx) && fx != null) fx.left = 0f;
    }

    void Update()
    {
        // 대상이 죽어도 이미 나온 불꽃은 그 자리에서 마저 타고 사라짐
        if (target != null) transform.position = target.position;
        else left = 0f;

        left -= Time.deltaTime;
        if (left > 0f)
        {
            burstTimer -= Time.deltaTime;
            if (burstTimer <= 0f) { burstTimer = BurstInterval; Burst(); }
        }
        else if (transform.childCount == 0) Destroy(gameObject);
    }

    void OnDestroy()
    {
        if (target != null && active.TryGetValue(target, out var fx) && fx == this) active.Remove(target);
    }

    void Burst()
    {
        var frames = Frames;
        if (frames == null) return;
        float r = Mathf.Max(0.1f, radius != null ? radius() : 0.4f);
        int n = UnityEngine.Random.Range(BurstMin, BurstMax + 1);
        for (int i = 0; i < n; i++)
        {
            Vector2 at = UnityEngine.Random.insideUnitCircle * r * SpreadRatio;
            float size = Mathf.Max(MinSize, r * UnityEngine.Random.Range(SizeMin, SizeMax));
            float delay = UnityEngine.Random.Range(0f, BurstInterval * 0.6f);   // 한 무리 안에서도 살짝 엇갈리게
            Flame.Spawn(transform, frames, at, size, delay, UnityEngine.Random.value < 0.5f);
        }
    }

    /// <summary>불꽃 하나: 0~11 프레임 재생 + 위로 떠오름 + 끝부분 페이드</summary>
    class Flame : MonoBehaviour
    {
        Sprite[] frames;
        SpriteRenderer sr;
        float t, delay;

        public static void Spawn(Transform parent, Sprite[] frames, Vector2 localPos, float size, float delay, bool flip)
        {
            var go = new GameObject("BurnFlame");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * size;
            var f = go.AddComponent<Flame>();
            f.frames = frames; f.delay = delay;
            f.sr = go.AddComponent<SpriteRenderer>();
            f.sr.sortingOrder = SortingOrder;
            f.sr.flipX = flip;                  // 좌우 반전으로 모양을 섞음
            f.sr.enabled = false;
        }

        void Update()
        {
            if (delay > 0f) { delay -= Time.deltaTime; return; }
            sr.enabled = true;
            t += Time.deltaTime;
            float life = LifeFrames / Fps, u = t / life;
            if (u >= 1f) { Destroy(gameObject); return; }
            sr.sprite = frames[Mathf.Min(LifeFrames - 1, (int)(t * Fps))];
            transform.localPosition += Vector3.up * (Rise * Time.deltaTime);
            float a = u < FadeFrom ? 1f : 1f - (u - FadeFrom) / (1f - FadeFrom);
            sr.color = new Color(1f, 1f, 1f, a);
        }
    }
}
