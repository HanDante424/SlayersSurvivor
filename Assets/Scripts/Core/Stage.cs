using System.Collections.Generic;
using UnityEngine;

/// <summary>스테이지 종류 (타이틀 메뉴에서 고름 → 캐릭터 선택 → Main 씬)</summary>
public enum StageKind
{
    /// <summary>생존: 무한 맵 · 일반 몹 · 3분마다 중간보스 · 10분 생존 시 승리</summary>
    Survival,
    /// <summary>레이드 보스 전용 (2026-11-13 사용자): 제한된 맵 · 레이드 보스 1마리만 · 쓰러뜨리면 승리</summary>
    Raid,
}

/// <summary>
/// 이번 판의 스테이지 설정. 씬은 생존과 같은 Main 을 쓰고, GameManager · CameraFollow · Player · Enemy 가 이 값을 보고 동작을 바꿈.
/// 재시작(R)해도 static 이라 같은 스테이지로 다시 시작.
/// </summary>
public static class Stage
{
    public static StageKind Current = StageKind.Survival;
    public static bool IsRaid => Current == StageKind.Raid;

    /// <summary>맵 경계가 있음 (플레이어 · 적이 밖으로 나가지 못함, 카메라도 경계 안쪽만)</summary>
    public static bool Bounded => IsRaid;

    /// <summary>
    /// [임시] 레이드 맵 크기의 절반 = 가로 36 × 세로 24 (바닥 타일 6유닛 × 6장 · 4장).
    /// 카메라 시야는 세로 14 · 가로 약 25 (16:9) → 맵이 화면보다 조금 큼
    /// </summary>
    public static readonly Vector2 RaidHalfSize = new Vector2(18f, 12f);
    public static Vector2 HalfSize => RaidHalfSize;
    public static Vector2 Center => Vector2.zero;

    public static string DisplayName => IsRaid ? "레이드 보스" : "생존";

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

    public static void Build()
    {
        var root = new GameObject("ArenaBounds").transform;
        Vector2 c = Stage.Center, h = Stage.HalfSize;
        float t = OutsideThickness, fullW = h.x * 2f + t * 2f;

        // 바깥 어둠 (위 · 아래는 모서리까지 덮게 넓게).
        // 장판 · 보스 경고(GroundOrder 1) 위, 적(5) 아래 → 맵을 가로지르는 경고가 경계 밖으로 삐져나와 보이지 않음
        Box(root, c + new Vector2(0f, h.y + t / 2f), new Vector2(fullW, t), Outside, OutsideOrder);
        Box(root, c - new Vector2(0f, h.y + t / 2f), new Vector2(fullW, t), Outside, OutsideOrder);
        Box(root, c + new Vector2(h.x + t / 2f, 0f), new Vector2(t, h.y * 2f), Outside, OutsideOrder);
        Box(root, c - new Vector2(h.x + t / 2f, 0f), new Vector2(t, h.y * 2f), Outside, OutsideOrder);

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
