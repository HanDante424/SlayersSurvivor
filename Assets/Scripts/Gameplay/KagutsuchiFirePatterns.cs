using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 화염구 투사체 관리 (공격 8 · 9 공용). 소유자 (Phase2) 가 매 프레임 Tick 해서 패턴이 끝난 뒤에도 날아가는 화염구가 이어진다.
/// 맵을 꿰뚫고 (벽에 부딪혀 사라지지 않음) 맵 밖으로 나가면 사라짐. 피해는 겹치는 순간 한 번 (피격 후에도 계속 날아감).
/// </summary>
public sealed class KagutsuchiFireVolley : IDisposable
{
    const float Damage = 5f;   // [임시] 기획서에 화염구 피해 수치 없음 — 공격 10 의 임시 피해 5 를 따름
    const int Order = Fx.DefaultOrder;
    sealed class Shot
    {
        public GameObject Go;
        public SpriteRenderer Sr;
        public Vector2 Pos, Vel;
        public float Radius, Growth, Life;
        public bool Hit, VisualOnly;
        public KagutsuchiSunFireAnim Anim;
        public float CellScale = 1f;
    }
    readonly List<Shot> shots = new List<Shot>();
    readonly Sprite fireball;
    readonly bool ownsSprite;
    bool disposed;

    public int ActiveCount => shots.Count;

    public KagutsuchiFireVolley()
    {
        var art = SpriteFactory.TryGet("Kagutsuchi_P2_Fireball");
        if (art != null)
        {
            // 일반 화염구와 같은 원화 · 화염 핵 중심 (450,445px) 을 기준점으로. 원화는 머리가 왼쪽 (회전 0 일 때 왼쪽으로 날아가는 모양).
            fireball = Sprite.Create(art.texture, art.rect, new Vector2(450f / 1774f, 1f - 445f / 887f), art.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            ownsSprite = true;
        }
        else fireball = SpriteFactory.Get("Fx_Circle", SpriteFactory.Shape.Circle);
    }

    /// <summary>일반 화염구 하나 발사 (visualOnly = 판정 없음, life 초 뒤 사라지는 연출용)</summary>
    public void Fire(Vector2 pos, Vector2 dir, float speed, float radius, bool visualOnly = false, float life = 0f)
    {
        if (disposed) return;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
        var go = new GameObject(visualOnly ? "KagutsuchiFireSpew" : "KagutsuchiFireball");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = fireball; sr.sortingOrder = Order;
        var shot = new Shot { Go = go, Sr = sr, Pos = pos, Vel = dir * speed, Radius = radius, VisualOnly = visualOnly, Life = life };
        go.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 180f);
        Apply(shot);
        shots.Add(shot);
    }

    /// <summary>천천히 날아가며 점점 커지는 초거대 화염구 (태양 화염 애니메이션). growth = 초당 반지름 증가</summary>
    public void FireGiant(Vector2 pos, Vector2 dir, float speed, float startRadius, float growth)
    {
        if (disposed) return;
        dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.down;
        var go = new GameObject("KagutsuchiGiantFireball");
        var sr = go.AddComponent<SpriteRenderer>();
        var anim = new KagutsuchiSunFireAnim();
        sr.sprite = anim.Valid ? anim.First : SpriteFactory.Get("Fx_Circle", SpriteFactory.Shape.Circle);
        sr.sortingOrder = Order - 1;
        var shot = new Shot { Go = go, Sr = sr, Pos = pos, Vel = dir * speed, Radius = startRadius, Growth = growth, Anim = anim,
                              CellScale = anim.Valid ? KagutsuchiSunFireAnim.CellScale : 1f };
        Apply(shot);
        shots.Add(shot);
    }

    void Apply(Shot shot)
    {
        shot.Go.transform.position = shot.Pos;
        float size = shot.Anim != null ? shot.Sr.sprite.bounds.size.x : shot.Sr.sprite.bounds.size.y;
        shot.Go.transform.localScale = Vector3.one * (shot.Radius * 2f * shot.CellScale / Mathf.Max(0.0001f, size));
    }

