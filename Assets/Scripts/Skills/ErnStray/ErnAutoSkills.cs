using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 자동 스킬 6종 (기획서 4.2)
// 등급(Power/Tier)은 기획서 그대로, 숫자는 SkillTuning 임시 환산표에서.
// 기획서에 등급조차 없는 값은 각 클래스 맨 위 "[임시]" 상수로 모아 둠.
// Lv.5·10·15 특수 강화: 파이어 슬래시·솔라 슬래시만 (특수 강화 기획 v1.0). 나머지는 기획 미설계.

/// <summary>
/// 에른 스트레이 스킬 전용 스프라이트 시트 이펙트 (FireForge 제작).
/// 시트가 없으면 Load 가 null → 각 스킬이 기존 도형 이펙트(Fx.*)로 대체.
/// </summary>
public static class ErnSkillFx
{
    public const string FireSlashSheet = "ErnStray_SkillFireSlash";     // 2행 x 4열, 7프레임 (마지막 칸 비어 있음)
    public const string TripleSlashSheet = "ErnStray_SkillTripleSlash"; // 2행 x 5열
    public const string SolarSlashSheet = "ErnStray_SkillSolarSlash";   // 4행 x 3열

    public static Sprite[] FireSlash => SpriteEffect.LoadFrames(FireSlashSheet, 2, 4, 1f, 7);
    public static Sprite[] TripleSlash => SpriteEffect.LoadFrames(TripleSlashSheet, 2, 5, 1f);
    public static Sprite[] SolarSlash => SpriteEffect.LoadFrames(SolarSlashSheet, 4, 3, 1f);

    /// <summary>
    /// 오른쪽을 향해 그려진 시트를 dir 방향으로 띄움. 왼쪽 방향은 뒤집힌 그림이 되지 않도록 좌우 반전.
    /// width = 한 칸의 월드 가로 길이 (LoadFrames 를 1유닛 기준으로 잘랐으므로 scale 로 사용).
    /// </summary>
    public static SpriteEffect SpawnDirected(Sprite[] frames, float fps, Vector2 at, Vector2 dir, float width)
    {
        if (frames == null) return null;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bool flip = dir.x < 0f;
        if (flip) angle -= 180f;
        return SpriteEffect.Spawn(frames, fps, null, at, angle, flip, 11, width);
    }
}

/// <summary>
/// A01 파이어 슬래시 (2026-11-11 스킬 설명 중간본 = 최신): 에른 기본 무장 · 자동 · 초기 피해량 10 · 쿨타임 2초 · 바라보는 방향.
/// 대상이 없어도 쿨타임마다 휘두름. 판정 크기는 SkillTuning.fireSlash 에서 플레이 테스트로 조정.
///
/// 한 번 사용 = "참격 생성 단계" 1회 (조건 없는 추가 공격은 모두 여기서 끝남):
///   ① Lv.5 방향 (추적 1 / 난사 3 / 양방향 2) → ② Lv.15 삼연발 (방향마다 좌우 60°, ×3)
///   → ③ 참격마다 Lv.10 검기 1발 또는 연속 공격 2회 (추가 참격은 다시 추가 공격을 만들지 않음)
///   예) 난사 + 연속 공격 생성 + 삼연발 = 3 × 3 × (1 + 2) = 27 참격
/// 직접 사용 = 첫 방향 참격 1개. 난사 · 양방향의 나머지 방향, 검기, 연속 공격, 삼연발, 죽음의 메아리 = 간접 사용
/// 죽음의 메아리만 "새 처치"라는 조건으로 새 참격을 만들며, 처치가 이어지는 동안 연쇄를 허용.
/// </summary>
public class FireSlash : AutoSkill
{
    // 기본 수치 (2026-11-11 스킬 설명 중간본: 초기 피해량 10 · 쿨타임 2초)
    const float BaseCooldown = 2f;
    const float BaseDamage = 10f;

    const float FxFps = 24f;            // [임시] 시트 재생 속도 (7프레임 → 약 0.3초)
    const float FxWidth = 3f;         // [임시] 이펙트 한 칸 가로 = 범위 x 이 값
    const float FxForward = 1.2f;      // [임시] 이펙트 중심 = 플레이어 앞 범위 x 이 값

    // 특수 강화 id
    public const string Track = "F5A", Scatter = "F5B", BothWays = "F5C";
    public const string Wave = "F10A", Chain = "F10B", Quick = "F10C";
    public const string TripleShot = "F15A", Heavy = "F15B", DeathEcho = "F15C";

    // 특수 강화 확정 수치 (기획 v1.0)
    const int ScatterCount = 3;
    const float TripleAngle = 60f;
    const float ChainAngle = 120f;      // 연속 공격 생성: 공격 방향 이외 120° 간격 2방향 · 죽음의 메아리: 120° 간격 3방향
    const float QuickMul = 0.5f;        // 빠른 공격: 사용 후 남은 쿨타임 절반 감소
    const float HeavyFinal = 1.0f;      // 묵직한 일격: 최종 피해량 +100%
    const float HeavyRange = 0.5f;      //               공격 범위 +50%
    const float HeavyPerReduction = 2f; //               무효화된 쿨타임 감소 1%당 최종 피해량 +2% (+100% 와 합쳐 최종 배율 1개)

    static SkillHitbox Hit => T.fireSlash;
    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => RangeOf(Hit.radius);

