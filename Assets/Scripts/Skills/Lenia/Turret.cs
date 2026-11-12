using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TurretKind { Standard, Missile, Tesla }

/// <summary>설치 시점에 스킬이 정해 주는 터렛 성능 (특수 강화 반영 값)</summary>
public class TurretSpec
{
    public TurretKind kind;
    public float duration, fireCooldown;
    public float rangeScale = 1f;   // 설치한 스킬의 범위 배율 (기본 · 특수 강화 '범위')
    /// <summary>공격 1발의 피해 (설치 = 직접 사용의 간접 사용)</summary>
    public Func<DamageInfo> shot;
    /// <summary>공격 1회의 투사체 수 = 1 + '투사체 증가' (0.2초 간격, 서로 다른 대상)</summary>
    public int shots = 1;
    /// <summary>난사: 대상 대신 무작위 방향 (미사일은 무작위 지점)</summary>
    public bool scatter;
    public int pierce, bounces;
    /// <summary>적중 시 '기절' 초 (테슬라 기본 · 철갑탄 · 2번)</summary>
    public float stun;
    /// <summary>미사일 궤도 폭격 / 테슬라 위로 방출: 플레이어 주변 무작위 위치로 떨어짐 (도탄 없음)</summary>
    public bool orbital;
}

/// <summary>
/// 레니아가 설치하는 터렛 1개 (표준 · 미사일 · 테슬라). 지속시간이 끝나면 사라짐.
/// 공격: 사거리 안 가장 가까운 적을 향해 방향을 바꾸고 발사. 공격 1회 = 투사체 shots 발 (0.2초 간격, 서로 다른 대상).
/// 공격 간격은 마지막 투사체를 쏜 뒤부터 (사용 완료 후 쿨타임).
/// 피해는 설치한 스킬이 만들어 줌 (spec.shot) → 그 스킬의 강화 · 전역 피해 규칙이 그대로 적용.
/// 터렛 강화 · 터렛 과부화: 지금은 연출만 (Boost / Overload) — 실제 수치 강화는 기획 대기
/// </summary>
public class Turret : MonoBehaviour
{
    public static readonly List<Turret> All = new List<Turret>();

    // [임시] 화면 크기 (월드 유닛 키) · 사거리 · 탄속 — 기획에 없는 값
    public const float StandardHeight = 1.1f, MissileHeight = 1.15f, TeslaHeight = 1.4f;
    public const float Range = 6f;               // 공격 사거리
    const float BulletSpeed = 12f;
    const float MissileSpeed = 7f;
    public const float MissileRadius = 1.2f;     // 미사일 범위 피해 반경
    const float FrameFps = 10f;                  // 반동 · 링 동작 재생 속도 (원본 권장)
    // 수치 지정용 (화면에 표기하지 않음)
    public const float OrbitalMissileArea = 5f;  // 궤도 폭격: 플레이어 주변 낙하 반경
    public const float OrbitalTeslaArea = 8f;    // 테슬라 3번(Lv15): 플레이어 주변 낙뢰 반경
    const float OrbitalStrikeRadius = 1.3f;      // [임시] 낙뢰 1발 피해 반경
    const float DropHeight = 7f, DropSpeed = 14f;   // [임시] 궤도 폭격 낙하 (포격 실시와 같은 값)

    public TurretKind kind;
    TurretSpec spec;
    float life, fireTimer;
    float rangeScale = 1f;
    Vector2 lastAim = Vector2.down;

    SpriteRenderer sr;
    Sprite[,] idle, action;
    int dir;
    float actionT = -1f;
    float height;

    public float LifeLeft => life;

