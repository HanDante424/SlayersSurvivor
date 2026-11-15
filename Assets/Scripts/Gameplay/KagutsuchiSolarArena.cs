using System;
using UnityEngine;

/// <summary>특수 패턴의 원형 기둥 차폐·충돌 계산. 경고 셰이더도 같은 선분-원 교차식을 사용.</summary>
public static class KagutsuchiSolarGeometry
{
    const float Epsilon = 0.0001f;
    const int EllipseSearchIterations = 24;

    /// <summary>타원과 플레이어 원의 겹침. 타원 경계의 실제 최근접 거리로 가장자리 접촉도 검사.</summary>
    public static bool EllipseOverlapsCircle(Vector2 center, Vector2 halfAxes, Vector2 circleCenter, float circleRadius)
    {
        float a = halfAxes.x, b = halfAxes.y;
        if (a <= 0f || b <= 0f || circleRadius < 0f) return false;
        Vector2 delta = circleCenter - center;
        float x = Mathf.Abs(delta.x), y = Mathf.Abs(delta.y);
        if (x > a + circleRadius || y > b + circleRadius) return false;
        if (x * x / (a * a) + y * y / (b * b) <= 1f) return true;

        float aa = a * a, bb = b * b;
        float low = 0f, high = halfAxes.magnitude * delta.magnitude;
        for (int i = 0; i < EllipseSearchIterations; i++)
        {
            float t = (low + high) * 0.5f;
            float u = a * x / (t + aa), v = b * y / (t + bb);
            if (u * u + v * v > 1f) low = t;
            else high = t;
        }
        float parameter = (low + high) * 0.5f;
        Vector2 nearest = new Vector2(aa * x / (parameter + aa), bb * y / (parameter + bb));
        return (new Vector2(x, y) - nearest).sqrMagnitude <= circleRadius * circleRadius;
    }

    public static bool Covered(Vector2 origin, Vector2 point, Vector2 pillar, float radius)
    {
        Vector2 ray = point - origin;
        float length = ray.magnitude;
        if (length <= Epsilon) return false;
        Vector2 dir = ray / length, offset = pillar - origin;
        // 시선과 기둥 중심 사이 각도를 절반으로 압축하여 기존 안전각 폭을 정확히 2배로 확장.
        // 교차 원의 반경은 그대로이므로 충돌 크기 및 중심선상의 차폐 시작 거리는 유지.
        float distance = offset.magnitude;
        float along = Mathf.Sqrt(Mathf.Max(0f, (offset.sqrMagnitude + Vector2.Dot(offset, dir) * distance) * 0.5f));
        float crossSq = offset.sqrMagnitude - along * along;
        if (crossSq > radius * radius) return false;
        float near = along - Mathf.Sqrt(Mathf.Max(0f, radius * radius - crossSq));
        return near >= 0f && near <= length;
    }

    public static Vector2 BlockMove(Vector2 from, Vector2 to, Vector2 center, float radius)
    {
        Vector2 start = from - center;
        if (start.sqrMagnitude < radius * radius)
        {
            Vector2 outward = to - center;
            return center + (outward.sqrMagnitude > Epsilon ? outward.normalized : Vector2.down) * radius;
        }
        Vector2 delta = to - from;
        float a = delta.sqrMagnitude;
        if (a <= Epsilon * Epsilon) return to;
        float b = Vector2.Dot(start, delta), c = start.sqrMagnitude - radius * radius;
        float discriminant = b * b - a * c;
        if (discriminant <= 0f) return to;
        float t = (-b - Mathf.Sqrt(discriminant)) / a;
        if (t < 0f || t > 1f) return to;
        Vector2 contact = from + delta * Mathf.Max(0f, t - Epsilon);
        Vector2 normal = (contact - center).normalized;
        Vector2 remainder = delta * (1f - t);
        remainder -= normal * Mathf.Min(0f, Vector2.Dot(remainder, normal));
        Vector2 result = contact + remainder;
        Vector2 radial = result - center;
        if (radial.sqrMagnitude < radius * radius) result = center + normal * radius;
        return result;
    }

