using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 홉 고블린 기사 (중형 · 중간보스, 2026-11-12 사용자 기획 · Docs/Design/Monsters_v1.md).
/// 이동 느림. 고블린 장로와 같은 규칙: 패턴은 한 번에 하나, 쿨타임은 패턴이 끝난 뒤부터,
/// 여러 패턴이 준비되면 무작위, 기절하면 진행 중인 패턴이 멈췄다가 이어서, 체력 50% 아래 강화.
/// 기획에 없는 값(피해량 · 기절 시간 · 속도 등)은 [임시].
/// </summary>
public class HobgoblinKnightAI : MonoBehaviour
{
    // ---------- 방패 돌진 ----------
    const float ChargeWindup = 1f, ChargeDistance = 12f, ChargeCooldown = 10f, ChargeCooldownEnraged = 5f;
    const float ChargeSpeed = 16f;          // [임시]
    const float ChargeWidth = 1.8f;         // [임시] 경고 폭
    const float ChargeDamage = 20f, ChargeStun = 1f;   // [임시] "맞으면 기절" 시간 미정 → 1초

    // ---------- 내려찍기 ----------
    const float SlamWindup = 1f, SlamRadius = 4f, SlamStun = 1f, SlamCooldown = 8f;
    const int SlamRepeatsEnraged = 3;
    const float SlamDamage = 25f;           // [임시]
    const float SlamReach = 3f;             // 노리는 위치 = 플레이어 쪽, 보스에서 최대 3 (2026-11-12 사용자)

    // ---------- 방패 막기 ----------
    const float GuardSeconds = 5f, GuardCut = 0.8f, GuardCooldown = 20f;

    // ---------- 횡 베기 ----------
    const float SweepWindup = 1f, SweepRadius = 4f, SweepRadiusEnraged = 5f, SweepCooldown = 6f;
    const int SweepRepeatsEnraged = 2;      // "1회 반복" = 총 2번
    const float SweepDamage = 20f, SweepKnock = 3f;   // [임시] 피해 · 밀려나는 거리
    const float SweepAdvance = 3f;                     // 벤 뒤 전방으로 3 이동 (사용자)
    const float SweepAdvanceTime = 0.2f;               // [임시] 이동에 걸리는 시간

    // ---------- 연속 내려베기 ----------
    const float ChopDelay = 1f, ChopLength = 6f, ChopCooldown = 7f;
    const int ChopCount = 3;
    const float ChopWidth = 1.6f, ChopDamage = 18f;   // [임시]
    const float ChopLunge = 1.5f, ChopLungeTime = 0.15f;   // [임시] 강화: 시전할 때마다 플레이어 쪽으로 조금 돌진

    // ---------- 뛰어오르기 ----------
    const float LeapWindup = 1f, LeapMaxRadius = 6f, LeapMaxRadiusEnraged = 8f, LeapCooldown = 25f;
    const float LeapHold = 1f, LeapStop = 2f;          // 최대 크기 도달 후 1초 뒤 정지, 정지 후 2초 뒤 낙하 (사용자: 1 → 2)
    const float LeapStartRadius = 1f;                  // [임시] 경고 시작 크기
    const float LeapGrow = 2f;                         // [임시] 초당 반경 증가 (강화 +50%)
    const float LeapFollow = 2.5f, LeapFollowEnraged = 4f;   // [임시] 경고가 플레이어를 따라가는 속도
    const float LeapDamage = 30f;                      // [임시]

    static readonly float[] FirstCooldown = { 4f, 3f, 12f, 2f, 6f, 15f };   // [임시] 등장 직후 첫 사용까지
    enum Pattern { Charge, Slam, Guard, Sweep, Chop, Leap }
    static readonly float[] Cooldowns = { ChargeCooldown, SlamCooldown, GuardCooldown, SweepCooldown, ChopCooldown, LeapCooldown };
    static readonly float[] CooldownsEnraged = { ChargeCooldownEnraged, SlamCooldown, GuardCooldown, SweepCooldown, ChopCooldown, LeapCooldown };

