using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 특수 패턴 2 의 돌기둥들. 낙하 → 착지(플레이어 이동 차단 · 레이저 차단) → 일부 파괴 (Kagutsuchi_P2_Fx_StonePillarBreak).
/// 시트: 384×384 칸 4×4, 0 = 멀쩡한 기둥, 1~13 = 파괴 (13 = 잔해 정지). 기둥 바닥 = 칸 아래에서 12px.
/// </summary>
public sealed class KagutsuchiPillarField : IDisposable
{
    public const float Radius = 2f;                 // 경고 · 충돌 반지름 (사용자: 워닝 radius 2)
    const string SheetName = "Kagutsuchi_P2_Fx_StonePillarBreak";
    const int Cell = 384, Columns = 4, FrameCount = 14;
    const float VisibleWidth = 155f;                // 원화 기둥 가시 폭 px → 지름 Radius*2 에 맞춤 [임시]
    const float GroundPivotY = 12f / Cell;
    const float PlaceGap = 0.3f;
    const float VisualScale = 0.49f;                // 사용자 (2026-11-16): 돌기둥 표시 크기 30% 감소를 두 번 (0.7 × 0.7). 판정 · 경고 radius 2 는 그대로
    const float BlockRadius = Radius * 0.5f;        // 사용자 (2026-11-16): 플레이어 이동을 막는 범위 50% 감소 (낙하 경고 · 적중 반지름 2 는 그대로)
    const float BlockLift = BlockRadius * 0.5f + 0.5f /* 사용자: 추가로 0.5 위로 */;    // 사용자: 아래쪽 막는 범위가 넓어 막는 원을 위로 반 (반지름의 절반) 올림. 위쪽 범위는 그만큼 늘어남
    public const float ShadowRadius = 1f;               // 사용자: 안전지대 각도 축소 — 레이저 차단 · 경고 제외는 이 반지름 기준 (충돌 radius 2 는 그대로)
    const float RubbleFade = 1.5f;                  // [임시] 파괴 애니메이션 종료 후 잔해가 투명해지는 시간
    static float SumOf(float[] values) { float sum = 0f; foreach (var v in values) sum += v; return sum; }
    const float BossSideMargin = 7f;                // 사용자: 보스쪽 가장자리에서 최소 7
    const float SideMargin = 4f;                   // 사용자: 좌우 가장자리에서 최소 거리 4 (중심 기준)
    // 파괴 프레임 1~13 의 표시 시간 (제작 manifest previewDurationsMs)
    static readonly float[] BreakFrameTime = { 0.065f, 0.055f, 0.05f, 0.05f, 0.045f, 0.045f, 0.05f, 0.06f, 0.065f, 0.075f, 0.09f, 0.11f, 0.35f };
    static readonly float BreakAnimTime = SumOf(BreakFrameTime);
    public const float BreakDuration = 1f;         // [임시] 파괴 연출 대기 시간 (프레임 합 0.76초 + 여유)

    enum State { Falling, Standing, Breaking }
    sealed class Pillar
    {
        public Vector2 Pos;
        public GameObject Root;
        public SpriteRenderer Renderer;
        public State State;
        public float Age;
        public bool Solid;
    }

    static KagutsuchiPillarField active;
    readonly List<Pillar> pillars = new List<Pillar>();
    readonly Sprite[] frames;
    readonly Sprite fallback;
    bool disposed;

    public int Count => pillars.Count;

    public KagutsuchiPillarField()
    {
        var texture = Resources.Load<Texture2D>("Sprites/" + SheetName);
        if (texture != null && texture.width == Cell * Columns && texture.height == Cell * Columns)
        {
            frames = new Sprite[FrameCount];
            float ppu = VisibleWidth / (Radius * 2f * VisualScale);
            for (int i = 0; i < FrameCount; i++)
            {
                var rect = new Rect(i % Columns * Cell, (Columns - 1 - i / Columns) * Cell, Cell, Cell);
                frames[i] = Sprite.Create(texture, rect, new Vector2(0.5f, GroundPivotY), ppu, 0, SpriteMeshType.FullRect);
            }
        }
        else
        {
            Debug.LogError("돌기둥 파괴 시트가 없거나 1536×1536 규격과 다릅니다. 임시 사각형으로 대체합니다.");
            fallback = SpriteFactory.Get("Fx_Square", SpriteFactory.Shape.Square);
        }
        active = this;
    }

    // ---------- 배치 ----------
    /// <summary>기존 기둥 · 이미 정해진 낙하 지점 · 보스와 겹치지 않는 임의 위치. 못 찾으면 false</summary>
    public bool TryPick(List<Vector2> pending, Vector2 avoid, float avoidRadius, int bossSide, out Vector2 result)
    {
        float margin = Radius;
        // 좌우 가장자리 최소 4, 보스가 있는 쪽 가장자리는 최소 7 (사용자)
        float minX = -Stage.HalfSize.x + (bossSide < 0 ? BossSideMargin : SideMargin);
        float maxX = Stage.HalfSize.x - (bossSide > 0 ? BossSideMargin : SideMargin);
        for (int attempt = 0; attempt < 80; attempt++)
        {
            Vector2 p = Stage.Center + new Vector2(
                UnityEngine.Random.Range(minX, maxX),
                UnityEngine.Random.Range(-Stage.HalfSize.y + margin, Stage.HalfSize.y - margin));
            float gap = Radius * 2f + PlaceGap;
            bool free = (p - avoid).sqrMagnitude >= avoidRadius * avoidRadius;   // 보스 중심과의 최소 거리
            foreach (var pillar in pillars)
                if (free && pillar.State != State.Breaking && (pillar.Pos - p).sqrMagnitude < gap * gap) free = false;   // 잔해 위에는 떨어져도 됨
            foreach (var other in pending)
                if (free && (other - p).sqrMagnitude < gap * gap) free = false;
            if (free) { result = p; return true; }
        }
        result = Vector2.zero;
        return false;
    }