    public static float SunFallProgress(float normalizedTime)
    {
        // 6초 낙하: 각 초의 지정 진행률 사이를 보간. 마지막 4초는 초당 10% 진행.
        float seconds = Mathf.Clamp01(normalizedTime) * 6f;
        if (seconds <= 1f) return seconds * 0.4f;
        if (seconds <= 2f) return 0.4f + (seconds - 1f) * 0.2f;
        return Mathf.Lerp(0.6f, 1f, (seconds - 2f) / 4f);
    }
}

/// <summary>한 특수 패턴이 소유하는 기둥·안전각 경고. 해제하면 이동 차단도 즉시 해제.</summary>
public sealed class KagutsuchiSolarArena : IDisposable
{
    public const float Radius = 1f;
    const float CornerInset = 4f;
    const float PillarVisualScale = 3f; // 사용자: 직전 표시 크기(1.5배)에서 추가 2배.
    static KagutsuchiSolarArena active;
    readonly Vector2[] positions = new Vector2[4];
    readonly GameObject[] roots = new GameObject[4];
    readonly SpriteRenderer[] renderers = new SpriteRenderer[4];
    readonly BossWarning[] warnings = new BossWarning[4];
    GameObject danger;
    Material dangerMaterial;
    bool solid, disposed;
    public Vector2[] Positions => positions;

    public KagutsuchiSolarArena()
    {
        Vector2 h = Stage.HalfSize - Vector2.one * CornerInset;
        positions[0] = Stage.Center + new Vector2(-h.x, -h.y);
        positions[1] = Stage.Center + new Vector2(h.x, -h.y);
        positions[2] = Stage.Center + new Vector2(-h.x, h.y);
        positions[3] = Stage.Center + new Vector2(h.x, h.y);
        var sprite = SpriteFactory.TryGet("Kagutsuchi_P2_StonePillar");
        for (int i = 0; i < roots.Length; i++)
        {
            roots[i] = new GameObject("SolarStonePillar");
            var sr = roots[i].AddComponent<SpriteRenderer>();
            renderers[i] = sr;
            sr.sprite = sprite != null ? sprite : SpriteFactory.Get("Fx_Square", SpriteFactory.Shape.Square);
            float width = Radius * 2f * PillarVisualScale, scale = width / Mathf.Max(0.0001f, sr.sprite.bounds.size.x);
            roots[i].transform.localScale = Vector3.one * scale;
            sr.sortingOrder = Fx.DefaultOrder;
            roots[i].SetActive(false);
        }
    }

    public void Warn()
    {
        for (int i = 0; i < positions.Length; i++) warnings[i] = BossWarning.Circle(positions[i], Radius, 0f);
    }

    public void DrawFalling(float height)
    {
        for (int i = 0; i < roots.Length; i++)
        {
            roots[i].SetActive(true);
            var sr = renderers[i];
            float foot = -sr.sprite.bounds.min.y * roots[i].transform.localScale.y;
            roots[i].transform.position = positions[i] + Vector2.up * (height + foot);
        }
    }

    public void Land()
    {
        ClearWarnings();
        DrawFalling(0f);
        solid = true;
        active = this;
    }

    public bool IsSafe(Vector2 point)
    {
        if (!solid) return false;
        foreach (var pillar in positions)
            if (KagutsuchiSolarGeometry.Covered(Stage.Center, point, pillar, Radius)) return true;
        return false;
    }

    public void WarnWholeMap()
    {
        danger = new GameObject("SolarMapDanger");
        var sr = danger.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("Fx_Square", SpriteFactory.Shape.Square);
        sr.color = new Color(1f, 0.2f, 0.15f, 0.32f);
        sr.sortingOrder = Fx.GroundOrder;
        danger.transform.position = Stage.Center;
        danger.transform.localScale = new Vector3(Stage.HalfSize.x * 2f, Stage.HalfSize.y * 2f, 1f);
        var shader = Resources.Load<Shader>("Shaders/SolarDanger");
        if (shader == null) { Debug.LogError("SolarDanger 셰이더를 찾을 수 없습니다."); return; }
        dangerMaterial = new Material(shader);
        dangerMaterial.SetVector("_Origin", new Vector4(Stage.Center.x, Stage.Center.y, 0f, 0f));
        for (int i = 0; i < positions.Length; i++)
            dangerMaterial.SetVector("_Pillar" + i, new Vector4(positions[i].x, positions[i].y, Radius, 0f));
        sr.sharedMaterial = dangerMaterial;
    }