    Enemy e;
    readonly float[] cd = new float[6];
    readonly List<int> ready = new List<int>();
    readonly List<BossWarning> warnings = new List<BossWarning>();
    bool busy, stopped, wasEnraged, guarding;
    Vector2 faceDir, guardDir;

    void Awake()
    {
        e = GetComponent<Enemy>();
        FirstCooldown.CopyTo(cd, 0);
    }

    void OnDisable()
    {
        foreach (var w in warnings) if (w != null) Destroy(w.gameObject);
        warnings.Clear();
    }

    bool Enraged => e.hp <= e.maxHp * 0.5f;
    Vector2 Pos => transform.position;
    static Player P => Player.I;
    Vector2 PlayerPos => P != null ? P.HurtCenter : Pos;
    Vector2 ToPlayer { get { var d = PlayerPos - Pos; return d.sqrMagnitude > 0.0001f ? d.normalized : Vector2.down; } }

    /// <summary>방패 막기: 막은 방향 180도 안에서 온 피해 -80%. fromDir = 피해가 밀어내는 방향 (공격은 그 반대쪽에서 옴)</summary>
    public float DamageMul(Vector2 fromDir)
    {
        if (!guarding) return 1f;
        Vector2 from = fromDir.sqrMagnitude > 0.0001f ? -fromDir.normalized : ToPlayer;
        return Vector2.Dot(from, guardDir) >= 0f ? 1f - GuardCut : 1f;
    }