    // ---------- 특수 강화 목록 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Track, title = "대상 추적", desc = "가장 가까운 대상을 향해 공격합니다." },
                new SpecialUpgradeOption { id = Scatter, title = "난사", keywords = new[] { "간접 사용", "직접 피해" }, desc = "무작위 3방향으로 공격합니다." },
                new SpecialUpgradeOption { id = BothWays, title = "양방향 공격", keywords = new[] { "간접 사용", "직접 피해" }, desc = "바라보는 곳 반대편도 공격합니다." },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Wave, title = "검기 발사", keywords = new[] { "간접 사용", "직접 피해", "원거리" }, desc = "같은 피해의 검기가 추가로 날아가 피해를 입힙니다." },
                new SpecialUpgradeOption { id = Chain, title = "연속 공격 생성", keywords = new[] { "간접 사용", "직접 피해" }, desc = "공격 방향 이외에 2개의 공격이 추가 발생합니다." },
                new SpecialUpgradeOption { id = Quick, title = "빠른 공격", desc = "사용 후 남은 쿨타임이 절반 감소합니다." },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = TripleShot, title = "삼연발", keywords = new[] { "간접 사용", "직접 피해" }, desc = "공격 방향 양쪽에 2회의 추가 공격이 발생합니다." },
                new SpecialUpgradeOption
                {
                    id = Heavy, title = "묵직한 일격",
                    specs = new[] { "'공격 범위' +50%", "'최종 피해량' +100%", "'쿨타임 감소' '무효화'", "'쿨타임 감소' +1% 당 '최종 피해량' +2%" },
                },
                new SpecialUpgradeOption { id = DeathEcho, title = "죽음의 메아리", keywords = new[] { "간접 사용", "직접 피해" }, desc = "적 사망시 해당 위치에서 같은 피해의 공격이 3회 발생합니다." },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 쿨타임 · 피해 ----------
    // 빠른 공격 = 사용 직후 남은 쿨타임 ×0.5 (남은 쿨타임 감소 → 90% 상한과 무관, AfterFire)
    // 묵직한 일격 = 쿨타임 감소 전부 무효화 (빠른 공격 포함) → 무효화된 최종 감소율 1%당 최종 피해량 +2%

    /// <summary>묵직한 일격이 무효화한 최종 쿨타임 감소율 (0.75 = 75%, 기본 강화 · 패시브 · 빠른 공격 포함, 실시간)</summary>
    public float HeavyConvertedReduction
    {
        get
        {
            float m = GeneralCooldownMultiplier * (HasSpecial(Quick) ? QuickMul : 1f);
            return Mathf.Round(Mathf.Max(0f, 1f - m) * 10000f) / 10000f;
        }
    }

    protected override float CooldownMultiplier => HasSpecial(Heavy) ? 1f : GeneralCooldownMultiplier;
    /// <summary>묵직한 일격: '최종 피해량' +100% 와 전환분(1%당 +2%)을 묵직한 일격 안에서 합쳐 최종 배율 1개 (예: 75% → 1 + 1 + 1.5 = ×3.5)</summary>
    protected override float SpecialFinalDamageMul => HasSpecial(Heavy) ? 1f + HeavyFinal + HeavyConvertedReduction * HeavyPerReduction : 1f;
    protected override float SpecialRangeBonus => HasSpecial(Heavy) ? HeavyRange : 0f;

    /// <summary>빠른 공격: 사용 후 남은 쿨타임 절반 (묵직한 일격이면 쿨타임 감소 무효화)</summary>
    protected override void AfterFire()
    {
        if (HasSpecial(Quick) && !HasSpecial(Heavy)) ReduceRemainingCooldown(1f - QuickMul);
    }

    // ---------- 발동 ----------
    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        var dirs = BaseDirections(from);

        // 수평베기 모션은 첫 방향으로 1번만 (시트가 없으면 모션의 기본 이펙트가 대신)
        var frames = ErnSkillFx.FireSlash;
        P.PlayAttack(dirs[0], baseFx: frames == null);

        // 첫 방향 = 직접 사용, 난사 · 양방향의 나머지 방향 = 간접 사용 (같은 사용 번호)
        var use = DirectCast;
        for (int i = 0; i < dirs.Count; i++) Strike(from, dirs[i], i == 0 ? use : use.AsIndirect);
        if (HasSpecial(TripleShot)) StartCoroutine(TripleAfter(from, dirs, use));
        return true;
    }

    /// <summary>Lv.5 특수 강화에 따른 기본 공격 방향</summary>
    List<Vector2> BaseDirections(Vector2 from)
    {
        var list = new List<Vector2>();
        if (HasSpecial(Scatter))
            for (int i = 0; i < ScatterCount; i++) list.Add(Random.insideUnitCircle.normalized);
        else if (HasSpecial(Track))
        {
            var t = Nearest(from, T.fireTrackRange);
            list.Add(t != null ? ((Vector2)t.transform.position - from).normalized : P.Facing);
        }
        else
        {
            list.Add(P.Facing);
            if (HasSpecial(BothWays)) list.Add(-P.Facing);
        }
        for (int i = 0; i < list.Count; i++) if (list[i].sqrMagnitude < 0.001f) list[i] = P.Facing;
        return list;
    }

    /// <summary>삼연발: 원래 공격 이후 각 방향의 좌우 60°</summary>
    IEnumerator TripleAfter(Vector2 from, List<Vector2> dirs, CastRef use)
    {
        if (T.fireTripleDelay > 0) yield return new WaitForSeconds(T.fireTripleDelay);
        if (Blocked) yield break;   // 지연 중에 F7 / 스킬 사용 금지 → 예약된 추가 공격도 취소
        foreach (var d in dirs)   // 삼연발 = 간접 사용 (같은 사용 번호)
        {
            Strike(from, Rotate(d, TripleAngle), use.AsIndirect);
            Strike(from, Rotate(d, -TripleAngle), use.AsIndirect);
        }
    }

    /// <summary>
    /// 발동 단계의 참격 1회 + Lv.10 추가 공격. 추가 참격(SlashOnce)은 더 이상 증식하지 않음.
    /// cast: 이 참격의 사용 구분 (직접 · 간접). 검기 · 연속 공격 = 항상 간접 사용 (사용 번호는 이어받음 — 메아리면 0)
    /// </summary>
    void Strike(Vector2 from, Vector2 dir, CastRef cast)
    {
        SlashOnce(from, dir, cast);
        if (HasSpecial(Wave)) StartCoroutine(WaveAfter(from, dir, cast.AsIndirect));
        if (HasSpecial(Chain)) StartCoroutine(ChainAfter(from + dir * (Hit.forward * RangeScale * T.fireChainOffset), dir, cast.AsIndirect));
    }

    /// <summary>참격 1개 (이펙트 + 판정). 추가 공격 없음</summary>
    void SlashOnce(Vector2 from, Vector2 dir, CastRef cast)
    {
        var frames = ErnSkillFx.FireSlash;
        if (frames != null)
            ErnSkillFx.SpawnDirected(frames, FxFps, from + dir * (Radius * FxForward), dir, Radius * FxWidth);
        else
            Fx.Arc(from, dir, Radius, Fx.Fire, 0.15f);
        SkillHits.Directed(Hit, from, dir, RangeScale, MakeDamage(BaseDamage, cast));   // 모양은 SkillTuning.fireSlash
    }

    IEnumerator WaveAfter(Vector2 from, Vector2 dir, CastRef cast)
    {
        if (T.fireWaveDelay > 0) yield return new WaitForSeconds(T.fireWaveDelay);
        if (Blocked) yield break;
        FireWave.Spawn(from, dir, T.fireWaveSpeed, T.fireWaveDistance * RangeScale,
                       Hit.width * RangeScale * T.fireWaveWidth * 0.5f, MakeDamage(BaseDamage, cast),
                       Enemy.KnockSpeed(Hit.knockback), ErnSkillFx.FireSlash, Radius * FxWidth);
    }

    IEnumerator ChainAfter(Vector2 at, Vector2 dir, CastRef cast)
    {
        if (T.fireChainDelay > 0) yield return new WaitForSeconds(T.fireChainDelay);
        if (Blocked) yield break;
        for (int k = 1; k <= 2; k++) SlashOnce(at, Rotate(dir, ChainAngle * k), cast);   // 공격 방향 이외 2방향 (120° 간격)
    }

    // ---------- 죽음의 메아리 (처치 조건 연쇄) ----------
    void OnEnable() => Enemy.Killed += OnEnemyKilled;
    protected override void OnDisable() { base.OnDisable(); Enemy.Killed -= OnEnemyKilled; echoQueue.Clear(); }

    /// <summary>예약된 메아리 1회: 사망 위치 · 발동 가능 시각 · 예약된 프레임</summary>
    struct PendingEcho { public Vector2 at; public float readyAt; public int frame; }
    readonly Queue<PendingEcho> echoQueue = new Queue<PendingEcho>();
    float lastEchoReadyAt;

    /// <summary>F7 자동 스킬 금지 / 치트 패널 스킬별 사용 금지</summary>
    bool Blocked => DebugCheats.NoAutoSkills || CheatDisabled;

    void OnEnemyKilled(Enemy e)
    {
        if (!HasSpecial(DeathEcho) || Blocked) return;
        // 적 1마리 사망 = 메아리 1회 (Enemy.Die 는 적마다 한 번만) → 처치가 있어야만 생성되므로 무한 생성 없음.
        // 메아리 참격에도 Lv.5 방향 강화 + Lv.10 추가 공격(검기·연속 공격 생성)을 적용해 최대한 많이 발생
        // Play 중 fireEchoDelay 를 줄여도 예약 순서가 뒤집히지 않게 앞 예약보다 이르지 않도록
        float readyAt = Mathf.Max(Time.time + T.fireEchoDelay, lastEchoReadyAt);
        lastEchoReadyAt = readyAt;
        echoQueue.Enqueue(new PendingEcho { at = e.transform.position, readyAt = readyAt, frame = Time.frameCount });
    }

    /// <summary>
    /// Tick 은 Playing 상태에서만 불림 → 레벨업 화면 등 일시정지 중에는 메아리가 나가지 않음.
    /// 예약된 다음 프레임 이후에만 발동 (연쇄가 한 프레임 안에서 쌓이지 않음),
    /// 한 프레임에 최대 fireEchoMaxPerFrame 회. 남은 메아리는 버리지 않고 다음 프레임으로 (총량 유지)
    /// </summary>
    public override void Tick(float dt)
    {
        int budget = Mathf.Max(1, T.fireEchoMaxPerFrame);
        while (budget > 0 && echoQueue.Count > 0)
        {
            var next = echoQueue.Peek();
            if (next.frame >= Time.frameCount || next.readyAt > Time.time) break;   // 예약 순서 = 발동 순서
            echoQueue.Dequeue();
            if (Blocked || !HasSpecial(DeathEcho)) continue;   // 발동 직전에 다시 확인 → 금지된 뒤에는 버림
            foreach (var d in EchoDirections(next.at)) Strike(next.at, d, CastRef.Indirect);   // 참격 + 검기 / 연속 공격 생성 (간접 발동)
            budget--;
        }
        base.Tick(dt);
    }

    /// <summary>
    /// 메아리 3방향(120°)에 Lv.5 특수 강화 적용 [해석]
    ///  대상 추적: 기준 방향 = 사망 위치에서 가장 가까운 적 (없으면 무작위) → 3방향
    ///  양방향 공격: 3방향 + 각 정반대 = 6방향
    ///  난사: 메아리 → 난사 순서. 120° 3방향을 먼저 정하고, 방향마다 ± fireEchoScatterSpread 안 무작위 3회 → 9방향
    ///        (기본 60° = 각 방향이 맡은 120° 칸 전체 → 세 칸에 3개씩, 한쪽으로 몰리지 않음)
    /// </summary>
    List<Vector2> EchoDirections(Vector2 at)
    {
        var list = new List<Vector2>();
        Vector2 d0 = Random.insideUnitCircle.normalized;   // [임시] 기준 각도 미정 → 무작위
        if (HasSpecial(Track))
        {
            var t = Nearest(at, T.fireTrackRange);
            if (t != null && ((Vector2)t.transform.position - at).sqrMagnitude > 0.0001f)
                d0 = ((Vector2)t.transform.position - at).normalized;
        }
        if (d0.sqrMagnitude < 0.001f) d0 = Vector2.right;

        for (int k = 0; k < 3; k++)
        {
            Vector2 d = Rotate(d0, ChainAngle * k);
            if (HasSpecial(Scatter))
                for (int i = 0; i < ScatterCount; i++)
                    list.Add(Rotate(d, Random.Range(-T.fireEchoScatterSpread, T.fireEchoScatterSpread)));
            else
            {
                list.Add(d);
                if (HasSpecial(BothWays)) list.Add(-d);
            }
        }
        return list;
    }

    static Vector2 Rotate(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }
}

