using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    static CameraFollow inst;
    float shakeTime, shakeAmp;
    Transform ground;
    float tile;

    void Awake()
    {
        inst = this;
        var g = new GameObject("Ground");
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("Ground", SpriteFactory.Shape.Ground);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(60, 40);
        sr.sortingOrder = -100;
        ground = g.transform;

        // 제한된 맵 (레이드): 바닥은 맵 크기 그대로 고정 + 경계 표시
        if (Stage.Bounded)
        {
            sr.size = Stage.HalfSize * 2f;
            ground.position = Stage.Center;
            ArenaView.Build();
        }
    }

    /// <summary>제한된 맵: 경계 밖은 이만큼만 보이게 (경계선이 화면 끝에 보이도록)</summary>
    const float BoundsMargin = 1.5f;

    /// <summary>카메라 중심을 맵 경계 + 여유 안으로 (맵이 화면보다 좁은 축은 가운데 고정)</summary>
    Vector3 ClampToBounds(Vector3 p)
    {
        var cam = GetComponent<Camera>();
        float h = cam != null ? cam.orthographicSize : GameManager.ViewSize;
        float w = h * (cam != null ? cam.aspect : 16f / 9f);
        Vector2 c = Stage.Center, room = Stage.HalfSize + Vector2.one * BoundsMargin - new Vector2(w, h);
        p.x = room.x > 0f ? Mathf.Clamp(p.x, c.x - room.x, c.x + room.x) : c.x;
        p.y = room.y > 0f ? Mathf.Clamp(p.y, c.y - room.y, c.y + room.y) : c.y;
        return p;
    }

    // ---------- 확대 · 축소 (가니악 특수 패턴 3: 맵 전체 보기) ----------
    float zoomTarget = -1f;          // < 0 = 기본 시야 (GameManager.ViewSize)
    const float ZoomSpeed = 4f;      // [임시] 초당 시야 변화 비율 (부드럽게)
    const float FullMapMargin = 1f;  // 맵 전체 보기 때 경계 바깥 여유

    /// <summary>제한된 맵 전체가 화면에 들어오게 축소 (경계 없는 스테이지는 무시)</summary>
    public static void ZoomToFullMap()
    {
        if (inst == null || !Stage.Bounded) return;
        var cam = inst.GetComponent<Camera>();
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        Vector2 h = Stage.HalfSize + Vector2.one * FullMapMargin;
        inst.zoomTarget = Mathf.Max(h.y, h.x / aspect);
    }

    /// <summary>기본 시야로 복귀</summary>
    public static void ResetZoom() { if (inst != null) inst.zoomTarget = -1f; }

    void UpdateZoom()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) return;
        float target = zoomTarget > 0f ? zoomTarget : GameManager.ViewSize;
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, target, 1f - Mathf.Exp(-ZoomSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(cam.orthographicSize - target) < 0.01f) cam.orthographicSize = target;
    }

    public static void Shake(float time, float amp)
    {
        if (inst == null) return;
        inst.shakeTime = Mathf.Max(inst.shakeTime, time);
        inst.shakeAmp = Mathf.Max(inst.shakeAmp, amp);
    }

    void LateUpdate()
    {
        if (Player.I == null) return;
        UpdateZoom();
        Vector3 p = Player.I.transform.position;
        Vector3 offset = Vector3.zero;
        if (shakeTime > 0)
        {
            shakeTime -= Time.unscaledDeltaTime;
            offset = (Vector3)Random.insideUnitCircle * shakeAmp;
            if (shakeTime <= 0) shakeAmp = 0;
        }
        if (Stage.Bounded)
        {
            transform.position = ClampToBounds(new Vector3(p.x, p.y, -10f)) + offset;
            return;   // 바닥 고정
        }
        transform.position = new Vector3(p.x, p.y, -10f) + offset;
        // 바닥 타일을 타일 한 장 단위로 따라오게 해서 무한 맵처럼 보이게 (도형 바닥 = 1유닛, 이미지 = GroundTileSize)
        if (tile <= 0f) tile = ground.GetComponent<SpriteRenderer>().sprite.bounds.size.x;
        ground.position = new Vector3(Mathf.Round(p.x / tile) * tile, Mathf.Round(p.y / tile) * tile, 0f);
    }
}
