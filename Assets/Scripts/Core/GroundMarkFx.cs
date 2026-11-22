using UnityEngine;

/// <summary>
/// 바닥 경고 표식 (단일 그림, 코드로 연출). 파이어 브레이커 재발동 위치 표시 (2026-11-22 사용자):
/// 시트 (2000×2000, 약 15MB) 를 쓰는 대신 고리 한 장 (1024×1024) 을 코드로 움직임 — 서서히 차오르며 (투명 → 불투명 · 살짝 커짐 · 천천히 회전 · 일렁임)
/// 끝 무렵 한 번 번쩍이고 사라짐. 크기는 고리 반지름 (그림 속 픽셀) 이 월드 반지름 r 이 되도록 맞춤. 바닥 레이어, 실제 시간 기준 (WorldSlow 무관).
/// </summary>
public class GroundMarkFx : MonoBehaviour
{
    const float FillEnd = 0.8f;       // [임시] 이 비율까지 서서히 차오름 (이후 가득 찬 채 일렁임)
    const float StartScale = 0.9f;    // [임시] 처음 크기 배율 (고리가 퍼지며 자리를 잡음)
    const float SpinDegPerSec = 25f;  // [임시] 회전 속도 (+ = 시계 반대)
    const float FlickerAmount = 0.1f; // [임시] 일렁임 (알파 ±)
    const float FlashStart = 0.88f;   // [임시] 이 비율부터 마지막 번쩍임 (살짝 커짐)

    SpriteRenderer sr;
    float life, t;

    /// <param name="ringRadiusPx">그림 속 고리 반지름 (원본 1254px 기준) — 텍스처가 줄어 있으면 비율로 보정</param>
    public static GroundMarkFx Spawn(Texture2D tex, Vector2 ringCenterNorm, float ringRadiusPx, float srcWidthPx,
                                     Vector2 at, float worldRadius, float seconds, int sortingOrder)
    {
        if (tex == null) return null;
        float ppu = ringRadiusPx * tex.width / srcWidthPx / Mathf.Max(0.01f, worldRadius);   // 1월드 유닛당 텍스처 px
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), ringCenterNorm, ppu, 0, SpriteMeshType.FullRect);
        var go = new GameObject("FX_GroundMark");
        go.transform.position = at;
        var fx = go.AddComponent<GroundMarkFx>();
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprite;
        fx.sr.sortingOrder = sortingOrder;
        fx.life = Mathf.Max(0.05f, seconds);
        fx.Apply();
        return fx;
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        if (t >= life) { Destroy(gameObject); return; }
        Apply();
    }

    void Apply()
    {
        float k = Mathf.Clamp01(t / life);
        float fill = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / FillEnd));
        float a = fill * (1f - FlickerAmount * 0.5f + FlickerAmount * 0.5f * Mathf.Sin(t * 17f + Mathf.Sin(t * 5f) * 2f));
        float scale = Mathf.Lerp(StartScale, 1f, 1f - (1f - fill) * (1f - fill));
        if (k > FlashStart) scale *= 1f + 0.04f * Mathf.Sin(Mathf.PI * (k - FlashStart) / (1f - FlashStart));   // 마지막 번쩍임
        transform.localScale = Vector3.one * scale;
        transform.rotation = Quaternion.Euler(0f, 0f, SpinDegPerSec * t);
        var c = sr.color; c.a = Mathf.Clamp01(a); sr.color = c;
    }

    void OnDestroy()
    {
        if (sr != null && sr.sprite != null) Destroy(sr.sprite);   // 만든 스프라이트 정리 (텍스처는 Resources 가 소유)
    }
}