/// <summary>
/// 파이어 슬래시 F10-A 검기: 직선으로 날아가며 닿는 적마다 1번씩 피해 (관통).
/// 속도·사거리는 기획 미정 → SkillTuning [임시].
/// </summary>
public class FireWave : MonoBehaviour
{
    static readonly List<Enemy> buf = new List<Enemy>();
    readonly HashSet<Enemy> hitOnce = new HashSet<Enemy>();
    Vector2 dir;
    float speed, distLeft, halfWidth, knock, fxTime, debugTimer;
    DamageInfo dmg;
    Sprite[] frames;
    SpriteRenderer sr;

    public static void Spawn(Vector2 from, Vector2 dir, float speed, float distance, float halfWidth,
                             DamageInfo dmg, float knock, Sprite[] frames, float fxWidth)
    {
        var go = new GameObject("FireWave");
        go.transform.position = from;
        var w = go.AddComponent<FireWave>();
        w.dir = dir.normalized; w.speed = speed; w.distLeft = distance;
        w.halfWidth = halfWidth; w.dmg = dmg; w.knock = knock; w.frames = frames;
        if (frames != null)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            bool flip = dir.x < 0f;
            if (flip) angle -= 180f;
            go.transform.rotation = Quaternion.Euler(0, 0, angle);
            go.transform.localScale = Vector3.one * fxWidth * 0.8f;
            w.sr = go.AddComponent<SpriteRenderer>();
            w.sr.sprite = frames[Mathf.Min(3, frames.Length - 1)];
            w.sr.flipX = flip;
            w.sr.sortingOrder = 11;
        }
        else Fx.Box(from, dir, distance, halfWidth * 2f, Fx.FireBright, distance / Mathf.Max(0.1f, speed));
    }

    void Update()
    {
        float step = speed * Time.deltaTime;
        transform.position += (Vector3)(dir * step);
        distLeft -= step;
        Vector2 pos = transform.position;

        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0 || hitOnce.Contains(e)) continue;
            float rr = halfWidth + e.radius;
            if (((Vector2)e.transform.position - pos).sqrMagnitude > rr * rr) continue;
            hitOnce.Add(e);
            e.Hit(dmg, dir, knock);
        }

        // 날아가는 동안 중간 프레임(3~5)을 반복 재생
        if (sr != null && frames != null && frames.Length > 5)
        {
            fxTime += Time.deltaTime;
            sr.sprite = frames[3 + (int)(fxTime * 18f) % 3];
        }
        if (HitboxView.Show && (debugTimer -= Time.deltaTime) <= 0f)
        {
            debugTimer = 0.06f;
            HitDebug.Circle(pos, halfWidth, HitDebug.SlashColor);
        }
        if (distLeft <= 0f) Destroy(gameObject);
    }
}

