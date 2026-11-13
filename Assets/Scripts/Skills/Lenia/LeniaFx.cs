using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레니아 스킬 공용 그림 · 이펙트 · 투사체 (2026-11-11 레니아 스킬 구성).
/// 리소스: Resources/Sprites/Lenia_* (탄환 · 미사일 · 스패너 · 망치 · 포탑 3종 · 폭발 · 추진 연기 3종 · 전기 이펙트 4종)
/// 화면 크기 [임시] 값은 이 클래스 맨 위 상수 — 레니아 화면 키 약 1.62 유닛 기준
/// </summary>
public static class LeniaFx
{
    // ---------- [임시] 화면 크기 (월드 유닛) ----------
    public const float SpannerLength = 0.55f;       // 스패너 세로 길이
    public const float HammerLength = 0.6f;
    public const float BigHammerLength = 2.2f;      // 지면 강타 · 터렛 과부화 연출용 큰 망치
    public const float BulletLength = 0.28f;
    public const float MissileLength = 0.5f;
    public const float SmokeSize = 0.55f;           // 추진 연기 한 칸 가로
    public const float ImpactSize = 1.1f;           // 전기 적중 스파크 한 칸 가로
    public const float LightningWidth = 0.42f;      // 번개 줄기 두께 (띠 텍스처 세로 128px 전체)
    public const float LightningTile = 1.7f;        // 번개 띠 1장이 덮는 길이

    // ---------- 단일 그림 (가로 = 오른쪽 방향 / 세로 = 위가 머리) ----------
    static readonly Dictionary<string, Sprite> single = new Dictionary<string, Sprite>();