    /// <summary>Enemy.Update 에서 매 프레임 (기절 · 일시정지 중에는 불리지 않음 → 쿨타임도 멈춤)</summary>
    public Vector2 Behave(Vector2 toPlayer, float moveSpeed, ref Vector2 face)
    {
        face = stopped && faceDir.sqrMagnitude > 0f ? faceDir : toPlayer;
        if (Enraged && !wasEnraged)
        {
            wasEnraged = true;
            for (int i = 0; i < cd.Length; i++) cd[i] = Mathf.Min(cd[i], CooldownsEnraged[i]);
        }
        if (!busy)
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < cd.Length; i++) if (cd[i] > 0f) cd[i] -= dt;
            TryStart();
        }
        return stopped ? Vector2.zero : toPlayer * moveSpeed;
    }

    void TryStart()
    {
        ready.Clear();
        for (int i = 0; i < cd.Length; i++) if (cd[i] <= 0f) ready.Add(i);
        if (ready.Count == 0) return;
        var p = (Pattern)ready[Random.Range(0, ready.Count)];
        IEnumerator body = p switch
        {
            Pattern.Charge => ChargeRoutine(),
            Pattern.Slam => SlamRoutine(),
            Pattern.Guard => GuardRoutine(),
            Pattern.Sweep => SweepRoutine(),
            Pattern.Chop => ChopRoutine(),
            _ => LeapRoutine(),
        };
        StartCoroutine(Run(p, body));
    }

    IEnumerator Run(Pattern p, IEnumerator body)
    {
        busy = true;
        stopped = true;
        yield return body;
        stopped = false;
        busy = false;
        guarding = false;
        cd[(int)p] = (Enraged ? CooldownsEnraged : Cooldowns)[(int)p];
    }

    /// <summary>패턴 대기: 게임 진행 중 + 기절이 아닐 때만 흐름</summary>
    IEnumerator Wait(float t, bool pauseOnStun = true)
    {
        while (t > 0f)
        {
            if (Playing && !(pauseOnStun && e.IsStunned)) t -= Time.deltaTime;
            yield return null;
        }
    }

    static bool Playing => GameManager.I.State == GameState.Playing;

    BossWarning Track(BossWarning w) { warnings.RemoveAll(x => x == null); warnings.Add(w); return w; }

    Vector2 InsideArena(Vector2 p) => MidBossArena.ClampInside(p, 1.5f);

    // ---------- 방패 돌진: 1초 준비 (매우 긴 경고) → 12 거리 돌진, 맞으면 기절 ----------
    IEnumerator ChargeRoutine()
    {
        Vector2 dir = faceDir = ToPlayer;
        Track(BossWarning.Box(Pos, dir, ChargeDistance, ChargeWidth, ChargeWindup));
        yield return Wait(ChargeWindup);
        float left = ChargeDistance;
        bool hit = false;
        while (left > 0f)
        {
            if (!Playing || e.IsStunned) { if (e.IsStunned) break; yield return null; continue; }   // 기절하면 돌진 중단
            float step = Mathf.Min(ChargeSpeed * Time.deltaTime, left);
            Vector2 next = Pos + dir * step;
            if (InsideArena(next) != next) break;   // 포위 원에 닿으면 멈춤
            transform.position = next;
            left -= step;
            if (!hit && P != null && (P.HurtCenter - Pos).sqrMagnitude < Sq(e.radius + P.radius + 0.2f))
            {
                hit = true;
                P.HitByBoss(ChargeDamage, ChargeStun, dir, 1f);
            }
            yield return null;
        }
        CameraFollow.Shake(0.12f, 0.12f);
    }

    // ---------- 내려찍기: 노린 위치에 경고 → 1초 뒤 원형 피해 + 1초 기절 (강화 3회) ----------
    IEnumerator SlamRoutine()
    {
        int n = Enraged ? SlamRepeatsEnraged : 1;
        for (int k = 0; k < n; k++)
        {
            Vector2 at = InsideArena(Pos + Vector2.ClampMagnitude(PlayerPos - Pos, SlamReach));   // 플레이어 쪽으로, 보스에서 최대 3
            faceDir = (at - Pos).sqrMagnitude > 0.0001f ? (at - Pos).normalized : faceDir;
            Track(BossWarning.Circle(at, SlamRadius, SlamWindup));
            yield return Wait(SlamWindup);
            if (!LeniaFx.Shockwave(at, SlamRadius)) Fx.Disc(at, SlamRadius, new Color(0.9f, 0.75f, 0.45f, 0.45f), 0.35f);
            CameraFollow.Shake(0.15f, 0.15f);
            if (P != null && (P.HurtCenter - at).sqrMagnitude < SlamRadius * SlamRadius)
                P.HitByBoss(SlamDamage, SlamStun, Vector2.zero, 0f);
        }
    }

    // ---------- 방패 막기: 5초 제자리, 처음 바라본 방향 180도에서 받는 피해 -80% ----------
    IEnumerator GuardRoutine()
    {
        guardDir = faceDir = ToPlayer;
        guarding = true;
        Track(BossWarning.HalfDisc(Pos, guardDir, e.radius + 1.2f, GuardSeconds, new Color(0.45f, 0.75f, 1f, 0.35f)));
        yield return Wait(GuardSeconds);
        guarding = false;
    }

    // ---------- 횡 베기: 1초 준비 (반원 경고) → 180도 베기, 맞으면 밀려남 (강화: 1회 더, 반경 5) ----------
    IEnumerator SweepRoutine()
    {
        int n = Enraged ? SweepRepeatsEnraged : 1;
        float r = Enraged ? SweepRadiusEnraged : SweepRadius;
        for (int k = 0; k < n; k++)
        {
            Vector2 dir = faceDir = ToPlayer;
            Track(BossWarning.HalfDisc(Pos, dir, r, SweepWindup));
            yield return Wait(SweepWindup);
            Fx.Arc(Pos, dir, r, new Color(1f, 0.95f, 0.85f, 0.8f), 0.2f);
            if (P != null)
            {
                Vector2 d = P.HurtCenter - Pos;
                if (d.sqrMagnitude < r * r && Vector2.Dot(d, dir) >= 0f)
                    P.HitByBoss(SweepDamage, 0f, d, SweepKnock);
            }
            yield return Dash(dir, SweepAdvance, SweepAdvanceTime);   // 벤 뒤 전방으로 3
        }
    }

    /// <summary>dir 방향으로 distance 만큼 seconds 동안 이동 (포위 원 밖으로는 안 나감, 기절 · 일시정지 중 멈춤)</summary>
    IEnumerator Dash(Vector2 dir, float distance, float seconds)
    {
        for (float t = 0f; t < seconds;)
        {
            if (Playing && !e.IsStunned)
            {
                float dt = Mathf.Min(Time.deltaTime, seconds - t);
                t += dt;
                transform.position = InsideArena(Pos + dir * (distance / seconds * dt));
            }
            yield return null;
        }
    }

    // ---------- 연속 내려베기: 직사각형 (길이 6) 1초 딜레이 × 3 (강화: 시전할 때마다 플레이어 쪽으로 조금 돌진) ----------
    IEnumerator ChopRoutine()
    {
        for (int k = 0; k < ChopCount; k++)
        {
            if (Enraged)
            {
                Vector2 to = ToPlayer;
                for (float t = 0f; t < ChopLungeTime;)
                {
                    if (Playing && !e.IsStunned)
                    {
                        float dt = Mathf.Min(Time.deltaTime, ChopLungeTime - t);
                        t += dt;
                        transform.position = InsideArena(Pos + to * (ChopLunge / ChopLungeTime * dt));
                    }
                    yield return null;
                }
            }
            Vector2 dir = faceDir = ToPlayer;
            Vector2 from = Pos;
            Track(BossWarning.Box(from, dir, ChopLength, ChopWidth, ChopDelay));
            yield return Wait(ChopDelay);
            Fx.Box(from, dir, ChopLength, ChopWidth, new Color(1f, 0.95f, 0.85f, 0.7f), 0.15f);
            CameraFollow.Shake(0.08f, 0.08f);
            if (P != null)
            {
                Vector2 d = P.HurtCenter - from;
                float along = Vector2.Dot(d, dir), side = Mathf.Abs(d.x * dir.y - d.y * dir.x);
                if (along >= -P.radius && along <= ChopLength + P.radius && side <= ChopWidth * 0.5f + P.radius)
                    P.HitByBoss(ChopDamage, 0f, Vector2.zero, 0f);
            }
        }
    }

    // ---------- 뛰어오르기: 1초 준비 → 맵 이탈 → 플레이어를 천천히 따라가며 커지는 경고 (반경 6) →
    //            최대 도달 1초 뒤 정지 → 정지 1초 뒤 낙하 (강화: 따라가는 속도 ↑, 반경 8, 커지는 속도 +50%) ----------
    IEnumerator LeapRoutine()
    {
        faceDir = ToPlayer;
        // 준비: 웅크림 (코드 연출)
        for (float t = 0f; t < LeapWindup;)
        {
            if (Playing && !e.IsStunned) t += Time.deltaTime;
            float k = Mathf.Clamp01(t / LeapWindup);
            transform.localScale = new Vector3(1f + 0.12f * k, 1f - 0.18f * k, 1f) * e.transform.localScale.z;
            yield return null;
        }
        float s = e.transform.localScale.z;
        transform.localScale = Vector3.one * s;
        Fx.Circle(Pos, 1.2f, new Color(0.8f, 0.8f, 0.7f, 0.6f), 0.3f);
        e.SetHidden(true);

        bool enraged = Enraged;
        float maxR = enraged ? LeapMaxRadiusEnraged : LeapMaxRadius;
        float grow = LeapGrow * (enraged ? 1.5f : 1f);
        float follow = enraged ? LeapFollowEnraged : LeapFollow;
        float r = LeapStartRadius;
        Vector2 c = InsideArena(PlayerPos);
        var w = Track(BossWarning.Circle(c, r, 0f));

        // 커지면서 따라감 → 최대 도달 후 1초 더 따라감 → 정지 1초 → 낙하
        float hold = LeapHold, stop = LeapStop;
        while (stop > 0f)
        {
            if (Playing)
            {
                float dt = Time.deltaTime;
                if (r < maxR) r = Mathf.Min(maxR, r + grow * dt);
                else if (hold > 0f) hold -= dt;
                else stop -= dt;
                if (r < maxR || hold > 0f) c = InsideArena(Vector2.MoveTowards(c, PlayerPos, follow * dt));
                w.Set(c, r);
            }
            yield return null;
        }

        if (w != null) Destroy(w.gameObject);
        transform.position = InsideArena(c);
        e.SetHidden(false);
        if (!LeniaFx.Shockwave(c, r)) Fx.Disc(c, r, new Color(0.9f, 0.75f, 0.45f, 0.45f), 0.35f);
        CameraFollow.Shake(0.3f, 0.25f);
        if (P != null && (P.HurtCenter - c).sqrMagnitude < r * r) P.HitByBoss(LeapDamage, 0f, Vector2.zero, 0f);
    }

    static float Sq(float v) => v * v;
}