    public void Tick(float dt)
    {
        if (disposed || shots.Count == 0 || dt <= 0f) return;
        var player = Player.I;
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var shot = shots[i];
            shot.Pos += shot.Vel * dt;
            shot.Radius += shot.Growth * dt;
            shot.Anim?.Tick(dt, shot.Sr);   // 회전은 SunFireAnim 이 transform 회전을 덮어씀 → 위치 · 크기만 갱신
            Apply(shot);
            bool remove = false;
            if (shot.VisualOnly)
            {
                shot.Life -= dt;
                remove = shot.Life <= 0f;
            }
            else
            {
                if (!shot.Hit && player != null && player.hp > 0f
                    && (player.HurtCenter - shot.Pos).sqrMagnitude <= (shot.Radius + player.radius) * (shot.Radius + player.radius))
                {
                    shot.Hit = true;
                    player.TakeDamage(Damage);
                }
                Vector2 d = shot.Pos - Stage.Center;
                bool outside = Mathf.Abs(d.x) - shot.Radius > Stage.HalfSize.x || Mathf.Abs(d.y) - shot.Radius > Stage.HalfSize.y;
                remove = outside && Vector2.Dot(shot.Vel, d) > 0f;   // 맵 밖으로 나가는 중일 때만 (맵 안에서 시작해도 안전)
            }
            if (remove) { Destroy(shot); shots.RemoveAt(i); }
        }
    }

    static void Destroy(Shot shot)
    {
        shot.Anim?.Dispose();
        if (shot.Go != null) { shot.Go.SetActive(false); UnityEngine.Object.Destroy(shot.Go); }
    }

    public void Clear()
    {
        foreach (var shot in shots) Destroy(shot);
        shots.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        Clear();
        disposed = true;
        if (ownsSprite && fireball != null) UnityEngine.Object.Destroy(fireball);
    }
}

/// <summary>공격 8 · 9 · 10 공통: 시작 · 틱 · 중단, 포즈 표시, 강화 (체력 50% 이하) 판정</summary>
public abstract class KagutsuchiFirePattern : MonoBehaviour
{
    protected Enemy body;
    protected SpriteRenderer bodyVisual;
    protected Player player;
    protected Sprite grabReady, grabDash, stomp, uppercut;
    protected Action<Sprite, float> showPose;
    protected Action stopPose;
    protected KagutsuchiFireVolley volley;
    protected Func<bool> isEnhanced;
    protected bool enhanced, active;
    protected float elapsed;
    public bool Finished => !active;

    public void Initialize(Enemy owner, Sprite grabReady, Sprite grabDash, Sprite stomp, Sprite uppercut,
                           Action<Sprite, float> showPose, Action stopPose, KagutsuchiFireVolley volley, Func<bool> isEnhanced)
    {
        body = owner; bodyVisual = owner.GetComponent<SpriteRenderer>();
        this.grabReady = grabReady; this.grabDash = grabDash; this.stomp = stomp; this.uppercut = uppercut;
        this.showPose = showPose; this.stopPose = stopPose; this.volley = volley; this.isEnhanced = isEnhanced;
    }

    public void Begin()
    {
        Cancel();
        player = Player.I;
        if (player == null || body == null) return;
        enhanced = isEnhanced != null && isEnhanced();
        active = true; elapsed = 0f;
        OnBegin();
    }

    public void Tick(float dt)
    {
        if (!active || dt <= 0f) return;
        if (body == null || body.hp <= 0f || player == null || player.hp <= 0f) { Cancel(); return; }
        elapsed += dt;
        OnTick(dt);
    }

    public void Cancel()
    {
        bool was = active;
        active = false;
        OnCancel();
        if (was) stopPose?.Invoke();
    }

    void Update()
    {
        if (!active || GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) Cancel();
    }

    void OnDisable() => Cancel();

    protected abstract void OnBegin();
    protected abstract void OnTick(float dt);
    protected virtual void OnCancel() { }