/// <summary>
/// A02 솔라 슬래시 (기획 v0.2 + 특수 강화 기획 v1.0): 자동 · 플레이어 중심 전방위.
/// 베기(피해 5) → 후속 폭발(피해 12 + 넉백). 두 피해는 별개 타격.
///
/// 넉백 범위: 후속 폭발이 적을 밀어내는 원. 기본 = 폭발 판정 원과 같고, 넉백 범위 증가(충격 발산·흑점 폭발·초신성)는
///   이 원만 키움 → 폭발 피해 원 밖이라도 넉백 범위 안이면 피해 없이 밀려남.
/// 타오름: 후속타 대신 5초 지속 공격 (매초 기존 총 피해 17). 넉백 범위 증가율은 실제 넉백에 쓰지 않고 범위·피해로 변환.
/// </summary>
public class SolarSlash : AutoSkill
{
    // 기본 수치 (2026-11-11 스킬 설명 중간본: 베기 5 · 폭발 12 · 쿨타임 5초)
    const float BaseCooldown = 5f;
    const float SlashDamage = 5f;
    const float ExplosionDamage = 12f;

    const float FxFps = 20f;            // [임시] 12프레임 → 0.6초 (원형 베기 → 태양 → 폭발)
    const float FxWidth = 2.6f;         // [임시] 한 칸 가로 = 폭발 범위 x 이 값 (마지막 폭발 고리 ≈ 판정 원)
    const int FxBlastFrom = 8;          // [임시] 시트에서 폭발 부분이 시작하는 프레임 (추가 폭발·흑점 폭발 연출용)

