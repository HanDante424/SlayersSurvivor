using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    static CameraFollow inst;
    float shakeTime, shakeAmp;
    bool transitionView;
    Vector2 transitionOffset;

    /// <summary>전환 연출의 시야·진동 갱신. Focus를 반복 호출해 위치 보간을 재시작하지 않음.</summary>
    public static void SetTransitionView(float size, Vector2 offset)
    {
        if (inst == null) return;
        inst.transitionView = true;
        if (size > 0f) inst.zoomTarget = size;
        inst.transitionOffset = offset;
    }

    public static void ClearTransitionView()
    {
        if (inst == null) return;
        inst.transitionView = false;
        inst.transitionOffset = Vector2.zero;
    }
    object outsideMapOwner;
    Transform beforeOutsideFocus;
    bool beforeOutsideFullMap;
    float beforeOutsideZoom, outsideBaseSize;

    /// <summary>소유자가 있는 임시 추적. 종료·중단 시 원래 대상과 맵 제한으로 복귀.</summary>
    public static void BeginOutsideMapFollow(object owner, Transform target, float magnification = 1f)
    {
        if (inst == null || owner == null) return;
        if (inst.outsideMapOwner != null && inst.outsideMapOwner != owner) return;
        if (inst.outsideMapOwner == null)
        {
            inst.beforeOutsideFocus = inst.focus;
            inst.beforeOutsideFullMap = inst.fullMap;
            inst.beforeOutsideZoom = inst.zoomTarget;
            var cam = inst.GetComponent<Camera>();
            inst.outsideBaseSize = cam != null ? cam.orthographicSize : GameManager.ViewSize;
        }
        inst.zoomTarget = magnification > 1f ? inst.outsideBaseSize / magnification : inst.beforeOutsideZoom;
        inst.outsideMapOwner = owner;
        inst.focus = target;
        inst.fullMap = false;
    }

    public static void EndOutsideMapFollow(object owner)
    {
        if (inst == null || inst.outsideMapOwner != owner || owner == null) return;
        inst.outsideMapOwner = null;
        inst.focus = inst.beforeOutsideFocus;
        inst.fullMap = inst.beforeOutsideFullMap;
        inst.zoomTarget = inst.beforeOutsideZoom;
        inst.beforeOutsideFocus = null;
        inst.blendLeft = BlendTime;
        inst.lastPos = inst.transform.position;
    }
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
        float margin = limitViewportToMap ? 0f : BoundsMargin;
        Vector2 c = Stage.Center, room = Stage.HalfSize + Vector2.one * margin - new Vector2(w, h);
        p.x = room.x > 0f ? Mathf.Clamp(p.x, c.x - room.x, c.x + room.x) : c.x;
        p.y = room.y > 0f ? Mathf.Clamp(p.y, c.y - room.y, c.y + room.y) : c.y;
        return p;
    }

    // ---------- 확대 · 축소 (가니악 특수 패턴 3: 맵 전체 보기) ----------
    float zoomTarget = -1f;          // < 0 = 기본 시야 (GameManager.ViewSize)
    const float ZoomSpeed = 4f;      // [임시] 초당 시야 변화 비율 (부드럽게)
    const float FullMapMargin = 1f;  // 맵 전체 보기 때 경계 바깥 여유

    // 맵 전체 보기 때 화면 위 · 아래 UI 자리를 비움 (2026-11-13: 보스 · 거울 · 검 체력 바가 맵 위쪽 보스를 가림)
    // 화면 높이 비율. 위 = 경험치 바 · 시간 · 보스 이름 · 체력 바 3줄 (약 175px / 1080), 아래 = 스킬 슬롯 (약 80px / 1080)
    const float HudTop = 0.17f, HudBottom = 0.08f;
    bool fullMap;
    bool limitViewportToMap;
    float fullMapOffsetY;   // 맵 전체 보기 때 카메라 중심을 맵 중심에서 위로 (위쪽 UI 가 맵 밖에 오도록)

    /// <summary>
    /// 제한된 맵 전체가 화면에 들어오게 축소 (경계 없는 스테이지는 무시).
    /// 화면 위 · 아래 UI (체력 바 · 스킬 슬롯) 를 뺀 영역에 맵이 들어오도록 시야와 중심을 맞춤
    /// </summary>
    public static void ZoomToFullMap() => ZoomToFullMap(FullMapMargin);

    /// <summary>맵 전체와 HUD 공간을 유지하면서 보스별 바깥 여백을 지정.</summary>
    public static void ZoomToFullMap(float margin)
    {
        if (inst == null || !Stage.Bounded) return;
        var cam = inst.GetComponent<Camera>();
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        float size = FullMapViewSize(aspect, margin);
        inst.zoomTarget = size;
        inst.fullMap = true;
        inst.fullMapOffsetY = (HudTop - HudBottom) * size;
        inst.blendLeft = BlendTime;
        inst.lastPos = inst.transform.position;
    }

    static float FullMapViewSize(float aspect, float margin)
    {
        Vector2 h = Stage.HalfSize + Vector2.one * Mathf.Max(0f, margin);
        return Mathf.Max(h.y / (1f - HudTop - HudBottom), h.x / aspect);
    }

    /// <summary>집중 연출·흔들림을 포함한 화면 전체를 맵 안에 제한.</summary>
    public static void ConstrainToMap()
    {
        if (inst != null && Stage.Bounded) inst.limitViewportToMap = true;
    }

    /// <summary>전체 맵 시야의 비율만큼 확대하고 플레이어를 따라감.</summary>
    public static void FollowMap(float viewScale, float margin)
    {
        if (inst == null || !Stage.Bounded) return;
        var cam = inst.GetComponent<Camera>();
        float aspect = cam != null ? cam.aspect : 16f / 9f;
        inst.zoomTarget = FullMapViewSize(aspect, margin) * Mathf.Max(0.0001f, viewScale);
        inst.limitViewportToMap = true;
        inst.fullMap = false;
        inst.focus = null;
        inst.blendLeft = BlendTime;
        inst.lastPos = inst.transform.position;
    }

    /// <summary>기본 시야로 복귀</summary>
    public static void ResetZoom()
    {
        if (inst == null) return;
        inst.zoomTarget = -1f;
        if (inst.fullMap) { inst.fullMap = false; inst.blendLeft = BlendTime; inst.lastPos = inst.transform.position; }
    }

    void UpdateZoom()
    {
        var cam = GetComponent<Camera>();
        if (cam == null) return;
        float target = zoomTarget > 0f ? zoomTarget : GameManager.ViewSize;
        float limit = float.MaxValue;
        if (limitViewportToMap && outsideMapOwner == null && Stage.Bounded)
        {
            // 넓은 화면 비율에서도 화면 전체가 맵 안에 들어가는 최대 시야.
            limit = Mathf.Min(Stage.HalfSize.y, Stage.HalfSize.x / Mathf.Max(0.0001f, cam.aspect));
            target = Mathf.Min(target, limit);
        }
        cam.orthographicSize = Mathf.Min(limit, Mathf.Lerp(cam.orthographicSize, target, 1f - Mathf.Exp(-ZoomSpeed * Time.unscaledDeltaTime)));
        if (Mathf.Abs(cam.orthographicSize - target) < 0.01f) cam.orthographicSize = target;
    }

    // ---------- 대상 비추기 (카구츠치 입장: 보스를 확대해서 따라감) ----------
    Transform focus;
    float blendLeft;                 // 대상이 바뀐 뒤 부드럽게 옮겨 가는 남은 시간
    Vector3 lastPos;
    const float BlendTime = 0.6f, BlendSpeed = 8f;   // [임시]

    /// <summary>플레이어 대신 target 을 size 시야(세로 절반)로 비춤. 맵 경계를 무시 (맵 밖도 보임)</summary>
    public static void Focus(Transform target, float size)
    {
        if (inst == null) return;
        inst.focus = target;
        inst.zoomTarget = size;
        inst.blendLeft = BlendTime;
        inst.lastPos = inst.transform.position;
    }

    /// <summary>다시 플레이어를 비춤 (시야는 그대로 — ZoomToFullMap / ResetZoom 으로 바꿈). 부드럽게 옮겨 감</summary>
    public static void ClearFocus()
    {
        if (inst == null) return;
        inst.focus = null;
        inst.blendLeft = BlendTime;
        inst.lastPos = inst.transform.position;
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
        if (transitionView && GameManager.I != null && GameManager.I.State != GameState.Playing) return;
        UpdateZoom();
        Vector3 p = focus != null ? focus.position : Player.I.transform.position;
        Vector3 offset = transitionOffset;
        if (shakeTime > 0)
        {
            shakeTime -= Time.unscaledDeltaTime;
            offset += (Vector3)Random.insideUnitCircle * shakeAmp;
            if (shakeTime <= 0) shakeAmp = 0;
        }
        if (Stage.Bounded)
        {
            // 보스별 화면 제한이 켜져 있으면 집중 연출도 최종 위치에서 경계 적용.
            Vector3 want = focus != null ? new Vector3(p.x, p.y, -10f)
                         : fullMap ? new Vector3(Stage.Center.x, Stage.Center.y + fullMapOffsetY, -10f)   // 맵 전체: 고정 (UI 자리만큼 위로)
                         : outsideMapOwner != null ? new Vector3(p.x, p.y, -10f) : ClampToBounds(new Vector3(p.x, p.y, -10f));
            if (blendLeft > 0f)
            {
                blendLeft -= Time.unscaledDeltaTime;
                want = Vector3.Lerp(lastPos, want, 1f - Mathf.Exp(-BlendSpeed * Time.unscaledDeltaTime));
            }
            if (limitViewportToMap && outsideMapOwner == null) want = ClampToBounds(want);
            lastPos = want;
            // 보간·화면 흔들림 이후에도 재검사해 경계 바깥이 노출되지 않게 함.
            transform.position = limitViewportToMap && outsideMapOwner == null ? ClampToBounds(want + offset) : want + offset;
            return;   // 바닥 고정
        }
        transform.position = new Vector3(p.x, p.y, -10f) + offset;
        // 바닥 타일을 타일 한 장 단위로 따라오게 해서 무한 맵처럼 보이게 (도형 바닥 = 1유닛, 이미지 = GroundTileSize)
        if (tile <= 0f) tile = ground.GetComponent<SpriteRenderer>().sprite.bounds.size.x;
        ground.position = new Vector3(Mathf.Round(p.x / tile) * tile, Mathf.Round(p.y / tile) * tile, 0f);
    }
}
