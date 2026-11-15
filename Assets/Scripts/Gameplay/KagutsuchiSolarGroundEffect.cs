using System;
using UnityEngine;

/// <summary>지면 화염 고리의 위/아래 프레임과 수명 소유. 원본 PNG·공용 Sprite 캐시는 변경하지 않는다.</summary>
public sealed class KagutsuchiSolarGroundEffect : IDisposable
{
    const string Sheet = "Kagutsuchi_P2_Fx_SunGround_Loop";
    const int CellWidth = 512, CellHeight = 256, FrameCount = 6;
    const float EffectScale = 0.621f; // 사용자 지정: 직전 69% 크기에서 10% 감소
    const float FramesPerSecond = 12f; // [임시] 제작 manifest의 미리보기 속도 재사용
    // 원화 6프레임 중앙 알파>128 경계의 중앙값. 안쪽 구멍과 마스크 곡선을 같은 크기로 맞춤.
    const float InnerHalfWidthPixels = 93.75f, InnerFrontDepthPixels = 24.5f;
    // 6프레임 가시 외곽의 폭/높이 중앙값. 사용자 확정: 경고와 피해도 이 비율 사용.
    public const float WarningAspectRatio = 157.5f / 337.5f;
    // 현재 SunFire 원화(alpha>16) 본체 92.25%·고리 62.1%의 곡선 마스크 첫 접촉 높이 실측.
    // 원화·최종 고리 크기·마스크 비율 변경 시 다시 측정해야 한다(1254px 원화의 590,1143px 접촉).
    public const float VisibleSunContactHeight = 7.178906f;
    readonly Sprite[] backFrames = new Sprite[FrameCount];
    readonly Sprite[] frontFrames = new Sprite[FrameCount];
    GameObject back, front;
    SpriteRenderer backVisual, frontVisual;
    float age;
    bool shown, disposed;

    public KagutsuchiSolarGroundEffect(SpriteRenderer sunVisual, Material sunMaterial, Vector2 center, float sunRadius)
    {
        var texture = Resources.Load<Texture2D>("Sprites/" + Sheet);
        if (texture == null || texture.width != CellWidth * FrameCount || texture.height != CellHeight)
        {
            Debug.LogError("초거대 화염구 지면 이펙트 시트가 없거나 3072×256 규격과 다릅니다.");
            return;
        }
        float effectRadius = sunRadius * EffectScale;
        float pixelsPerUnit = InnerHalfWidthPixels / effectRadius;
        for (int i = 0; i < FrameCount; i++)
        {
            // 같은 지면 원점에서 양쪽 절단선이 정확히 만남. 상단은 뒤, 하단은 앞에 표시.
            backFrames[i] = Sprite.Create(texture, new Rect(i * CellWidth, CellHeight / 2, CellWidth, CellHeight / 2),
                                          new Vector2(0.5f, 0f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
            frontFrames[i] = Sprite.Create(texture, new Rect(i * CellWidth, 0, CellWidth, CellHeight / 2),
                                           new Vector2(0.5f, 1f), pixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
        back = new GameObject("SolarGroundFireBack");
        front = new GameObject("SolarGroundFireFront");
        back.transform.position = front.transform.position = center;
        backVisual = back.AddComponent<SpriteRenderer>();
        frontVisual = front.AddComponent<SpriteRenderer>();
        backVisual.sortingLayerID = frontVisual.sortingLayerID = sunVisual.sortingLayerID;
        backVisual.sortingOrder = sunVisual.sortingOrder - 1;
        frontVisual.sortingOrder = sunVisual.sortingOrder + 1;
        SetFrame(0);
        back.SetActive(false); front.SetActive(false);
        if (sunMaterial != null)
        {
            sunMaterial.SetFloat("_ClipCenterX", center.x);
            sunMaterial.SetFloat("_ClipRadius", effectRadius);
            sunMaterial.SetFloat("_ClipDepth", InnerFrontDepthPixels / pixelsPerUnit);
        }
    }

    public void Show(float growth = 1f)
    {
        if (disposed || back == null) return;
        if (!shown)
        {
            shown = true; age = 0f; SetFrame(0);
            back.SetActive(true); front.SetActive(true);
        }
        // 크기를 갱신해도 재생 시간을 초기화하지 않아 두 레이어가 끊김 없이 함께 진행.
        back.transform.localScale = front.transform.localScale = Vector3.one * Mathf.Clamp01(growth);
    }

    public void Tick(float dt)
    {
        if (disposed || !shown || dt <= 0f) return;
        age = (age + dt) % (FrameCount / FramesPerSecond);
        SetFrame(Mathf.FloorToInt(age * FramesPerSecond) % FrameCount);
    }

    void SetFrame(int index)
    {
        backVisual.sprite = backFrames[index];
        frontVisual.sprite = frontFrames[index];
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; shown = false;
        if (back != null) { back.SetActive(false); UnityEngine.Object.Destroy(back); back = null; }
        if (front != null) { front.SetActive(false); UnityEngine.Object.Destroy(front); front = null; }
        for (int i = 0; i < FrameCount; i++)
        {
            if (backFrames[i] != null) UnityEngine.Object.Destroy(backFrames[i]);
            if (frontFrames[i] != null) UnityEngine.Object.Destroy(frontFrames[i]);
        }
    }
}