    public void BeginFall(Vector2 position)
    {
        var root = new GameObject("Special2StonePillar");
        var sr = root.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 7;   // 보스 (5) 위 · 플레이어 (10) 아래 [임시]
        if (frames != null) sr.sprite = frames[0];
        else { sr.sprite = fallback; root.transform.localScale = new Vector3(Radius * 2f, Radius * 3f, 1f); }
        root.transform.position = position;
        pillars.Add(new Pillar { Pos = position, Root = root, Renderer = sr, State = State.Falling });
    }

    /// <summary>발 위치 y 로 앞뒤를 정하는 표시 순서를 모든 기둥에 적용 (원근감 가림)</summary>
    public void SetDepth(Func<Vector2, int> orderOf)
    {
        foreach (var pillar in pillars) pillar.Renderer.sortingOrder = orderOf(pillar.Pos);
    }

    /// <summary>낙하 중인 기둥을 지면 위 height 에 표시</summary>
    public void SetFallHeight(float height)
    {
        foreach (var pillar in pillars)
            if (pillar.State == State.Falling)
                pillar.Root.transform.position = pillar.Pos + Vector2.up * height;
    }

    /// <summary>
    /// 낙하 중이던 기둥을 모두 착지시키고 그 위치들을 돌려줌. 사용자: 한 번에 10개 낙하, 9개는 낙하와 동시에 부서지고 1개(무작위)만 남음.
    /// </summary>
    public void LandAll(List<Vector2> landed)
    {
        var batch = new List<Pillar>();
        foreach (var pillar in pillars)
        {
            if (pillar.State != State.Falling) continue;
            pillar.State = State.Standing; pillar.Solid = true;
            pillar.Root.transform.position = pillar.Pos;
            landed?.Add(pillar.Pos);
            batch.Add(pillar);
        }
        if (batch.Count == 0) return;
        var survivor = batch[UnityEngine.Random.Range(0, batch.Count)];
        foreach (var pillar in batch)
        {
            if (pillar == survivor) continue;
            pillar.State = State.Breaking; pillar.Solid = false; pillar.Age = 0f;
        }
    }

    /// <summary>center 반지름 reach 원에 닿은 서 있는 기둥을 파괴 (초거대 화염구)</summary>
    public void BreakTouching(Vector2 center, float reach)
    {
        foreach (var pillar in pillars)
        {
            if (pillar.State != State.Standing) continue;
            if ((pillar.Pos - center).sqrMagnitude > reach * reach) continue;
            pillar.State = State.Breaking; pillar.Solid = false; pillar.Age = 0f;
        }
    }

    /// <summary>서 있는 기둥 중 무작위 하나만 남기고 파괴 (사용자: 10개 중 9개 파괴)</summary>
    public void BreakAllExceptOne()
    {
        var standing = new List<Pillar>();
        foreach (var pillar in pillars) if (pillar.State == State.Standing) standing.Add(pillar);
        if (standing.Count == 0) return;
        var survivor = standing[UnityEngine.Random.Range(0, standing.Count)];
        foreach (var pillar in standing)
        {
            if (pillar == survivor) continue;
            pillar.State = State.Breaking; pillar.Solid = false; pillar.Age = 0f;
        }
    }

    public void Tick(float dt)
    {
        foreach (var pillar in pillars)
        {
            if (pillar.State != State.Breaking) continue;
            if (frames == null || pillar.Age > BreakAnimTime + RubbleFade) { pillar.Root.SetActive(false); continue; }
            pillar.Age += dt;
            // 파괴 애니메이션이 끝나면 잔해가 천천히 투명해지며 사라짐 (사용자)
            pillar.Renderer.color = new Color(1f, 1f, 1f, 1f - Mathf.Clamp01((pillar.Age - BreakAnimTime) / RubbleFade));
            float t = pillar.Age;
            int frame = 1;
            for (int i = 0; i < BreakFrameTime.Length; i++)
            {
                frame = i + 1;
                if (t < BreakFrameTime[i]) break;
                t -= BreakFrameTime[i];
            }
            pillar.Renderer.sprite = frames[Mathf.Clamp(frame, 1, FrameCount - 1)];
        }
    }

    // ---------- 기하 ----------
    /// <summary>origin 에서 dir(단위) 방향 반직선이 서 있는 기둥에 처음 닿는 거리. 없으면 maxDistance</summary>
    public float RayBlock(Vector2 origin, Vector2 dir, float maxDistance)
    {
        float best = maxDistance;
        foreach (var pillar in pillars)
        {
            if (!pillar.Solid) continue;
            Vector2 m = origin - pillar.Pos;
            float b = Vector2.Dot(m, dir), c = m.sqrMagnitude - ShadowRadius * ShadowRadius;
            if (c > 0f && b > 0f) continue;
            float discriminant = b * b - c;
            if (discriminant < 0f) continue;
            float t = Mathf.Max(0f, -b - Mathf.Sqrt(discriminant));
            if (t < best) best = t;
        }
        return best;
    }

    /// <summary>origin 에서 point 가 보이지 않게 기둥이 가리는가 (레이저 안전지대 · 경고 제외 영역)</summary>
    public bool Shadowed(Vector2 origin, Vector2 point)
    {
        Vector2 ray = point - origin;
        float length = ray.magnitude;
        if (length < 0.0001f) return false;
        return RayBlock(origin, ray / length, length) < length;
    }

    public static void ConstrainPlayer(Player player, Vector2 previousPosition)
    {
        if (active == null || active.disposed || player == null) return;
        Vector2 offset = player.HurtCenter - (Vector2)player.transform.position;
        Vector2 from = previousPosition + offset, target = player.HurtCenter;
        for (int pass = 0; pass < 2; pass++)
            foreach (var pillar in active.pillars)
            {
                if (!pillar.Solid) continue;
                Vector2 resolved = KagutsuchiSolarGeometry.BlockMove(from, target, pillar.Pos + Vector2.up * BlockLift, BlockRadius + player.radius);
                if ((resolved - target).sqrMagnitude > 0.00000001f) { target = resolved; from = resolved; }
            }
        player.transform.position = target - offset;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (active == this) active = null;
        foreach (var pillar in pillars) if (pillar.Root != null) { pillar.Root.SetActive(false); UnityEngine.Object.Destroy(pillar.Root); }
        pillars.Clear();
        if (frames != null) foreach (var frame in frames) if (frame != null) UnityEngine.Object.Destroy(frame);
    }
}