    protected Vector2 Origin => body.transform.position;
    protected Vector2 ToPlayer()
    {
        Vector2 aim = player.HurtCenter - Origin;
        return aim.sqrMagnitude > 1e-6f ? aim.normalized : Vector2.down;
    }
    protected int facingSign = 1;   // 보스가 바라보는 가로 방향 (오른쪽 = 1)
    protected void Face(Vector2 dir) { bodyVisual.flipX = dir.x > 0f; facingSign = dir.x > 0f ? 1 : -1; }
    /// <summary>손 위치: 보스 몸 옆 (바라보는 쪽) 으로 2 (사용자 2026-11-16: 공격 8 · 10 시작 위치 옆으로 2)</summary>
    protected Vector2 Hand => Origin + new Vector2(facingSign * HandOffset, 0f);
    protected const float HandOffset = 2f;
    protected static void Kill(BossWarning w)
    {
        if (w != null) { w.gameObject.SetActive(false); Destroy(w.gameObject); }
    }
}

/// <summary>
/// 공격 8 · 화염구 연발 (사용자 2026-11-16, 특수 패턴 1 이후 해금).
/// 플레이어 방향 160도 부채꼴 조준 → 0.5초 워닝 → 맵을 꿰뚫는 화염구 (반지름 0.5, 속도 플레이어의 3배) 를 0.1초 간격으로 총 2초.
/// 체력 50% 이하: 사용 후 1초 뒤 천천히 (플레이어 속도의 0.5배) 날아가며 맵을 나갈 때까지 점점 커지는 초거대 화염구.
/// [임시] 발사 방향은 부채꼴 안 무작위, 초거대 화염구의 시작 반지름 · 성장 속도, 화염구 피해 5, 포즈 (GrabReady → GrabDash).
/// </summary>
public sealed class KagutsuchiFireballBarrage : KagutsuchiFirePattern
{
    public const float Cooldown = 12f;   // [임시]
    const float WarnTime = 0.5f, FanAngle = 160f, ShotInterval = 0.1f, TotalTime = 2f;
    const float SpeedMul = 3f, ShotRadius = 0.5f;
    const float GiantDelay = 1f, GiantSpeedMul = 0.5f, GiantStartRadius = 1.5f, GiantGrowth = 1f;
    enum Step { Warn, Fire, Wait }
    Step step;
    Vector2 fanDirection;
    float shotSpeed;
    int emitted;
    BossWarning warning;

    protected override void OnBegin()
    {
        fanDirection = ToPlayer();
        shotSpeed = Mathf.Max(1f, player.moveSpeed) * SpeedMul;
        Face(fanDirection);
        showPose(grabReady, 0f);
        warning = BossWarning.Sector(Hand, fanDirection, Stage.HalfSize.magnitude * 2f, FanAngle, 0f);
        step = Step.Warn;
    }

    protected override void OnTick(float dt)
    {
        switch (step)
        {
            case Step.Warn:
                if (elapsed >= WarnTime)
                {
                    Kill(warning); warning = null;
                    showPose(grabDash, 0f);
                    step = Step.Fire; elapsed = 0f; emitted = 0;
                }
                break;
            case Step.Fire:
                int total = Mathf.RoundToInt(TotalTime / ShotInterval);
                while (emitted < total && elapsed >= emitted * ShotInterval)
                {
                    float angle = UnityEngine.Random.Range(-FanAngle * 0.5f, FanAngle * 0.5f);
                    Vector2 dir = Quaternion.Euler(0f, 0f, angle) * fanDirection;
                    volley.Fire(Hand, dir, shotSpeed, ShotRadius);
                    emitted++;
                }
                if (elapsed >= TotalTime)
                {
                    if (enhanced) { step = Step.Wait; elapsed = 0f; }
                    else Cancel();
                }
                break;
            case Step.Wait:
                if (elapsed >= GiantDelay)
                {
                    Vector2 aim = ToPlayer();
                    Face(aim);
                    volley.FireGiant(Hand, aim, Mathf.Max(0.5f, player.moveSpeed) * GiantSpeedMul, GiantStartRadius, GiantGrowth);
                    Cancel();
                }
                break;
        }
    }