    // 특수 강화 id
    public const string Shock = "S5A", Concussion = "S5B", SlowRecover = "S5C";
    public const string Echo = "S10A", Burn = "S10B", Sunspot = "S10C";
    public const string Multi = "S15A", Supernova = "S15B", Flare = "S15C";

    // 특수 강화 확정 수치 (기획 v1.0)
    const float ShockKnockRange = 1.0f;     // 충격 발산: 넉백 범위 +100%
    const float StunSeconds = 1f;           // 뇌진탕: 기절 1초
    const float SlowMul = 0.5f, SlowSeconds = 2f;   // 느린 회복 · 플레어: 2초간 이동속도 50% 감소
    const float EchoCooldown = 0.25f;       // 메아리: 쿨타임 +25%
    const float BurnDuration = 5f, BurnTick = 1f;   // 타오름: 5초간 매초
    const float BurnRange = 0.5f;           //         공격 범위 +50%
    const float BurnRangePerKnock = 1f;     //         넉백 범위 5%당 공격 범위 +5%
    const float BurnDamagePerKnock = 0.2f;  //         넉백 범위 5%당 피해 +1%
    const float SunspotKnockRange = 0.5f;   // 흑점 폭발: 넉백 범위 +50%
    const int MultiExtra = 2;               // 연속 폭발: 후속타 +2회
    const float SupernovaKnockRange = 0.5f; // 초신성: 넉백 범위 +50%
    const float SupernovaDamagePerKnock = 1f;   //     누적 넉백 범위 1%당 피해 +1%
    const float SupernovaCooldown = 0.5f;   //         쿨타임 +50%
    const float FlareDamage = 0.5f;         // 플레어: 최종 피해량 +50% (독립 배율)
    const float FlareCooldown = 0.25f;      //         쿨타임 -25%

    bool Burning => HasSpecial(Burn);
    /// <summary>뇌진탕은 후속타가 있을 때만 (타오름이면 획득 기록만 남고 비활성)</summary>
    bool ConcussionActive => HasSpecial(Concussion) && !Burning;
    /// <summary>연속 폭발은 후속타가 있을 때만 (예외 데이터로 타오름과 함께 있어도 발동 안 함)</summary>
    bool MultiActive => HasSpecial(Multi) && !Burning;
    bool SlowOnHit => HasSpecial(SlowRecover) || HasSpecial(Flare);

    /// <summary>누적 넉백 범위 증가율 (타오름이어도 보존 → 변환 계산에 사용)</summary>
    public float KnockRangeBonus =>
        (HasSpecial(Shock) ? ShockKnockRange : 0f) + (HasSpecial(Sunspot) ? SunspotKnockRange : 0f)
        + (HasSpecial(Supernova) ? SupernovaKnockRange : 0f);

    /// <summary>'피해량' % (타오름·초신성 넉백 변환)</summary>
    protected override float SpecialDamageBonus =>
        (Burning ? KnockRangeBonus * BurnDamagePerKnock : 0f)
        + (HasSpecial(Supernova) ? KnockRangeBonus * SupernovaDamagePerKnock : 0f);
    /// <summary>플레어 = '최종 피해량' +50% (결정 기록 v1.0)</summary>
    protected override float SpecialFinalDamageMul => HasSpecial(Flare) ? 1f + FlareDamage : 1f;
    protected override float SpecialRangeBonus => Burning ? BurnRange + KnockRangeBonus * BurnRangePerKnock : 0f;
    /// <summary>쿨타임 독립 배율 곱연산: 플레어 ×0.75 · 메아리 ×1.25 · 초신성 ×1.5</summary>
    protected override float SpecialCooldownMul =>
        (HasSpecial(Flare) ? 1f - FlareCooldown : 1f)
        * (HasSpecial(Echo) ? 1f + EchoCooldown : 1f)
        * (HasSpecial(Supernova) ? 1f + SupernovaCooldown : 1f);

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    // 타오름: 지속 공격이 끝난 뒤부터 쿨타임 → BurnRoutine 이 Casting 으로 처리 (CastDuration 미사용)