    public static void ConstrainPlayer(Player player, Vector2 previousPosition)
    {
        if (active == null || !active.solid || player == null) return;
        Vector2 offset = player.HurtCenter - (Vector2)player.transform.position;
        Vector2 from = previousPosition + offset, target = player.HurtCenter;
        // 모서리의 기둥은 서로 겹치지 않음. 두 번 투영하여 경계 근처의 동시 접촉 처리.
        for (int pass = 0; pass < 2; pass++)
            foreach (var pillar in active.positions)
            {
                Vector2 resolved = KagutsuchiSolarGeometry.BlockMove(from, target, pillar, Radius + player.radius);
                if ((resolved - target).sqrMagnitude > 0.00000001f) { target = resolved; from = resolved; }
            }
        player.transform.position = target - offset;
    }

    void ClearWarnings()
    {
        for (int i = 0; i < warnings.Length; i++)
            if (warnings[i] != null)
            { warnings[i].gameObject.SetActive(false); UnityEngine.Object.Destroy(warnings[i].gameObject); warnings[i] = null; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        solid = false;
        if (active == this) active = null;
        ClearWarnings();
        foreach (var root in roots) if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
        if (danger != null) { danger.SetActive(false); UnityEngine.Object.Destroy(danger); }
        if (dangerMaterial != null) UnityEngine.Object.Destroy(dangerMaterial);
    }
}

/// <summary>한 패턴 안에서 낙하물을 재사용. 재발사 시 위치·경고·수명·적중 상태 초기화.</summary>
public sealed class KagutsuchiSolarRain : IDisposable
{
    enum DropState { Idle, Falling, Impact }
    sealed class Drop
    {
        public GameObject root;
        public SpriteRenderer visual;
        public BossWarning warning;
        public float age, warnTime, radius;
        public Vector2 target;
        public DropState state;
    }
    const string ImpactSheet = "Kagutsuchi_P2_Fx_FireImpact";
    const int ImpactCell = 512, ImpactCount = 6;
    const float ImpactFps = 12f; // [임시] 기존 제작 manifest의 미리보기 재생 속도
    // 원화 알파 경계 실측. 최대 불꽃 폭을 기존 경고 지름에 맞추고 공통 지면점 유지.
    const float ImpactContentWidth = 390f, ImpactBottomMargin = 40f;
    const float FallTime = 0.25f, Damage = 5f; // [임시] 사용자 지정 피해
    readonly System.Collections.Generic.List<Drop> pool = new System.Collections.Generic.List<Drop>();
    readonly Sprite sprite;
    readonly Sprite[] impactFrames;
    readonly bool ownsSprite;
    bool disposed;
    readonly float startHeight;
    public KagutsuchiSolarRain(float startHeight)
    {
        this.startHeight = startHeight;
        var art = SpriteFactory.TryGet("Kagutsuchi_P2_Fireball");
        // 원화의 화염 핵 중심(450,445px)을 회전축으로 삼아 경고 중앙에 핵이 착지하도록 정렬.
        sprite = art != null ? Sprite.Create(art.texture, art.rect, new Vector2(450f / 1774f, 1f - 445f / 887f),
                                            art.pixelsPerUnit, 0, SpriteMeshType.FullRect)
                             : SpriteFactory.Get("Fx_Circle", SpriteFactory.Shape.Circle);
        ownsSprite = art != null;
        var impactTexture = Resources.Load<Texture2D>("Sprites/" + ImpactSheet);
        if (impactTexture == null || impactTexture.width != ImpactCell * ImpactCount || impactTexture.height != ImpactCell)
        {
            Debug.LogError("일반 화염구 착지 이펙트 시트가 없거나 3072×512 규격과 다릅니다.");
            return;
        }
        impactFrames = new Sprite[ImpactCount];
        for (int i = 0; i < ImpactCount; i++)
            impactFrames[i] = Sprite.Create(impactTexture, new Rect(i * ImpactCell, 0, ImpactCell, ImpactCell),
                                            new Vector2(0.5f, ImpactBottomMargin / ImpactCell), ImpactContentWidth, 0, SpriteMeshType.FullRect);
    }