/// <summary>레이저 준비 경고: 부채꼴에서 기둥 그림자 영역을 뺀 모양. BossWarning 과 같은 색 · 깜빡임.</summary>
public sealed class KagutsuchiShadowSectorWarning : MonoBehaviour
{
    const int TextureSize = 512;
    static readonly Color Red = new Color(1f, 0.2f, 0.15f, 0.32f);
    Texture2D texture;
    Sprite sprite;
    SpriteRenderer sr;

    public static KagutsuchiShadowSectorWarning Create(Vector2 origin, Vector2 dir, float totalAngle, KagutsuchiPillarField pillars)
    {
        // 맵 모서리까지의 최대 거리만큼 정사각형으로 덮는다.
        float extent = 0f;
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                extent = Mathf.Max(extent, (Stage.Center + new Vector2(sx * Stage.HalfSize.x, sy * Stage.HalfSize.y) - origin).magnitude);
        extent = Mathf.Max(extent, 1f);

        var go = new GameObject("Special2LaserWarning");
        go.transform.position = origin;
        var warning = go.AddComponent<KagutsuchiShadowSectorWarning>();
        warning.sr = go.AddComponent<SpriteRenderer>();
        warning.sr.sortingOrder = Fx.GroundOrder;

        warning.texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[TextureSize * TextureSize];
        float cosHalf = Mathf.Cos(Mathf.Clamp(totalAngle, 0f, 360f) * 0.5f * Mathf.Deg2Rad);
        Vector2 forward = dir.normalized;
        Vector2 min = Stage.Center - Stage.HalfSize, max = Stage.Center + Stage.HalfSize;
        for (int y = 0; y < TextureSize; y++)
            for (int x = 0; x < TextureSize; x++)
            {
                Vector2 p = origin + new Vector2(((x + 0.5f) / TextureSize * 2f - 1f) * extent, ((y + 0.5f) / TextureSize * 2f - 1f) * extent);
                Vector2 delta = p - origin;
                float distance = delta.magnitude;
                bool inside = distance > 0.0001f && distance <= extent
                              && Vector2.Dot(delta / distance, forward) >= cosHalf
                              && p.x >= min.x && p.x <= max.x && p.y >= min.y && p.y <= max.y
                              && !(pillars != null && pillars.Shadowed(origin, p));
                pixels[y * TextureSize + x] = new Color32(255, 255, 255, (byte)(inside ? 255 : 0));
            }
        warning.texture.SetPixels32(pixels);
        warning.texture.Apply();
        warning.sprite = Sprite.Create(warning.texture, new Rect(0f, 0f, TextureSize, TextureSize), new Vector2(0.5f, 0.5f),
                                       TextureSize / (extent * 2f), 0, SpriteMeshType.FullRect);
        warning.sr.sprite = warning.sprite;
        warning.sr.color = Red;
        return warning;
    }

    void Update()
    {
        if (GameManager.I == null || GameManager.I.State != GameState.Playing) return;
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 14f);
        sr.color = new Color(Red.r, Red.g, Red.b, Red.a * pulse);
    }

    void OnDestroy()
    {
        if (sprite != null) Destroy(sprite);
        if (texture != null) Destroy(texture);
    }
}

/// <summary>풍압 이펙트 (Kagutsuchi_P2_Fx_KickWind 3×2 칸 512). 펀치 앞에서 한 번 재생, 판정 없음.</summary>
public sealed class KagutsuchiKickWindFx : IDisposable
{
    const string SheetName = "Kagutsuchi_P2_Fx_KickWind";
    const int FrameCount = 6, Columns = 3, Rows = 2, Cell = 512;
    const float TipX = 88f / Cell;      // 원화에서 뾰족한 끝 (왼쪽) 위치
    const float PixelsPerUnit = 64f / 3f;   // [임시] 사용자: 풍압 크기 3배 (이전 64 = 가장 큰 프레임이 약 6칸 길이 → 약 18칸)
    const float Fps = 12f;              // [임시]
    const float FrontDistance = 2f;     // [임시] 보스 앞 거리
    readonly Sprite[] frames;
    GameObject root;
    SpriteRenderer renderer;
    float age;
    bool playing;

