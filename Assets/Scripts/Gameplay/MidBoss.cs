using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 중간보스 (2026-11-12 사용자 기획 · Docs/Design/Monsters_v1.md)
// - 등장 시 중간보스 체력의 1.5배인 고블린들이 넓은 원으로 둘러싸 맵을 제한 (MidBossArena)
// - 고블린 장로: 마법 · 느림 · 모든 패턴은 겹치지 않음 (GoblinElderAI)
// 기획에 없는 값은 [임시].

/// <summary>
/// 중간보스 포위: 중간보스가 나타난 자리(플레이어 위치)를 중심으로 반경 Radius 원 위에 포위 고블린을 촘촘히 세움.
/// 포위 고블린 = 움직이지 않음 · 넉백 없음 · 체력 = 중간보스 최대 체력 × 1.5 · 접촉 피해는 고블린과 같음.
/// 플레이어는 그 사이로 지나갈 수 없음 (돌진 포함). 포위 고블린을 쓰러뜨리면 그 자리는 뚫림.
/// 중간보스를 쓰러뜨리면 남은 포위 고블린은 사라짐 (경험치 없음).
/// </summary>
public static class MidBossArena
{
    public const float Radius = 20f;            // 포위 반경 (2026-11-12 사용자: 20). 카메라 세로 시야 14, 가로 약 25
    const float WallSpacing = 0.9f;             // [임시] 포위 고블린 간격 (플레이어가 빠져나갈 틈이 없게)
    public const float WallHpMul = 1.5f;        // 중간보스 체력 × 1.5 (사용자)
    public const int WallXp = 5;                // [임시] 포위 고블린 처치 경험치
    const float PlayerBlockRadius = 0.3f;       // [임시] 막히는 몸 반경 (피격 반경 0.4 보다 작아 벽에 붙으면 접촉 피해)
    const float BossSpawnDistance = 6f;         // [임시] 중간보스가 나타나는 거리 (원 안쪽)

    public static Vector2 Center { get; private set; }
    static Enemy boss;
    static readonly List<Enemy> walls = new List<Enemy>();
    static bool subscribed, playerInside;

    /// <summary>중간보스가 살아 있음 (다음 중간보스는 이게 끝난 뒤)</summary>
    public static bool Active => boss != null && boss.hp > 0;
    public static Enemy Boss => Active ? boss : null;

    public static int WallCount
    {
        get { walls.RemoveAll(w => w == null || w.hp <= 0); return walls.Count; }
    }

    /// <summary>새 판 시작 (씬을 다시 불러와도 static 은 남으므로 GameManager.Awake 에서)</summary>
    public static void Reset()
    {
        boss = null;
        walls.Clear();
        if (!subscribed) { Enemy.Killed += OnKilled; subscribed = true; }
    }