    public bool HasActive
    {
        get { foreach (var drop in pool) if (drop.state == DropState.Falling) return true; return false; }
    }

    public void Spawn(Vector2 target, float radius, float warnTime)
    {
        if (disposed) return;
        Drop drop = null;
        foreach (var entry in pool) if (entry.state == DropState.Idle) { drop = entry; break; }
        if (drop == null)
        {
            drop = new Drop { root = new GameObject("SolarFallingFireball") };
            drop.visual = drop.root.AddComponent<SpriteRenderer>(); drop.visual.sortingOrder = Fx.DefaultOrder;
            drop.warning = BossWarning.Circle(target, radius, 0f);
            pool.Add(drop);
        }
        else drop.warning.Set(target, radius);
        drop.age = 0f; drop.warnTime = warnTime; drop.radius = radius; drop.target = target; drop.state = DropState.Falling;
        drop.visual.sprite = sprite;
        drop.warning.gameObject.SetActive(true);
        drop.root.SetActive(false);
        float scale = radius * 2f / Mathf.Max(0.0001f, sprite.bounds.size.y);
        drop.root.transform.localScale = Vector3.one * scale;
        drop.root.transform.rotation = Quaternion.Euler(0f, 0f, 90f);
    }

    public void Tick(float dt)
    {
        foreach (var drop in pool)
        {
            if (drop.state == DropState.Idle) continue;
            drop.age += dt;
            if (drop.state == DropState.Impact)
            {
                int frame = Mathf.FloorToInt(drop.age * ImpactFps);
                if (frame >= ImpactCount) { drop.state = DropState.Idle; drop.root.SetActive(false); }
                else drop.visual.sprite = impactFrames[frame];
                continue;
            }
            if (drop.age < drop.warnTime) continue;
            drop.warning.gameObject.SetActive(false);
            drop.root.SetActive(true);
            float t = Mathf.Clamp01((drop.age - drop.warnTime) / FallTime);
            drop.root.transform.position = drop.target + Vector2.up * (startHeight * (1f - t));
            if (t < 1f) continue;
            var player = Player.I;
            if (player != null && (player.HurtCenter - drop.target).sqrMagnitude <= (drop.radius + player.radius) * (drop.radius + player.radius))
                player.TakeDamage(Damage);
            // 착지 후는 표시만 재생. HasActive에서 제외해 다음 패턴 대기 기준은 착지 시점 유지.
            if (impactFrames != null)
            {
                drop.state = DropState.Impact; drop.age = 0f;
                drop.visual.sprite = impactFrames[0];
                drop.root.transform.position = drop.target;
                drop.root.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
                drop.root.transform.localScale = Vector3.one * (drop.radius * 2f);
            }
            else { drop.state = DropState.Idle; drop.root.SetActive(false); }
        }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        foreach (var drop in pool)
        {
            drop.state = DropState.Idle;
            if (drop.root != null) { drop.root.SetActive(false); UnityEngine.Object.Destroy(drop.root); }
            if (drop.warning != null) { drop.warning.gameObject.SetActive(false); UnityEngine.Object.Destroy(drop.warning.gameObject); }
        }
        pool.Clear();
        if (ownsSprite && sprite != null) UnityEngine.Object.Destroy(sprite);
        if (impactFrames != null)
            foreach (var frame in impactFrames) if (frame != null) UnityEngine.Object.Destroy(frame);
    }
}