    public KagutsuchiKickWindFx()
    {
        var texture = Resources.Load<Texture2D>("Sprites/" + SheetName);
        if (texture == null || texture.width != Columns * Cell || texture.height != Rows * Cell) return;
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            var rect = new Rect(i % Columns * Cell, (Rows - 1 - i / Columns) * Cell, Cell, Cell);
            frames[i] = Sprite.Create(texture, rect, new Vector2(TipX, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }
    }

    public void Play(Vector2 bossPosition, bool facingRight, int sortingOrder)
    {
        if (frames == null) return;
        if (root == null)
        {
            root = new GameObject("Special2KickWind");
            renderer = root.AddComponent<SpriteRenderer>();
        }
        renderer.sortingOrder = sortingOrder;
        renderer.flipX = !facingRight;     // 원화는 오른쪽으로 뻗음 (끝이 왼쪽)
        root.transform.position = bossPosition + Vector2.right * (facingRight ? FrontDistance : -FrontDistance);
        age = 0f; playing = true;
        renderer.sprite = frames[0];
        root.SetActive(true);
    }

    public void Tick(float dt)
    {
        if (!playing) return;
        age += dt;
        int frame = (int)(age * Fps);
        if (frame >= FrameCount) { Stop(); return; }
        renderer.sprite = frames[frame];
    }

    public void Stop()
    {
        playing = false;
        if (root != null) root.SetActive(false);
    }

    public void Dispose()
    {
        Stop();
        if (root != null) UnityEngine.Object.Destroy(root);
        if (frames != null) foreach (var frame in frames) if (frame != null) UnityEngine.Object.Destroy(frame);
    }
}

/// <summary>
/// 2페이즈 특수 패턴 2 (사용자 2026-11-16). Phase2 가 Begin · Tick · Cancel 을 소유 (특수 패턴 1 과 같은 구조).
/// 흐름: 중앙 이동 → 대기 → 점프 → 좌/우 끝 경고 → 낙하 · 기절 → 펀치 넉백 → (발구르기 → 내려찍기 → 돌기둥 2개) ×5
///  → 돌기둥 9개 파괴 → 레이저 준비 → 레이저 5초 → 초거대 화염구 → 돌진 펀치 → 화면 점멸 → 10초 취약 (받는 피해 +200%).
/// 모든 피해 [임시] 5. 패턴 동안 맵 전체가 보이게 카메라 축소.
/// </summary>
public sealed class KagutsuchiSpecial2Pattern : MonoBehaviour
{
    // ---- 사용자 지정 ----
    const float Damage = 5f;                       // [임시] 사용자: 모든 피해 임시 5
    const float EdgeToCenterTime = 1f, PrepareTime = 2.5f, ExitTime = 0.5f;   // 특수 패턴 1 과 동일 스펙
    const float EdgeInset = 3f, LandRadius = 4f, AimTime = 2f;                // 끝에서 3 거리, 경고 radius 4, 2초 후 낙하
    const float LandShakeTime = 0.5f;
    const float StunTime = 1.5f, PunchTime = 1.5f, KnockSpeed = 30f;   // 사용자 (2026-11-16): 첫 펀치 넉백 속도 10 → 3배
    const float ReadyTime = 1f, StompShakeTime = 1f;
    const float PillarWarnRadius = 2f, PillarFallTime = 0.25f, PillarStun = 1f;
    const float SlowMultiplier = 0.4f;             // 이동속도 60% 감소
    const int Cycles = 5, PillarsPerCycle = 10;    // 한 번에 10개 낙하 (사용자 2026-11-16: 9개는 낙하와 동시에 파괴)
    const float LaserReadyTime = 2f, LaserAngle = 170f /* 사용자: 160 → 180 → 170 */, LaserWidth = 1f, LaserLife = 0.25f, LaserInterval = 0.02f /* 사용자: 0.05 → 0.02 */, LaserTotal = 5f;
    const float LaserEndWait = 1f;
    const float FireGrowTime = 1f, FireMoveDistance = 8f /* 사용자: 3 → +5 */, FireMoveTime = 1f, FireHoldTime = 3f, DashTime = 0.25f;
    const float FlashHold = 1f, FlashFade = 2f, VulnerableStart = 0.5f, VulnerableTime = 10f;
    const float VulnerableMultiplier = 3f;         // 받는 최종 피해량 +200%
    // ---- [임시] 기획에 없는 값 ----
    const float DropTime = 0.25f;                  // 보스 낙하 시간
    const float StompRadius = 4f;                  // 내려찍기 먼지 · 판정 반지름
    const float PillarWarnTime = 1f;               // 돌기둥 경고 시간
    const float BossClearance = 5f;                // 사용자 (2026-11-16): 돌기둥은 보스 중심에서 최소 5 떨어져 낙하
    const float PillarBreakDelay = 1f;             // 5회차 돌기둥 착지 후 파괴까지
    // FireHeight = 화염구 중심이 지면 기준점 (판정 · 안전지대 · 정렬 기준) 보다 위로 뜬 높이. 0 = 화염구 중심이 레이저 시작점과 같은 높이 (사용자: 작은 화염구를 레이저 시작점에 맞춤)
    const float FireRadius = 5f, FireHeight = 0f;
    const float SmallFireRadius = 0.9f /* 사용자: 0.3 → 3배 */;  // [임시] 레이저 시작 위치의 아주 작은 화염구 (이후 FireRadius 까지 커짐). 시작 위치 = 레이저 시작 위치 (LaserHandOffset)
    const float LaserTick = 0.25f;
    const float WeakShake = 0.05f, StrongShake = 0.35f, PillarShakeAmp = 0.2f;
    const float LaserShake = 0.1f;                 // [임시] 레이저 발사 중 화면 흔들림 세기
    const float DashZoomLead = 0.5f;               // 사용자: 돌진 펀치 0.5초 전 보스 확대
    const float CameraZoom = 1.5f;                // [임시] 보스 확대 배율 (특수 패턴 1 의 Magnification 과 같음)
    const float Epsilon = 0.0001f;

    enum Step
    {
        Idle, CenterRun, Prepare, Exit, Aim, Drop, Landed, Punch, StompReady, Stomp, PillarFall, PillarRest, Breaking,
        LaserReady, LaserFire, LaserEnd, FireGrow, FireMove, FireHold, DashPunch, Flash
    }

    Step step;
    Enemy body;
    SpriteRenderer bodyVisual;
    Sprite stomp, jump, stompReady, punch, grabReady, grabDash, laugh;
    Action<Sprite, float> showPose;
    Action<float> showRun;
    Action stopPose;
    Func<Transform> visualTarget;
    Action<Vector2, float> stompFx;
    Action<Vector2, bool> punchFx;

    float elapsed, startHeight, dashDistance, lastDt;
    Vector2 moveOnce;
    int side, cycle, emitted;
    Vector2 landPoint, fireStart, firePosition;
    bool beforeScripted, beforeHidden, disposed, vulnerableStarted, dashZoomed, cameraReleased;
    BossWarning landWarning;
    readonly List<BossWarning> pillarWarnings = new List<BossWarning>();
    readonly List<Vector2> pending = new List<Vector2>();
    readonly List<RaidLaser> lasers = new List<RaidLaser>();
    KagutsuchiPillarField pillars;
    KagutsuchiShadowSectorWarning laserWarning, fireWarning;
    KagutsuchiKickWindFx wind;
    GameObject fire;
    SpriteRenderer fireRenderer;
    KagutsuchiSunFireAnim fireAnim;
    float fireCellScale = 1f;
    bool fireBreaksPillars;
    Vulnerability modifier;