    // ---------- 특수 강화 목록 (선택 불가 · 경고 포함) ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Shock, title = "충격 발산", desc = "넉백 범위 +100%" },
                new SpecialUpgradeOption { id = Concussion, title = "뇌진탕", desc = "후속타로 넉백된 적이 다른 적과 부딪히면 1초 기절" },
                new SpecialUpgradeOption { id = SlowRecover, title = "느린 회복", desc = "피해를 준 적의 이동속도 2초간 50% 감소" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Echo, title = "메아리", desc = "솔라 슬래시가 끝난 뒤 1회 재발동 · 쿨타임 +25%" },
                new SpecialUpgradeOption
                {
                    id = Burn, title = "타오름",
                    desc = "후속타 대신 5초 지속 공격 (매초 총 피해) · 범위 +50% · 넉백 범위 증가 → 범위·피해로 변환",
                    warning = HasSpecial(Concussion)
                        ? "타오름을 선택하면 후속타가 제거되어 뇌진탕 효과가 발동하지 않습니다.\n그래도 선택하시겠습니까?"
                        : null,
                },
                new SpecialUpgradeOption { id = Sunspot, title = "흑점 폭발", desc = "후속타가 즉시 발생 · 넉백 범위 +50%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption
                {
                    id = Multi, title = "연속 폭발", desc = "후속타 이후 후속타 2회 추가 (총 3회)",
                    locked = HasSpecial(Burn), lockReason = "선택 불가 — 타오름은 후속타가 없어 연속 폭발이 발동하지 않습니다",
                },
                new SpecialUpgradeOption { id = Supernova, title = "초신성", desc = "넉백 범위 +50% · 누적 넉백 범위 1%당 피해량 +1% · 쿨타임 +50%" },
                new SpecialUpgradeOption { id = Flare, title = "플레어", desc = "최종 피해량 +50% · 피해 시 2초간 50% 둔화 · 쿨타임 -25%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 발동 ----------
    protected override bool Fire()
    {
        P.PlayAttack();
        StartCoroutine(Burning ? BurnRoutine() : CastWhole());
        return true;
    }

    /// <summary>쿨타임은 공격이 모두 끝난 뒤부터: 베기 → 폭발(+연속 폭발) → 메아리 → 폭발(+연속 폭발)</summary>
    IEnumerator CastWhole()
    {
        Casting = true;
        yield return Sequence(false);
        Casting = false;
    }

    /// <summary>일반형: 베기 → 후속 폭발 (연속 폭발이면 +2) → 메아리면 끝난 뒤 1회 재발동 (재발동은 다시 메아리 없음)</summary>
    IEnumerator Sequence(bool isEcho)
    {
        Vector2 from = P.transform.position;
        float slashR = RangeOf(T.solarSlash.radius), blastR = RangeOf(T.solarExplosion.radius);
        bool sunspot = HasSpecial(Sunspot);

        // 시트 = 베기 → 태양 → 폭발 (폭발 프레임이 solarExplosionDelay 에 맞춰져 있음). 흑점 폭발은 폭발 부분만 즉시
        var frames = ErnSkillFx.SolarSlash;
        if (frames != null)
        {
            if (sunspot) SpawnBlastFx(frames, blastR);
            else SpriteEffect.Spawn(frames, FxFps, P.transform, Vector2.zero, 0f, false, 11, blastR * FxWidth);
        }
        else Fx.Circle(from, slashR, Fx.FireBright, 0.3f, P.transform);

        // 1) 베기
        var cast = isEcho ? CastRef.Indirect : DirectCast;   // 메아리 재발동 = 간접 발동
        SkillHits.Circle(from, slashR, MakeDamage(SlashDamage, cast), Enemy.KnockSpeed(T.solarSlash.knockback), OnDamaged);
        HitDebug.Circle(from, slashR, HitDebug.SlashColor);

        // 2) 후속 폭발 (흑점 폭발 = 즉시)
        if (!sunspot) yield return new WaitForSeconds(T.solarExplosionDelay);
        Explode(frames == null, null, cast);
        if (MultiActive)
            for (int i = 0; i < MultiExtra; i++)
            {
                // [임시 해석] 간격 = 기본 베기→폭발 간격 × 비율 (흑점 폭발로 0 이 되어도 기본 간격 사용)
                yield return new WaitForSeconds(T.solarExplosionDelay * T.solarMultiIntervalRatio);
                Explode(frames == null, frames, cast);
            }

        // 3) 메아리: 연속 폭발까지 모두 끝난 뒤 1회
        if (!isEcho && HasSpecial(Echo))
        {
            yield return new WaitForSeconds(T.solarEchoGap);
            P.PlayAttack();
            yield return Sequence(true);
        }
    }

    /// <summary>후속 폭발: 피해 원 안 = 피해 + 넉백, 넉백 범위(더 큰 원) 안 = 넉백만</summary>
    void Explode(bool drawShape, Sprite[] blastFx, CastRef cast)
    {
        Vector2 at = P.transform.position;   // 이펙트가 플레이어를 따라가므로 폭발도 현재 위치
        float r = RangeOf(T.solarExplosion.radius);
        float knockR = Mathf.Max(r, KnockRadius(T.solarExplosion.radius));
        float knock = Enemy.KnockSpeed(T.solarExplosion.knockback);

        if (blastFx != null) SpawnBlastFx(blastFx, r);
        if (drawShape) Fx.Disc(at, r, Fx.FireBright, 0.25f);

        SkillHits.Circle(at, r, MakeDamage(ExplosionDamage, cast), knock, e => { OnDamaged(e); OnKnocked(e); });
        HitDebug.Circle(at, r, HitDebug.BlastColor);

        if (knockR > r + 0.001f)
        {
            // 피해 원 밖 ~ 넉백 범위: 피해 없이 밀기
            foreach (var e in SkillHits.InCircle(at, knockR, around))
            {
                Vector2 d = (Vector2)e.transform.position - at;
                float rr = r + e.radius;
                if (d.sqrMagnitude < rr * rr) continue;   // 이미 폭발에 맞음
                e.Push(d.sqrMagnitude > 0.0001f ? d : Random.insideUnitCircle, knock);
                OnKnocked(e);
            }
            HitDebug.Circle(at, knockR, HitDebug.KnockColor);
        }
    }
    readonly List<Enemy> around = new List<Enemy>();

    /// <summary>넉백 범위 = 기본 반경 × (1 + 기본 범위 강화 + 넉백 범위 증가율) (합연산)</summary>
    float KnockRadius(float baseR) => baseR * (1f + RangeBonus + KnockRangeBonus) * P.areaMul;

    /// <summary>타오름: 5초간 매초 (베기+폭발 총 피해) · 실제 넉백 범위는 기본값 고정 · 끝난 뒤 쿨타임</summary>
    IEnumerator BurnRoutine()
    {
        // 지속 중에는 쿨타임이 돌지 않음 (남은 쿨타임 감소로 지속이 겹치지 않게) → 끝난 뒤부터 쿨타임
        Casting = true;
        var cast = DirectCast;
        var frames = ErnSkillFx.SolarSlash;
        float r0 = RangeOf(T.solarBurn.radius);
        if (frames != null) SpriteEffect.Spawn(frames, FxFps, P.transform, Vector2.zero, 0f, false, 11, r0 * FxWidth);
        var ring = Fx.GroundCircle(P.transform.position, r0, Fx.FireDeep, BurnDuration, P.transform);   // 장판 = 바닥 레이어

        // 0초부터 피해 간격마다 (기본 1초 → 0·1·2·3·4초 5회). 가속 연소면 간격이 줄어 더 자주
        float elapsed = 0f;
        for (int i = 0; elapsed < BurnDuration - 1e-4f; i++)
        {
            Vector2 at = P.transform.position;
            float r = RangeOf(T.solarBurn.radius);
            // 실제 넉백 범위 = 기본값 (넉백 범위 증가율·타오름 범위 증가 미적용, 기본 강화 범위만)
            float knockR = T.solarBurn.radius * (1f + RangeBonus) * P.areaMul;
            bool knockThisTick = i == 0 || T.solarBurnKnockEveryTick;
            float knock = Enemy.KnockSpeed(T.solarBurn.knockback);

            if (frames == null) Fx.Disc(at, r, Fx.Fire, 0.25f);
            else if (i > 0) SpawnBlastFx(frames, r);

            // 타오름 = '지속 피해' (결정 기록 v1.0). 예외로 화염의 검 화상은 부여 (canBurn)
            var burnDmg = MakeDamage(SlashDamage + ExplosionDamage, cast, DamageType.DoT);
            burnDmg.canBurn = true;
            SkillHits.Circle(at, r, burnDmg, 0f, e =>
            {
                OnDamaged(e);
                if (!knockThisTick) return;
                Vector2 d = (Vector2)e.transform.position - at;
                float rr = knockR + e.radius;
                if (d.sqrMagnitude < rr * rr) e.Push(d.sqrMagnitude > 0.0001f ? d : Random.insideUnitCircle, knock);
            });
            HitDebug.Circle(at, r, HitDebug.BlastColor);
            if (knockThisTick && r > knockR + 0.001f) HitDebug.Circle(at, knockR, HitDebug.KnockColor);
            // 마지막 대기는 남은 지속시간까지만 (간격이 5초를 나누어떨어지지 않아도 5초에 끝나고 쿨타임 시작)
            float iv = Mathf.Min(CombatMods.DotInterval(BurnTick), BurnDuration - elapsed);
            yield return new WaitForSeconds(iv);
            elapsed += iv;
        }
        if (ring != null) Destroy(ring.gameObject);
        Casting = false;
    }

    void SpawnBlastFx(Sprite[] frames, float r)
    {
        int from = Mathf.Min(FxBlastFrom, frames.Length - 1);
        var sub = new Sprite[frames.Length - from];
        System.Array.Copy(frames, from, sub, 0, sub.Length);
        SpriteEffect.Spawn(sub, FxFps, P.transform, Vector2.zero, 0f, false, 11, r * FxWidth);
    }

    void OnDamaged(Enemy e)
    {
        if (SlowOnHit && e != null && e.hp > 0) e.Slow(SlowMul, SlowSeconds);
    }

    void OnKnocked(Enemy e)
    {
        if (ConcussionActive && e != null && e.hp > 0) e.MarkConcussion(T.concussionWindow, StunSeconds, T.concussionStunsOther);
    }
}

/// <summary>A03 파이어 볼: 가까운 적에게 투사체 발사 · 초기 피해량 7 · 쿨타임 3초</summary>
public class FireBall : AutoSkill
{
    const float Speed = 10f;            // [임시] 투사체 속도
    const int Pierce = 1;               // [임시] 관통 수 (기획 미정 → 1)
    const float BaseDamage = 7f, BaseCooldown = 3f;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float MaxRange => Reach(Tier.Long);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        var target = Nearest(from, MaxRange);
        if (target == null) return false;
        Vector2 dir = ((Vector2)target.transform.position - from).normalized;
        // 크기 = 범위 좁음의 일부, 수명 = 사거리 / 속도
        float size = Mathf.Max(0.3f, Range(Tier.Short) * 0.3f);
        Projectile.Spawn(from, dir, Speed, MakeDamage(BaseDamage), Pierce, MaxRange / Speed,
                         "FireBall", new Color(1f, 0.55f, 0.15f), size);
        return true;
    }
}

/// <summary>A04 파이어 버스터: 거대한 폭발로 주변 적 밀치기 · 초기 피해량 20 · 쿨타임 10초</summary>
public class FireBuster : AutoSkill
{
    const float Knock = 9f;             // [임시] 밀치기 세기
    const float BaseDamage = 20f, BaseCooldown = 10f;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => Range(Tier.Mid);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (Nearest(from, Radius) == null) return false;
        Fx.Disc(from, Radius, Fx.FireDeep, 0.25f);
        SkillHits.Circle(from, Radius, MakeDamage(BaseDamage), Knock);
        CameraFollow.Shake(0.08f, 0.08f);
        return true;
    }
}