/// <summary>
/// 보스 공격 경고 표시 (붉은 반투명 · 깜빡임, 바닥 레이어). seconds 가 지나면 사라짐 (0 = 직접 지울 때까지).
/// 원 · 반원(방향) · 직사각형(시작점에서 방향으로)
/// </summary>
public class BossWarning : MonoBehaviour
{
    static readonly Color Red = new Color(1f, 0.2f, 0.15f, 0.32f);

    SpriteRenderer sr;
    float life;
    Color color;
    bool timed;

    public static BossWarning Circle(Vector2 at, float r, float seconds) =>
        Make(SpriteFactory.Shape.Circle, at, new Vector2(r * 2f, r * 2f), 0f, seconds, Red);

    public static BossWarning HalfDisc(Vector2 at, Vector2 dir, float r, float seconds) => HalfDisc(at, dir, r, seconds, Red);

    public static BossWarning HalfDisc(Vector2 at, Vector2 dir, float r, float seconds, Color c) =>
        Make(SpriteFactory.Shape.HalfDisc, at, new Vector2(r * 2f, r * 2f), Angle(dir), seconds, c);

    public static BossWarning Box(Vector2 from, Vector2 dir, float length, float width, float seconds) =>
        Make(SpriteFactory.Shape.Square, from + dir.normalized * length * 0.5f, new Vector2(length, width), Angle(dir), seconds, Red);