    public bool Active => step != Step.Idle;
    public bool Vulnerable => step == Step.Flash && elapsed >= VulnerableStart && elapsed < VulnerableStart + VulnerableTime;
    Vector2 Facing => new Vector2(side < 0 ? 1f : -1f, 0f);     // 왼쪽 끝이면 오른쪽(맵 안쪽)을 봄
    bool FacingRight => side < 0;
    const float LaserHandOffset = 4f; /* 사용자: 2 → +2 */              // 사용자 (2026-11-16): 레이저 발사 위치를 앞(손)쪽으로 2 이동
    Vector2 FireOrigin => firePosition - Vector2.up * FireHeight;   // 화염구 지면 위치 = 전체 공격의 기준점
    Vector2 LaserOrigin => (Vector2)body.transform.position + Facing * LaserHandOffset;

    sealed class Vulnerability : CombatModifier
    {
        readonly KagutsuchiSpecial2Pattern owner;
        public Vulnerability(KagutsuchiSpecial2Pattern owner) { this.owner = owner; }
        public override float ReceivedMul(Enemy target, in DamageInfo damage)
            => owner.Vulnerable && target == owner.body ? VulnerableMultiplier : 1f;
    }

    public void Initialize(Enemy body, Sprite stomp, Sprite jump, Sprite stompReady, Sprite punch, Sprite grabReady,
                           Sprite grabDash, Sprite laugh, Action<Sprite, float> showPose, Action<float> showRun,
                           Action stopPose, Func<Transform> visualTarget, Action<Vector2, float> stompFx, Action<Vector2, bool> punchFx)
    {
        this.body = body; bodyVisual = body.GetComponent<SpriteRenderer>();
        this.stomp = stomp; this.jump = jump; this.stompReady = stompReady; this.punch = punch;
        this.grabReady = grabReady; this.grabDash = grabDash; this.laugh = laugh;
        this.showPose = showPose; this.showRun = showRun; this.stopPose = stopPose; this.visualTarget = visualTarget;
        this.stompFx = stompFx; this.punchFx = punchFx;
        wind = new KagutsuchiKickWindFx();
        modifier = new Vulnerability(this); CombatMods.Add(modifier);
    }

    public void Begin()
    {
        Cancel();
        if (body == null || disposed) return;
        beforeScripted = body.Scripted; beforeHidden = body.Hidden;
        body.SetScripted(true);
        startHeight = Stage.HalfSize.y * 2f + 8f;
        pillars = new KagutsuchiPillarField();
        vulnerableStarted = false; dashZoomed = false; cameraReleased = false;
        Enter(Step.CenterRun);
        showRun(0f);
        // 카메라 (사용자 2026-11-16): 중앙 이동 · 대기 때 보스 확대 · 귀속 → 점프 때 맵 안으로 제한된 추적
        // → 맵 전체 → 낙하하면 다시 보스 확대 → 펀치 뒤 맵 전체
        CameraFollow.BeginOutsideMapFollow(this, visualTarget(), CameraZoom);
    }

    void Update()
    {
        if (!Active || GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) Cancel();
    }

    void Enter(Step next) { step = next; elapsed = 0f; }

