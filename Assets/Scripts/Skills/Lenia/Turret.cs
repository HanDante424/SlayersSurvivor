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
    /// <summary>특수 강화 전 기본 공격 간격 (공격 간격 감소 90% 상한 기준)</summary>
    public float baseFireCooldown;
    public float rangeScale = 1f;   // 설치한 스킬의 범위 배율 (기본 · 특수 강화 '범위')
    /// <summary>
    /// 공격 1발의 피해 (설치 = 직접 사용의 간접 사용).
    /// 인자 = 외부 부여 '피해량' % (터렛 강화), 외부 부여 '최종 피해량' 배율 (과부화 · 병렬 출력 · 집중 포화)
    /// </summary>
    public Func<float, float, DamageInfo> shot;
    /// <summary>공격 1회의 투사체 수 = 1 + '투사체 증가' (0.2초 간격, 서로 다른 대상)</summary>
    public int shots = 1;
    /// <summary>난사: 대상 대신 무작위 방향 (미사일은 무작위 지점)</summary>
    public bool scatter;
    public int pierce, bounces;
    /// <summary>적중 시 '기절' 초 (테슬라 기본 · 철갑탄 · 고압 전류)</summary>
    public float stun;
    /// <summary>미사일 궤도 폭격 / 테슬라 낙뢰 발생: 플레이어 주변 무작위 위치로 떨어짐 (도탄 없음)</summary>
    public bool orbital;
}

