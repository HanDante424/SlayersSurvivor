using System.Collections.Generic;
using UnityEngine;

/// <summary>스테이지 종류 (타이틀 메뉴에서 고름 → 캐릭터 선택 → Main 씬)</summary>
public enum StageKind
{
    /// <summary>생존: 무한 맵 · 일반 몹 · 3분마다 중간보스 · 10분 생존 시 승리</summary>
    Survival,
    /// <summary>레이드 보스 전용 (2026-11-13 사용자): 제한된 맵 · 레이드 보스 1마리만 · 쓰러뜨리면 승리</summary>
    Raid,
    /// <summary>디버그 맵 (2026-11-16 사용자, 개발 빌드 전용): 보스 · 몬스터를 골라 소환하고 패턴을 하나씩 실행해 스프라이트 크기 · 위치 · 판정을 확인. 맵 크기를 바꿀 수 있음</summary>
    Debug,
}

/// <summary>디버그 맵 크기 (기본 = 레이드 맵 36 × 24). 크기 배율은 [임시]</summary>
public enum MapPreset { Base, Large, Medium, Small }

/// <summary>
/// 이번 판의 스테이지 설정. 씬은 생존과 같은 Main 을 쓰고, GameManager · CameraFollow · Player · Enemy 가 이 값을 보고 동작을 바꿈.
/// 재시작(R)해도 static 이라 같은 스테이지로 다시 시작.
/// </summary>
public static class Stage
{
    public static StageKind Current = StageKind.Survival;
    public static bool IsRaid => Current == StageKind.Raid;
    public static bool IsDebug => Current == StageKind.Debug;

    /// <summary>맵 경계가 있음 (플레이어 · 적이 밖으로 나가지 못함, 카메라도 경계 안쪽만)</summary>
    public static bool Bounded => IsRaid || IsDebug;

    /// <summary>
    /// [임시] 기본 맵 크기의 절반 = 가로 36 × 세로 24 (바닥 타일 6유닛 × 6장 · 4장).
    /// 카메라 시야는 세로 14 · 가로 약 25 (16:9) → 맵이 화면보다 조금 큼.
    /// 패턴의 위치 · 크기 값은 이 기본 맵 기준으로 설계됨 (디버그 맵에서 크기를 바꿔도 값은 그대로, 맵 밖은 ClampInside 로 보정)
    /// </summary>
    public static readonly Vector2 RaidHalfSize = new Vector2(18f, 12f);
    public static Vector2 BaseHalfSize => RaidHalfSize;

    static Vector2 halfSize = RaidHalfSize;
    /// <summary>지금 맵 크기의 절반 (디버그 맵이 아니면 항상 기본)</summary>
    public static Vector2 HalfSize => halfSize;
    public static Vector2 Center => Vector2.zero;

    /// <summary>지금 디버그 맵 크기 프리셋</summary>
    public static MapPreset Preset { get; private set; } = MapPreset.Base;
    /// <summary>맵 크기가 바뀜 → 바닥 · 경계 · 카메라가 다시 맞춤 (CameraFollow 가 구독)</summary>
    public static event System.Action MapChanged;

    /// <summary>[임시] 프리셋 배율: 기본 1 · 크게 1.5 · 중간 0.75 · 작게 0.5</summary>
    public static float PresetScale(MapPreset p) => p switch
    {
        MapPreset.Large => 1.5f,
        MapPreset.Medium => 0.75f,
        MapPreset.Small => 0.5f,
        _ => 1f,
    };

    public static string PresetLabel(MapPreset p) => p switch
    {
        MapPreset.Large => "크게",
        MapPreset.Medium => "중간",
        MapPreset.Small => "작게",
        _ => "기본",
    };

    /// <summary>디버그 맵 크기 변경 (디버그 맵에서만)</summary>
    public static void SetPreset(MapPreset p)
    {
        if (!IsDebug) return;
        Preset = p;
        halfSize = RaidHalfSize * PresetScale(p);
        MapChanged?.Invoke();
    }