    public Vector2 Tick(float dt)
    {
        if (!Active || dt <= 0f) return Vector2.zero;
        if (body == null || body.hp <= 0f || Player.I == null || Player.I.hp <= 0f) { Cancel(); return Vector2.zero; }
        elapsed += dt; lastDt = dt;
        pillars?.Tick(dt);
        wind?.Tick(dt);
        if (fire != null) fireAnim?.Tick(dt, fireRenderer);   // 화염구 표면 흐름 반복
        Vector2 velocity = Vector2.zero;
        switch (step)
        {
            case Step.CenterRun:
                Vector2 current = body.transform.position;
                Vector2 next = Vector2.MoveTowards(current, Stage.Center, Stage.HalfSize.magnitude / EdgeToCenterTime * dt);
                bodyVisual.flipX = Stage.Center.x > current.x;
                showRun(elapsed);
                if ((next - Stage.Center).sqrMagnitude <= Epsilon * Epsilon)
                {
                    Enter(Step.Prepare); showPose(stomp, 0f);
                    CameraFollow.Shake(PrepareTime, WeakShake);
                }
                return (next - current) / dt;
            case Step.Prepare:
                if (elapsed >= PrepareTime)
                {
                    Enter(Step.Exit); showPose(jump, 0f);
                    CameraFollow.SetOutsideMapFollowClamped(this, true);   // 보스가 맵 밖으로 올라가도 카메라는 따라가지 않음
                    CameraFollow.Shake(ExitTime, StrongShake);
                }
                break;
            case Step.Exit:
                showPose(jump, startHeight * Mathf.Clamp01(elapsed / ExitTime));
                if (elapsed >= ExitTime)
                {
                    body.SetHidden(true); stopPose();
                    CameraFollow.SetOutsideMapFullView(this);   // 맵 전체로 축소
                    BeginAim();
                }
                break;
            case Step.Aim:
                if (elapsed >= AimTime) BeginDrop();
                break;
            case Step.Drop:
                showPose(stompReady, startHeight * (1f - Mathf.Clamp01(elapsed / DropTime)));
                if (elapsed >= DropTime) Land();
                break;
            case Step.Landed:
                if (elapsed >= StunTime) BeginPunch();
                break;
            case Step.Punch:
                if (elapsed >= PunchTime)
                {
                    CameraFollow.SetOutsideMapFullView(this);   // 펀치 뒤 다시 축소
                    cycle = 0; BeginStompReady();
                }
                break;
            case Step.StompReady:
                if (elapsed >= ReadyTime) DoStomp();
                break;
            case Step.Stomp:
                if (elapsed >= PillarWarnTime) BeginPillarFall();
                break;
            case Step.PillarFall:
                pillars.SetFallHeight(startHeight * (1f - Mathf.Clamp01(elapsed / PillarFallTime)));
                if (elapsed >= PillarFallTime) LandPillars();
                break;
            case Step.PillarRest:
                if (elapsed >= PillarBreakDelay) BeginLaserReady();   // 파괴는 착지와 동시에 끝남 (남은 기둥 = 회차당 1개)
                break;
            case Step.Breaking:
                if (elapsed >= KagutsuchiPillarField.BreakDuration) BeginLaserReady();
                break;
            case Step.LaserReady:
                if (elapsed >= LaserReadyTime)
                {
                    ClearLaserWarning();
                    showPose(grabDash, 0f); emitted = 0; Enter(Step.LaserFire);
                    CameraFollow.Shake(LaserTotal, LaserShake);   // 사용자: 레이저 발사 동안 화면 흔들림
                }
                break;
            case Step.LaserFire:
                SpawnLasers();
                if (elapsed >= LaserTotal) { showPose(grabReady, 0f); Enter(Step.LaserEnd); }   // 사용자: 돌진 펀치 전까지 GrabReady 유지
                break;
            case Step.LaserEnd:
                if (elapsed >= LaserEndWait) BeginFire();
                break;
            case Step.FireGrow:
                float grow = Mathf.Clamp01(elapsed / FireGrowTime);
                SetFire(fireStart, Mathf.Lerp(SmallFireRadius, FireRadius, Mathf.SmoothStep(0f, 1f, grow)));
                if (elapsed >= FireGrowTime) Enter(Step.FireMove);
                break;
            case Step.FireMove:
                float flight = Mathf.Clamp01(elapsed / FireMoveTime);
                // 사용자 (2026-11-16): 맵 중앙까지 날아감 (이전: 정면으로 FireMoveDistance)
                firePosition = Vector2.Lerp(fireStart, Stage.Center + Vector2.up * FireHeight, flight);
                SetFire(firePosition, Mathf.Lerp(SmallFireRadius, FireRadius, flight));   // 날아가는 동안 커짐
                if (elapsed >= FireMoveTime)
                {
                    Enter(Step.FireHold);
                    // 사용자: 이동이 끝나면 레이저와 같은 방식으로 전체 맵 (360도) 워닝 + 돌기둥 뒤 안전지대
                    fireWarning = KagutsuchiShadowSectorWarning.Create(FireOrigin, Facing, 360f, pillars);
                }
                break;
            case Step.FireHold:
                if (!dashZoomed && elapsed >= FireHoldTime - DashZoomLead)
                {
                    dashZoomed = true;   // 사용자: 펀치 돌진 0.5초 전 보스 확대 · 귀속
                    CameraFollow.BeginOutsideMapFollow(this, visualTarget(), CameraZoom, true);
                }
                if (elapsed >= FireHoldTime) BeginDash();
                break;
            case Step.DashPunch:
                velocity = Facing * (dashDistance / DashTime);
                if (elapsed >= DashTime) Explode();
                break;
            case Step.Flash:
                if (!vulnerableStarted && elapsed >= VulnerableStart) { vulnerableStarted = true; body.SetScripted(false); pillars?.Dispose(); pillars = null; }   // 사용자: 점멸 0.5초 후 설치된 돌기둥 모두 삭제 (화면은 아직 하얗다)
                if (!cameraReleased && elapsed >= FlashHold + FlashFade)
                {
                    cameraReleased = true;   // 사용자: 점멸 후 화면이 복구되면 카메라도 원래 상태로
                    CameraFollow.EndOutsideMapFollow(this);
                }
                if (elapsed >= VulnerableStart + VulnerableTime) Cancel();
                break;
        }
        UpdateDepth();
        velocity += moveOnce; moveOnce = Vector2.zero;
        return velocity;
    }

    // ---------- 원근감 (사용자 2026-11-16): y 가 아래일수록 앞에 그려짐 ----------
    // 기존 표시 순서는 보스 5 · 플레이어 10 고정이라, 기둥 · 화염구만 발 y 를 비교해 세 구간 중 하나에 넣는다.
    //  플레이어보다 아래(앞) = 플레이어 위 / 보스보다 아래 = 보스 위 · 플레이어 아래 / 그 외 = 보스 아래
    const int PlayerOrder = 10, LaserOrder = 8;   // RaidLaser 코어 표시 순서 (Ganiac.cs CoreOrder)

    int BandOrder(float y)
    {
        float playerY = Player.I != null ? Player.I.transform.position.y : float.MaxValue;
        int bossOrder = bodyVisual.sortingOrder;
        if (y < playerY) return PlayerOrder + 2;
        // 레이저 (표시 순서 8) 는 보스 높이의 평면에 있다고 보고, 보스보다 아래(앞)의 기둥은 레이저 위 (9),
        // 위(뒤)의 기둥은 레이저 아래 (3) 에 둠 → 기둥이 레이저를 가리거나 레이저가 기둥 앞을 지나는 모습이 일관됨
        return y < body.transform.position.y ? LaserOrder + 1 : bossOrder - 2;
    }

    void UpdateDepth()
    {
        if (Player.I == null) return;
        bool hasFire = fire != null;
        float fireY = firePosition.y - FireHeight;      // 화염구의 발 위치 = 생성 시 보스 발 높이
        int highest = int.MinValue;
        pillars?.SetDepth(spot => { int order = BandOrder(spot.y); highest = Mathf.Max(highest, order); return order; });
        // 사용자: 초거대 화염구는 돌기둥보다 항상 앞
        if (hasFire) fireRenderer.sortingOrder = Mathf.Max(BandOrder(fireY), highest + 1);
    }

    void BreakPillarsTouchingFire(float radius)
    {
        // 화염구 발 위치 (지면) 기준 원에 닿으면 파괴. 기둥 판정은 가시 폭에 가까운 ShadowRadius
        pillars?.BreakTouching(firePosition - Vector2.up * FireHeight, radius + KagutsuchiPillarField.ShadowRadius);
    }