    /// <summary>
    /// 그림 1장. visiblePx = 원본에서 그림이 차지하는 길이(측정값), worldSize = 화면 길이, pivot = 칸 안 기준점.
    /// (텍스처를 읽을 수 없게 임포트되므로 크기 · 기준점은 미리 잰 값을 씀)
    /// </summary>
    public static Sprite Single(string name, float visiblePx, float worldSize, Vector2 pivot)
    {
        string key = $"{name}|{worldSize}|{pivot.x}|{pivot.y}";
        if (single.TryGetValue(key, out var s) && s != null) return s;
        var tex = Resources.Load<Texture2D>("Sprites/" + name);
        if (tex == null) return null;
        s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), pivot, visiblePx / worldSize, 0, SpriteMeshType.FullRect);
        single[key] = s;
        return s;
    }

    // 측정값: 그림 길이(px) · 기준점. 탄환 · 미사일은 오른쪽을 향한 가로 그림 (여백 8px 대칭 → 가운데)
    public static Sprite Bullet => Single("Lenia_TurretBullet", 294f, BulletLength, new Vector2(0.5f, 0.5f));
    public static Sprite Missile => Single("Lenia_Missile", 403f, MissileLength, new Vector2(0.5f, 0.5f));
    /// <summary>스패너 · 망치: 세로 그림 (머리가 위). grip = 손잡이 끝 기준 (휘두르기 · 내려치기), 아니면 가운데 (던지기 회전)</summary>
    public static Sprite Spanner(float length, bool grip) => Single("Lenia_Spanner", 352f, length, new Vector2(0.503f, grip ? SpannerGripY : 0.501f));
    public static Sprite Hammer(float length, bool grip) => Single("Lenia_Hammer", 329f, length, new Vector2(0.501f, grip ? HammerGripY : 0.493f));

    /// <summary>기어 (터렛 강화 표시 등): 그림 349px, 정가운데 → 제자리 회전</summary>
    public static Sprite Gear(float size) => Single("Lenia_Gear", 349f, size, new Vector2(0.5f, 0.5f));
    public const float BoostGearSize = 0.38f;       // [임시] 강화된 터렛 우측 상단 기어 크기
    public const float BoostGearSpin = 140f;        // [임시] 회전 속도 (도/초)

    /// <summary>회전 기준점: 손잡이 끝 (휘두르기 · 내려치기). 스패너 0.13 · 망치 0.15 (측정값)</summary>
    public const float SpannerGripY = 0.13f, HammerGripY = 0.15f;

    // ---------- 포탑 시트 (발 기준 피벗) ----------
    /// <summary>포탑 시트 1종: 방향(아래·왼쪽·오른쪽·위) × 프레임. 칸 안 보이는 키 · 발 아래 여백(px)으로 크기 · 피벗 계산</summary>
    public class TurretSheet
    {
        public string name;
        public bool directionsAsColumns;   // true = 4열 1행 (정지 이미지), false = 4행 × N열 (동작)
        public int frames;
        public float visiblePx, footPx;
        Sprite[,] cache;

        public Sprite[,] Load(float worldHeight)
        {
            if (cache != null && cache[0, 0] != null) return cache;
            var tex = Resources.Load<Texture2D>("Sprites/" + name);
            if (tex == null) return null;
            int cols = directionsAsColumns ? 4 : frames, rows = directionsAsColumns ? 1 : 4;
            float cw = tex.width / (float)cols, ch = tex.height / (float)rows;
            float ppu = visiblePx / worldHeight;
            var pivot = new Vector2(0.5f, footPx / ch);
            cache = new Sprite[4, directionsAsColumns ? 1 : frames];
            for (int d = 0; d < 4; d++)
                for (int f = 0; f < cache.GetLength(1); f++)
                {
                    int c = directionsAsColumns ? d : f, r = directionsAsColumns ? 0 : d;
                    var rect = new Rect(c * cw, (rows - 1 - r) * ch, cw, ch);
                    cache[d, f] = Sprite.Create(tex, rect, pivot, ppu, 0, SpriteMeshType.FullRect);
                }
            return cache;
        }
    }

    // 측정값 (원본 시트 기준): 보이는 키 · 발 아래 여백
    public static readonly TurretSheet StandardTurret = new TurretSheet { name = "Lenia_TurretStandard", frames = 8, visiblePx = 345f, footPx = 46f };
    public static readonly TurretSheet MissileTurret = new TurretSheet { name = "Lenia_MissileTurret", directionsAsColumns = true, frames = 1, visiblePx = 485f, footPx = 134f };
    public static readonly TurretSheet TeslaTower = new TurretSheet { name = "Lenia_TeslaTower", directionsAsColumns = true, frames = 1, visiblePx = 508f, footPx = 135f };
    public static readonly TurretSheet TeslaRing = new TurretSheet { name = "Lenia_TeslaRingAttack", frames = 8, visiblePx = 388f, footPx = 34f };

    /// <summary>방향 → 시트 방향 번호 (아래 0 · 왼쪽 1 · 오른쪽 2 · 위 3)</summary>
    public static int Dir4(Vector2 d)
    {
        if (d.sqrMagnitude < 0.0001f) return 0;
        if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) return d.x < 0 ? 1 : 2;
        return d.y < 0 ? 0 : 3;
    }

    // ---------- 시트 이펙트 ----------
    public static Sprite[] Explosion => SpriteEffect.LoadFrames("Lenia_MissileExplosion", 2, 4, 1f);
    public static Sprite[] Impact => SpriteEffect.LoadFrames("Lenia_ElectricImpact", 2, 4, 1f);
    public static Sprite[] TeslaCharge => SpriteEffect.LoadFrames("Lenia_TeslaCharge", 2, 4, 1f);
    public static Sprite[] Shocked => SpriteEffect.LoadFrames("Lenia_ShockedStatus", 2, 4, 1f);
    static readonly string[] SmokeSheets = { "Lenia_ExhaustSmoke_A", "Lenia_ExhaustSmoke_B", "Lenia_ExhaustSmoke_C" };
    public static Sprite[] RandomSmoke => SpriteEffect.LoadFrames(SmokeSheets[UnityEngine.Random.Range(0, SmokeSheets.Length)], 2, 4, 1f);

    /// <summary>미사일 폭발: 범위 반경 r 에 맞춰 (폭발 최대 크기 ≈ 칸의 85%)</summary>
    public static void Explode(Vector2 at, float r)
    {
        var f = Explosion;
        if (f != null) SpriteEffect.Spawn(f, 12.5f, null, at, 0f, false, 12, r * 2f / 0.85f);
        else Fx.Disc(at, r, Fx.FireBright, 0.3f);
        HitDebug.Circle(at, r, HitDebug.BlastColor);
    }

    /// <summary>
    /// 낙뢰 착탄: 피격 범위(반경 r)가 그대로 보이도록 전기 충격 시트를 범위 크기로 키우고,
    /// 범위 경계 원 + 옅은 채움 + 경계까지 뻗는 작은 방전을 함께 표시
    /// </summary>
    public static void LightningStrike(Vector2 at, float r)
    {
        var col = new Color(0.55f, 0.85f, 1f, 1f);
        Fx.Disc(at, r, new Color(col.r, col.g, col.b, 0.22f), 0.3f, 0f);
        Fx.Circle(at, r, new Color(col.r, col.g, col.b, 0.85f), 0.35f, null, 0f);
        var f = Impact;
        if (f != null) SpriteEffect.Spawn(f, 12.5f, null, at, UnityEngine.Random.Range(0f, 360f), false, 12, r * 2f / 0.85f);
        const int arcs = 5;   // [임시] 경계까지 뻗는 방전 수
        float a0 = UnityEngine.Random.Range(0f, 360f);
        for (int i = 0; i < arcs; i++)
        {
            float a = (a0 + 360f / arcs * i + UnityEngine.Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
            Bolt(at, at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 0.2f);
        }
    }

    // ---------- 지면 · 터렛 연출 시트 (2행 × 4열, 칸 443px, 2026-11-12) ----------
    public static Sprite[] GroundShockwave => SpriteEffect.LoadFrames("Lenia_GroundShockwave", 2, 4, 1f);
    public static Sprite[] OverloadCast => SpriteEffect.LoadFrames("Lenia_OverloadCast", 2, 4, 1f);
    public static Sprite[] BoostCast => SpriteEffect.LoadFrames("Lenia_BoostCast", 2, 4, 1f);
    public static Sprite[] TurretDeploy => SpriteEffect.LoadFrames("Lenia_TurretDeploy", 2, 4, 1f);
    public static Sprite[] TurretDespawn => SpriteEffect.LoadFrames("Lenia_TurretDespawn", 2, 4, 1f);
    public const float TurretFxSize = 2.2f;         // [임시] 터렛 설치 · 소멸 한 칸 (먼지 최대 ≈ 칸의 70% ≈ 1.55유닛)
    public const float OverloadCastRadius = 1.2f;   // [임시] 터렛 과부화 시전 고리 반경
    public const float BoostCastRadius = 1.2f;      // [임시] 터렛 강화 시전 고리 반경
    public const float FootOffset = 0.64f;          // 플레이어 transform 은 발 위 0.64유닛 (Player.CharacterVisual 참고)

    /// <summary>지면 충격파: 반경 r 범위에 맞춰 (고리 최대 ≈ 칸의 90%). 시트가 없으면 false → 호출 쪽 도형 연출</summary>
    public static bool Shockwave(Vector2 at, float r)
    {
        var f = GroundShockwave;
        if (f == null) return false;
        SpriteEffect.Spawn(f, 12.5f, null, at, UnityEngine.Random.Range(0f, 360f), false, 3, r * 2f / 0.9f);   // 적(5) 아래 지면
        return true;
    }

    /// <summary>
    /// 시전 고리: 플레이어 발밑에서 따라다니며 반경 r 까지 퍼짐. extent = 고리 최대 크기 / 칸 (과부화 0.86 · 강화 0.89).
    /// 시트가 없으면 false
    /// </summary>
    public static bool CastRing(Sprite[] frames, Transform owner, float r, float fps = 12.5f, float extent = 0.86f)
    {
        if (frames == null || owner == null) return false;
        SpriteEffect.Spawn(frames, fps, owner, Vector2.down * FootOffset, 0f, false, 4, r * 2f / extent);
        return true;
    }

    /// <summary>
    /// 포격 낙하 지점 표시: 반경 r 원 (그림의 원 지름 497px / 512), seconds 동안 깜빡이다 사라짐.
    /// 그림이 없으면 기존 붉은 원
    /// </summary>
    public static void TargetMarker(Vector2 at, float r, float seconds)
    {
        var s = Single("Lenia_TargetMarker", 497f, r * 2f, new Vector2(0.5f, 0.5f));
        if (s == null) { Fx.Circle(at, r, new Color(1f, 0.3f, 0.2f, 0.5f), seconds); return; }
        var go = new GameObject("TargetMarker");
        go.transform.position = at;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = s;
        sr.sortingOrder = 2;   // 지면 바로 위, 적 · 보석 아래
        go.AddComponent<MarkerFx>().Init(sr, seconds);
    }

    public static void Spark(Vector2 at) => SpriteEffect.Spawn(Impact, 12.5f, null, at, UnityEngine.Random.Range(0f, 360f), false, 12, ImpactSize);

    /// <summary>감전 표시: 대상 몸에 seconds 동안 반복 (적 반경에 맞춤, 가운데 비어 있음)</summary>
    public static void ShowShocked(Enemy e, float seconds)
    {
        if (e == null || Shocked == null) return;
        // 가운데 빈 원(반지름 65px / 443) ≈ 적 반경의 0.8배 → 칸 크기 ≈ 적 반경 × 5.4
        LoopFx.Attach(e.transform, Shocked, 10f, Vector2.zero, Mathf.Max(0.8f, e.radius * 5.4f), seconds, 12);
    }

    // ---------- 적 찾기 ----------
    public static Enemy Nearest(Vector2 from, float maxDist, ICollection<Enemy> exclude = null)
    {
        Enemy best = null;
        float bestSq = maxDist * maxDist;
        foreach (var e in Enemy.All)
        {
            if (e == null || e.hp <= 0 || e.IsWall || e.Hidden || e.Scripted || (exclude != null && exclude.Contains(e))) continue;   // 포위 고블린은 조준하지 않음
            float sq = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        return best;
    }

    /// <summary>위로 쏘아 올리는 연출 (궤도 폭격 미사일 · 위로 방출하는 전류). 피해 없음</summary>
    public static void LaunchUp(Vector2 from, Sprite sprite, float seconds = 0.35f, float height = 4f)
    {
        var go = new GameObject("LaunchUp");
        go.transform.position = from;
        go.transform.rotation = Quaternion.Euler(0, 0, 90f);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 13;
        go.AddComponent<LaunchUpFx>().Init(height / seconds, seconds);
    }

    // ---------- 번개 줄기 (띠 텍스처를 길이에 맞춰 반복) ----------
    static Material[] boltMats;

    static Material BoltMaterial()
    {
        if (boltMats == null || boltMats[0] == null)
        {
            boltMats = new Material[4];
            var shader = Shader.Find("Sprites/Default");
            for (int i = 0; i < 4; i++)
            {
                var tex = Resources.Load<Texture2D>($"Sprites/Lenia_Lightning_0{i + 1}");
                if (tex != null) tex.wrapModeU = TextureWrapMode.Repeat;   // 가로 반복 (프로젝트 기본은 Clamp)
                boltMats[i] = new Material(shader) { mainTexture = tex };
            }
        }
        return boltMats[UnityEngine.Random.Range(0, 4)];
    }

    /// <summary>a → b 번개 줄기를 seconds 동안 (띠 4종 중 무작위, 길이에 맞춰 반복)</summary>
    public static void Bolt(Vector2 a, Vector2 b, float seconds = 0.18f)
    {
        var go = new GameObject("LeniaBolt");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.SetPosition(0, a);
        lr.SetPosition(1, b);
        lr.widthMultiplier = LightningWidth;
        lr.textureMode = LineTextureMode.Tile;
        lr.textureScale = new Vector2(1f / LightningTile, 1f);
        lr.sharedMaterial = BoltMaterial();
        lr.sortingOrder = 13;
        lr.numCapVertices = 0;
        UnityEngine.Object.Destroy(go, seconds);
    }
}

/// <summary>LeniaFx.LaunchUp: 위로 날아가며 사라짐</summary>
public class LaunchUpFx : MonoBehaviour
{
    float speed, life, t;
    SpriteRenderer sr;

    public void Init(float speed, float life) { this.speed = speed; this.life = life; sr = GetComponent<SpriteRenderer>(); }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= life) { Destroy(gameObject); return; }
        transform.position += Vector3.up * (speed * Time.deltaTime);
        if (sr != null) { var c = sr.color; c.a = 1f - t / life; sr.color = c; }
    }
}