    protected override void OnCancel()
    {
        Kill(warning); warning = null;
    }
}

/// <summary>
/// 공격 9 · 화염구 쏟아 내리기 (사용자 2026-11-16, 특수 패턴 1 이후 해금).
/// 1.5초 StompAttack 준비 → Uppercut 으로 전환해 패턴 끝까지 유지 → 1초 동안 화염구 50개를 위로 쏟아냄 → 맵 전체 무작위 위치에 반지름 2 로 0.05초 간격 낙하.
/// 체력 50% 이하: 2초 동안 100개. [임시] 낙하 경고 0.5초, 낙하 개수 = 쏟아낸 개수, 낙하 피해 5 (일반 화염구 낙하와 같음), 쿨타임.
/// </summary>
public sealed class KagutsuchiFireRainPattern : KagutsuchiFirePattern
{
    public const float Cooldown = 15f;   // [임시]
    const float PrepareTime = 1.5f, FallInterval = 0.05f, FallRadius = 2f, FallWarning = 0.5f;
    const float SpewStartForward = 1f;   // 보스가 보는 방향으로 1 (사용자 2026-11-16)
    const float SpewStartHeight = 3f;  // 쏟아내는 화염구 시작 높이: 이전 1 에서 2 위로 (사용자 2026-11-16)
    const float SpewSpeed = 18f, SpewSpread = 25f, SpewLife = 1.2f, SpewRadius = 0.4f;   // [임시] 위로 쏟아내는 연출
    const float SettleTime = 0.6f;
    enum Step { Prepare, Spew, Fall, Settle }
    Step step;
    int count, emitted;
    float spewTime;
    KagutsuchiSolarRain rain;

    protected override void OnBegin()
    {
        count = enhanced ? 100 : 50;
        spewTime = enhanced ? 2f : 1f;
        Face(ToPlayer());
        showPose(stomp, 0f);
        rain = new KagutsuchiSolarRain(Stage.HalfSize.y * 2f + 12f);
        step = Step.Prepare;
    }

    protected override void OnTick(float dt)
    {
        rain?.Tick(dt);
        switch (step)
        {
            case Step.Prepare:
                if (elapsed >= PrepareTime)
                {
                    showPose(uppercut, 0f);   // 패턴이 끝날 때까지 유지
                    step = Step.Spew; elapsed = 0f; emitted = 0;
                }
                break;
            case Step.Spew:
                while (emitted < count && elapsed >= emitted * (spewTime / count))
                {
                    float angle = UnityEngine.Random.Range(-SpewSpread, SpewSpread);
                    Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                    volley.Fire(Origin + new Vector2(facingSign * SpewStartForward, SpewStartHeight), dir, SpewSpeed * UnityEngine.Random.Range(0.7f, 1.1f), SpewRadius, true, SpewLife);
                    emitted++;
                }
                if (elapsed >= spewTime) { step = Step.Fall; elapsed = 0f; emitted = 0; }
                break;
            case Step.Fall:
                while (emitted < count && elapsed >= emitted * FallInterval)
                {
                    Vector2 half = Stage.HalfSize - Vector2.one * FallRadius;
                    Vector2 target = Stage.Center + new Vector2(UnityEngine.Random.Range(-half.x, half.x), UnityEngine.Random.Range(-half.y, half.y));
                    rain.Spawn(target, FallRadius, FallWarning);
                    emitted++;
                }
                if (emitted >= count && !rain.HasActive) { step = Step.Settle; elapsed = 0f; }
                break;
            case Step.Settle:
                if (elapsed >= SettleTime) Cancel();   // 착지 폭발 연출이 끝나도록 잠시 유지
                break;
        }
    }

    protected override void OnCancel()
    {
        rain?.Dispose(); rain = null;
    }
}