/// <summary>
/// A05 파이어 블레이드: 범위 중간 · 쿨 김 · 총 피해 높음
/// 범위 내 대상 타겟팅 → 각 대상에 다수 참격 → 참격 종료 후 각 대상 위치 폭발 범위 피해
/// </summary>
public class FireBlade : AutoSkill
{
    const int MaxTargets = 8;           // [임시] 타겟 수 제한 (기획 미정)
    const int SlashCount = 5;           // 베기 공격 횟수 5
    const float SlashDamage = 3f, ExplosionDamage = 20f, BaseCooldown = 15f;
    const float SlashInterval = 0.12f;  // [임시]
    const float ExplosionRatio = 0.35f; // [임시] 폭발 반경 = 범위 좁음 × 비율 (기획 미정)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => Range(Tier.Mid);

    readonly List<Enemy> found = new List<Enemy>();

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        SkillHits.InCircle(from, Radius, found);
        if (found.Count == 0) return false;

        // 가까운 순으로 제한
        found.Sort((a, b) => ((Vector2)a.transform.position - from).sqrMagnitude
                       .CompareTo(((Vector2)b.transform.position - from).sqrMagnitude));
        var targets = found.GetRange(0, Mathf.Min(MaxTargets, found.Count));
        StartCoroutine(Run(targets));
        return true;
    }

    IEnumerator Run(List<Enemy> targets)
    {
        // 베기 초기 피해량 3 × 5회, 폭발 초기 피해량 20 (각각 피해 이벤트마다 내림)
        var cast = DirectCast;
        DamageInfo perSlash = MakeDamage(SlashDamage, cast);
        DamageInfo explosion = MakeDamage(ExplosionDamage, cast);
        float expR = Range(Tier.Short) * ExplosionRatio;

        var lastPos = new Vector2[targets.Count];
        for (int s = 0; s < SlashCount; s++)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                if (e == null || e.hp <= 0) continue;
                lastPos[i] = e.transform.position;
                var d = Random.insideUnitCircle.normalized;
                Fx.Box(lastPos[i] - d * 0.33f, d, 0.67f, 0.08f, Fx.FireBright, 0.12f);   // 카메라 시야 7 복귀에 맞춰 × 2/3
                e.Hit(perSlash, d, 0.5f);
            }
            yield return new WaitForSeconds(SlashInterval);
        }

        for (int i = 0; i < targets.Count; i++)
        {
            var e = targets[i];
            Vector2 at = e != null ? (Vector2)e.transform.position : lastPos[i];
            if (at == Vector2.zero && e == null) continue;
            Fx.Disc(at, expR, Fx.Fire, 0.25f);
            SkillHits.Circle(at, expR, explosion, 2f);
        }
    }
}