/// <summary>대상에 붙어 반복 재생되는 시트 이펙트 (감전 표시 · 테슬라 충전 · 강화 연출 등)</summary>
public class LoopFx : MonoBehaviour
{
    Sprite[] frames;
    float fps, t, life;
    SpriteRenderer sr;

    public static LoopFx Attach(Transform parent, Sprite[] frames, float fps, Vector2 localPos, float size, float seconds, int order)
    {
        if (frames == null || frames.Length == 0 || parent == null) return null;
        var go = new GameObject("LoopFx");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        // 부모 크기와 무관하게 월드 크기 = size
        float ps = Mathf.Max(0.0001f, parent.lossyScale.x);
        go.transform.localScale = Vector3.one * (size / ps);
        var fx = go.AddComponent<LoopFx>();
        fx.frames = frames; fx.fps = fps; fx.life = seconds;
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = frames[0];
        fx.sr.sortingOrder = order;
        return fx;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (life > 0 && t >= life) { Destroy(gameObject); return; }
        sr.sprite = frames[(int)(t * fps) % frames.Length];
    }
}

// 스패너 · 망치 · 총알 = LeniaProjectile (LeniaShots.cs — 관통 · 도탄 · 부메랑)

/// <summary>
/// 미사일: 목표 지점으로 날아가 범위 폭발. 꼬리(노즐)에서 추진 연기를 일정 간격으로 남김.
/// 비행 중 목표 적이 살아 있으면 그 위치를 따라감 (약한 유도). 다른 적에 닿아도 그 자리에서 폭발
/// </summary>
public class MissileProjectile : MonoBehaviour
{
    Vector2 target;
    Enemy follow;
    float speed, radius, smokeTimer, maxLife;
    DamageInfo dmg;
    bool fromAbove, noBlast;
    Action<Enemy> onHit;
    Action<Vector2> onLand;
    Vector2 followOffset;