    public static void Begin(EnemyKind kind, Vector2 center, float hpMul)
    {
        Reset();
        Center = center;
        playerInside = true;
        boss = Enemy.Spawn(kind, center + Random.insideUnitCircle.normalized * BossSpawnDistance, hpMul);
        float wallHp = boss.maxHp * WallHpMul;
        int n = Mathf.CeilToInt(2f * Mathf.PI * Radius / WallSpacing);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.PI * 2f / n;
            walls.Add(Enemy.SpawnWall(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius, wallHp));
        }
        Fx.GroundCircle(center, Radius, new Color(1f, 0.3f, 0.2f, 0.5f), 1.2f);   // 포위 순간 표시
        CameraFollow.Shake(0.25f, 0.2f);
    }

    static void OnKilled(Enemy e)
    {
        if (e == null) return;
        if (e == boss) End();
    }

    static void End()
    {
        foreach (var w in walls)
        {
            if (w == null || w.hp <= 0) continue;
            Fx.Disc(w.transform.position, 0.4f, new Color(0.7f, 0.7f, 0.6f, 0.5f), 0.3f);
            Object.Destroy(w.gameObject);
        }
        walls.Clear();
        boss = null;
    }

    /// <summary>원 안쪽으로 반경 margin 만큼 들인 위치 (장로 순간이동 · 낙뢰 · 얼음 지대 위치)</summary>
    public static Vector2 ClampInside(Vector2 p, float margin)
    {
        if (!Active) return p;
        Vector2 d = p - Center;
        float max = Radius - margin;
        return d.sqrMagnitude > max * max ? Center + d.normalized * max : p;
    }

    /// <summary>
    /// 플레이어 주변 반경 around 안 무작위 위치 (2026-11-12 사용자: 낙뢰 · 얼음 지대 반경 12).
    /// 포위 중이면 포위 원 밖으로 나가지 않게 원 안쪽 margin 만큼 들여서
    /// </summary>
    public static Vector2 RandomNearPlayer(float around, float margin)
    {
        var p = Player.I;
        Vector2 at = (p != null ? (Vector2)p.transform.position : Center) + Random.insideUnitCircle * around;
        return ClampInside(at, margin);
    }

    /// <summary>
    /// 플레이어가 포위 고블린과 겹치지 않게 밀어냄 + 빠른 이동(돌진)으로 원을 뚫고 나가는 것 방지.
    /// 쓰러뜨린 자리(근처에 포위 고블린이 없음)로는 드나들 수 있음
    /// </summary>
    public static void ConstrainPlayer(Player p)
    {
        if (walls.Count == 0 || p == null) return;
        Vector2 c = p.HurtCenter;
        Vector2 shift = Vector2.zero;
        foreach (var w in walls)
        {
            if (w == null || w.hp <= 0) continue;
            Vector2 d = c - (Vector2)w.transform.position;
            float min = w.radius + PlayerBlockRadius;
            float sq = d.sqrMagnitude;
            if (sq >= min * min) continue;
            float m = Mathf.Sqrt(sq);
            Vector2 n = m > 1e-4f ? d / m : (playerInside ? (Center - c).normalized : (c - Center).normalized);
            shift += n * (min - m);
        }
        c += shift;

        // 원을 넘었는데 그 자리에 포위 고블린이 서 있으면 원래 쪽으로 되돌림
        Vector2 fromCenter = c - Center;
        bool inside = fromCenter.sqrMagnitude < Radius * Radius;
        if (inside != playerInside)
        {
            Vector2 cross = Center + fromCenter.normalized * Radius;
            bool blocked = false;
            foreach (var w in walls)
                if (w != null && w.hp > 0 && ((Vector2)w.transform.position - cross).sqrMagnitude < WallSpacing * WallSpacing) { blocked = true; break; }
            if (blocked)
            {
                float r = playerInside ? Radius - PlayerBlockRadius - 0.35f : Radius + PlayerBlockRadius + 0.35f;
                c = Center + fromCenter.normalized * r;
            }
            else playerInside = inside;
        }
        p.transform.position += (Vector3)(c - p.HurtCenter);
    }
}

/// <summary>
/// 고블린 장로 (마법 · 중간보스). 이동 느림. 패턴은 한 번에 하나 (겹치지 않음).
/// 쿨타임은 패턴이 끝난 뒤부터. 패턴 중에 기절하면 그 패턴이 멈췄다가 기절이 끝나면 이어서 진행
/// (단, 낙뢰는 경고 표시가 뜬 뒤 3초에 정확히 떨어짐).
/// </summary>
public class GoblinElderAI : MonoBehaviour
{
    // ---------- 기획 수치 ----------
    const float BoltCooldown = 1f;                                   // 마력탄 발사 (이동하며)
    const float FanStop = 1.5f, FanArc = 120f, FanStep = 20f, FanCooldown = 5f;   // 마력탄 부채꼴
    const float NovaStep = 15f, NovaShift = 5f, NovaDelay = 3f, NovaCooldown = 15f; // 마력탄 전방위 발사
    const int NovaWaves = 3, NovaWavesEnraged = 5;                   //   체력 50% 아래 5회
    const float StrikeCast = 1f, StrikeDelay = 3f, StrikeRadius = 3f, StrikeCooldown = 20f;   // 낙뢰 발생
    const int StrikeCount = 20;   // 2026-11-12 사용자: 10 → 20
    const float IceSlow = 0.7f, IceSeconds = 5f, IceCooldown = 25f; // 얼음 지대 (이동 속도 30% 감소)
    const int IceCount = 3;                                          //   한 번에 3개
    const float BlinkRadius = 8f, BlinkCooldown = 20f;              // 순간이동 마력탄 (정삼각형 꼭짓점 3곳)
    const float BlinkAim = 0.25f, BlinkRecover = 0.25f;             //   쏘기 전 · 쏜 뒤 정지 (사용자)
    const int BlinkCount = 3;                                        //   삼각형 꼭짓점 수

