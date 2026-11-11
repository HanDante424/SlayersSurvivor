using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum TurretKind { Standard, Missile, Tesla }

/// <summary>
/// 레니아가 설치하는 터렛 1개 (표준 · 미사일 · 테슬라). 지속시간이 끝나면 사라짐.
/// 공격: 사거리 안 가장 가까운 적을 향해 방향을 바꾸고 공격 쿨타임마다 발사.
/// 피해는 설치한 스킬이 만들어 줌 (shot 델리게이트) → 그 스킬의 강화 · 전역 피해 규칙이 그대로 적용.
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
    const float TeslaBounceRange = 3f;           // 도탄 대상 탐색 거리
    const float FrameFps = 10f;                  // 반동 · 링 동작 재생 속도 (원본 권장)

    public TurretKind kind;
    float life, fireCd, fireTimer;
    Func<DamageInfo> shot;
    float stun;
    int bounces;
    float rangeScale = 1f;   // 설치한 스킬의 범위 배율 (기본 강화 '범위')

    SpriteRenderer sr;
    Sprite[,] idle, action;
    int dir;
    float actionT = -1f;
    float height;

    public float LifeLeft => life;

    public static Turret Spawn(TurretKind kind, Vector2 at, float duration, float fireCooldown, Func<DamageInfo> shot,
                               float stunSeconds = 0f, int bounces = 0, float rangeScale = 1f)
    {
        var go = new GameObject("Turret_" + kind);
        go.transform.position = at;
        var t = go.AddComponent<Turret>();
        t.kind = kind; t.life = duration; t.fireCd = fireCooldown; t.shot = shot; t.stun = stunSeconds; t.bounces = bounces;
        t.rangeScale = rangeScale;
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
        fireTimer = fireCd;
        Vector2 to = (Vector2)target.transform.position - (Vector2)transform.position;
        dir = LeniaFx.Dir4(to);
        ShowIdle();
        switch (kind)
        {
            case TurretKind.Standard: FireBullet(to); break;
            case TurretKind.Missile: FireMissile(target); break;
            case TurretKind.Tesla: StartCoroutine(FireTesla(target)); break;
        }
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
        BulletProjectile.Spawn(m, to - (m - (Vector2)transform.position), BulletSpeed, Range * rangeScale * 1.2f, shot());   // 포신에서 조준
    }

    void FireMissile(Enemy target)
    {
        StartCoroutine(Kick());
        MissileProjectile.Spawn(Muzzle(0.72f, 0.3f), target, target.transform.position, MissileSpeed, MissileRadius * rangeScale, shot());
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

    /// <summary>테슬라: 링이 내려가는 3번째 프레임에 방전 → 대상 + 도탄 bounces 회 (같은 적 중복 없음)</summary>
    IEnumerator FireTesla(Enemy first)
    {
        actionT = 0f;
        Vector2 electrode = (Vector2)transform.position + Vector2.up * (height * 0.9f);
        // 충전 빛 고리 폭(칸의 65%) ≈ 전극 링 폭(타워 키의 약 0.3) → 칸 크기 ≈ 키 × 0.5
        LoopFx.Attach(transform, LeniaFx.TeslaCharge, 10f, Vector2.up * (height * 0.9f), height * 0.5f, 0.45f, 9);
        yield return new WaitForSeconds(2f / FrameFps);   // 링 하강 3번째 프레임

        var hit = new HashSet<Enemy>();
        Vector2 from = electrode;
        Enemy cur = first != null && first.hp > 0 ? first : LeniaFx.Nearest(transform.position, Range * rangeScale);
        for (int i = 0; i <= bounces && cur != null; i++)
        {
            Vector2 at = cur.transform.position;
            LeniaFx.Bolt(from, at);
            LeniaFx.Spark(at);
            hit.Add(cur);
            var e = cur;
            e.Hit(shot(), at - from, Enemy.NoKnock);
            if (e != null && e.hp > 0 && stun > 0f)
            {
                e.Stun(stun);
                LeniaFx.ShowShocked(e, stun);
            }
            from = at;
            cur = LeniaFx.Nearest(at, TeslaBounceRange * rangeScale, hit);
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