    // ---------- 단계 ----------
    void BeginAim()
    {
        side = UnityEngine.Random.value < 0.5f ? -1 : 1;     // 좌측 또는 우측 중앙 끝
        landPoint = Stage.Center + new Vector2(side * (Stage.HalfSize.x - EdgeInset), 0f);
        landWarning = BossWarning.Circle(landPoint, LandRadius, 0f);
        Enter(Step.Aim);
    }

    void BeginDrop()
    {
        ClearLandWarning();
        // Enemy.Update 가 Behave 뒤에 transform.position 을 덮어쓰므로 직접 대입은 무효 → 이번 프레임 속도로 순간이동
        moveOnce = (landPoint - (Vector2)body.transform.position) / Mathf.Max(Epsilon, lastDt);
        bodyVisual.flipX = FacingRight;
        body.SetHidden(false);
        showPose(stompReady, startHeight);
        CameraFollow.BeginOutsideMapFollow(this, visualTarget(), CameraZoom, true);   // 낙하하면 다시 보스 확대
        CameraFollow.SetOutsideMapFollowClamped(this, true);
        Enter(Step.Drop);
    }

    void Land()
    {
        showPose(stomp, 0f);
        stompFx?.Invoke(landPoint, LandRadius);
        CameraFollow.Shake(LandShakeTime, StrongShake);
        HitCircle(landPoint, LandRadius);
        var player = Player.I;
        if (player != null) player.StunByBoss(StunTime);   // 적중 여부와 상관없이 기절
        Enter(Step.Landed);
    }

    void BeginPunch()
    {
        showPose(punch, 0f);
        punchFx?.Invoke(body.transform.position, FacingRight);
        wind?.Play(body.transform.position, FacingRight, bodyVisual.sortingOrder + 1);
        KnockPlayerToFarEnd();
        Enter(Step.Punch);
    }

    void KnockPlayerToFarEnd()
    {
        var player = Player.I;
        if (player == null) return;
        Vector2 hurtOffset = player.HurtCenter - (Vector2)player.transform.position;
        float targetX = Stage.Center.x - side * (Stage.HalfSize.x - player.radius);   // 보스 반대편 맵 끝
        Vector2 destination = new Vector2(targetX, player.HurtCenter.y);
        float seconds = Mathf.Max(0.05f, Mathf.Abs(destination.x - player.HurtCenter.x) / KnockSpeed);
        player.MoveByBoss(destination - hurtOffset, seconds);
    }

    void BeginStompReady()
    {
        showPose(stompReady, 0f);
        Enter(Step.StompReady);
    }

    void DoStomp()
    {
        Vector2 ground = body.transform.position;
        showPose(stomp, 0f);
        stompFx?.Invoke(ground, StompRadius);
        CameraFollow.Shake(StompShakeTime, StrongShake);
        HitCircle(ground, StompRadius);
        var player = Player.I;
        if (player != null) player.ApplySlowByBoss(SlowMultiplier, StompShakeTime + PillarWarnTime + 6f);   // [임시] 지속시간
        // 돌기둥 낙하 지점: 기존 기둥이 있는 곳은 제외
        pending.Clear();
        for (int i = 0; i < PillarsPerCycle; i++)
            if (pillars.TryPick(pending, ground, BossClearance, side, out Vector2 spot)) pending.Add(spot);
        foreach (var spot in pending) pillarWarnings.Add(BossWarning.Circle(spot, PillarWarnRadius, 0f));
        Enter(Step.Stomp);
    }

    void BeginPillarFall()
    {
        ClearPillarWarnings();
        foreach (var spot in pending) pillars.BeginFall(spot);
        pillars.SetFallHeight(startHeight);
        Enter(Step.PillarFall);
    }

    void LandPillars()
    {
        var landed = new List<Vector2>();
        pillars.LandAll(landed);
        pending.Clear();
        var player = Player.I;
        foreach (var spot in landed)
            if (player != null && (player.HurtCenter - spot).sqrMagnitude <= (PillarWarnRadius + player.radius) * (PillarWarnRadius + player.radius))
                player.HitByBoss(Damage, PillarStun, Vector2.zero, 0f);
        CameraFollow.Shake(0.25f, PillarShakeAmp);
        cycle++;
        if (cycle < Cycles) BeginStompReady();
        else Enter(Step.PillarRest);
    }

    void BeginLaserReady()
    {
        showPose(grabReady, 0f);
        laserWarning = KagutsuchiShadowSectorWarning.Create(LaserOrigin, Facing, LaserAngle, pillars);
        SpawnSmallFire();   // 레이저 시작 위치에 아주 작은 화염구 (이후 이것이 커져서 날아감)
        Enter(Step.LaserReady);
    }

    void SpawnLasers()
    {
        int total = Mathf.RoundToInt(LaserTotal / LaserInterval);
        while (emitted < total && elapsed >= emitted * LaserInterval)
        {
            emitted++;
            float angle = UnityEngine.Random.Range(-LaserAngle * 0.5f, LaserAngle * 0.5f);
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Facing;
            Vector2 from = LaserOrigin;
            float length = RayExitDistance(from, dir);
            if (pillars != null) length = pillars.RayBlock(from, dir, length);   // 돌기둥이 경로에 있으면 막힘
            if (length < 0.05f) continue;
            lasers.RemoveAll(x => x == null);
            var laser = RaidLaser.Line(from, dir, length, LaserWidth, LaserLife, Damage, LaserTick);
            KagutsuchiFlameLaserFx.Attach(laser);
            lasers.Add(laser);
        }
    }

    static float RayExitDistance(Vector2 origin, Vector2 dir)
    {
        Vector2 min = Stage.Center - Stage.HalfSize, max = Stage.Center + Stage.HalfSize;
        float tx = dir.x > Epsilon ? (max.x - origin.x) / dir.x : dir.x < -Epsilon ? (min.x - origin.x) / dir.x : float.PositiveInfinity;
        float ty = dir.y > Epsilon ? (max.y - origin.y) / dir.y : dir.y < -Epsilon ? (min.y - origin.y) / dir.y : float.PositiveInfinity;
        return Mathf.Max(0f, Mathf.Min(tx, ty));
    }