/// <summary>
/// A06 파이어 스톰: 플레이어 중심 화염 소용돌이 (간접 사용 · 지속 피해 2, 3초, 적 느려짐) → 폭발 (직접 피해 15) 하며 밀치기 · 쿨타임 14초
/// </summary>
public class FireStorm : AutoSkill
{
    const float Duration = 3f;          // 화염 소용돌이 지속 시간 3초
    const float TickInterval = 1f;      // [해석] 지속 피해 기본 피해 간격 1초 (결정 기록)
    const float SlowMul = 0.5f;         // [임시] 슬로우 비율 (기획 미정)
    const float DotDamage = 2f, ExplosionDamage = 15f, BaseCooldown = 14f;
    const float Knock = 9f;             // [임시]

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        if (Nearest(from, Range(Tier.Short)) == null) return false;
        StartCoroutine(Run());
        return true;
    }

    IEnumerator Run()
    {
        // 화염 소용돌이 = 간접 사용 · 지속 피해 (틱마다 내림), 마지막 폭발 = 직접 사용 · 직접 피해
        var cast = DirectCast;
        DamageInfo perTick = MakeDamage(DotDamage, cast.AsIndirect, DamageType.DoT);
        float rSmall = Range(Tier.Short);

        // [임시 해석] '주변' = 플레이어를 따라다니는 범위
        var ring = Fx.GroundCircle(P.transform.position, rSmall, Fx.FireDeep, Duration, P.transform);   // 장판 = 바닥 레이어
        // 틱 피해는 기본 간격 기준으로 정하고, 가속 연소면 같은 피해가 더 자주
        for (float elapsed = 0f; elapsed < Duration - 1e-4f;)
        {
            float iv = CombatMods.DotInterval(TickInterval);
            SkillHits.Circle(P.transform.position, rSmall, perTick, 0.3f,
                             e => e.SustainSlow(SlowMul, iv + 0.1f));   // 범위 안에 있는 동안 유지 (누적 없음)
            yield return new WaitForSeconds(iv);
            elapsed += iv;
        }
        if (ring != null) Destroy(ring.gameObject);

        Vector2 at = P.transform.position;
        float rBig = Range(Tier.Mid);
        Fx.Disc(at, rBig, Fx.Fire, 0.3f);
        SkillHits.Circle(at, rBig, MakeDamage(ExplosionDamage, cast), Knock);
        CameraFollow.Shake(0.1f, 0.1f);
    }
}