    /// <summary>판 시작 때 (GameManager.Awake): 디버그 맵이면 고른 크기 유지, 아니면 기본 크기로 되돌림 (static 이라 이전 판의 크기가 남지 않게)</summary>
    public static void ApplyStartMap()
    {
        if (!IsDebug) Preset = MapPreset.Base;
        halfSize = RaidHalfSize * PresetScale(Preset);
    }

    public static string DisplayName => IsDebug ? "디버그 맵" : IsRaid ? "레이드 보스" : "생존";

    /// <summary>맵 안의 점 p 에서 dir(단위 벡터) 방향으로 맵 경계까지 거리</summary>
    public static float ExitDistance(Vector2 p, Vector2 dir)
    {
        float t = float.MaxValue;
        Vector2 min = Center - HalfSize, max = Center + HalfSize;
        if (dir.x > 1e-4f) t = Mathf.Min(t, (max.x - p.x) / dir.x); else if (dir.x < -1e-4f) t = Mathf.Min(t, (min.x - p.x) / dir.x);
        if (dir.y > 1e-4f) t = Mathf.Min(t, (max.y - p.y) / dir.y); else if (dir.y < -1e-4f) t = Mathf.Min(t, (min.y - p.y) / dir.y);
        return t == float.MaxValue ? 0f : Mathf.Max(0f, t);
    }

    /// <summary>
    /// 맵 벽에 닿으면 반사되는 직선 경로 (2026-11-13 카구츠치 반사 레이저 — 자주 쓰는 기믹).
    /// from 에서 dir 로 출발, 벽에 닿을 때마다 입사각 = 반사각으로 꺾여 bounces 번 반사 → 꺾이는 점 목록 (처음 점 포함, 구간 수 = bounces + 1).
    /// 모서리에 정확히 닿으면 두 축 모두 반사 (되돌아옴)
    /// </summary>
    public static List<Vector2> ReflectPath(Vector2 from, Vector2 dir, int bounces)
    {
        var pts = new List<Vector2>(bounces + 2);
        Vector2 p = ClampInside(from, 0f);
        Vector2 d = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
        Vector2 min = Center - HalfSize, max = Center + HalfSize;
        const float eps = 1e-3f;
        pts.Add(p);
        for (int i = 0; i <= bounces; i++)
        {
            float t = ExitDistance(p, d);
            Vector2 q = p + d * t;
            q.x = Mathf.Clamp(q.x, min.x, max.x);
            q.y = Mathf.Clamp(q.y, min.y, max.y);
            pts.Add(q);
            if (i == bounces) break;
            // 닿은 벽에 따라 반사 (세로 벽 = x 반전, 가로 벽 = y 반전)
            if ((q.x >= max.x - eps && d.x > 0f) || (q.x <= min.x + eps && d.x < 0f)) d.x = -d.x;
            if ((q.y >= max.y - eps && d.y > 0f) || (q.y <= min.y + eps && d.y < 0f)) d.y = -d.y;
            p = q;
        }
        return pts;
    }

    /// <summary>맵 경계 안쪽으로 margin 만큼 들인 위치 (경계 없는 스테이지면 그대로)</summary>
    public static Vector2 ClampInside(Vector2 p, float margin)
    {
        if (!Bounded) return p;
        Vector2 h = Vector2.Max(HalfSize - Vector2.one * margin, Vector2.zero);
        return new Vector2(Mathf.Clamp(p.x, Center.x - h.x, Center.x + h.x),
                           Mathf.Clamp(p.y, Center.y - h.y, Center.y + h.y));
    }
}

/// <summary>
/// 제한된 맵의 경계 표시: 바깥 = 어두운 면 4장, 경계 = 붉은 선 4줄.
/// [임시] 도형 — 벽 그림이 생기면 교체
/// </summary>
public static class ArenaView
{
    const float OutsideThickness = 30f;   // 카메라가 경계 밖을 조금 보여 줘도 비지 않게
    const float EdgeWidth = 0.12f;
    const int OutsideOrder = Fx.GroundOrder + 2;   // 3 (경계선 4)
    static readonly Color Outside = new Color(0.02f, 0.02f, 0.03f, 1f);
    static readonly Color Edge = new Color(0.85f, 0.3f, 0.2f, 0.85f);