/// <summary>
/// 레니아가 설치하는 터렛 1개 (표준 · 미사일 · 테슬라). 지속시간이 끝나면 사라짐.
/// 공격: 사거리 안 가장 가까운 적을 향해 방향을 바꾸고 발사. 공격 1회 = 투사체 shots 발 (0.2초 간격, 서로 다른 대상).
/// 공격 간격은 마지막 투사체를 쏜 뒤부터 (사용 완료 후 쿨타임).
/// 피해는 설치한 스킬이 만들어 줌 (spec.shot) → 그 스킬의 강화 · 전역 피해 규칙이 그대로 적용.
///
/// 터렛 강화 · 터렛 과부화 (2026-11-12 사용자 규칙):
/// - 둘 다 동시에 적용됨. 같은 효과를 다시 받으면 계수는 곱연산, 지속 시간은 합연산
/// - '공격 간격' 증감은 '쿨타임 감소'처럼: 효과 하나 안에서는 합산, 효과끼리는 곱, 최종 감소 90% 상한
/// - 과부화의 남은 '지속 시간' 증감은 곱연산 (냉각 포함)
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
    public const float OrbitalTeslaArea = 8f;    // 낙뢰 발생(Lv15): 플레이어 주변 낙뢰 반경
    const float OrbitalStrikeRadius = 1.3f * 0.4f;   // [임시] 낙뢰 1발 피해 반경 (1.3 → -60%, 2026-11-12 사용자)
    const float DropHeight = 7f, DropSpeed = 14f;   // [임시] 궤도 폭격 낙하 (포격 실시와 같은 값)
    const float MaxIntervalCut = 0.9f;           // '공격 간격' 감소 상한 (쿨타임 감소와 같은 90%)

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
        if (spec.baseFireCooldown <= 0f) spec.baseFireCooldown = spec.fireCooldown;
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
        // 설치 표시: 착지 먼지 (가운데 = 터렛 자리는 비어 있음). 터렛(8) 바로 아래
        var deploy = LeniaFx.TurretDeploy;
        if (deploy != null) SpriteEffect.Spawn(deploy, 12.5f, null, at, 0f, false, 7, LeniaFx.TurretFxSize);
        else Fx.Circle(at, 0.6f, Fx.FireBright, 0.25f);
        return t;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void ShowIdle()
    {
        if (idle != null) sr.sprite = idle[dir, 0];
    }

    // ---------- 강화 상태 ----------
    TurretBoost boostSrc;
    float boostLeft, boostDmgMul = 1f, boostIntervalMul = 1f, boostRangeMul = 1f;
    int boostShots;
    bool autoMaintDone;

    TurretOverload overSrc;
    float overLeft, overFinalMul = 1f, overIntervalMul = 1f;
    int overShots;

    // 집중 포화 (과부화 Lv15): 이 터렛의 최종 피해량 스택
    int focusStacks;
    float focusUntil;

    /// <summary>터렛 강화 적용 중</summary>
    public bool Boosted => boostLeft > 0f;
    /// <summary>터렛 과부화 적용 중</summary>
    public bool Overloaded => overLeft > 0f;

    float AttackRange => Range * rangeScale * (Boosted ? boostRangeMul : 1f);
    int Shots => Mathf.Max(1, spec.shots + (Boosted ? boostShots : 0) + (Overloaded ? overShots : 0));

    float FireInterval
    {
        get
        {
            float v = spec.fireCooldown * (Boosted ? boostIntervalMul : 1f) * (Overloaded ? overIntervalMul : 1f);
            return Mathf.Max(spec.baseFireCooldown * (1f - MaxIntervalCut), v);
        }
    }

    /// <summary>공격 1발의 피해 (강화 · 과부화 몫 포함)</summary>
    DamageInfo Shot()
    {
        float pct = Boosted ? boostDmgMul - 1f : 0f;
        float fin = 1f;
        if (Overloaded) fin *= overFinalMul;
        if (Boosted && boostSrc != null && boostSrc.Parallel) fin *= 1f + TurretBoost.ParallelPerTurret * BoostedCount();
        if (focusStacks > 0 && focusUntil > Time.time) fin *= 1f + TurretOverload.FocusPerStack * focusStacks;
        return spec.shot(pct, fin);
    }

    static int BoostedCount()
    {
        int n = 0;
        foreach (var t in All) if (t != null && t.Boosted) n++;
        return n;
    }

    /// <summary>터렛 강화 1회 적용 (같은 효과 재적용 = 계수 곱, 지속 시간 합)</summary>
    public void Boost(TurretBoost src)
    {
        boostSrc = src;
        if (Boosted)
        {
            boostLeft += src.BuffDuration;
            boostDmgMul *= 1f + src.DamagePct;
            boostIntervalMul *= 1f - src.IntervalCut;
            boostRangeMul *= src.RangeMul;
            boostShots += src.ExtraShots;
        }
        else
        {
            boostLeft = src.BuffDuration;
            boostDmgMul = 1f + src.DamagePct;
            boostIntervalMul = 1f - src.IntervalCut;
            boostRangeMul = src.RangeMul;
            boostShots = src.ExtraShots;
        }
        life += src.TurretLifeAdd;                                         // 내구성 개선
        if (src.AutoMaint && !autoMaintDone) { autoMaintDone = true; life += src.BuffDuration; }   // 자동 관리 프로토콜 (터렛당 1회)
        BoostFx();
    }

    /// <summary>터렛 과부화 1회 적용 (같은 효과 재적용 = 계수 곱, 지속 시간 합 · 남은 지속 시간 곱)</summary>
    public void Overload(TurretOverload src)
    {
        overSrc = src;
        if (Overloaded)
        {
            overLeft += src.BuffDuration;
            overFinalMul *= src.FinalMul;
            overIntervalMul *= 1f - src.IntervalCut;
            overShots += src.ExtraShots;
        }
        else
        {
            overLeft = src.BuffDuration;
            overFinalMul = src.FinalMul;
            overIntervalMul = 1f - src.IntervalCut;
            overShots = src.ExtraShots;
        }
        life *= src.LifeMul;
        OverloadFx();
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        if (boostLeft > 0f) boostLeft -= dt;
        if (overLeft > 0f) overLeft -= dt;
        life -= dt;
        if (life <= 0f) { Expire(); return; }

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
        var target = LeniaFx.Nearest(transform.position, AttackRange);
        if (target == null) { fireTimer = 0f; return; }   // 대상이 없으면 쿨타임을 소모하지 않음
        int n = Shots;
        fireTimer = FireInterval + (n - 1) * LeniaShots.Interval;   // 공격 간격 = 마지막 투사체 이후부터
        StartCoroutine(Attack(target, n));
    }

    /// <summary>지속 시간 종료 = 소멸. 과부화 중이면 재활용 · 자폭 프로토콜</summary>
    void Expire()
    {
        Vector2 at = transform.position;
        if (Overloaded && overSrc != null)
        {
            if (overSrc.Recycle) ReduceTurretCooldowns(TurretOverload.RecycleRatio);
            if (overSrc.SelfDestruct)
            {
                float r = overSrc.SelfDestructRadius;
                var d = overSrc.SelfDestructDamage(Shot().amount * TurretOverload.SelfDestructRatio);
                LeniaFx.Explode(at, r);
                HitDebug.Circle(at, r, HitDebug.BlastColor);
                SkillHits.Circle(at, r, d, 2f);
                CameraFollow.Shake(0.1f, 0.12f);
            }
        }
        // 소멸: 분해 연기 · 부품 조각 (터렛 높이 중간에서)
        var despawn = LeniaFx.TurretDespawn;
        if (despawn != null) SpriteEffect.Spawn(despawn, 10f, null, at + Vector2.up * (height * 0.35f), 0f, false, 9, LeniaFx.TurretFxSize);
        else Fx.Disc(at, 0.5f, new Color(0.7f, 0.7f, 0.7f, 0.5f), 0.25f);
        Destroy(gameObject);
    }

    /// <summary>모든 '설치' 스킬의 남은 쿨타임 감소</summary>
    public static void ReduceTurretCooldowns(float ratio)
    {
        if (Player.I == null) return;
        foreach (var s in Player.I.skills)
            if (s is TurretSkill) s.ReduceRemainingCooldown(ratio);
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
            else t = LeniaShots.NextTarget(transform.position, AttackRange, used);
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

    /// <summary>
    /// 이 터렛의 직접 피해 적중 1회 뒤 (피해 → 적중 시 효과 → 상태이상 순, 상태이상이 가장 늦게).
    /// 동기화 · 집중 포화 · 융해 · 기절
    /// </summary>
    void ApplyOnHit(Enemy e)
    {
        if (this != null)
        {
            if (Boosted && boostSrc != null && boostSrc.Sync) ReduceTurretCooldowns(TurretBoost.SyncRatio);   // 동기화
            if (Overloaded && overSrc != null && overSrc.FocusFire)                                             // 집중 포화
            {
                if (focusUntil <= Time.time) focusStacks = 0;
                focusStacks = Mathf.Min(TurretOverload.FocusMaxStacks, focusStacks + 1);
                focusUntil = Time.time + TurretOverload.FocusDuration;
            }
        }
        if (e == null || e.hp <= 0) return;
        if (this != null && Overloaded && overSrc != null && overSrc.MeltOn) Melt.Apply(e);   // 융해
        if (spec.stun > 0f)
        {
            e.Stun(spec.stun);
            if (kind == TurretKind.Tesla) LeniaFx.ShowShocked(e, spec.stun);
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
        var mods = new ShotMods
        {
            pierce = spec.pierce, bounces = spec.bounces, bounceRange = LeniaShots.BounceRadius * rangeScale,
            onHit = (e, wasStunned, d) => ApplyOnHit(e),
        };
        // 포신에서 조준 (to 는 발밑 기준 → 포신 높이만큼 보정)
        Vector2 aim = spec.scatter ? to : to - (m - (Vector2)transform.position);
        LeniaProjectile.Spawn(LeniaFx.Bullet, m, aim, BulletSpeed, 0f, AttackRange * 1.2f, 0.12f, Shot(), 0.5f, mods);
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
        Vector2 point = target != null ? (Vector2)target.transform.position : (Vector2)transform.position + to.normalized * (AttackRange * 0.7f);
        MissileProjectile.Spawn(m, target, point, MissileSpeed, r, Shot(), onHit: ApplyOnHit);
    }

    IEnumerator OrbitalDrop(float r)
    {
        var dmg = Shot();
        yield return new WaitForSeconds(0.3f);
        if (Player.I == null) yield break;
        Vector2 point = (Vector2)Player.I.transform.position + UnityEngine.Random.insideUnitCircle * OrbitalMissileArea;
        LeniaFx.TargetMarker(point, r, DropHeight / DropSpeed);   // 낙하 지점 표시
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
            LeniaFx.LightningStrike(p, r);   // 피격 범위 크기 그대로 표시
            HitDebug.Circle(p, r, HitDebug.BlastColor);
            SkillHits.Circle(p, r, Shot(), Enemy.NoKnock, ApplyOnHit);
            yield break;
        }

        Vector2 from = electrode;
        Enemy cur = first != null && first.hp > 0 ? first : null;
        if (cur == null)
        {
            // 난사 · 대상 소실: 그 방향의 가장 가까운 적 (없으면 허공 방전)
            Vector2 probe = (Vector2)transform.position + to.normalized * (AttackRange * 0.5f);
            cur = LeniaFx.Nearest(probe, AttackRange * 0.5f);
            if (cur == null) { LeniaFx.Bolt(from, probe); yield break; }
        }
        var current = new Enemy[1];
        for (int i = 0; i <= spec.bounces && cur != null; i++)
        {
            Vector2 at = cur.transform.position;
            LeniaFx.Bolt(from, at);
            LeniaFx.Spark(at);
            var e = cur;
            e.Hit(Shot(), at - from, Enemy.NoKnock);   // 도탄분 = 간접 사용 · 직접 피해 (shot 이 이미 간접 사용)
            ApplyOnHit(e);
            from = at;
            current[0] = e;
            cur = LeniaFx.Nearest(at, LeniaShots.BounceRadius * rangeScale, current);
        }
    }

    // ---------- 터렛 강화 · 과부화 연출 ----------
    const float SpannerScrewTime = 1.5f;   // [임시] 스패너 조이기 연출 시간
    GameObject boostGear, boostSpanner, overloadHammer;
    Coroutine screwFx, gearFx, hammerFx;

    /// <summary>
    /// 터렛 강화 연출: 적용할 때마다 스패너가 나사를 조이듯 돌아가고 (1.5초),
    /// 강화되어 있는 동안 우측 상단에서 기어가 회전
    /// </summary>
    void BoostFx()
    {
        if (screwFx != null) StopCoroutine(screwFx);
        if (boostSpanner != null) Destroy(boostSpanner);   // 연출 도중 다시 쓰면 이전 스패너 정리
        screwFx = StartCoroutine(ScrewRoutine());
        if (gearFx == null) gearFx = StartCoroutine(SpinGear());
    }

    IEnumerator ScrewRoutine()
    {
        var go = boostSpanner = new GameObject("BoostSpanner");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector2.up * (height * 0.55f) + Vector2.right * 0.25f;
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = LeniaFx.Spanner(LeniaFx.SpannerLength, grip: false);
        s.sortingOrder = 12;
        // 조이기: 90° 돌리고 잠깐 멈췄다 다시 (렌치 손잡이를 끊어 돌리는 느낌)
        for (float t = 0f; t < SpannerScrewTime && go != null; t += Time.deltaTime)
        {
            float cycle = t % 0.5f;
            float ang = (Mathf.Floor(t / 0.5f) * 90f) + Mathf.SmoothStep(0f, 90f, Mathf.Clamp01(cycle / 0.3f));
            go.transform.localRotation = Quaternion.Euler(0, 0, -ang);
            yield return null;
        }
        if (go != null) Destroy(go);
        screwFx = null;
    }

    /// <summary>강화 기어: 강화가 남아 있는 동안 회전, 끝나면 사라짐</summary>
    IEnumerator SpinGear()
    {
        boostGear = new GameObject("BoostGear");
        boostGear.transform.SetParent(transform, false);
        boostGear.transform.localPosition = new Vector2(height * 0.38f, height * 0.95f);
        var g = boostGear.AddComponent<SpriteRenderer>();
        g.sprite = LeniaFx.Gear(LeniaFx.BoostGearSize);
        g.sortingOrder = 12;
        while (Boosted && boostGear != null)
        {
            boostGear.transform.Rotate(0, 0, -LeniaFx.BoostGearSpin * Time.deltaTime);
            yield return null;
        }
        if (boostGear != null) Destroy(boostGear);
        boostGear = null;
        gearFx = null;
    }

    /// <summary>터렛 과부화 연출: 과부화가 남아 있는 동안 망치가 터렛을 반복해서 내려침</summary>
    void OverloadFx()
    {
        if (hammerFx == null) hammerFx = StartCoroutine(OverloadRoutine());
    }

    IEnumerator OverloadRoutine()
    {
        var go = overloadHammer = new GameObject("OverloadHammer");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector2.up * (height * 0.6f) + Vector2.left * 0.45f;   // 손잡이 끝 = 회전 축
        var s = go.AddComponent<SpriteRenderer>();
        s.sprite = LeniaFx.Hammer(LeniaFx.HammerLength * 1.3f, grip: true);
        s.sortingOrder = 12;
        const float period = 0.6f;   // [임시] 내려치는 간격
        for (float t = 0f; Overloaded && go != null; t += Time.deltaTime)
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
        hammerFx = null;
    }
}