    const float SmokeInterval = 0.05f;   // [임시] 연기 간격

    /// <param name="follow">살아 있으면 그 위치를 따라감 (위에서 떨어지는 미사일은 처음 잡은 높이 차이를 유지한 채 따라감 — 호밍 미사일)</param>
    /// <param name="onHit">폭발에 맞은 적마다 피해 뒤 호출 (상태이상 부여 — 철갑탄 기절 등)</param>
    /// <param name="onLand">도착(폭발) 지점에서 호출 (자기장 · 작열 지대 · 집속탄 분리)</param>
    /// <param name="noBlast">폭발 · 피해 없이 도착만 (집속탄 분리 전 운반체)</param>
    public static void Spawn(Vector2 from, Enemy follow, Vector2 target, float speed, float radius, DamageInfo dmg, bool fromAbove = false,
                             Action<Enemy> onHit = null, Action<Vector2> onLand = null, bool noBlast = false)
    {
        var go = new GameObject("Missile");
        go.transform.position = from;
        var m = go.AddComponent<MissileProjectile>();
        m.follow = follow; m.target = target; m.speed = speed; m.radius = radius; m.dmg = dmg; m.fromAbove = fromAbove; m.onHit = onHit;
        m.onLand = onLand; m.noBlast = noBlast;
        if (follow != null) m.followOffset = target - (Vector2)follow.transform.position;
        m.maxLife = 4f;
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = LeniaFx.Missile;
        sr.sortingOrder = fromAbove ? 13 : 11;
        m.Face();
    }

