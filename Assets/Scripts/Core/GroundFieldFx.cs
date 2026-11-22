using UnityEngine;

/// <summary>
/// 지속 바닥 장판 (단일 그림, 코드로 연출). 솔라 슬래시 타오름 장판 (2026-11-22 사용자):
/// 시트 (2000×1600, 20프레임) 대신 불타는 원판 한 장 (1024×1024) 을 코드로 움직임 — 천천히 회전 · 크기 맥동 · 투명도 일렁임, 처음 조금 서서히 나타나고 끝나기 전 서서히 사라짐.
/// 부모 (플레이어) 를 따라가고, 원판의 단단한 가장자리 반지름 (그림 속 픽셀) 이 월드 반지름 r 이 되도록 크기를 맞춤. 바닥 레이어.
/// </summary>
public class GroundFieldFx : MonoBehaviour
{
    const float FadeIn = 0.25f, FadeOut = 0.5f;   // [임시] 나타나고 사라지는 시간 (초)
    const float PeakAlpha = 0.9f;                 // [임시] 가장 진할 때 투명도 (바닥이 살짝 비치게)
    const float SpinDegPerSec = 12f;              // [임시] 회전 (+ = 시계 반대)
    const float PulseAmount = 0.025f;             // [임시] 크기 맥동 (±)
    const float PulseSpeed = 2.2f;
    const float FlickerAmount = 0.08f;            // [임시] 투명도 일렁임 (±)

    SpriteRenderer sr;
    float life, t, shrink;   // shrink > 0 이면 끝나기 shrink 초 전부터 중심 (= 플레이어) 으로 줄어들며 사라짐 (FadeOut 대신)

    public static GroundFieldFx Spawn(Texture2D tex, Vector2 centerNorm, float radiusPx, float srcWidthPx,
                                      Transform parent, float worldRadius, float seconds, int sortingOrder, float shrinkSeconds = 0f)
    {
        if (tex == null) return null;
        float ppu = radiusPx * tex.width / srcWidthPx / Mathf.Max(0.01f, worldRadius);   // 1월드 유닛당 텍스처 px
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), centerNorm, ppu, 0, SpriteMeshType.FullRect);
        var go = new GameObject("FX_GroundField");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        var fx = go.AddComponent<GroundFieldFx>();
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprite;
        fx.sr.sortingOrder = sortingOrder;
        fx.life = Mathf.Max(0.1f, seconds);
        fx.shrink = Mathf.Clamp(shrinkSeconds, 0f, fx.life);
        fx.Apply();
        return fx;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= life + 0.05f) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply()
    {
        float outTime = shrink > 0f ? shrink : FadeOut;
        float fade = Mathf.Clamp01(t / FadeIn) * Mathf.Clamp01((life - t) / outTime);
        float shrinkMul = shrink > 0f ? 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - (life - shrink)) / shrink)) : 1f;   // 끝나기 shrink 초 전부터 중심으로 줄어듦
        float flick = 1f + FlickerAmount * (Mathf.Sin(t * 13f) * 0.6f + Mathf.Sin(t * 23f + 1.1f) * 0.4f);
        var c = sr.color; c.a = Mathf.Clamp01(fade * PeakAlpha * flick); sr.color = c;
        transform.localScale = Vector3.one * ((1f + PulseAmount * Mathf.Sin(t * PulseSpeed)) * shrinkMul);
        transform.localRotation = Quaternion.Euler(0f, 0f, SpinDegPerSec * t);
    }

    void OnDestroy()
    {
        if (sr != null && sr.sprite != null) Destroy(sr.sprite);
    }
}