    // ---------- 체력 50% 아래 강화 (2026-11-12 사용자) ----------
    const float FanCooldownEnraged = 3f, NovaCooldownEnraged = 10f, IceCooldownEnraged = 15f, BlinkCooldownEnraged = 15f;
    const int IceCountEnraged = 7;                                   // 얼음 지대 7곳
    const int BlinkJumpsEnraged = 5;                                 // 순간이동 총 5회 (삼각형을 따라 계속 돎)
    const float FanStopEnraged = 0.75f, FanShiftEnraged = 5f;        // 부채꼴: 0.75초 딜레이로 3회, 회마다 5도씩 돌림
    const int FanVolleysEnraged = 3;
    const float NovaDelayEnraged = 1.5f;                             // 전방위: 반복 간격 3초 → 1.5초

    // ---------- [임시] 기획에 없는 값 ----------
    const float BoltDamage = 8f, BoltSpeed = 4.5f, BoltTravel = 16f;
    const float StrikeDamage = 20f;
    const float IceRadius = 3f;
    const float PlaceAround = 12f;  // 낙뢰 · 얼음 지대 위치: 플레이어 주변 이 반경 안 무작위 (사용자)
    static readonly float[] FirstCooldown = { 3f, 10f, 8f, 6f, 14f };   // 등장 직후 첫 사용까지 (부채꼴 · 전방위 · 낙뢰 · 얼음 · 순간이동)

    enum Pattern { Fan, Nova, Strike, Ice, Blink }
    static readonly float[] Cooldowns = { FanCooldown, NovaCooldown, StrikeCooldown, IceCooldown, BlinkCooldown };
    static readonly float[] CooldownsEnraged = { FanCooldownEnraged, NovaCooldownEnraged, StrikeCooldown, IceCooldownEnraged, BlinkCooldownEnraged };
    float CooldownOf(Pattern p) => (Enraged ? CooldownsEnraged : Cooldowns)[(int)p];
    bool wasEnraged;

    Enemy e;
    readonly float[] cd = new float[5];
    float boltCd = 1.5f;
    bool busy, stopped;
    readonly List<int> ready = new List<int>();

    void Awake()
    {
        e = GetComponent<Enemy>();
        FirstCooldown.CopyTo(cd, 0);
    }

    bool Enraged => e.hp <= e.maxHp * 0.5f;
    Vector2 Pos => transform.position;
    Vector2 AimAt => Player.I != null ? Player.I.HurtCenter : Pos + Vector2.down;