    void Face()
    {
        Vector2 d = target - (Vector2)transform.position;
        if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        maxLife -= Time.deltaTime;
        if (follow != null && follow.hp > 0) target = (Vector2)follow.transform.position + (fromAbove ? followOffset : Vector2.zero);
        Vector2 pos = transform.position;
        Vector2 to = target - pos;
        float step = speed * Time.deltaTime;
        Face();

        // 추진 연기: 노즐(그림 왼쪽 끝 = 진행 반대 방향)에서
        smokeTimer -= Time.deltaTime;
        if (smokeTimer <= 0f)
        {
            smokeTimer = SmokeInterval;
            Vector2 back = -(Vector2)transform.right * (LeniaFx.MissileLength * 0.5f);
            SpriteEffect.Spawn(LeniaFx.RandomSmoke, 10f, null, pos + back, UnityEngine.Random.Range(0f, 360f), false, 10,
                               LeniaFx.SmokeSize * UnityEngine.Random.Range(0.8f, 1.2f));
        }

        bool arrive = to.magnitude <= step;
        if (!arrive && !fromAbove)
            foreach (var e in Enemy.All)
            {
                if (e == null || e.hp <= 0) continue;
                float rr = 0.15f + e.radius;
                if (((Vector2)e.transform.position - pos).sqrMagnitude < rr * rr) { arrive = true; break; }
            }
        if (arrive || maxLife <= 0f)
        {
            Vector2 at = arrive && to.magnitude <= step ? target : pos;
            if (!noBlast)
            {
                LeniaFx.Explode(at, radius);
                SkillHits.Circle(at, radius, dmg, 1.5f, onHit);
                CameraFollow.Shake(0.05f, 0.06f);
            }
            onLand?.Invoke(at);
            Destroy(gameObject);
            return;
        }
        transform.position = pos + to.normalized * step;
    }
}


/// <summary>LeniaFx.TargetMarker: 나타나며 살짝 줄어들고, 깜빡임이 점점 빨라지다 사라짐</summary>
public class MarkerFx : MonoBehaviour
{
    SpriteRenderer sr;
    float life, t;

    public void Init(SpriteRenderer r, float seconds) { sr = r; life = Mathf.Max(0.05f, seconds); }

    void Update()
    {
        t += Time.deltaTime;
        if (t >= life) { Destroy(gameObject); return; }
        float k = t / life;
        transform.localScale = Vector3.one * Mathf.Lerp(1.15f, 1f, Mathf.Clamp01(t / 0.12f));
        float blink = 0.75f + 0.25f * Mathf.Cos(t * Mathf.Lerp(10f, 30f, k));   // [임시] 떨어질수록 빠르게
        var c = sr.color; c.a = Mathf.Clamp01(t / 0.08f) * blink; sr.color = c;
    }
}