    static float Angle(Vector2 d) => Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;

    static BossWarning Make(SpriteFactory.Shape shape, Vector2 pos, Vector2 size, float angle, float seconds, Color c)
    {
        var go = new GameObject("BossWarning");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0, 0, angle);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);
        var w = go.AddComponent<BossWarning>();
        w.sr = go.AddComponent<SpriteRenderer>();
        w.sr.sprite = SpriteFactory.Get("Fx_" + shape, shape);
        w.sr.sortingOrder = Fx.GroundOrder;
        w.color = c;
        w.sr.color = c;
        w.life = seconds;
        w.timed = seconds > 0f;
        return w;
    }

    /// <summary>경고 색 바꾸기 (깜빡임은 이 색 기준). 카구츠치 화염장벽의 흐린 바탕 경고 등</summary>
    public void SetColor(Color c) { color = c; if (sr != null) sr.color = c; }

    /// <summary>원 경고 위치 · 반경 바꾸기 (뛰어오르기)</summary>
    public void Set(Vector2 at, float r)
    {
        transform.position = at;
        transform.localScale = new Vector3(r * 2f, r * 2f, 1f);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 14f);
        sr.color = new Color(color.r, color.g, color.b, color.a * pulse);
        if (!timed) return;
        life -= Time.deltaTime;
        if (life <= 0f) Destroy(gameObject);
    }
}