    // ---- 카구츠치 맵 배경 이미지 (임시, 2026-11-16: 분위기를 보고 결정) ----
    // 이미지 전체를 바닥 타일 아래에 깔아 맵 바깥에서만 보이게 하고, 바깥 어둠 상자는 생략한다. (안쪽 맵 바닥은 따로 디자인해 덮을 예정)
    // 크기는 맵 프리셋과 무관하게 고정: 이미지 속 마당 높이 약 375px = 기본 맵 높이 (24).  마당 중심 = 이미지 중심.
    // v2 (2026-11-16): 마당이 평평한 직사각형으로 나온 판. 실측 마당 x 366~1169 (804px) · y 260~767 (507px), 중심 (767.5, 513.5) ≈ 이미지 중심. 이전 판은 Kagutsuchi_Map_Ruins (롤백용)
    const string BackdropSprite = "Kagutsuchi_Map_Ruins_v2";
    // 2페이즈 배경 (더 부서지고 불타는 판). 1페이즈 판과 같은 구도 · 같은 마당 위치여야 함. 파일이 없으면 1페이즈 판을 계속 씀.
    // 페이즈 전환의 화면 점멸 중에 교체 (KagutsuchiPhaseTransition.BeginWarning)
    const string BackdropSpritePhase2 = "Kagutsuchi_Map_Ruins_P2";
    // 마당 바닥 높이 / 이미지 높이 (v2 실측 507 / 1024). 해상도가 달라도 같은 구도면 그대로 맞음. 마당 높이 = 기본 맵 높이 (24).
    // 마당 가로는 804px 로 맵 가로 (36 = 761px) 보다 좌우 약 1칸씩 넓음. 값이 작을수록 배경이 커진다.
    const float BackdropFloorHeightFraction = 507f / 1024f;
    const float BackdropFloorWidthFraction = 803f / 1536f;   // 마당 가로 (x 366~1169) / 이미지 가로
    // 맵 안쪽 바닥 (카구츠치): 페이즈별 한 장 (3:2, 1536×1024). 맵 크기 (프리셋 반영) 에 꽉 차게 늘려 기본 바닥 타일 위에 덮음.
    // 파일이 없으면 기존 바닥. 2페이즈 파일이 없으면 1페이즈 바닥을 계속 씀. 점멸 때 배경과 같이 교체됨.
    const string FloorSprite = "Kagutsuchi_Floor_P1", FloorSpritePhase2 = "Kagutsuchi_Floor_P2";
    const int FloorOrder = -99;   // 기본 바닥 (-100) 위 · 장판 · 경고 (1) 아래
    /// <summary>지금 보여 줄 배경의 페이즈 (1 = 평소, 2 = 불타는 판). 전환 점멸 중 SetBackdropPhase(2)</summary>
    public static int BackdropPhase { get; private set; } = 1;

    public static void ResetBackdropPhase() => BackdropPhase = 1;

    public static void SetBackdropPhase(int phase)
    {
        if (BackdropPhase == phase) return;
        BackdropPhase = phase;
        if (GameObject.Find("ArenaBounds") != null) Rebuild();
    }
    const int BackdropOrder = -101;                // 바닥 (맵 안쪽, -100) 아래 → 맵 바깥에서만 보임. 안쪽 맵은 따로 디자인한 바닥이 덮음 (사용자 2026-11-16)
    /// <summary>디버그 맵에서 배경 이미지 표시 (패널 토글)</summary>
    public static bool DebugBackdrop = true;
    static bool UseBackdrop => (Stage.IsRaid && RaidStage.Selected == EnemyKind.Kagutsuchi) || (Stage.IsDebug && DebugBackdrop);

    /// <summary>맵 크기가 바뀐 뒤 경계 표시를 새 크기로 다시 만듦</summary>
    public static void Rebuild()
    {
        var old = GameObject.Find("ArenaBounds");
        if (old != null) { old.SetActive(false); Object.Destroy(old); }
        Build();
    }