/// <summary>
/// 공격 10 · 부채꼴 반사 레이저 (사용자 2026-11-16, 특수 패턴 2 이후 해금). 쿨타임 10초, 피해 [임시] 5.
/// 플레이어 방향 60도 부채꼴에 폭 1 · 5회 반사 레이저 3개 → 1초 워닝 → 레이저. 0.5초 뒤 한 번 더 (다시 조준).
/// 체력 50% 이하: 부채꼴 120도 · 레이저 6개 · 추가 사용 2회 (총 3번).
/// [임시] 레이저 지속 0.5초 (1페이즈 반사 레이저와 같음), 레이저는 부채꼴 안에 같은 간격으로 배치, 포즈 (GrabReady → GrabDash).
/// </summary>
public sealed class KagutsuchiReflectLaserPattern : KagutsuchiFirePattern
{
    public const float Cooldown = 10f;
    const float WarnTime = 1f, LaserTime = 0.5f, Gap = 0.5f, Width = 1f, Damage = 5f, LaserTick = 0.5f;
    const int Bounces = 5, ExtraUses = 1, EnhancedExtraUses = 2;   // 추가 사용: 일반 1회 (총 2번), 강화 2회 (총 3번, 사용자 2026-11-16)
    enum Step { Warn, Fire, Gap }
    Step step;
    int use;
    readonly List<List<Vector2>> paths = new List<List<Vector2>>();
    readonly List<BossWarning> warnings = new List<BossWarning>();
    readonly List<RaidLaser> lasers = new List<RaidLaser>();

    protected override void OnBegin()
    {
        use = 0;
        BeginWarning();
    }

    void BeginWarning()
    {
        Vector2 aim = ToPlayer();
        Face(aim);
        showPose(grabReady, 0f);
        float fan = enhanced ? 120f : 60f;
        int n = enhanced ? 6 : 3;
        Vector2 muzzle = Stage.ClampInside(Hand + aim, 0f);   // 몸 옆 2 + 앞 1 에서 시작 (사용자: 옆으로 2)
        paths.Clear();
        for (int i = 0; i < n; i++)
        {
            float angle = -fan * 0.5f + fan * (i + 0.5f) / n;   // 부채꼴을 n 등분한 가운데 방향
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * aim;
            var pts = Stage.ReflectPath(muzzle, dir, Bounces);
            paths.Add(pts);
            for (int s = 0; s + 1 < pts.Count; s++)
            {
                Vector2 seg = pts[s + 1] - pts[s];
                if (seg.sqrMagnitude < 1e-4f) continue;
                warnings.Add(BossWarning.Box(pts[s], seg.normalized, seg.magnitude, Width, WarnTime));
            }
        }
        step = Step.Warn; elapsed = 0f;
    }

    protected override void OnTick(float dt)
    {
        switch (step)
        {
            case Step.Warn:
                if (elapsed >= WarnTime)
                {
                    ClearWarnings();
                    showPose(grabDash, 0f);
                    foreach (var pts in paths)
                        for (int s = 0; s + 1 < pts.Count; s++)
                        {
                            Vector2 seg = pts[s + 1] - pts[s];
                            if (seg.sqrMagnitude < 1e-4f) continue;
                            var laser = RaidLaser.Line(pts[s], seg.normalized, seg.magnitude, Width, LaserTime, Damage, LaserTick);
                            KagutsuchiFlameLaserFx.Attach(laser);
                            lasers.Add(laser);
                        }
                    CameraFollow.Shake(0.15f, 0.12f);
                    step = Step.Fire; elapsed = 0f;
                }
                break;
            case Step.Fire:
                if (elapsed >= LaserTime)
                {
                    if (use < (enhanced ? EnhancedExtraUses : ExtraUses)) { step = Step.Gap; elapsed = 0f; }
                    else Cancel();
                }
                break;
            case Step.Gap:
                if (elapsed >= Gap) { use++; BeginWarning(); }
                break;
        }
    }

    void ClearWarnings()
    {
        foreach (var w in warnings) Kill(w);
        warnings.Clear();
    }

    protected override void OnCancel()
    {
        ClearWarnings();
        foreach (var laser in lasers)
            if (laser != null) { laser.gameObject.SetActive(false); Destroy(laser.gameObject); }
        lasers.Clear();
        paths.Clear();
    }
}