    public static Turret Spawn(TurretSpec spec, Vector2 at)
    {
        var kind = spec.kind;
        var go = new GameObject("Turret_" + kind);
        go.transform.position = at;
        var t = go.AddComponent<Turret>();
        t.kind = kind; t.spec = spec; t.life = spec.duration; t.rangeScale = spec.rangeScale;
        t.fireTimer = 0.25f;   // [임시] 설치 직후 첫 발사까지
        t.sr = go.AddComponent<SpriteRenderer>();
        t.sr.sortingOrder = 8;   // 적(5·6) 위, 플레이어(10) 아래
        switch (kind)
        {
            case TurretKind.Standard:
                t.height = StandardHeight;
                t.action = LeniaFx.StandardTurret.Load(StandardHeight);
                t.idle = t.action;   // 반동 시트 1번 프레임 = 정지 자세
                break;
            case TurretKind.Missile:
                t.height = MissileHeight;
                t.idle = LeniaFx.MissileTurret.Load(MissileHeight);
                break;
            case TurretKind.Tesla:
                t.height = TeslaHeight;
                t.idle = LeniaFx.TeslaTower.Load(TeslaHeight);
                t.action = LeniaFx.TeslaRing.Load(TeslaHeight);
                break;
        }
        t.ShowIdle();
        Fx.Circle(at, 0.6f, Fx.FireBright, 0.25f);   // 설치 표시
        return t;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void ShowIdle()
    {
        if (idle != null) sr.sprite = idle[dir, 0];
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        life -= dt;
        if (life <= 0f) { Fx.Disc(transform.position, 0.5f, new Color(0.7f, 0.7f, 0.7f, 0.5f), 0.25f); Destroy(gameObject); return; }

        // 공격 동작 재생 (8프레임 1회)
        if (actionT >= 0f && action != null)
        {
            actionT += dt;
            int f = (int)(actionT * FrameFps);
            if (f >= action.GetLength(1)) { actionT = -1f; ShowIdle(); }
            else sr.sprite = action[dir, f];
        }

        fireTimer -= dt;
        if (fireTimer > 0f) return;
        var target = LeniaFx.Nearest(transform.position, Range * rangeScale);
        if (target == null) { fireTimer = 0f; return; }   // 대상이 없으면 쿨타임을 소모하지 않음
        int n = Mathf.Max(1, spec.shots);
        fireTimer = spec.fireCooldown + (n - 1) * LeniaShots.Interval;   // 공격 간격 = 마지막 투사체 이후부터
        StartCoroutine(Attack(target, n));
    }

    /// <summary>공격 1회: 투사체 n 발을 0.2초 간격으로, 같은 공격 안에서는 서로 다른 대상 (없으면 중복 허용)</summary>
    IEnumerator Attack(Enemy first, int n)
    {
        var used = new HashSet<Enemy>();
        for (int i = 0; i < n; i++)
        {
            if (i > 0)
            {
                yield return new WaitForSeconds(LeniaShots.Interval);
                if (this == null || life <= 0f) yield break;
            }
            Enemy t;
            if (i == 0 && first != null && first.hp > 0) { t = first; used.Add(t); }
            else t = LeniaShots.NextTarget(transform.position, Range * rangeScale, used);
            FireOne(t);
        }
    }

    void FireOne(Enemy target)
    {
        Vector2 to = target != null ? (Vector2)target.transform.position - (Vector2)transform.position : lastAim;
        if (spec.scatter) to = UnityEngine.Random.insideUnitCircle.normalized;   // 난사: 무작위 방향
        if (to.sqrMagnitude < 0.0001f) to = lastAim;
        lastAim = to;
        dir = LeniaFx.Dir4(to);
        ShowIdle();
        switch (kind)
        {
            case TurretKind.Standard: FireBullet(to); break;
            case TurretKind.Missile: FireMissile(spec.scatter ? null : target, to); break;
            case TurretKind.Tesla: StartCoroutine(FireTesla(spec.scatter ? null : target, to)); break;
        }
    }

    /// <summary>적중 시 상태이상 (기절) — 피해 뒤 가장 늦게</summary>
    void ApplyOnHit(Enemy e)
    {
        if (e == null || e.hp <= 0 || spec.stun <= 0f) return;
        e.Stun(spec.stun);
        if (kind == TurretKind.Tesla) LeniaFx.ShowShocked(e, spec.stun);
    }

    /// <summary>포신 · 발사관 위치 (발 기준 높이 비율 + 바라보는 쪽으로 약간)</summary>
    Vector2 Muzzle(float heightRatio, float forward)
    {
        Vector2 f = dir switch { 1 => Vector2.left, 2 => Vector2.right, 3 => Vector2.up, _ => Vector2.down };
        return (Vector2)transform.position + Vector2.up * (height * heightRatio) + f * forward;
    }

    void FireBullet(Vector2 to)
    {
        actionT = 0f;   // 포신 반동
        Vector2 m = Muzzle(0.62f, 0.35f);
        var mods = new ShotMods
        {
            pierce = spec.pierce, bounces = spec.bounces, bounceRange = LeniaShots.BounceRadius * rangeScale,
            onHit = (e, wasStunned, d) => ApplyOnHit(e),
        };
        // 포신에서 조준 (to 는 발밑 기준 → 포신 높이만큼 보정)
        Vector2 aim = spec.scatter ? to : to - (m - (Vector2)transform.position);
        LeniaProjectile.Spawn(LeniaFx.Bullet, m, aim, BulletSpeed, 0f, Range * rangeScale * 1.2f, 0.12f, spec.shot(), 0.5f, mods);
    }

    void FireMissile(Enemy target, Vector2 to)
    {
        StartCoroutine(Kick());
        float r = MissileRadius * rangeScale;
        Vector2 m = Muzzle(0.72f, 0.3f);
        if (spec.orbital)
        {
            // 궤도 폭격: 위로 쏘아 올린 뒤 플레이어 주변 무작위 위치에 낙하
            LeniaFx.LaunchUp(m, LeniaFx.Missile);
            StartCoroutine(OrbitalDrop(r));
            return;
        }
        Vector2 point = target != null ? (Vector2)target.transform.position : (Vector2)transform.position + to.normalized * (Range * rangeScale * 0.7f);
        MissileProjectile.Spawn(m, target, point, MissileSpeed, r, spec.shot(), onHit: ApplyOnHit);
    }

    IEnumerator OrbitalDrop(float r)
    {
        var dmg = spec.shot();
        yield return new WaitForSeconds(0.3f);
        if (Player.I == null) yield break;
        Vector2 point = (Vector2)Player.I.transform.position + UnityEngine.Random.insideUnitCircle * OrbitalMissileArea;
        Fx.Circle(point, r, new Color(1f, 0.3f, 0.2f, 0.5f), DropHeight / DropSpeed);   // 낙하 지점 표시
        MissileProjectile.Spawn(point + Vector2.up * DropHeight, null, point, DropSpeed, r, dmg, fromAbove: true, onHit: ApplyOnHit);
    }

    /// <summary>미사일 포탑은 공격 동작 시트가 없어 발사 순간 살짝 눌렸다 돌아옴 (코드 연출)</summary>
    IEnumerator Kick()
    {
        for (float t = 0f; t < 0.15f; t += Time.deltaTime)
        {
            float k = Mathf.Sin(t / 0.15f * Mathf.PI);
            transform.localScale = new Vector3(1f + 0.06f * k, 1f - 0.08f * k, 1f);
            yield return null;
        }
        transform.localScale = Vector3.one;
    }

    /// <summary>
    /// 테슬라: 링이 내려가는 3번째 프레임에 방전 → 대상 + 도탄 bounces 회.
    /// 도탄은 지금 맞은 대상만 빼고 고름 (이전 대상 다시 가능 → 적 2마리면 도탄 수만큼 왕복).
    /// 위로 방출(orbital) = 플레이어 주변 무작위 위치에 낙뢰, 도탄 없음
    /// </summary>
    IEnumerator FireTesla(Enemy first, Vector2 to)
    {
        actionT = 0f;
        Vector2 electrode = (Vector2)transform.position + Vector2.up * (height * 0.9f);
        // 충전 빛 고리 폭(칸의 65%) ≈ 전극 링 폭(타워 키의 약 0.3) → 칸 크기 ≈ 키 × 0.5
        LoopFx.Attach(transform, LeniaFx.TeslaCharge, 10f, Vector2.up * (height * 0.9f), height * 0.5f, 0.45f, 9);
        yield return new WaitForSeconds(2f / FrameFps);   // 링 하강 3번째 프레임
        if (this == null) yield break;

        if (spec.orbital)
        {
            LeniaFx.Bolt(electrode, electrode + Vector2.up * 6f);   // 위로 방출
            if (Player.I == null) yield break;
            Vector2 p = (Vector2)Player.I.transform.position + UnityEngine.Random.insideUnitCircle * OrbitalTeslaArea;
            float r = OrbitalStrikeRadius * rangeScale;
            LeniaFx.Bolt(p + Vector2.up * 8f, p, 0.25f);
            LeniaFx.Spark(p);
            HitDebug.Circle(p, r, HitDebug.BlastColor);
            SkillHits.Circle(p, r, spec.shot(), Enemy.NoKnock, ApplyOnHit);
            yield break;
        }

        Vector2 from = electrode;
        Enemy cur = first != null && first.hp > 0 ? first : null;
        if (cur == null)
        {
            // 난사 · 대상 소실: 그 방향의 가장 가까운 적 (없으면 허공 방전)
            Vector2 probe = (Vector2)transform.position + to.normalized * (Range * rangeScale * 0.5f);
            cur = LeniaFx.Nearest(probe, Range * rangeScale * 0.5f);
            if (cur == null) { LeniaFx.Bolt(from, probe); yield break; }
        }
        var current = new Enemy[1];
        for (int i = 0; i <= spec.bounces && cur != null; i++)
        {
            Vector2 at = cur.transform.position;
            LeniaFx.Bolt(from, at);
            LeniaFx.Spark(at);
            var e = cur;
            e.Hit(spec.shot(), at - from, Enemy.NoKnock);   // 도탄분 = 간접 사용 · 직접 피해 (shot 이 이미 간접 사용)
            ApplyOnHit(e);
            from = at;
            current[0] = e;
            cur = LeniaFx.Nearest(at, LeniaShots.BounceRadius * rangeScale, current);
        }
    }

    // ---------- 터렛 강화 · 과부화 연출 (수치 강화 없음) ----------
    Coroutine boostFx, overloadFx;

    /// <summary>
    /// 터렛 강화: 처음에 스패너가 나사를 조이듯 돌아가고 (1.5초), 강화되어 있는 동안(seconds) 우측 상단에서 기어가 회전.
    /// 다시 걸면 지속시간을 새로 시작
    /// </summary>
    public void Boost(float seconds)
    {
        if (boostFx != null) StopCoroutine(boostFx);
        if (boostSpanner != null) Destroy(boostSpanner);   // 연출 도중 다시 쓰면 이전 스패너 정리
        boostFx = StartCoroutine(BoostRoutine(seconds));
    }

    const float SpannerScrewTime = 1.5f;   // [임시] 스패너 조이기 연출 시간
    GameObject boostGear, boostSpanner, overloadHammer;

    IEnumerator BoostRoutine(float seconds)
    {
        // 강화 표시: 우측 상단 기어 회전 (강화 내내)
        if (boostGear == null)
        {
            boostGear = new GameObject("BoostGear");
            boostGear.transform.SetParent(transform, false);
            boostGear.transform.localPosition = new Vector2(height * 0.38f, height * 0.95f);
            var g = boostGear.AddComponent<SpriteRenderer>();
            g.sprite = LeniaFx.Gear(LeniaFx.BoostGearSize);
            g.sortingOrder = 12;
        }
        StartCoroutine(SpinGear(seconds));

        var go = boostSpanner = new GameObject("BoostSpanner");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector2.up * (height * 0.55f) + Vector2.right * 0.25f;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = LeniaFx.Spanner(LeniaFx.SpannerLength, grip: false);
        s.sortingOrder = 12;
        // 조이기: 90° 돌리고 잠깐 멈췄다 다시 (렌치 손잡이를 끊어 돌리는 느낌)
        for (float t = 0f; t < Mathf.Min(seconds, SpannerScrewTime) && go != null; t += Time.deltaTime)
        {
            float cycle = t % 0.5f;
            float ang = (Mathf.Floor(t / 0.5f) * 90f) + Mathf.SmoothStep(0f, 90f, Mathf.Clamp01(cycle / 0.3f));
            go.transform.localRotation = Quaternion.Euler(0, 0, -ang);
            yield return null;
        }
        if (go != null) Destroy(go);
        boostFx = null;
    }

    float gearLeft;

    /// <summary>강화 기어: 남은 시간 동안 회전, 끝나면 사라짐 (재강화 시 남은 시간만 갱신)</summary>
    IEnumerator SpinGear(float seconds)
    {
        bool running = gearLeft > 0f;
        gearLeft = seconds;
        if (running) yield break;   // 이미 돌고 있는 코루틴이 이어서 처리
        while (gearLeft > 0f && boostGear != null)
        {
            gearLeft -= Time.deltaTime;
            boostGear.transform.Rotate(0, 0, -LeniaFx.BoostGearSpin * Time.deltaTime);
            yield return null;
        }
        gearLeft = 0f;
        if (boostGear != null) Destroy(boostGear);
        boostGear = null;
    }

    /// <summary>강화 중인지 (기어 표시 중)</summary>
    public bool Boosted => gearLeft > 0f;

    /// <summary>터렛 과부화: 망치가 터렛을 내려침 (seconds 동안 반복)</summary>
    public void Overload(float seconds)
    {
        if (overloadFx != null) StopCoroutine(overloadFx);
        if (overloadHammer != null) Destroy(overloadHammer);   // 연출 도중 다시 쓰면 이전 망치 정리
        overloadFx = StartCoroutine(OverloadRoutine(seconds));
    }

    IEnumerator OverloadRoutine(float seconds)
    {
        var go = overloadHammer = new GameObject("OverloadHammer");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector2.up * (height * 0.6f) + Vector2.left * 0.45f;   // 손잡이 끝 = 회전 축
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = LeniaFx.Hammer(LeniaFx.HammerLength * 1.3f, grip: true);
        s.sortingOrder = 12;
        const float period = 0.6f;   // [임시] 내려치는 간격
        for (float t = 0f; t < seconds && go != null; t += Time.deltaTime)
        {
            float c = (t % period) / period;
            // 0~0.6 들어 올림(-10° → 70°), 0.6~0.75 내려침(→ -60°), 이후 정지
            float ang = c < 0.6f ? Mathf.Lerp(-10f, 70f, c / 0.6f) : c < 0.75f ? Mathf.Lerp(70f, -60f, (c - 0.6f) / 0.15f) : -60f;
            go.transform.localRotation = Quaternion.Euler(0, 0, ang);
            if (c >= 0.75f && c - Time.deltaTime / period < 0.75f)
            {
                LeniaFx.Spark((Vector2)transform.position + Vector2.up * (height * 0.65f));
                StartCoroutine(Kick());
            }
            yield return null;
        }
        if (go != null) Destroy(go);
        overloadFx = null;
    }
}