    /// <summary>Enemy.Update 에서 매 프레임 (기절 · 일시정지 중에는 불리지 않음 → 쿨타임도 멈춤)</summary>
    public Vector2 Behave(Vector2 toPlayer, float moveSpeed, ref Vector2 face)
    {
        face = toPlayer;
        if (Enraged && !wasEnraged)
        {
            // 체력 50% 아래로 내려간 순간: 돌고 있던 쿨타임이 강화 쿨타임보다 길면 줄임
            wasEnraged = true;
            for (int i = 0; i < cd.Length; i++) cd[i] = Mathf.Min(cd[i], CooldownsEnraged[i]);
        }
        if (!busy)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < cd.Length; i++) if (cd[i] > 0f) cd[i] -= dt;
            if (boltCd > 0f) boltCd -= dt;
            TryStart();
        }
        return stopped ? Vector2.zero : toPlayer * moveSpeed;
    }

    void TryStart()
    {
        ready.Clear();
        for (int i = 0; i < cd.Length; i++) if (cd[i] <= 0f) ready.Add(i);
        if (ready.Count > 0)
        {
            var p = (Pattern)ready[Random.Range(0, ready.Count)];
            switch (p)
            {
                case Pattern.Fan: StartCoroutine(Run(p, FanRoutine())); break;
                case Pattern.Nova: StartCoroutine(Run(p, NovaRoutine())); break;
                case Pattern.Strike: StartCoroutine(Run(p, StrikeRoutine())); break;
                case Pattern.Blink: StartCoroutine(Run(p, BlinkRoutine())); break;
                case Pattern.Ice:
                    for (int i = 0, n = Enraged ? IceCountEnraged : IceCount; i < n; i++)   // 플레이어 주변 반경 12 안 무작위 3곳 (강화 7곳)
                        IceZone.Spawn(MidBossArena.RandomNearPlayer(PlaceAround, IceRadius), IceRadius, IceSeconds, IceSlow);
                    cd[(int)Pattern.Ice] = CooldownOf(Pattern.Ice);
                    break;
            }
            return;
        }
        if (boltCd <= 0f)
        {
            Shoot((AimAt - Pos).normalized);   // 마력탄 발사: 이동하며
            boltCd = BoltCooldown;
        }
    }

    IEnumerator Run(Pattern p, IEnumerator body)
    {
        busy = true;
        yield return body;
        stopped = false;
        busy = false;
        cd[(int)p] = CooldownOf(p);
    }

    /// <summary>패턴 대기: 게임 진행 중 + 기절이 아닐 때만 흐름</summary>
    IEnumerator Wait(float t, bool pauseOnStun = true)
    {
        while (t > 0f)
        {
            if (GameManager.I.State == GameState.Playing && !(pauseOnStun && e.IsStunned)) t -= Time.deltaTime;
            yield return null;
        }
    }

    void Shoot(Vector2 dir) => EnemyShot.MagicBolt(Pos, dir, BoltSpeed, BoltTravel, BoltDamage);

    /// <summary>플레이어 방향 기준 120도 · 20도 간격 = 7발</summary>
    void FireFan() => FireFan(AimAngle());

    float AimAngle() => Mathf.Atan2(AimAt.y - Pos.y, AimAt.x - Pos.x) * Mathf.Rad2Deg;

    void FireFan(float baseAng)
    {
        for (float a = -FanArc * 0.5f; a <= FanArc * 0.5f + 0.01f; a += FanStep)
            Shoot(Dir(baseAng + a));
    }

    static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

    // 마력탄 부채꼴: 0.5초 정지 후 발사
    // 강화 (체력 50% 아래): 0.75초 딜레이 → 부채꼴, 3회 반복. 첫 발 조준 방향에서 회마다 5도씩 돌려 이전 사이를 노림
    IEnumerator FanRoutine()
    {
        stopped = true;
        if (!Enraged)
        {
            yield return Wait(FanStop);
            FireFan();
            yield break;
        }
        float baseAng = 0f;
        for (int k = 0; k < FanVolleysEnraged; k++)
        {
            yield return Wait(FanStopEnraged);
            if (k == 0) baseAng = AimAngle();
            FireFan(baseAng + k * FanShiftEnraged);
        }
    }

    // 마력탄 전방위 발사: 사용 중 정지, 10도 간격 36발 × 3회 (체력 50% 아래 5회), 3초 간격, 회마다 5도씩 돌려 이전 사이를 노림
    IEnumerator NovaRoutine()
    {
        stopped = true;
        int waves = Enraged ? NovaWavesEnraged : NovaWaves;
        float baseAng = Random.Range(0f, 360f);
        for (int k = 0; k < waves; k++)
        {
            if (k > 0) yield return Wait(Enraged ? NovaDelayEnraged : NovaDelay);
            float off = baseAng + k * NovaShift;
            for (float a = 0f; a < 360f - 0.01f; a += NovaStep) Shoot(Dir(off + a));
        }
    }

    // 낙뢰 발생: 1초 정지 시전 → 10곳에 붉은 경고 (반경 3) → 3초 뒤 낙뢰. 경고 동안에는 이동 (다른 패턴은 쓰지 않음)
    IEnumerator StrikeRoutine()
    {
        stopped = true;
        Fx.Circle(Pos, 1.2f, new Color(0.6f, 0.8f, 1f, 0.6f), StrikeCast, transform);   // 시전 표시
        yield return Wait(StrikeCast);
        stopped = false;
        var points = new Vector2[StrikeCount];
        for (int i = 0; i < StrikeCount; i++)
        {
            points[i] = MidBossArena.RandomNearPlayer(PlaceAround, 1f);   // 플레이어 주변 반경 12 안 무작위
            LeniaFx.TargetMarker(points[i], StrikeRadius, StrikeDelay);
        }
        yield return Wait(StrikeDelay, pauseOnStun: false);   // 경고 표시와 시간을 맞춤
        bool hit = false;
        var p = Player.I;
        foreach (var at in points)
        {
            LeniaFx.Bolt(at + Vector2.up * 8f, at, 0.25f);
            LeniaFx.LightningStrike(at, StrikeRadius);
            if (!hit && p != null && (p.HurtCenter - at).sqrMagnitude < StrikeRadius * StrikeRadius) hit = true;
        }
        CameraFollow.Shake(0.2f, 0.15f);
        if (hit) p.TakeDamage(StrikeDamage);   // 여러 낙뢰가 겹쳐도 1번
    }

    // 순간이동 마력탄 (2026-11-12 사용자): 시작할 때 플레이어 위치를 중심으로 반경 8 정삼각형 꼭짓점 3곳을 정하고
    // 그 삼각형을 따라 순간이동 → 0.25초 정지(선딜) → 부채꼴 발사 → 0.25초 정지(후딜), 3번.
    // 부채꼴은 쏘는 순간의 플레이어 방향 기준
    IEnumerator BlinkRoutine()
    {
        stopped = true;
        var p = Player.I;
        if (p == null) yield break;
        Vector2 center = p.transform.position;
        float a0 = Random.Range(0f, 360f);
        var corners = new Vector2[BlinkCount];
        for (int i = 0; i < BlinkCount; i++)
            corners[i] = MidBossArena.ClampInside(center + Dir(a0 + 360f / BlinkCount * i) * BlinkRadius, 1.5f);
        int jumps = Enraged ? BlinkJumpsEnraged : BlinkCount;   // 강화: 삼각형을 따라 5회 (꼭짓점 1→2→3→1→2)
        for (int i = 0; i < jumps; i++)
        {
            Vector2 to = corners[i % BlinkCount];
            Fx.Circle(Pos, 0.9f, new Color(0.75f, 0.4f, 1f, 0.7f), 0.3f);
            transform.position = to;
            Fx.Circle(to, 0.9f, new Color(0.75f, 0.4f, 1f, 0.7f), 0.3f);
            yield return Wait(BlinkAim);
            FireFan();
            yield return Wait(BlinkRecover);
        }
    }
}

/// <summary>얼음 지대: 지속 시간 동안 안에 있는 플레이어 이동 속도 감소 (적에게는 영향 없음)</summary>
public class IceZone : MonoBehaviour
{
    float radius, life, slowMul;

    public static void Spawn(Vector2 at, float radius, float seconds, float slowMul)
    {
        var go = new GameObject("IceZone");
        go.transform.position = at;
        var z = go.AddComponent<IceZone>();
        z.radius = radius; z.life = seconds; z.slowMul = slowMul;
        Fx.GroundDisc(at, radius, new Color(0.6f, 0.85f, 1f, 0.3f), seconds);
        Fx.GroundCircle(at, radius, new Color(0.75f, 0.95f, 1f, 0.7f), seconds);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        life -= Time.deltaTime;
        if (life <= 0f) { Destroy(gameObject); return; }
        var p = Player.I;
        if (p != null && ((Vector2)p.transform.position - (Vector2)transform.position).sqrMagnitude < radius * radius)
            p.ApplyZoneSlow(slowMul);
    }
}