    /// <summary>레이저 시작 위치에 아주 작은 초거대 화염구를 만든다 (사용자 2026-11-16). 이후 BeginFire 가 같은 화염구를 키워 날린다.</summary>
    void SpawnSmallFire()
    {
        if (fire != null) return;
        fire = new GameObject("KagutsuchiSpecial2SunFire");
        var sr = fire.AddComponent<SpriteRenderer>();
        fireRenderer = sr;
        // 사용자 (2026-11-16): 정지 그림 대신 반복 애니메이션 (Kagutsuchi_SunFire_Loop). 시트가 없으면 이전 정지 그림.
        fireAnim?.Dispose();
        fireAnim = new KagutsuchiSunFireAnim();
        fireCellScale = fireAnim.Valid ? KagutsuchiSunFireAnim.CellScale : 1f;
        sr.sprite = fireAnim.Valid ? fireAnim.First
            : SpriteFactory.TryGet("Kagutsuchi_P2_SunFire") ?? SpriteFactory.Get("Fx_Circle", SpriteFactory.Shape.Circle);
        sr.sortingOrder = bodyVisual.sortingOrder - 2;   // 이후 UpdateDepth 가 y 로 갱신
        fireStart = LaserOrigin + Vector2.up * FireHeight;
        fireBreaksPillars = false;                        // 작은 상태 (레이저 구간) 에서는 돌기둥을 부수지 않음
        SetFire(fireStart, SmallFireRadius);
    }

    void BeginFire()
    {
        SpawnSmallFire();   // 보통은 레이저 준비 때 이미 있음
        fireBreaksPillars = true;
        firePosition = fireStart;
        Enter(Step.FireMove);   // 사용자 (2026-11-16): 제자리에서 커진 뒤 발사가 아니라, 작은 화염구가 날아가면서 커짐
    }

    void SetFire(Vector2 position, float radius)
    {
        if (fire == null) return;
        firePosition = position;
        if (fireBreaksPillars) BreakPillarsTouchingFire(radius);   // 사용자: 닿으면 돌기둥 파괴
        var sprite = fire.GetComponent<SpriteRenderer>().sprite;
        fire.transform.position = position;
        fire.transform.localScale = Vector3.one * (radius * 2f * fireCellScale / Mathf.Max(Epsilon, sprite.bounds.size.x));
    }

    void BeginDash()
    {
        showPose(punch, 0f);
        float horizontal = Mathf.Abs(firePosition.x - body.transform.position.x);
        dashDistance = Mathf.Max(0f, horizontal - FireRadius * 0.5f);   // [임시] 화염구 안쪽까지 파고듦
        Enter(Step.DashPunch);
    }

    void Explode()
    {
        punchFx?.Invoke(body.transform.position, FacingRight);
        var player = Player.I;
        // 사용자: 화염구는 360도 맵 전체 공격. 돌기둥 뒤 (안전지대) 만 피함. 피해 [임시] 5
        if (player != null && !(pillars != null && pillars.Shadowed(FireOrigin, player.HurtCenter)))
            player.TakeDamage(Damage);
        ClearFireWarning();
        DestroyFire();
        showPose(laugh, 0f);   // 취약 시간 동안 Laugh 유지
        Enter(Step.Flash);
    }

    void HitCircle(Vector2 center, float radius)
    {
        var player = Player.I;
        if (player != null && (player.HurtCenter - center).sqrMagnitude <= (radius + player.radius) * (radius + player.radius))
            player.TakeDamage(Damage);
    }

    void OnGUI()
    {
        if (step != Step.Flash || elapsed >= FlashHold + FlashFade) return;
        int depth = GUI.depth; Color color = GUI.color;
        GUI.depth = -1000;
        float alpha = elapsed <= FlashHold ? 1f : 1f - Mathf.Clamp01((elapsed - FlashHold) / FlashFade);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.depth = depth; GUI.color = color;
    }

    // ---------- 정리 ----------
    void ClearLandWarning()
    {
        if (landWarning == null) return;
        landWarning.gameObject.SetActive(false); Destroy(landWarning.gameObject); landWarning = null;
    }

    void ClearPillarWarnings()
    {
        foreach (var warning in pillarWarnings)
            if (warning != null) { warning.gameObject.SetActive(false); Destroy(warning.gameObject); }
        pillarWarnings.Clear();
    }

    void ClearLaserWarning()
    {
        if (laserWarning == null) return;
        laserWarning.gameObject.SetActive(false); Destroy(laserWarning.gameObject); laserWarning = null;
    }

    void ClearFireWarning()
    {
        if (fireWarning == null) return;
        fireWarning.gameObject.SetActive(false); Destroy(fireWarning.gameObject); fireWarning = null;
    }

    void ClearLasers()
    {
        foreach (var laser in lasers)
            if (laser != null) { laser.gameObject.SetActive(false); Destroy(laser.gameObject); }
        lasers.Clear();
    }

    void DestroyFire()
    {
        if (fire != null) { fire.SetActive(false); Destroy(fire); fire = null; }
        if (fireAnim != null) { fireAnim.Dispose(); fireAnim = null; }
    }

    public void Cancel()
    {
        bool wasActive = Active;
        step = Step.Idle;
        ClearLandWarning(); ClearPillarWarnings(); ClearLaserWarning(); ClearFireWarning(); ClearLasers(); DestroyFire();
        pending.Clear();
        wind?.Stop();
        pillars?.Dispose(); pillars = null;
        CameraFollow.EndOutsideMapFollow(this);
        if (wasActive && body != null)
        {
            body.SetHidden(beforeHidden);
            if (!RaidStage.InTransition && body.hp > 0f) body.SetScripted(beforeScripted);
        }
        stopPose?.Invoke();
    }

    void OnDisable()
    {
        Cancel();
        if (modifier != null) CombatMods.Remove(modifier);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Cancel();
        wind?.Dispose(); wind = null;
        if (modifier != null) CombatMods.Remove(modifier);
    }
}