    public static void Build()
    {
        var root = new GameObject("ArenaBounds").transform;
        Vector2 c = Stage.Center, h = Stage.HalfSize;
        float t = OutsideThickness, fullW = h.x * 2f + t * 2f;

        Sprite backdrop = null;
        if (UseBackdrop)
        {
            if (BackdropPhase >= 2) backdrop = SpriteFactory.TryGet(BackdropSpritePhase2);
            if (backdrop == null) backdrop = SpriteFactory.TryGet(BackdropSprite);
        }
        if (backdrop != null)
        {
            var go = new GameObject("ArenaBackdrop");
            go.transform.SetParent(root, false);
            go.transform.position = c;
            float worldHeight = Stage.BaseHalfSize.y * 2f / BackdropFloorHeightFraction;   // 이미지 전체의 월드 높이
            float worldWidth = Stage.BaseHalfSize.x * 2f / BackdropFloorWidthFraction;     // 이미지 전체의 월드 너비 (마당 가로 = 맵 가로)
            // 마당이 맵보다 가로로 넓은 비율 (1.586 : 1.5) 을 가로만 약 5.7% 늘려 맞춤 → 맵 좌우에 마당 바닥이 남지 않음 (사용자 2026-11-16: 살짝 남는다)
            go.transform.localScale = new Vector3(worldWidth / Mathf.Max(0.0001f, backdrop.bounds.size.x),
                                                  worldHeight / Mathf.Max(0.0001f, backdrop.bounds.size.y), 1f);
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = backdrop;
            sprite.sortingOrder = BackdropOrder;
        }
        else
        {
            // 바깥 어둠 (위 · 아래는 모서리까지 덮게 넓게).
            // 장판 · 보스 경고(GroundOrder 1) 위, 적(5) 아래 → 맵을 가로지르는 경고가 경계 밖으로 삐져나와 보이지 않음
            Box(root, c + new Vector2(0f, h.y + t / 2f), new Vector2(fullW, t), Outside, OutsideOrder);
            Box(root, c - new Vector2(0f, h.y + t / 2f), new Vector2(fullW, t), Outside, OutsideOrder);
            Box(root, c + new Vector2(h.x + t / 2f, 0f), new Vector2(t, h.y * 2f), Outside, OutsideOrder);
            Box(root, c - new Vector2(h.x + t / 2f, 0f), new Vector2(t, h.y * 2f), Outside, OutsideOrder);
        }

        // 맵 안쪽 바닥 이미지 (카구츠치)
        if (UseBackdrop)
        {
            Sprite floor = null;
            if (BackdropPhase >= 2) floor = SpriteFactory.TryGet(FloorSpritePhase2);
            if (floor == null) floor = SpriteFactory.TryGet(FloorSprite);
            if (floor != null)
            {
                var go = new GameObject("ArenaFloor");
                go.transform.SetParent(root, false);
                go.transform.position = c;
                go.transform.localScale = new Vector3(h.x * 2f / Mathf.Max(0.0001f, floor.bounds.size.x),
                                                      h.y * 2f / Mathf.Max(0.0001f, floor.bounds.size.y), 1f);
                var sprite = go.AddComponent<SpriteRenderer>();
                sprite.sprite = floor;
                sprite.sortingOrder = FloorOrder;
            }
        }

        // 경계선
        Box(root, c + new Vector2(0f, h.y), new Vector2(h.x * 2f + EdgeWidth, EdgeWidth), Edge, OutsideOrder + 1);
        Box(root, c - new Vector2(0f, h.y), new Vector2(h.x * 2f + EdgeWidth, EdgeWidth), Edge, OutsideOrder + 1);
        Box(root, c + new Vector2(h.x, 0f), new Vector2(EdgeWidth, h.y * 2f + EdgeWidth), Edge, OutsideOrder + 1);
        Box(root, c - new Vector2(h.x, 0f), new Vector2(EdgeWidth, h.y * 2f + EdgeWidth), Edge, OutsideOrder + 1);
    }

    static void Box(Transform parent, Vector2 pos, Vector2 size, Color col, int order)
    {
        var go = new GameObject("ArenaEdge");
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("__ArenaSquare", SpriteFactory.Shape.Square);   // 도형 사각형 = 1유닛
        sr.color = col;
        sr.sortingOrder = order;
    }
}
