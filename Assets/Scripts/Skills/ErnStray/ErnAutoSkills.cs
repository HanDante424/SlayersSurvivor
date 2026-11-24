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
    // 파이어 슬래시 이펙트: 2026-11-20 신규 시트로 교체 (1행 x 6열, 칸 384x192, 16fps, 1회 재생).
    // 예전 시트 "ErnStray_SkillFireSlash" (2행 x 4열, 7프레임) 는 Art/Incoming/Retired 로 옮김.
    public const string FireSlashSheet = "ErnStray_SkillFireSlashAnim";
    // 검기 발사 (파이어 슬래시 Lv.10): 2026-11-20 신규 시트 (1행 x 6열, 칸 320x512, 12fps 반복). 예전에는 슬래시 이펙트를 그대로 날렸음
    public const string FireBladeWaveSheet = "ErnStray_FireBladeWave_One";   // 한 장 (프레임 6, 2026-11-22 용량 절약, 원본 6프레임 시트는 Art/Incoming/Retired/FxSheets)
    // 트리플 슬래시 이펙트는 2026-11-21 에 불꽃 참격 (BentSlashFx + ErnStray_TripleSlashFlame) 으로 교체, 예전 시트는 Art/Incoming/Retired 로 옮김.
    public const string SolarSlashSheet = "ErnStray_SkillSolarSlash";   // 4행 x 3열

    public static Sprite[] FireSlash => SpriteEffect.LoadFrames(FireSlashSheet, 1, 6, 1f);
    public static Sprite[] FireBladeWave => SpriteEffect.LoadFrames(FireBladeWaveSheet, 1, 1, 1f);
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

    const float FxFps = 16f;            // 신규 시트 제작 사양 (6프레임 → 약 0.38초)
    const float FxWidth = 3.5f;         // [임시] 이펙트 한 칸 가로 = 범위 x 이 값 (신규 시트는 가장 큰 프레임이 칸의 84% → 예전 시트의 최대 길이에 맞춤)
    const float FxDistance = 2f;        // 이펙트 중심 = 플레이어 앞 이 거리 (사용자 2026-11-22: 1 → 2). 처음에는 범위 x 1.2
    const float FxUpExtra = 0.5f;       // 위를 볼 때 이펙트를 더 멀리 (사용자 2026-11-22)
    const float FxDownExtra = 1.0f;     // 아래를 볼 때 더 멀리 + 높이 올림 없음 (사용자 2026-11-22: 아래는 좀더 내려가게)
    const float FxHeight = 0.3f;        // 이펙트를 위로 올리는 높이 (월드, 사용자 2026-11-22: y 0.3). 판정은 그대로

    // 특수 강화 id
    public const string Track = "F5A", Scatter = "F5B", BothWays = "F5C";
    public const string Wave = "F10A", Chain = "F10B", Quick = "F10C";
    public const string TripleShot = "F15A", Heavy = "F15B", DeathEcho = "F15C";

    // 특수 강화 확정 수치 (기획 v1.0)
    const int ScatterCount = 3;
    const float TripleAngle = 60f;
    const float ChainAngle = 120f;      // 연속 공격 생성: 공격 방향 이외 120° 간격 2방향 · 죽음의 메아리: 120° 간격 3방향
    const float QuickMul = 0.5f;        // 빠른 공격: 사용 후 남은 쿨타임 절반 감소
    const float TrackDamage = 0.5f;     // 대상 추적: 피해량 +50% (2026-11-24 밸런스)
    const float ScatterCooldown = 0.9f; // 난사: 쿨타임 감소 +10%
    const float BothWaysDamage = 0.25f; // 양방향 공격: 피해량 +25%
    const float ChainCooldown = 0.9f;   // 연속 공격: 쿨타임 감소 +10%
    const float TripleFinal = 1.25f;    // 삼연발: 최종 피해량 +25%
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
                new SpecialUpgradeOption { id = Scatter, title = "난사", desc = "무작위 3방향으로 공격합니다." },
                new SpecialUpgradeOption { id = BothWays, title = "양방향 공격", desc = "바라보는 곳 반대편도 공격합니다." },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Wave, title = "검기 발사", keywords = new[] { "원거리", "간접 사용", "직접 피해", "고정 피해", "투사체", "추가 사용" }, desc = "같은 피해의 검기가 추가로 날아가 피해를 입힙니다." },
                new SpecialUpgradeOption { id = Chain, title = "연속 공격", desc = "공격 방향 이외에 2개의 공격이 추가 발생합니다." },
                new SpecialUpgradeOption { id = Quick, title = "빠른 공격", desc = "사용 후 남은 쿨타임이 절반 감소합니다." },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = TripleShot, title = "삼연발", desc = "공격 방향 양쪽에 2회의 추가 공격이 발생합니다." },
                new SpecialUpgradeOption
                {
                    id = Heavy, title = "묵직한 일격",
                    specs = new[] { "'공격 범위' +50%", "'최종 피해량' +100%", "'쿨타임 감소' '무효화'", "'쿨타임 감소' +1% 당 '최종 피해량' +2%" },
                },
                new SpecialUpgradeOption { id = DeathEcho, title = "죽음의 메아리", keywords = new[] { "근거리", "간접 사용", "직접 피해", "고정 피해" }, desc = "적 사망시 해당 위치에서 같은 피해의 공격이 3회 발생합니다." },
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
    protected override float SpecialFinalDamageMul =>
        (HasSpecial(Heavy) ? 1f + HeavyFinal + HeavyConvertedReduction * HeavyPerReduction : 1f)
        * (HasSpecial(TripleShot) ? TripleFinal : 1f);
    protected override float SpecialRangeBonus => HasSpecial(Heavy) ? HeavyRange : 0f;
    /// <summary>대상 추적 +50% · 양방향 공격 +25% (피해량, 기본 강화와 합연산)</summary>
    protected override float SpecialDamageBonus => (HasSpecial(Track) ? TrackDamage : 0f) + (HasSpecial(BothWays) ? BothWaysDamage : 0f);
    /// <summary>난사 · 연속 공격: 쿨타임 감소 +10% (×0.9)</summary>
    protected override float SpecialCooldownMul => (HasSpecial(Scatter) ? ScatterCooldown : 1f) * (HasSpecial(Chain) ? ChainCooldown : 1f);
    // 기본 강화 1회 (밸런스 확정 2026-11-24): 피해량 +20% · 쿨타임 -5%
    public override float BasicDamageStep => 0.2f;
    public override float BasicCooldownStep => 0.05f;

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

        // 공격 모션은 수동 스킬 사용 때만 (2026-11-18 사용자) — 자동 스킬은 이펙트만. 스킬 이펙트 시트가 없을 때 쓰던 기본 베기 이펙트 대체도 모션과 함께 빠짐 (시트는 있음)

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
            {
            // 위 · 아래를 볼 때는 더 멀리: 방향의 세로 성분만큼 보정, 아래쪽은 0.3 올림을 없앰
            float dist = FxDistance + Mathf.Abs(dir.y) * (dir.y > 0f ? FxUpExtra : FxDownExtra);
            float h = FxHeight * (1f - Mathf.Max(0f, -dir.y));
            ErnSkillFx.SpawnDirected(frames, FxFps, from + dir * dist + new Vector2(0f, h), dir, Radius * FxWidth);
        }
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
                       Enemy.KnockSpeed(Hit.knockback), ErnSkillFx.FireBladeWave);
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

    const float WaveFps = 12f;          // 신규 시트 제작 사양 (6프레임 반복)
    const float WaveHeightMul = 1.1f;   // [임시] 이펙트 세로 길이 = 판정 너비 x 이 값 (그림 세로 높이가 칸의 약 90%)
    const float WaveCellAspect = 512f / 320f;   // 신규 시트 한 칸 세로 / 가로 (LoadFrames 는 칸 가로를 1유닛으로 자름)

    public static void Spawn(Vector2 from, Vector2 dir, float speed, float distance, float halfWidth,
                             DamageInfo dmg, float knock, Sprite[] frames)
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
            // 세로(반달의 길이)를 판정 너비에 맞춤: 칸 세로 = 가로 x 비율 → scale = 목표 높이 / 비율
            go.transform.localScale = Vector3.one * (halfWidth * 2f * WaveHeightMul / WaveCellAspect);
            w.sr = go.AddComponent<SpriteRenderer>();
            w.sr.sprite = frames[0];
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

        // 날아가는 동안 전체 프레임을 반복 재생
        if (sr != null && frames != null && frames.Length > 0)
        {
            fxTime += Time.deltaTime;
            sr.sprite = frames[(int)(fxTime * WaveFps) % frames.Length];
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
    const float SlashDamage = 7f;           // 밸런스 확정 2026-11-24 (5 → 7)
    const float ExplosionDamage = 15f;      // (12 → 15)

    const float FxFps = 20f;            // [임시] 12프레임 → 0.6초 (원형 베기 → 태양 → 폭발)
    const float FxWidth = 2.6f;         // [임시] 한 칸 가로 = 폭발 범위 x 이 값 (마지막 폭발 고리 ≈ 판정 원)
    const int FxBlastFrom = 8;          // [임시] 시트에서 폭발 부분이 시작하는 프레임 (추가 폭발·흑점 폭발 연출용)
    // ↑ 기존 한 장짜리 시트 (ErnSkillFx.SolarSlash) = 새 3분할 그림이 없을 때만 사용

    // 새 3분할 이펙트 (2026-11-20 사용자): 첫 베기 고리 → 중간 고리 (반복 · 회전, 타오름에도 사용) → 폭발 고리
    // 원본 Project Slayers/output/ErnStray_SolarSlash_RingFormation_v1 · RingBurn_Loop_v1 · RingBurst_v1
    const string RingFormSheet = "ErnStray_SolarSlashRing";        // 4×4, 칸 512, 16프레임 (시계 반대로 고리가 그려짐)
    // 한 장 (프레임 1) + 코드 회전 (RingLoopSpin) — 2026-11-22 사용자, 용량 절약. 원본 4행×5열 20프레임 시트는 Art/Incoming/Retired/FxSheets (되돌리기: 시트 이름 · LoadFrames(…, 4, 5, …))
    const string RingLoopSheet = "ErnStray_SolarSlashRingLoop_One";
    const string RingBurstSheet = "ErnStray_SolarSlashRingBurst";  // 4×4, 칸 768 (같은 픽셀 축척), 15프레임 + 빈 칸
    const float RingCellPx = 512f, RingBurstCellPx = 768f;
    // 고리 반지름(px) = 판정 반경 (2026-11-20 사용자: 고리 = 베기 반경). 측정: 안쪽 경계 152px · 바깥 불꽃 끝 약 186px 의 중간
    const float RingCorePx = 169f;
    const float RingFormTime = 0.35f;   // [임시] 첫 베기 16프레임 재생 시간 (원래 0.74초 → 폭발 0.45초 전에 고리 완성, 2026-11-20 사용자: 그림을 0.45초에 맞춤)
    const float RingLoopFps = 25f;      // manifest 40ms
    const float RingLoopSpin = 90f;     // [임시] 중간 고리 회전 속도 (도/초, + = 시계 반대 · 첫 베기와 같은 방향)
    const float RingBurstFps = 20f;     // manifest 30~70ms (15프레임 약 0.73초)
    const int RingBurstFrames = 15;
    const int FxOrder = 11;

    // 특수 강화 id
    public const string Shock = "S5A", Concussion = "S5B", SlowRecover = "S5C";
    public const string Echo = "S10A", Burn = "S10B", Sunspot = "S10C";
    public const string Multi = "S15A", Supernova = "S15B", Flare = "S15C";

    // 특수 강화 확정 수치 (기획 v1.0)
    const float ShockKnockRange = 1.0f;     // 충격 발산: 넉백 범위 +100%
    const float StunSeconds = 1f;           // 뇌진탕: 기절 1초
    const float SlowMul = 0.5f, SlowSeconds = 2f;   // 느린 회복 · 플레어: 2초간 이동속도 50% 감소
    const float EchoCooldown = 0.25f;       // 메아리: 쿨타임 +25%
    // 타오름 장판 ErnStray_SolarSlashBurnField (2026-11-22 사용자 전달 단일 그림, 원본 1254px → 게임용 1024px). 시트 (2000×1600 20프레임) 대신 코드로 연출 (GroundFieldFx)
    const string BurnFieldSheet = "ErnStray_SolarSlashBurnField";
    const float BurnFieldSrcPx = 1254f;                  // 원본 가로 (좌표 기준)
    const float BurnFieldRadiusPx = 470f;                // 측정: 원판의 단단한 가장자리 반지름 (알파 > 200 픽셀의 99% 가 465px 이내, 가장자리는 흐림)
    static readonly Vector2 BurnFieldCenterNorm = new Vector2(613.5f / 1254f, 631f / 1254f);   // 측정: 원판 중심 (위 기준 623 → 아래 기준 631)
    const float BurnDuration = 5f, BurnTick = 1f;
    const float BurnShrinkTime = 0.5f;   // 타오름이 끝나면 장판 · 고리가 플레이어 쪽으로 줄어들며 사라지는 시간 (사용자 2026-11-22). [임시]   // 타오름: 5초간 매초
    const float BurnRange = 0.5f;           //         공격 범위 +50%
    const float BurnRangePerKnock = 1f;     //         넉백 범위 5%당 공격 범위 +5%
    const float BurnDamagePerKnock = 0.2f;  //         넉백 범위 5%당 피해 +1%
    const float SunspotKnockRange = 0.5f;   // 흑점 폭발: 넉백 범위 +50%
    const int MultiExtra = 2;               // 연속 폭발: 후속타 +2회
    const float SupernovaKnockRange = 0.5f; // 초신성: 넉백 범위 +50%
    const float SupernovaDamagePerKnock = 1f;   //     누적 넉백 범위 1%당 피해 +1%
    const float SupernovaCooldown = 0.5f;   //         쿨타임 +50%
    const float FlareDamage = 1f;           // 플레어: 최종 피해량 +100% (독립 배율, 2026-11-24 +50% → +100%)
    const float FlareCooldown = 0.1f;       //         쿨타임 -10% (2026-11-24 -25% → -10%)
    const float ShockDamage = 0.5f;         // 충격 발산: 피해량 +50% (2026-11-24)
    const float ConcussionFinal = 1.25f;    // 뇌진탕: 최종 피해량 +25%
    const float BurnFinal = 1.25f;          // 타오름: 최종 피해량 +25%
    const float SunspotFinal = 2f;          // 흑점 폭발: 최종 피해량 +100%
    const float MultiCooldown = 0.75f;      // 연속 폭발: 쿨타임 감소 +25%

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
        + (HasSpecial(Supernova) ? KnockRangeBonus * SupernovaDamagePerKnock : 0f)
        + (HasSpecial(Shock) ? ShockDamage : 0f);
    /// <summary>최종 피해량 배율 (독립 효과끼리 곱, 2026-11-24 밸런스): 뇌진탕 ×1.25 · 타오름 ×1.25 · 흑점 폭발 ×2 · 플레어 ×2</summary>
    protected override float SpecialFinalDamageMul =>
        (HasSpecial(Flare) ? 1f + FlareDamage : 1f)
        * (HasSpecial(Concussion) ? ConcussionFinal : 1f)
        * (Burning ? BurnFinal : 1f)
        * (HasSpecial(Sunspot) ? SunspotFinal : 1f);
    protected override float SpecialRangeBonus => Burning ? BurnRange + KnockRangeBonus * BurnRangePerKnock : 0f;
    /// <summary>쿨타임 독립 배율 곱연산: 플레어 ×0.9 · 메아리 ×1.25 · 초신성 ×1.5 · 연속 폭발 ×0.75</summary>
    protected override float SpecialCooldownMul =>
        (HasSpecial(Flare) ? 1f - FlareCooldown : 1f)
        * (HasSpecial(Echo) ? 1f + EchoCooldown : 1f)
        * (HasSpecial(Supernova) ? 1f + SupernovaCooldown : 1f)
        * (HasSpecial(Multi) ? MultiCooldown : 1f);
    // 기본 강화 1회 (밸런스 확정 2026-11-24): 피해량 +20% (쿨타임은 공통값)
    public override float BasicDamageStep => 0.2f;

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
                new SpecialUpgradeOption { id = Concussion, title = "뇌진탕", keywords = new[] { "기절" }, desc = "후속타로 넉백된 적이 다른 적과 부딪히면 1초 기절" },
                new SpecialUpgradeOption { id = SlowRecover, title = "느린 회복", keywords = new[] { "이동 속도 감소" }, desc = "피해를 준 적의 이동속도 2초간 50% 감소" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Echo, title = "메아리", desc = "솔라 슬래시가 끝난 뒤 1회 재발동 · 쿨타임 +25%" },
                new SpecialUpgradeOption
                {
                    id = Burn, title = "타오름", keywords = new[] { "직접 사용", "지속 피해", "고정 피해" },
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
                new SpecialUpgradeOption { id = Flare, title = "플레어", keywords = new[] { "이동 속도 감소" }, desc = "최종 피해량 +50% · 피해 시 2초간 50% 둔화 · 쿨타임 -25%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    // ---------- 발동 ----------
    protected override bool Fire()
    {
        // 공격 모션 없음 (자동 스킬, 2026-11-18 사용자: 모션은 수동 스킬만)
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

        // 새 3분할 그림이 있으면: 첫 베기 고리 (0.35초) → 중간 고리 (폭발까지 회전) → 폭발마다 폭발 고리.
        // [해석] 흑점 폭발 (폭발 즉시) = 첫 베기 · 중간 없이 폭발 고리만 (기존 시트도 폭발 부분만 쓰던 방식)
        bool ringFx = RingFramesReady;
        var ring = new RingState();
        Sprite[] frames = null;
        if (ringFx)
        {
            if (!sunspot) StartRing(ring, slashR);
        }
        else
        {
            // 기존 한 장짜리 시트 = 베기 → 태양 → 폭발 (폭발 프레임이 solarExplosionDelay 에 맞춰져 있음). 흑점 폭발은 폭발 부분만 즉시
            frames = ErnSkillFx.SolarSlash;
            if (frames != null)
            {
                if (sunspot) SpawnBlastFx(frames, blastR);
                else SpriteEffect.Spawn(frames, FxFps, P.transform, Vector2.zero, 0f, false, 11, blastR * FxWidth);
            }
            else Fx.Circle(from, slashR, Fx.FireBright, 0.3f, P.transform);
        }

        // 1) 베기
        var cast = isEcho ? CastRef.Indirect : DirectCast;   // 메아리 재발동 = 간접 발동
        SkillHits.Circle(from, slashR, MakeDamage(SlashDamage, cast), Enemy.KnockSpeed(T.solarSlash.knockback), OnDamaged);
        HitDebug.Circle(from, slashR, HitDebug.SlashColor);

        // 2) 후속 폭발 (흑점 폭발 = 즉시)
        if (!sunspot) yield return new WaitForSeconds(T.solarExplosionDelay);
        if (ringFx) SpawnRingBurst(EndRing(ring));
        Explode(!ringFx && frames == null, null, cast);
        if (MultiActive)
            for (int i = 0; i < MultiExtra; i++)
            {
                // [임시 해석] 간격 = 기본 베기→폭발 간격 × 비율 (흑점 폭발로 0 이 되어도 기본 간격 사용)
                yield return new WaitForSeconds(T.solarExplosionDelay * T.solarMultiIntervalRatio);
                if (ringFx) SpawnRingBurst();   // 연속 폭발 = 폭발 고리를 다시
                Explode(!ringFx && frames == null, frames, cast);
            }

        // 3) 메아리: 연속 폭발까지 모두 끝난 뒤 1회
        if (!isEcho && HasSpecial(Echo))
        {
            yield return new WaitForSeconds(T.solarEchoGap);
            yield return Sequence(true);   // 공격 모션 없음 (자동 스킬)
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
        float r0 = RangeOf(T.solarBurn.radius);
        // 새 3분할 그림이 있으면: 첫 베기 고리 → 지속 시간 내내 중간 고리 (반복 · 회전), 폭발 없음 (2026-11-20 사용자: 중간 = 타오름에도 사용)
        bool ringFx = RingFramesReady;
        var ringState = new RingState();
        var frames = ringFx ? null : ErnSkillFx.SolarSlash;
        if (ringFx) StartRing(ringState, r0);
        else if (frames != null) SpriteEffect.Spawn(frames, FxFps, P.transform, Vector2.zero, 0f, false, 11, r0 * FxWidth);
        // 장판 = 바닥 레이어: 불타는 원판 그림 한 장을 코드로 회전 · 맥동 (시트 대신, 용량 절약 · 2026-11-22 사용자). 그림이 없으면 예전 단색 원
        GameObject ring = null;
        var fieldTex = SpriteLib.Load(BurnFieldSheet);
        if (fieldTex != null)
        {
            var field = GroundFieldFx.Spawn(fieldTex, BurnFieldCenterNorm, BurnFieldRadiusPx, BurnFieldSrcPx, P.transform, r0, BurnDuration + BurnShrinkTime, Fx.GroundOrder, BurnShrinkTime);   // 장판은 끝나면 스스로 줄어들며 사라짐
            if (field != null) ring = field.gameObject;
        }
        else { var shape = Fx.GroundCircle(P.transform.position, r0, Fx.FireDeep, BurnDuration, P.transform); if (shape != null) ring = shape.gameObject; }

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

            if (!ringFx)   // 기존 시트: 틱마다 폭발 부분 (새 그림은 중간 고리가 계속 타오르므로 틱 연출 없음)
            {
                if (frames == null) Fx.Disc(at, r, Fx.Fire, 0.25f);
                else if (i > 0) SpawnBlastFx(frames, r);
            }

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
        if (ring != null && ring.GetComponent<GroundFieldFx>() == null) Destroy(ring);   // 단색 원 대체만 바로 지움 (그림 장판은 스스로 줄어들며 사라짐)
        if (ringFx) ShrinkRing(ringState);   // 중간 고리도 플레이어 쪽으로 줄어들며 사라짐
        Casting = false;
    }

    // ---------- 새 3분할 고리 이펙트 ----------
    static Sprite[] RingForm => SpriteEffect.LoadFrames(RingFormSheet, 4, 4, 1f);
    static Sprite[] RingLoop => SpriteEffect.LoadFrames(RingLoopSheet, 1, 1, 1f);
    static Sprite[] RingBurst => SpriteEffect.LoadFrames(RingBurstSheet, 4, 4, 1f, RingBurstFrames);
    /// <summary>세 장이 모두 있을 때만 새 연출 (하나라도 없으면 기존 한 장짜리 시트)</summary>
    static bool RingFramesReady => RingForm != null && RingLoop != null && RingBurst != null;

    /// <summary>고리 반지름 r 일 때 칸 가로 (월드). 고리(RingCorePx) = r 이 되게 같은 픽셀 축척</summary>
    static float RingCellWidth(float r, float cellPx) => r * cellPx / RingCorePx;

    /// <summary>사용 1번의 첫 베기 → 중간 고리 상태 (중간 고리는 폭발 · 타오름 종료 때 끔)</summary>
    class RingState { public SpriteLoopEffect loop; public bool ended; }

    /// <summary>첫 베기 고리 재생 → RingFormTime 뒤 중간 고리 (반복 · 회전) 로 교체. 플레이어를 따라감</summary>
    void StartRing(RingState s, float r)
    {
        var form = RingForm;
        SpriteEffect.Spawn(form, form.Length / RingFormTime, P.transform, Vector2.zero, 0f, false, FxOrder, RingCellWidth(r, RingCellPx));
        StartCoroutine(LoopAfterForm(s, r));
    }

    IEnumerator LoopAfterForm(RingState s, float r)
    {
        yield return new WaitForSeconds(RingFormTime);
        if (s.ended || P == null) yield break;   // 폭발이 먼저 왔으면 (폭발 간격을 줄인 경우) 중간 고리 없음
        s.loop = SpriteLoopEffect.Spawn(RingLoop, RingLoopFps, P.transform, Vector2.zero, FxOrder, RingCellWidth(r, RingCellPx), RingLoopSpin);
    }

    /// <summary>중간 고리를 끄고, 끌 때의 회전 각도를 돌려줌 (폭발 고리를 같은 각도에서 시작 → 불꽃 위치가 튀지 않게)</summary>
    float EndRing(RingState s)
    {
        s.ended = true;
        float angle = s.loop != null ? s.loop.transform.localEulerAngles.z : 0f;
        if (s.loop != null) s.loop.Stop();
        s.loop = null;
        return angle;
    }

    /// <summary>타오름 종료: 중간 고리를 끄지 않고 플레이어 (고리 중심) 쪽으로 줄어들며 사라지게 함</summary>
    void ShrinkRing(RingState s)
    {
        s.ended = true;
        if (s.loop != null) s.loop.ShrinkOut(BurnShrinkTime);
        s.loop = null;
    }

    /// <summary>폭발 고리 (1번째 프레임 = 중간 고리 1번째와 같은 그림 → 이어서 바깥으로 터짐). 크기 = 지금 베기 반경 기준</summary>
    void SpawnRingBurst(float angleDeg = 0f)
    {
        float r = RangeOf(T.solarSlash.radius);
        SpriteEffect.Spawn(RingBurst, RingBurstFps, P.transform, Vector2.zero, angleDeg, false, FxOrder, RingCellWidth(r, RingBurstCellPx));
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

/// <summary>
/// A03 파이어 볼 (2026-11-20 노션 기준): 가까운 적에게 화염구 발사 · 직격 초기 피해량 3 · 적중 시 폭발 (범위 피해) 초기 피해량 4 · 쿨타임 3초.
/// 폭발은 직격당한 적도 포함. 적중 없이 사거리 끝에 닿으면 폭발하지 않음 (노션 "적중 시").
/// 특수 강화 9종 (2026-11-22 구현, 노션 기준. 노션에 없는 세부는 [해석] · [임시] 표시):
///   5레벨 산탄 (양쪽 30° 추가 사용 2) · 관통 (+2) · 도탄 (+2)
///   10레벨 집속탄 (적중 시 120° 3발, 총 피해량 60% 고정) · 연속 폭발 (종료 시 0.5초 간격 2회, 범위 +50%씩) · 화염강타 (화상 적 적중 시 기절 +1초 · 기절 적 최종 피해량 +25%)
///   15레벨 홍염 (적중 시 120° 3발, 모든 대상 관통 후 발사 지점으로 복귀) · 폭죽놀이 (도탄 · 관통 · 공격 횟수 무효화 → 종료 시 12발) · 초거대 (최종 피해량 +50% · 범위 +50%)
/// </summary>
public class FireBall : AutoSkill
{
    const float Speed = 10f;            // [임시] 투사체 속도
    const int Pierce = 1;               // [임시] 기본 관통 수 (기획 미정 → 1: 한 명 맞으면 종료)
    const float DirectDamage = 5f, ExplosionDamage = 6f, BaseCooldown = 3f;   // 밸런스 확정 2026-11-24 (직격 3 → 5 · 폭발 4 → 6)
    // 기본 강화 1회 (밸런스 확정 2026-11-24): 피해량 +15% · 쿨타임 -3%
    public override float BasicDamageStep => 0.15f;
    public override float BasicCooldownStep => 0.03f;
    const float ExplosionRatio = 0.5f;  // [임시] 폭발 반경 = 범위 좁음 × 비율 (기획에 반경 없음) → 기본 0.8
    // 폭발 이펙트 ErnStray_FireExplosion: 3×3, 칸 512, 8프레임 (9번째 칸은 빈 칸), 1회 재생
    const string ExplosionSheet = "ErnStray_FireExplosion";
    const int ExplosionFrames = 8;
    const float ExplosionFps = 15f;     // manifest 제안 프레임 40~75ms (평균 약 0.067초) → 8프레임 약 0.53초
    const float ExplosionFxPerDiameter = 1f / 0.705f;   // 가장 큰 프레임(4번)의 그림 폭 361px / 칸 512px → 칸 가로 = 폭발 지름 ÷ 0.705 (측정값)

    // 특수 강화 id (파이어 볼)
    public const string Spread = "FB5A", PierceUp = "FB5B", Ricochet = "FB5C";
    public const string Cluster = "FB10A", ChainBlast = "FB10B", FlameStrike = "FB10C";
    public const string Crimson = "FB15A", Fireworks = "FB15B", Colossal = "FB15C";

    // 특수 강화 확정 수치 (노션)
    const float SpreadAngle = 30f;                  // 산탄: 양쪽 각 30°
    const int SpreadUses = 2;                       // 산탄: 추가 사용 +2 (양쪽 1회씩)
    const int PierceBonus = 2, RicochetBonus = 2;   // 관통 +2 · 도탄 +2
    const float ShardPct = 0.6f;                    // 집속탄 · 홍염: 파이어 볼 총 피해량의 60% 고정 피해 (직격 총 피해량 기준 [해석])
    const float ChainDelay = 0.5f;                  // 연속 폭발 간격
    const float Chain1Pct = 0.6f, Chain2Pct = 0.9f; // 연속 폭발: 1회 60% · 2회 90% 고정 피해 (폭발 총 피해량 기준 [해석])
    const float ChainRangeStep = 0.5f;              // 폭발마다 범위 +50% ([해석] 1회 ×1.5 · 2회 ×2.0)
    const float StrikeStun = 1f, StrikeStunnedBonus = 0.25f;   // 화염강타: 기절 +1초 · 기절 적 최종 피해량 +25%
    const int FireworksCount = 12;                  // 폭죽놀이: 종료 시 화염구 12개
    const float FireworksPct = 0.2f;                // 폭죽놀이: 총 피해량의 20% 고정 피해
    const float FireworksNullBonus = 0.5f;          // 폭죽놀이: 능력치 무효화 1당 최종 피해량 +50%
    const float ColossalFinal = 0.5f, ColossalRange = 0.5f;   // 초거대: 최종 피해량 +50% · 범위 +50%
    // [임시] 노션에 없는 수치
    const float RicochetRadius = 6f;                // 도탄 대상 탐색 반경
    const float ShardSpeed = 9f, ShardRange = 3.5f, ShardSizeMul = 0.55f;   // 집속탄 화염구
    const float CrimsonOut = 4f, CrimsonSpeed = 9f, CrimsonSizeMul = 0.6f;  // 홍염: 나가는 거리 · 속도 · 크기
    const float FireworksSpeed = 7f, FireworksRange = 3f, FireworksSizeMul = 0.5f;   // 폭죽놀이 화염구

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    const float RangeMul = 10f;         // 사거리 배율 (사용자 2026-11-22: 10배). 대상 인식 거리와 투사체 수명이 같이 늘어남
    float MaxRange => Reach(Tier.Long) * RangeMul;
    float ExplosionRadius => Range(Tier.Short) * ExplosionRatio;

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Spread, title = "산탄", desc = "양쪽 각 30도 방향으로 1회씩 추가 사용합니다." },
                new SpecialUpgradeOption { id = PierceUp, title = "관통", desc = "관통 +2" },
                new SpecialUpgradeOption { id = Ricochet, title = "도탄", desc = "도탄 +2" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Cluster, title = "집속탄", keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "원거리", "투사체" }, desc = "적중 시 화염구 3개를 120도 간격으로 발사합니다. 파이어 볼 총 피해량의 60% 고정 피해." },
                new SpecialUpgradeOption { id = ChainBlast, title = "연속 폭발", keywords = new[] { "간접 사용", "직접 피해", "고정 피해" }, desc = "종료 시 0.5초 간격으로 폭발이 2회 추가됩니다. 폭발마다 범위 +50%. 1회 총 피해량 60%, 2회 90% 고정 피해." },
                new SpecialUpgradeOption { id = FlameStrike, title = "화염강타", keywords = new[] { "기절" }, desc = "화상 상태 적 적중 시 기절 +1초, 기절 상태 적 적중 시 최종 피해량 +25%." },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Crimson, title = "홍염", keywords = new[] { "간접 사용", "직접 사용", "고정 피해", "원거리", "투사체" }, desc = "적중 시 화염구 3개를 120도 간격으로 생성합니다. 모든 대상을 관통한 뒤 발사 지점으로 복귀합니다. 총 피해량의 60% 고정 피해." },
                new SpecialUpgradeOption
                {
                    id = Fireworks, title = "폭죽놀이", keywords = new[] { "간접 사용", "직접 사용", "고정 피해", "원거리", "투사체" },
                    specs = new[] { "'도탄' '능력치 무효화'", "'관통' '능력치 무효화'", "'공격 횟수' '능력치 무효화'", "'종료 시' 화염구 12개 생성", "각 화염구 '종료 시' 1회 추가 발동", "'능력치 무효화' 1 당 '최종 피해량' +50%", "총 피해량의 20% 고정 피해" },
                },
                new SpecialUpgradeOption { id = Colossal, title = "초거대", desc = "최종 피해량 +50%, 범위 +50%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    bool Nullified => HasSpecial(Fireworks);                                  // 폭죽놀이: 도탄 · 관통 · 공격 횟수 능력치 무효화
    int PierceCount => Pierce + (HasSpecial(PierceUp) && !Nullified ? PierceBonus : 0);
    int Bounces => HasSpecial(Ricochet) && !Nullified ? RicochetBonus : 0;
    bool UseSpread => HasSpecial(Spread) && !Nullified;
    /// <summary>폭죽놀이가 무효화한 능력치 수 ([해석] 도탄 +2 · 관통 +2 · 공격 횟수 +2 를 각각 2로 셈, 고른 것만)</summary>
    int NullifiedUnits => (HasSpecial(PierceUp) ? PierceBonus : 0) + (HasSpecial(Ricochet) ? RicochetBonus : 0) + (HasSpecial(Spread) ? SpreadUses : 0);
    protected override float SpecialFinalDamageMul =>
        (HasSpecial(Colossal) ? 1f + ColossalFinal : 1f) * (Nullified ? 1f + FireworksNullBonus * NullifiedUnits : 1f);
    protected override float SpecialRangeBonus => HasSpecial(Colossal) ? ColossalRange : 0f;

    /// <summary>화염강타: 기절 상태 적에게 최종 피해량 +25% (대상 단계 배율)</summary>
    static float StrikeMul(Enemy e) => e != null && e.IsStunned ? 1f + StrikeStunnedBonus : 1f;

    /// <summary>
    /// 화염강타: 화상 상태 적이 이 스킬에 적중하면 기절 +1초. 직격 · 폭발 · 파생 화염구 (집속탄 · 홍염 · 폭죽놀이) 의 모든 적중에 적용 ([해석] 노션 "적중 시").
    /// 화염의 검 (화상) 이 없거나 기절 면역 (보스 연출 중 등) 인 대상에는 걸리지 않음
    /// </summary>
    void StrikeOn(Enemy e)
    {
        if (!HasSpecial(FlameStrike) || e == null || e.hp <= 0 || !FlameSword.IsBurning(e)) return;
        e.Stun(StrikeStun);
        if (!e.IsStunned) Debug.Log($"화염강타: {e.DisplayName} 에게 기절이 걸리지 않음 (기절 면역 · 연출 중 · 숨김 상태)");   // 진단용
    }

    static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    /// <summary>같은 사용에서 나온 간접 사용 고정 피해 (모든 증감 무효)</summary>
    DamageInfo FixedOf(float amount, CastRef cast)
    {
        var d = FixedDamage(amount, DamageType.Direct, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    // ---------- 발사 ----------
    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        var target = Nearest(from, MaxRange);
        if (target == null) return false;
        Vector2 dir = ((Vector2)target.transform.position - from).normalized;
        var cast = DirectCast;   // 발사 시점 번호 (폭발이 나중에 일어나도 같은 사용)
        Launch(from, dir, cast);
        if (UseSpread)   // 산탄: 양쪽 각 30도로 추가 사용 (간접 사용, 같은 사용 번호)
        {
            Launch(from, Rot(dir, SpreadAngle), cast.AsIndirect);
            Launch(from, Rot(dir, -SpreadAngle), cast.AsIndirect);
        }
        return true;
    }

    /// <summary>화염구 1발 (도탄 · 관통 · 적중 시 · 종료 시 효과 포함)</summary>
    void Launch(Vector2 from, Vector2 dir, CastRef cast)
    {
        // 크기 = 범위 좁음의 일부, 수명 = 사거리 / 속도
        float size = Mathf.Max(0.3f, Range(Tier.Short) * 0.3f);
        var dmg = MakeDamage(DirectDamage, cast);
        if (HasSpecial(FlameStrike)) dmg.perTarget = StrikeMul;
        var p = Projectile.Spawn(from, dir, Speed, dmg, PierceCount, MaxRange / Speed,
                                 "FireBall", new Color(1f, 0.55f, 0.15f), size);
        AttachBallFx(p, dir, size);
        int bounces = Bounces;
        Vector2 origin = from;
        p.onHit = (e, at) =>
        {
            // 화염강타: 화상 상태 적 적중 시 기절 +1초 (이후 폭발 · 피해는 기절 상태로 계산)
            StrikeOn(e);
            Explode(at, cast);
            if (HasSpecial(Cluster)) ShootShards(at, p.Direction, cast);
            if (HasSpecial(Crimson)) ShootCrimson(at, p.Direction, origin, cast);
            if (bounces > 0 && Redirect(p, at)) bounces--;   // 도탄: 지금 맞은 적을 뺀 가까운 적으로 방향 전환
        };
        p.onEnd = pos => OnBallEnd(pos, cast);
    }

    static readonly List<Enemy> ricochetBuf = new List<Enemy>();

    /// <summary>도탄: 이 화염구가 아직 맞지 않은 가장 가까운 적 (RicochetRadius 안) 쪽으로 방향을 바꾸고 관통 1 추가 (적중 직후 감소분 상쇄). 대상이 없으면 false</summary>
    bool Redirect(Projectile p, Vector2 at)
    {
        Enemy best = null; float bestD = RicochetRadius * RicochetRadius;
        foreach (var x in Enemy.CopyAll(ricochetBuf))
        {
            if (x == null || x.hp <= 0 || p.HasHit(x)) continue;
            float d = ((Vector2)x.transform.position - at).sqrMagnitude;
            if (d < bestD) { bestD = d; best = x; }
        }
        if (best == null) return false;
        p.Direction = ((Vector2)best.transform.position - at).normalized;
        p.Pierce += 1;
        p.Life = Mathf.Max(p.Life, RicochetRadius * 1.5f / Speed + 0.2f);
        FlipFx(p, p.Direction);
        return true;
    }

    /// <summary>종료 시 효과: 연속 폭발 (0.5초 간격 2회) · 폭죽놀이 (화염구 12개)</summary>
    void OnBallEnd(Vector2 pos, CastRef cast)
    {
        if (CheatDisabled) return;
        if (HasSpecial(ChainBlast)) StartCoroutine(ChainRoutine(pos, cast));
        if (HasSpecial(Fireworks)) Fireworks12(pos, cast);
    }

    IEnumerator ChainRoutine(Vector2 at, CastRef cast)
    {
        yield return new WaitForSeconds(ChainDelay);
        Explode(at, cast, 1f + ChainRangeStep, Chain1Pct);
        yield return new WaitForSeconds(ChainDelay);
        Explode(at, cast, 1f + ChainRangeStep * 2f, Chain2Pct);
    }

    /// <summary>집속탄: 적중 위치에서 화염구 3개를 120도 간격으로 (60도 비껴서 직진 방향과 겹치지 않게). 총 피해량 60% 고정 피해</summary>
    void ShootShards(Vector2 at, Vector2 dir, CastRef cast)
    {
        float size = Mathf.Max(0.3f, Range(Tier.Short) * 0.3f) * ShardSizeMul;
        float dmg = FinalDamageOf(DirectDamage) * ShardPct;
        for (int k = 0; k < 3; k++)
        {
            Vector2 d = Rot(dir, 60f + 120f * k);
            var sp = Projectile.Spawn(at, d, ShardSpeed, FixedOf(dmg, cast), 1, ShardRange / ShardSpeed, "FireBall", new Color(1f, 0.55f, 0.15f), size);
            AttachBallFx(sp, d, size);
            sp.onHit = (e, hitAt) => StrikeOn(e);
        }
    }

    /// <summary>홍염: 적중 위치에서 화염구 3개를 120도 간격으로 → 모든 대상을 관통하며 나갔다가 발사 지점으로 복귀. 총 피해량 60% 고정 피해</summary>
    void ShootCrimson(Vector2 at, Vector2 dir, Vector2 origin, CastRef cast)
    {
        float size = Mathf.Max(0.3f, Range(Tier.Short) * 0.3f) * CrimsonSizeMul;
        float dmg = FinalDamageOf(DirectDamage) * ShardPct;
        for (int k = 0; k < 3; k++)
            FireBallReturner.Spawn(at, Rot(dir, 60f + 120f * k), origin, CrimsonSpeed, CrimsonOut, FixedOf(dmg, cast), size, () => this == null || CheatDisabled, StrikeOn);
    }

    /// <summary>
    /// 폭죽놀이: 종료 위치에서 화염구 12개 (30도 간격). 각 화염구는 종료 시 폭발하고, 1세대 화염구의 폭발은 한 번 더 12개로 퍼져나감 (2세대는 폭발만, 사용자 2026-11-22).
    /// 총 피해량 20% 고정 피해. generation = 1 (처음 퍼짐) · 2 (폭발에서 다시 퍼짐)
    /// </summary>
    void Fireworks12(Vector2 at, CastRef cast, int generation = 1)
    {
        float size = Mathf.Max(0.3f, Range(Tier.Short) * 0.3f) * FireworksSizeMul;
        float dmg = FinalDamageOf(DirectDamage) * FireworksPct;
        float baseAngle = Random.Range(0f, 360f / FireworksCount);
        for (int k = 0; k < FireworksCount; k++)
        {
            Vector2 d = Rot(Vector2.right, baseAngle + 360f / FireworksCount * k);
            var sp = Projectile.Spawn(at, d, FireworksSpeed, FixedOf(dmg, cast), 1, FireworksRange / FireworksSpeed, "FireBall", new Color(1f, 0.55f, 0.15f), size);
            AttachBallFx(sp, d, size);
            sp.onHit = (e, hitAt) => StrikeOn(e);
            sp.onEnd = pos =>   // 종료 시 1회 추가 발동: 폭발 + (1세대면) 한 번 더 퍼져나감
            {
                if (CheatDisabled) return;
                Explode(pos, cast, 1f, FireworksPct);
                if (generation < 2) Fireworks12(pos, cast, generation + 1);
            };
        }
    }

    // 투사체 이펙트 ErnStray_FireBallFx (2026-11-22 사용자): 2×2 칸 640×352, 4프레임 반복. 머리 = 오른쪽, 꼬리 = 왼쪽 (진행 방향이 머리).
    // 머리 (밝은 구) 지름 약 215px → 투사체 지름과 같게, 머리 중심은 칸 중심에서 오른쪽 180px
    const string BallSheet = "ErnStray_FireBallFx";
    const float BallFps = 12f;          // [임시] 4프레임 = 약 0.33초 반복
    const float BallCellPerHead = 640f / 215f;   // 칸 가로 = 머리 지름 x 이 값 (측정값)
    const float BallHeadOffsetCells = 180f / 640f;   // 머리 중심이 칸 중심에서 떨어진 정도 (칸 가로 단위)

    /// <summary>도형 투사체 대신 불덩이 그림을 붙임 (그림이 없으면 기존 원 그대로)</summary>
    public static void AttachBallFx(Component host, Vector2 dir, float size)
    {
        var frames = SpriteEffect.LoadFrames(BallSheet, 2, 2, 1f, 4);
        if (frames == null || host == null) return;
        if (host.TryGetComponent<SpriteRenderer>(out var circle)) circle.enabled = false;
        float scale = BallCellPerHead;   // 투사체 크기 (부모 스케일 = 머리 지름) 기준
        var fx = SpriteLoopEffect.Spawn(frames, BallFps, host.transform, new Vector2(-BallHeadOffsetCells * scale, 0f), 9, scale);
        if (fx != null && dir.x < 0f) fx.transform.localScale = new Vector3(scale, -scale, 1f);   // 왼쪽으로 날 때 위아래가 뒤집히지 않게
    }

    /// <summary>방향이 바뀌었을 때 (도탄 · 복귀) 그림이 뒤집히지 않게 세로 부호를 맞춤</summary>
    public static void FlipFx(Component host, Vector2 dir)
    {
        var fx = host != null ? host.GetComponentInChildren<SpriteLoopEffect>() : null;
        if (fx == null) return;
        var sc = fx.transform.localScale;
        fx.transform.localScale = new Vector3(sc.x, Mathf.Abs(sc.y) * (dir.x < 0f ? -1f : 1f), sc.z);
    }

    /// <summary>
    /// 적중 위치 폭발: 반경 안 모든 적에게 폭발 피해 (직격당한 적 포함).
    /// [해석] 폭발 = '직접 피해' · '간접 사용' (같은 사용 번호) — 직격에서 파생된 공격이라 간접 사용으로 판정.
    /// radiusMul = 범위 배율 (연속 폭발), fixedPct > 0 이면 폭발 총 피해량의 그 비율을 고정 피해로 (연속 폭발 · 폭죽놀이)
    /// </summary>
    void Explode(Vector2 at, CastRef cast, float radiusMul = 1f, float fixedPct = 0f)
    {
        if (CheatDisabled) return;
        float r = ExplosionRadius * radiusMul;
        var frames = SpriteEffect.LoadFrames(ExplosionSheet, 3, 3, 1f, ExplosionFrames);
        if (frames != null) SpriteEffect.Spawn(frames, ExplosionFps, null, at, 0f, false, 12, r * 2f * ExplosionFxPerDiameter);
        else Fx.Disc(at, r, Fx.Fire, 0.2f);   // 그림이 없으면 도형 대체
        HitDebug.Circle(at, r, HitDebug.BlastColor);
        DamageInfo dmg;
        if (fixedPct > 0f) dmg = FixedOf(FinalDamageOf(ExplosionDamage) * fixedPct, cast);
        else
        {
            dmg = MakeDamage(ExplosionDamage, cast.AsIndirect);
            if (HasSpecial(FlameStrike)) dmg.perTarget = StrikeMul;
        }
        SkillHits.Circle(at, r, dmg, Enemy.NoKnock, StrikeOn);
    }
}

/// <summary>
/// 홍염 화염구: 모든 대상을 관통 (대상마다 1번 피해) 하며 outDist 만큼 나갔다가 발사 지점 (origin) 으로 복귀해 사라짐.
/// 고정 피해 · 간접 사용 (홍염 규칙). 그림은 파이어 볼 불덩이 그림 재사용.
/// </summary>
public class FireBallReturner : MonoBehaviour
{
    static readonly List<Enemy> buf = new List<Enemy>();
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    Vector2 dir, origin;
    float speed, outLeft, radius;
    bool returning;
    DamageInfo dmg;
    System.Func<bool> cancelled;

    System.Action<Enemy> onHit;

    public static FireBallReturner Spawn(Vector2 at, Vector2 dir, Vector2 origin, float speed, float outDist, DamageInfo dmg, float size, System.Func<bool> cancelled, System.Action<Enemy> onHit = null)
    {
        var go = new GameObject("FireBallReturner");
        go.transform.position = at;
        go.transform.localScale = Vector3.one * size;
        go.transform.right = dir;
        var r = go.AddComponent<FireBallReturner>();
        r.dir = dir.normalized; r.origin = origin; r.speed = speed; r.outLeft = outDist; r.dmg = dmg; r.radius = size * 0.5f; r.cancelled = cancelled; r.onHit = onHit;
        SpriteFactory.AddRenderer(go, "FireBall", SpriteFactory.Shape.Circle, new Color(1f, 0.55f, 0.15f), 8);
        FireBall.AttachBallFx(r, r.dir, size);
        return r;
    }

    void Update()
    {
        if (GameManager.I != null && GameManager.I.State != GameState.Playing) return;
        if (cancelled != null && cancelled()) { Destroy(gameObject); return; }
        float step = speed * Time.deltaTime;
        if (!returning)
        {
            transform.position += (Vector3)(dir * step);
            outLeft -= step;
            if (outLeft <= 0f) returning = true;
        }
        else
        {
            Vector2 to = origin - (Vector2)transform.position;
            if (to.magnitude <= step + 0.1f) { Destroy(gameObject); return; }
            dir = to.normalized;
            transform.right = dir;
            FireBall.FlipFx(this, dir);
            transform.position += (Vector3)(dir * step);
        }
        Vector2 pos = transform.position;
        foreach (var e in Enemy.CopyAll(buf))
        {
            if (e == null || e.hp <= 0 || hit.Contains(e)) continue;
            float rr = radius + e.radius;
            if (((Vector2)e.transform.position - pos).sqrMagnitude > rr * rr) continue;
            hit.Add(e);
            e.Hit(dmg, dir);
            onHit?.Invoke(e);
        }
    }
}

/// <summary>
/// A04 파이어 버스터: 거대한 폭발로 주변 적 밀치기 · 초기 피해량 20 · 쿨타임 10초.
/// 특수 강화 (2026-11-22 구현, 노션 기준. 노션에 없는 세부는 [해석] · [임시] 표시):
///   5레벨 열파 (범위 +30% · 넉백 +50%) · 점화 (적중 시 화상 +2) · 고농축 (범위 -25% · 피해량 +75%)
///   10레벨 이중 폭발 (종료 시 0.5초 뒤 폭발 1회, 총 피해량 100% 고정) · 뇌진탕 (노션 미입력 → 선택 불가) · 과열 반응 (화상 적 적중 시 최종 피해량 +50%)
///   15레벨 초신성 (범위 +100% · 최종 피해량 +150% · 쿨타임 +100%) · 연쇄 폭발 (화상 적 적중 시 폭발, 대상 화상 총 피해량 100% 고정) · 연속 폭발 (종료 시 0.25초 간격 3회, 총 피해량 40% 고정)
/// </summary>
public class FireBuster : AutoSkill
{
    const float Knock = 9f;             // [임시] 밀치기 세기
    const float BaseDamage = 60f, BaseCooldown = 10f;   // 초기 피해량 20 → 60 (밸런스 확정 2026-11-24)
    // 폭발 이펙트 ErnStray_FireBusterExplosion (2026-11-20, 원본 Project Slayers/output/FireBuster_Explosion_v2): 3×3, 칸 512, 8프레임 (9번째 칸 빈 칸), 1회 재생
    const string FxSheet = "ErnStray_FireBusterExplosion";
    const int FxFrames = 8;
    const float FxFps = 15f;            // manifest 제안 프레임 30~90ms (평균 약 0.064초) → 8프레임 약 0.53초
    const float FxPerDiameter = 1f / 0.80f;   // 최대 폭발 프레임(5번) 그림 폭 410px / 칸 512px → 칸 가로 = 판정 지름 ÷ 0.80 (측정값). 6~8번 불씨는 판정 밖으로 조금 더 퍼짐

    // 특수 강화 id (파이어 버스터)
    public const string Wave = "FU5A", Ignite = "FU5B", Dense = "FU5C";
    public const string Double = "FU10A", Concuss = "FU10B", Overheat = "FU10C";
    public const string Nova = "FU15A", ChainBlast = "FU15B", Barrage = "FU15C";

    // 특수 강화 확정 수치 (노션)
    const float WaveRange = 0.3f, WaveKnock = 0.5f;          // 열파: 범위 +30% · 넉백 +50%
    const int IgniteStacks = 2;                              // 점화: 화상 +2
    const float DenseRange = -0.25f, DenseFinal = 0.75f;     // 고농축: 범위 -25% · 최종 피해량 +75% (2026-11-24 피해량 → 최종 피해량)
    const float WaveDamage = 0.25f;                          // 열파: 피해량 +25% (2026-11-24)
    const float ConcussFinal = 1.25f, ConcussStun = 1f;      // 뇌진탕: 최종 피해량 +25% · 넉백된 적이 다른 적과 충돌하면 기절 1초 (2026-11-24 노션)
    const float OverheatCooldownCut = 0.025f;                // 과열 반응: 화상 상태 적 적중 시 남은 쿨타임 2.5% 감소
    const float DoubleDelay = 0.5f, DoublePct = 1f;          // 이중 폭발: 0.5초 뒤 1회, 총 피해량 100% 고정
    const float OverheatFinal = 0.5f;                        // 과열 반응: 화상 적 최종 피해량 +50%
    const float NovaRange = 1f, NovaFinal = 1.5f, NovaCooldown = 1f;   // 초신성: 범위 +100% · 최종 피해량 +150% · 쿨타임 +100%
    const float ChainBlastPct = 1f;                          // 연쇄 폭발: 대상 화상 총 피해량의 100% 고정
    const float BarrageInterval = 0.25f, BarragePct = 0.4f;  // 연속 폭발: 0.25초 간격 3회, 총 피해량 40% 고정
    const int BarrageCount = 3;
    // [임시]
    const float ChainBlastRatio = 0.5f; // 연쇄 폭발 반경 = 범위 좁음 × 비율 (노션에 반경 없음, 파이어 볼 폭발과 같은 비율)

    const float RangeMul = 1.75f;       // 범위 배율 (사용자 2026-11-22: 2.5배 → 30% 감소 = 1.75배). 판정 · 이펙트 크기 모두
    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => Range(Tier.Mid) * RangeMul;
    float KnockSpeed => Knock * (HasSpecial(Wave) ? 1f + WaveKnock : 1f);

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Wave, title = "열파", desc = "범위 +30%, 넉백 +50%, 피해량 +25%" },
                new SpecialUpgradeOption { id = Ignite, title = "점화", desc = "적중 시 적용된 모든 화상 +2" },
                new SpecialUpgradeOption { id = Dense, title = "고농축", desc = "범위 -25%, 최종 피해량 +75%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Double, title = "이중 폭발", keywords = new[] { "근거리", "간접 사용", "직접 피해", "넉백" }, desc = "종료 시 0.5초 뒤 폭발이 1회 추가 발동합니다. 파이어 버스터 총 피해량의 100% 고정 피해." },
                new SpecialUpgradeOption { id = Concuss, title = "뇌진탕", keywords = new[] { "기절" }, desc = "넉백된 적이 다른 적과 부딪히면 1초 기절, 최종 피해량 +25%" },
                new SpecialUpgradeOption { id = Overheat, title = "과열 반응", desc = "화상 상태의 적 적중 시 최종 피해량 +50%, 남은 쿨타임 2.5% 감소" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Nova, title = "초신성", desc = "범위 +100%, 최종 피해량 +150%, 쿨타임 +100%" },
                new SpecialUpgradeOption { id = ChainBlast, title = "연쇄 폭발", keywords = new[] { "간접 사용", "지속 피해", "고정 피해", "근거리" }, desc = "화상 상태의 적 적중 시 폭발이 발생합니다. 폭발은 대상의 모든 화상 총 피해량의 100% 고정 피해." },
                new SpecialUpgradeOption { id = Barrage, title = "연속 폭발", keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "근거리" }, desc = "종료 시 0.25초 간격으로 폭발이 3회 추가 발생합니다. 파이어 버스터 총 피해량의 40% 고정 피해." },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override float SpecialRangeBonus => (HasSpecial(Wave) ? WaveRange : 0f) + (HasSpecial(Dense) ? DenseRange : 0f) + (HasSpecial(Nova) ? NovaRange : 0f);
    /// <summary>열파: 피해량 +25% (2026-11-24 밸런스 · 기본 강화와 합연산)</summary>
    protected override float SpecialDamageBonus => HasSpecial(Wave) ? WaveDamage : 0f;
    /// <summary>최종 피해량 (독립 배율): 고농축 ×1.75 (피해량 → 최종 피해량) · 뇌진탕 ×1.25 · 초신성 ×2.5</summary>
    protected override float SpecialFinalDamageMul =>
        (HasSpecial(Dense) ? 1f + DenseFinal : 1f) * (HasSpecial(Concuss) ? ConcussFinal : 1f) * (HasSpecial(Nova) ? 1f + NovaFinal : 1f);
    protected override float SpecialCooldownMul => HasSpecial(Nova) ? 1f + NovaCooldown : 1f;
    // 기본 강화 1회 (밸런스 확정 2026-11-24): 피해량 +30% · 쿨타임 -5%
    public override float BasicDamageStep => 0.3f;
    public override float BasicCooldownStep => 0.05f;

    /// <summary>과열 반응: 화상 상태 적에게 최종 피해량 +50% (대상 단계 배율 — 피해가 들어가기 전의 화상 상태 기준).
    /// 화상 상태 적에게 적중할 때마다 남은 쿨타임 2.5% 감소 (2026-11-24, 적중마다 적용 [해석])</summary>
    float OverheatMul(Enemy e)
    {
        if (!FlameSword.IsBurning(e)) return 1f;
        ReduceRemainingCooldown(OverheatCooldownCut);
        return 1f + OverheatFinal;
    }

    /// <summary>같은 사용에서 나온 간접 사용 고정 피해 (모든 증감 무효)</summary>
    DamageInfo FixedOf(float amount, CastRef cast, DamageType type = DamageType.Direct)
    {
        var d = FixedDamage(amount, type, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    // ---------- 발동 ----------
    static readonly List<Enemy> scan = new List<Enemy>();

    protected override bool Fire()
    {
        Vector2 from = P.transform.position;
        // 인식 범위 없음: 주변에 적이 없어도 쿨타임마다 상시 발동 (사용자 2026-11-22)
        var cast = DirectCast;
        Burst(from, cast, 0f, KnockSpeed);
        CameraFollow.Shake(0.08f, 0.08f);
        // 종료 시 추가 발동 (같은 자리, 플레이어 위치에 고정)
        if (HasSpecial(Double)) StartCoroutine(EchoRoutine(from, cast, 1, DoubleDelay, DoublePct, KnockSpeed));   // 이중 폭발: 넉백 유지
        if (HasSpecial(Barrage)) StartCoroutine(EchoRoutine(from, cast, BarrageCount, BarrageInterval, BarragePct, 0f));   // 연속 폭발: 넉백 없음 (노션 키워드에 넉백 없음)
        return true;
    }

    /// <summary>count 회, interval 초 간격으로 같은 자리에서 폭발 (총 피해량의 pct 고정 피해, 간접 사용)</summary>
    IEnumerator EchoRoutine(Vector2 at, CastRef cast, int count, float interval, float pct, float knock)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new WaitForSeconds(interval);
            if (CheatDisabled) yield break;
            Burst(at, cast.AsIndirect, pct, knock);
        }
    }

    /// <summary>
    /// 폭발 1회 (이펙트 + 판정). fixedPct = 0 이면 기본 피해 (직접 피해), > 0 이면 총 피해량의 그 비율을 고정 피해로 (추가 폭발).
    /// 점화 · 연쇄 폭발은 이 스킬의 모든 폭발 적중에 적용 (연쇄 폭발의 폭발 자체는 제외)
    /// </summary>
    void Burst(Vector2 at, CastRef cast, float fixedPct, float knock)
    {
        float r = Radius;
        var frames = SpriteEffect.LoadFrames(FxSheet, 3, 3, 1f, FxFrames);
        if (frames != null) SpriteEffect.Spawn(frames, FxFps, null, at, 0f, false, 12, r * 2f * FxPerDiameter);
        else Fx.Disc(at, r, Fx.FireDeep, 0.25f);   // 그림이 없으면 도형 대체

        DamageInfo dmg;
        if (fixedPct > 0f) dmg = FixedOf(FinalDamageOf(BaseDamage) * fixedPct, cast);
        else
        {
            dmg = MakeDamage(BaseDamage, cast);
            if (HasSpecial(Overheat)) dmg.perTarget = OverheatMul;   // 인스턴스 메서드 (화상 적중마다 쿨타임 감소)
        }

        // 연쇄 폭발: "화상 상태의 적" = 맞기 전에 이미 화상인 적 (이번 피해가 화상을 새로 거는 경우는 제외)
        Dictionary<Enemy, float> burning = null;
        if (HasSpecial(ChainBlast))
        {
            burning = new Dictionary<Enemy, float>();
            foreach (var e in SkillHits.InCircle(at, r, scan))
                if (FlameSword.IsBurning(e)) burning[e] = FlameSword.OneTickDamage(e);   // 총 피해량 = 1틱 × 스택 (전체 지속시간 아님, 사용자 2026-11-23)
        }

        SkillHits.Circle(at, r, dmg, knock, e =>
        {
            if (HasSpecial(Concuss) && e != null && e.hp > 0) e.MarkConcussion(T.concussionWindow, ConcussStun, T.concussionStunsOther);   // 뇌진탕: 넉백 중 다른 적과 충돌하면 기절 (솔라 슬래시와 같은 방식)
            if (HasSpecial(Ignite) && FlameSword.I != null) FlameSword.I.AddStacks(e, IgniteStacks);   // 점화: 적용된 화상 +2 (이번 적중이 건 화상 포함 [해석])
            if (burning != null && burning.TryGetValue(e, out float total)) ChainExplode(e.transform.position, total, cast);
        });
    }

    /// <summary>연쇄 폭발: 화상 상태 적이 맞은 자리에서 폭발. 그 적의 화상 총 피해량의 100% 를 범위 안에 고정 · 지속 피해로</summary>
    void ChainExplode(Vector2 at, float burnTotal, CastRef cast)
    {
        if (burnTotal <= 0f) return;
        float r = Range(Tier.Short) * ChainBlastRatio;
        var frames = SpriteEffect.LoadFrames(FxSheet, 3, 3, 1f, FxFrames);
        if (frames != null) SpriteEffect.Spawn(frames, FxFps, null, at, 0f, false, 12, r * 2f * FxPerDiameter);
        else Fx.Disc(at, r, Fx.Fire, 0.2f);
        HitDebug.Circle(at, r, HitDebug.BlastColor);
        SkillHits.Circle(at, r, FixedOf(burnTotal * ChainBlastPct, cast.AsIndirect, DamageType.DoT), Enemy.NoKnock);
    }
}

/// <summary>
/// A05 파이어 블레이드 (2026-11-20 노션 기준): 일정 범위를 베어냄 (첫 베기 초기 피해량 5)
/// → 첫 베기에 맞은 적들에게서 연속 참격 (초기 피해량 3 × 5회) → 끝나면 각 대상 위치 폭발 범위 피해 (초기 피해량 20) · 쿨타임 15초.
/// 특수 강화 (2026-11-22 구현, 노션 기준. 노션에 없는 세부는 [해석] · [임시] 표시):
///   5레벨 난무 (연속 참격 횟수 +5) · 회전 베기 (첫 베기를 플레이어 둘레 회전 베기로, 범위 +50%) · 확산 (종료 시 폭발에 맞은 적마다 연속 베기 + 폭발, 1회만, 고정 피해)
///   10레벨 참격 집중 (연속 베기 삭제, 횟수 1당 폭발 피해량 +75%) · 흉터 새기기 (연속 베기 적중 시 받는 최종 피해량 +5% 부여, 최대 10스택) · 절삭 가속 (적중 시 남은 쿨타임 감소 +2.5%)
///   15레벨 삼연발 (120° 각도로 2회 추가 사용) · 연쇄 폭발 (폭발 종료 시 대상에게 폭발 추가 발동) · 타오르는 참격 (화상 상태 적 적중 시 최종 피해량 +100%)
/// </summary>
public class FireBlade : AutoSkill
{
    const int MaxTargets = 8;           // [임시] 연속 참격 대상 수 제한 (기획 미정, 첫 베기는 범위 안 전부)
    const int SlashCount = 5;           // 베기 공격 횟수 5
    const float FirstSlashDamage = 10f; // 첫 베기 초기 피해량 5 → 10 (밸런스 확정 2026-11-24)
    const float SlashDamage = 15f, ExplosionDamage = 40f, BaseCooldown = 15f;   // 연속 베기 3 → 15 · 폭발 20 → 40 (2026-11-24)
    const float SpinFinal = 0.5f;               // 회전 베기: 최종 피해량 +50% (2026-11-24)
    const float SpreadDamage = 0.25f;           // 확산: 피해량 +25%
    const float TripleDamage = 0.5f;            // 삼연발: 피해량 +50%
    const float SlashInterval = 0.12f;  // [임시]
    const float ExplosionRatio = 0.35f; // [임시] 폭발 반경 = 범위 좁음 × 비율 (기획 미정)
    const float FirstSlashDistance = 1.5f;   // 첫 베기 중심 = 플레이어 앞 이 거리 (사용자 2026-11-22: 2.5 → 1.5 더 가깝게). 판정 · 이펙트 · 적 인식이 같은 중심. 이전에는 플레이어 위치

    // 특수 강화 id (파이어 블레이드)
    public const string Frenzy = "FBL5A", Spin = "FBL5B", Spread = "FBL5C";
    public const string Focus = "FBL10A", Scar = "FBL10B", Accel = "FBL10C";
    public const string Triple = "FBL15A", ChainBlast = "FBL15B", Blazing = "FBL15C";

    // 특수 강화 확정 수치 (노션)
    const int FrenzyBonus = 5;                  // 난무: 연속 참격 공격 횟수 +5
    const float SpinRange = 0.5f;               // 회전 베기: 범위 +50%
    const float FocusPerSlash = 0.75f;          // 참격 집중: 연속 베기 공격 횟수 1당 폭발 피해량 +75%
    const float ScarPerStack = 0.05f;           // 흉터 새기기: 받는 최종 피해량 +5% 부여
    const int ScarMaxStacks = 10;               // 최대 스택 10
    const float AccelPerHit = 0.025f;           // 절삭 가속: 적중 시 남은 쿨타임 감소 +2.5%
    const float TripleAngle = 120f;             // 삼연발: 120도 각도로 2회 추가 사용
    const float BlazingFinal = 1f;              // 타오르는 참격: 화상 상태 적 최종 피해량 +100%
    // [임시] 노션에 없는 수치
    const int SpreadMaxTargets = 12;            // 확산: 폭발에 맞은 적 중 추가 발생을 일으킬 최대 수
    const float ChainBlastDelay = 0.4f;         // 연쇄 폭발: 폭발이 끝난 뒤 추가 폭발까지 (폭발 연출 길이 정도)
    const float AccelReductionCap = 0.9f;       // 절삭 가속 누적 감소 상한 (스킬 쿨타임 최종 감소 90% 상한과 같게)

    // 첫 베기 이펙트 ErnStray_FireBladeSlash (원본 Project Slayers/output/FireSlash_OpenRing_Animation_v1):
    // 4×4, 칸 768, 15프레임 + 빈 칸, 시계 반대로 약 300° 베고 오른쪽이 열린 고리. 열린 쪽이 바라보는 방향을 향하게 회전 (2026-11-20 사용자)
    const string FirstSlashSheet = "ErnStray_FireBladeSlash";
    const int FirstSlashFrames = 15;
    const float FirstSlashFps = 26f;    // manifest 30~40ms (15프레임 약 0.53초)
    const float FirstSlashCellPx = 768f;
    const float ArcCorePx = 267f;       // 측정: 고리 안쪽 경계 약 232px · 바깥 불꽃 끝 약 302px 의 중간 → 이 반지름 = 판정 반경
    static readonly Vector2 ArcCenterPx = new Vector2(22.5f, 5.5f);   // 측정: 고리 중심이 칸 중심에서 오른쪽 22.5px · 위 5.5px
    const float FirstSlashHitDelay = 0.24f;   // [해석] 그림에서 고리가 거의 닫히는 9번 프레임 (8 × 30ms) 에 첫 베기 판정

    // 연속 베기 이펙트 ErnStray_FireBladeComboSlash (원본 Project Slayers/output/FireSlash_ComboSingle_v1):
    // 4열×3행, 칸 512, 11프레임 + 빈 칸, 왼쪽 아래 → 오른쪽 위 한 줄 참격. 베기마다 방향 · 각도 무작위 (2026-11-20 사용자)
    const string ComboSlashSheet = "ErnStray_FireBladeComboSlash";
    const int ComboSlashFrames = 11;
    const float ComboSlashFps = 40f;          // manifest 20~30ms (11프레임 약 0.28초)
    const float ComboSlashCellPx = 512f;
    const float ComboStrokePx = 450f;         // 측정: 가장 큰 프레임(6번) 참격 길이 (43,343)→(415,90)
    const float ComboStrokeAngle = 34f;       // 측정: 같은 두 끝점을 이은 각도 (화면 기준 오른쪽 위)
    static readonly Vector2 ComboStrokeCenterPx = new Vector2(-27f, 39.5f);   // 측정: 참격 중심이 칸 중심에서 왼쪽 27 · 위 39.5px
    const float ComboStrokeWorld = 2.4f;      // [임시] 참격 길이 (월드). 사용자 2026-11-22: 2배 (1.2 → 2.4). 처음 도형 참격 0.67

    // 마지막 폭발 이펙트 ErnStray_FireBladeExplosion (원본 Project Slayers/output/FireExplosion_DiagonalTear_v1):
    // 4열×3행, 칸 768, 11프레임 + 빈 칸, 불꽃 덩어리 + 왼쪽 아래 ↔ 오른쪽 위 대각선 파편. 그림 그대로 (회전 · 반전 없음)
    const string ExplosionSheet = "ErnStray_FireBladeExplosion";
    const int ExplosionFrames = 11;
    const float ExplosionFps = 20f;           // manifest 30~70ms (11프레임 약 0.53초)
    const float ExplosionFxPerDiameter = 768f / 400f;   // 측정: 최대 불꽃 덩어리(9번, 파편 제외) 폭 473 · 높이 326px 의 평균 약 400px = 판정 지름. 파편은 더 바깥으로 퍼짐

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    float Radius => Range(Tier.Mid);

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Frenzy, title = "난무", desc = "연속 참격의 공격 횟수 +5" },
                new SpecialUpgradeOption { id = Spin, title = "회전 베기", desc = "첫 베기를 회전 베기로 변경합니다. 범위 +50%, 최종 피해량 +50%" },
                new SpecialUpgradeOption { id = Spread, title = "확산", keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "근거리" }, desc = "종료 시 폭발에 맞은 적들에게 연속 베기와 폭발이 발생합니다. 각 피해량은 원본과 동일, 추가 연속 베기는 1회만 발생." },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Focus, title = "참격 집중", desc = "연속 베기를 삭제합니다. 연속 베기 공격 횟수 1당 마지막 폭발 피해량 +75%" },
                new SpecialUpgradeOption { id = Scar, title = "흉터 새기기", desc = "연속 베기 적중 시 받는 최종 피해량 +5%를 부여합니다. 최대 스택 10" },
                new SpecialUpgradeOption { id = Accel, title = "절삭 가속", desc = "적중 시 남은 쿨타임 감소 +2.5%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Triple, title = "삼연발", desc = "120도 각도로 2회 추가 사용합니다." },
                new SpecialUpgradeOption { id = ChainBlast, title = "연쇄 폭발", desc = "폭발 종료 시 대상에게 폭발이 추가 발동합니다." },
                new SpecialUpgradeOption { id = Blazing, title = "타오르는 참격", desc = "화상 상태 적 적중 시 최종 피해량 +100%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    int ChainSlashCount => HasSpecial(Focus) ? 0 : SlashCount + (HasSpecial(Frenzy) ? FrenzyBonus : 0);
    /// <summary>참격 집중이 삭제한 연속 베기 횟수 (폭발 피해량 증가의 기준)</summary>
    int FocusSlashCount => SlashCount + (HasSpecial(Frenzy) ? FrenzyBonus : 0);
    protected override float SpecialRangeBonus => HasSpecial(Spin) ? SpinRange : 0f;
    protected override float SpecialFinalDamageMul => HasSpecial(Spin) ? 1f + SpinFinal : 1f;
    protected override float SpecialDamageBonus => (HasSpecial(Spread) ? SpreadDamage : 0f) + (HasSpecial(Triple) ? TripleDamage : 0f);
    // 기본 강화 1회 (밸런스 확정 2026-11-24): 피해량 +20% · 쿨타임 -3.5%
    public override float BasicDamageStep => 0.2f;
    public override float BasicCooldownStep => 0.035f;

    /// <summary>타오르는 참격: 화상 상태 적에게 최종 피해량 +100% (대상 단계, 피해가 들어가기 전 화상 기준)</summary>
    static float BlazingMul(Enemy e) => FlameSword.IsBurning(e) ? 1f + BlazingFinal : 1f;

    DamageInfo Dmg(float initial, CastRef cast, float extraPct = 0f)
    {
        var d = MakeDamage(initial, cast, extraPct, 1f);
        if (HasSpecial(Blazing)) d.perTarget = BlazingMul;
        return d;
    }

    DamageInfo FixedOf(float amount, CastRef cast)
    {
        var d = FixedDamage(amount, DamageType.Direct, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    // ---------- 흉터 새기기: 받는 최종 피해량 증가 부여 (대상마다 스택, 사망하면 정리) ----------
    readonly Dictionary<Enemy, int> scars = new Dictionary<Enemy, int>();
    ScarModifier scarMod;

    sealed class ScarModifier : CombatModifier
    {
        readonly FireBlade owner;
        public ScarModifier(FireBlade o) { owner = o; }
        public override float ReceivedMul(Enemy target, in DamageInfo d)
            => owner != null && owner.scars.TryGetValue(target, out int n) && n > 0 ? 1f + ScarPerStack * n : 1f;
    }

    void AddScar(Enemy e)
    {
        if (e == null || e.hp <= 0) return;
        if (scarMod == null) { scarMod = new ScarModifier(this); CombatMods.Add(scarMod); }
        scars.TryGetValue(e, out int n);
        scars[e] = Mathf.Min(ScarMaxStacks, n + 1);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (scarMod != null) { CombatMods.Remove(scarMod); scarMod = null; }
        scars.Clear();
        running = 0;   // 코루틴이 끊겨도 영구 정지하지 않게 (Casting 은 base 가 해제)
    }

    // ---------- 발동 ----------
    /// <summary>한 번의 사용 (삼연발의 추가 사용 포함) 이 진행 중에 모으는 값</summary>
    class Use
    {
        public Vector2 aim;
        public CastRef cast;
        public int hits;        // 절삭 가속: 적중 횟수 (사용이 모두 끝난 뒤 쿨타임 감소로 환산)
        public bool extra;      // 삼연발의 추가 사용
    }

    int running;                // 진행 중인 사용 수 (0 이 되면 쿨타임 시작)
    int accelHits;              // 이번 발동의 적중 횟수 합계 (모든 사용)

    protected override bool Fire()
    {
        // 범위 (플레이어에서 첫 베기 중심까지 + 반경) 안에 적이 있으면 가장 가까운 적 방향으로 알아서 사용 (사용자 2026-11-22)
        Vector2 me = P.transform.position;
        var target = Nearest(me, FirstSlashDistance + Radius);
        if (target == null) return false;
        Vector2 aim = ((Vector2)target.transform.position - me);
        aim = aim.sqrMagnitude > 0.0001f ? aim.normalized : P.Facing.normalized;
        Casting = true;   // 첫 베기 → 연속 참격 → 폭발이 끝난 뒤 쿨타임
        accelHits = 0;
        var cast = DirectCast;
        StartUse(new Use { aim = aim, cast = cast });
        if (HasSpecial(Triple))   // 삼연발: 120도 각도로 2회 추가 사용 (간접 사용, 같은 사용 번호)
        {
            StartUse(new Use { aim = Rot(aim, TripleAngle), cast = cast.AsIndirect, extra = true });
            StartUse(new Use { aim = Rot(aim, -TripleAngle), cast = cast.AsIndirect, extra = true });
        }
        return true;
    }

    void StartUse(Use u) { running++; StartCoroutine(RunUse(u)); }

    static Vector2 Rot(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    void EndUse(Use u)
    {
        accelHits += u.hits;
        if (--running > 0) return;
        Casting = false;
        // 절삭 가속: 적중마다 남은 쿨타임 -2.5% (곱연산 누적), 쿨타임이 시작된 뒤 한 번에 적용 (진행 중에는 쿨타임이 돌지 않아 감소가 무시되므로)
        if (HasSpecial(Accel) && accelHits > 0)
            ReduceRemainingCooldown(Mathf.Min(AccelReductionCap, 1f - Mathf.Pow(1f - AccelPerHit, accelHits)));
    }

    /// <summary>첫 베기 이펙트: 열린 쪽 = 플레이어 쪽 (대상 방향의 반대), 고리 중심 = 대상 방향으로 플레이어에서 center 만큼 (그림 속 고리 중심 오프셋 보정). 플레이어를 따라감</summary>
    void SpawnFirstSlashFx(Vector2 aimDir, float r, float centerDist)
    {
        var frames = SpriteEffect.LoadFrames(FirstSlashSheet, 4, 4, 1f, FirstSlashFrames);
        Vector2 ahead = aimDir * centerDist;
        if (frames == null) { Fx.Circle((Vector2)P.transform.position + ahead, r, Fx.FireBright, 0.25f, P.transform); return; }
        float width = r * FirstSlashCellPx / ArcCorePx;
        float angle = Mathf.Atan2(-aimDir.y, -aimDir.x) * Mathf.Rad2Deg;   // 고리의 열린 쪽 = 플레이어 쪽
        Vector2 offset = (Vector2)(Quaternion.Euler(0f, 0f, angle) * (ArcCenterPx * (width / FirstSlashCellPx)));
        SpriteEffect.Spawn(frames, FirstSlashFps, P.transform, ahead - offset, angle, false, 11, width);
    }

    IEnumerator RunUse(Use u)
    {
        var cast = u.cast;
        float r = Radius;
        Vector2 dirNow = u.aim;   // 사용 시점의 대상 방향 고정
        float centerDist = HasSpecial(Spin) ? 0f : FirstSlashDistance;   // 회전 베기: 플레이어 자신이 중심 (둘레 전체)
        SpawnFirstSlashFx(dirNow, r, centerDist);
        yield return new WaitForSeconds(FirstSlashHitDelay);
        if (CheatDisabled) { EndUse(u); yield break; }

        // 1) 첫 베기: 범위 안 모든 적 (넉백 없음 — 노션 키워드에 넉백 없음). 맞은 적 = 연속 참격 대상
        Vector2 from = (Vector2)P.transform.position + dirNow * centerDist;   // 첫 베기 중심 (플레이어가 움직여도 방향은 사용 시점 값)
        var hit = new List<Enemy>();
        SkillHits.Circle(from, r, Dmg(FirstSlashDamage, cast), 0f, e => { hit.Add(e); u.hits++; });
        HitDebug.Circle(from, r, HitDebug.SlashColor);

        // 가까운 순으로 제한 (살아 있는 적만)
        hit.RemoveAll(e => e == null || e.hp <= 0);
        hit.Sort((a, b) => ((Vector2)a.transform.position - from).sqrMagnitude
                       .CompareTo(((Vector2)b.transform.position - from).sqrMagnitude));
        var targets = hit.GetRange(0, Mathf.Min(MaxTargets, hit.Count));
        if (targets.Count > 0) yield return ChainSlashes(u, targets);
        EndUse(u);
    }

    /// <summary>
    /// 연속 베기 1회 이펙트: 참격 방향 = d (밀리는 방향과 같게), 좌우 반전 무작위 → 베는 방향 · 각도가 매번 다름.
    /// 참격 중심이 적 위치에 오도록 그림 속 중심 오프셋 보정. 적을 따라가지 않음 (0.28초). 그림이 없으면 false
    /// </summary>
    bool SpawnComboSlashFx(Vector2 at, Vector2 d)
    {
        var frames = SpriteEffect.LoadFrames(ComboSlashSheet, 3, 4, 1f, ComboSlashFrames);
        if (frames == null) return false;
        bool flip = Random.value < 0.5f;
        // 그림 속 참격 각도: 그대로 34°, 좌우 반전이면 180 - 34° → d 방향에 맞게 돌림
        float strokeAngle = flip ? 180f - ComboStrokeAngle : ComboStrokeAngle;
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - strokeAngle;
        float k = ComboStrokeWorld / ComboStrokePx;   // 월드 / px
        var centerPx = new Vector2(flip ? -ComboStrokeCenterPx.x : ComboStrokeCenterPx.x, ComboStrokeCenterPx.y);
        Vector2 offset = (Vector2)(Quaternion.Euler(0f, 0f, angle) * (centerPx * k));
        SpriteEffect.Spawn(frames, ComboSlashFps, null, at - offset, angle, flip, 12, ComboSlashCellPx * k);
        return true;
    }

    /// <summary>폭발 이펙트 (그림이 없으면 도형)</summary>
    void ExplosionFx(Vector2 at, float expR)
    {
        var fxFrames = SpriteEffect.LoadFrames(ExplosionSheet, 3, 4, 1f, ExplosionFrames);
        if (fxFrames != null) SpriteEffect.Spawn(fxFrames, ExplosionFps, null, at, 0f, false, 12, expR * 2f * ExplosionFxPerDiameter);
        else Fx.Disc(at, expR, Fx.Fire, 0.25f);   // 그림이 없으면 도형 대체
        HitDebug.Circle(at, expR, HitDebug.BlastColor);
    }

    IEnumerator ChainSlashes(Use u, List<Enemy> targets)
    {
        var cast = u.cast;
        // 베기 초기 피해량 3 × 5회, 폭발 초기 피해량 20 (각각 피해 이벤트마다 내림). 참격 집중: 연속 베기 삭제, 횟수 1당 폭발 피해량 +75%
        DamageInfo perSlash = Dmg(SlashDamage, cast);
        DamageInfo explosion = Dmg(ExplosionDamage, cast, HasSpecial(Focus) ? FocusPerSlash * FocusSlashCount : 0f);
        float expR = Range(Tier.Short) * ExplosionRatio;

        var lastPos = new Vector2[targets.Count];
        for (int i = 0; i < targets.Count; i++) lastPos[i] = targets[i].transform.position;
        int slashes = ChainSlashCount;
        for (int s = 0; s < slashes; s++)
        {
            for (int i = 0; i < targets.Count; i++)
            {
                var e = targets[i];
                if (e == null || e.hp <= 0) continue;
                lastPos[i] = e.transform.position;
                var d = Random.insideUnitCircle.normalized;
                if (!SpawnComboSlashFx(lastPos[i], d))
                    Fx.Box(lastPos[i] - d * 0.33f, d, 0.67f, 0.08f, Fx.FireBright, 0.12f);   // 그림이 없을 때 도형 (카메라 시야 7 기준 × 2/3)
                e.Hit(perSlash, d, 0.5f);
                u.hits++;
                if (HasSpecial(Scar)) AddScar(e);   // 흉터 새기기: 연속 베기 적중 시 받는 최종 피해량 +5% 부여
            }
            yield return new WaitForSeconds(SlashInterval);
        }

        // 폭발: 각 대상 위치. 확산 · 연쇄 폭발은 폭발이 끝난 뒤 이어서
        var hitByExplosion = new HashSet<Enemy>();
        var positions = new List<Vector2>();
        for (int i = 0; i < targets.Count; i++)
        {
            var e = targets[i];
            Vector2 at = e != null ? (Vector2)e.transform.position : lastPos[i];
            if (at == Vector2.zero && e == null) continue;
            positions.Add(at);
            ExplosionFx(at, expR);
            SkillHits.Circle(at, expR, explosion, 2f, x => { hitByExplosion.Add(x); u.hits++; });
        }

        if (CheatDisabled) yield break;
        // 확산 (5레벨): 폭발에 맞은 적마다 연속 베기와 폭발이 1회 더 (고정 피해, 각 피해량은 원본과 동일, 확산 자체는 다시 퍼지지 않음)
        if (HasSpecial(Spread) && hitByExplosion.Count > 0)
            yield return SpreadRoutine(u, hitByExplosion, expR);

        // 연쇄 폭발 (15레벨): 폭발 종료 시 대상에게 폭발 추가 발동 (피해량은 원본 폭발과 동일 [해석], 한 번만)
        if (HasSpecial(ChainBlast))
        {
            yield return new WaitForSeconds(ChainBlastDelay);
            if (CheatDisabled) yield break;
            DamageInfo again = Dmg(ExplosionDamage, cast.AsIndirect, HasSpecial(Focus) ? FocusPerSlash * FocusSlashCount : 0f);
            foreach (var at in positions)
            {
                ExplosionFx(at, expR);
                SkillHits.Circle(at, expR, again, 2f, x => u.hits++);
            }
        }
    }

    /// <summary>
    /// 확산: 폭발에 맞은 적 (최대 SpreadMaxTargets) 마다 원본과 같은 피해량의 연속 베기 (공격 횟수만큼) 와 폭발을 고정 피해로 1회씩.
    /// 참격 집중이면 연속 베기가 없으므로 폭발만 (집중으로 늘어난 폭발 피해량도 원본과 동일하게)
    /// </summary>
    IEnumerator SpreadRoutine(Use u, HashSet<Enemy> victims, float expR)
    {
        var cast = u.cast.AsIndirect;
        var list = new List<Enemy>(victims);
        if (list.Count > SpreadMaxTargets) list.RemoveRange(SpreadMaxTargets, list.Count - SpreadMaxTargets);
        float slashFixed = FinalDamageOf(SlashDamage);
        float blastFixed = FinalDamageOf(ExplosionDamage) * (1f + (HasSpecial(Focus) ? FocusPerSlash * FocusSlashCount : 0f));
        int slashes = ChainSlashCount;
        var pos = new Vector2[list.Count];
        for (int i = 0; i < list.Count; i++) pos[i] = list[i] != null ? (Vector2)list[i].transform.position : Vector2.zero;

        for (int s = 0; s < slashes; s++)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || e.hp <= 0) continue;
                pos[i] = e.transform.position;
                var d = Random.insideUnitCircle.normalized;
                if (!SpawnComboSlashFx(pos[i], d)) Fx.Box(pos[i] - d * 0.33f, d, 0.67f, 0.08f, Fx.FireBright, 0.12f);
                e.Hit(FixedOf(slashFixed, cast), d, 0.5f);
                u.hits++;
            }
            yield return new WaitForSeconds(SlashInterval);
        }
        for (int i = 0; i < list.Count; i++)
        {
            Vector2 at = list[i] != null ? (Vector2)list[i].transform.position : pos[i];
            if (at == Vector2.zero) continue;
            ExplosionFx(at, expR);
            SkillHits.Circle(at, expR, FixedOf(blastFixed, cast), 2f, x => u.hits++);
        }
    }
}

/// <summary>
/// A06 파이어 스톰: 플레이어 중심 화염 소용돌이 (간접 사용 · 지속 피해 2, 3초, 적 느려짐) → 폭발 (직접 피해 15) 하며 밀치기 · 쿨타임 14초.
/// 특수 강화 (2026-11-22 구현, 노션 기준. 노션에 없는 세부는 [해석] · [임시] 표시):
///   5레벨 거대한 폭풍 (범위 +30% · 지속 시간 +1초) · 가속 회전 (피해 간격 -50%) · 폭풍전야 (소용돌이 최종 피해량 -75% · 폭발 +100%)
///   10레벨 고정 폭풍 (플레이어를 따라오지 않음 · 범위 +50% · 지속 시간 +100%) · 추적 폭풍 (적을 추적 · 유도, 쿨타임 감소 +25%) · 타오르는 불씨 (화상 상태 적 적중 시 받는 최종 피해량 +5% 부여, 최대 10스택)
///   15레벨 폭심 붕괴 (지속 시간 -75% · 최종 피해량 +200%) · 쌍둥이 태풍 (주위 무작위 장소에 추가 소용돌이) · 초가속 (공격 간격 -75%)
///   특수 조합 천염폭풍 (고정 폭풍 또는 추적 폭풍 + 쌍둥이 태풍): 폭발 삭제 · 무작위 장소에 추가 소용돌이 10개 · 범위 -50% · 최종 피해량 +50% · 쿨타임 +25%
/// </summary>
public class FireStorm : AutoSkill
{
    const float Duration = 3f;          // 화염 소용돌이 지속 시간 3초
    const float TickInterval = 1f;      // [해석] 지속 피해 기본 피해 간격 1초 (결정 기록)
    const float SlowMul = 0.5f;         // [임시] 슬로우 비율 (기획 미정)
    const float DotDamage = 8f, ExplosionDamage = 15f, BaseCooldown = 14f;   // 소용돌이 틱 2 → 8 (밸런스 확정 2026-11-24)
    // 기본 강화 1회 (밸런스 확정 2026-11-24): 피해량 +20% · 쿨타임 -3.5%
    public override float BasicDamageStep => 0.2f;
    public override float BasicCooldownStep => 0.035f;
    const float Knock = 9f;             // [임시]
    const float RangeMul = 1.69f;       // 범위 배율 (사용자 2026-11-22: 30% 증가를 두 번 = 1.3 x 1.3). 소용돌이 · 폭발 모두

    // 특수 강화 id (파이어 스톰)
    public const string Giant = "FS5A", Accel = "FS5B", Eve = "FS5C";
    public const string FixedStorm = "FS10A", Chase = "FS10B", Ember = "FS10C";
    public const string Collapse = "FS15A", Twin = "FS15B", Overdrive = "FS15C";
    public const string ComboFixed = "FSC1", ComboChase = "FSC2";   // 천염폭풍 (고정 폭풍 + 쌍둥이 태풍 / 추적 폭풍 + 쌍둥이 태풍)

    // 특수 강화 확정 수치 (노션)
    const float GiantRange = 0.3f, GiantDuration = 1f;           // 거대한 폭풍: 범위 +30% · 지속 시간 +1초
    const float AccelInterval = 0.5f;                            // 가속 회전: 피해 간격 -50% (배율 0.5)
    const float EveVortexFinal = 0.25f, EveBlastFinal = 2f;      // 폭풍전야: 소용돌이 최종 피해량 -75% · 폭발 +100%
    const float FixedRange = 0.5f, FixedDurationPct = 1f;        // 고정 폭풍: 범위 +50% · 지속 시간 +100%
    const float ChaseCooldown = 0.75f;                           // 추적 폭풍: 쿨타임 감소 +25% (배율 0.75)
    const float EmberPerStack = 0.05f;                           // 타오르는 불씨: 받는 최종 피해량 +5% 부여
    const int EmberMaxStacks = 10;                               // 최대 스택 10
    const float CollapseDurationPct = -0.75f, CollapseFinal = 3f;   // 폭심 붕괴: 지속 시간 -75% · 최종 피해량 +200%
    const float OverdriveInterval = 0.25f;                       // 초가속: 공격 간격 -75% (배율 0.25)
    const int TwinCount = 1;                                     // 쌍둥이 태풍: 추가 소용돌이 1개 [해석]
    const int ComboCount = 10;                                   // 천염폭풍: 추가 소용돌이 10개
    const float ComboRange = -0.5f, ComboFinal = 1.5f, ComboCooldown = 1.25f;   // 천염폭풍: 범위 -50% · 최종 피해량 +50% · 쿨타임 +25%
    // [임시] 노션에 없는 수치
    const float ExtraMinDist = 2.5f, ExtraMaxDist = 7f;          // 추가 소용돌이가 생기는 플레이어로부터의 거리
    const float ChaseSpeed = 3.5f;                               // 추적 폭풍: 소용돌이 이동 속도 (월드/초)
    const float ChaseSearch = 30f;                               // 추적 폭풍: 적 탐색 거리
    const float MinTickInterval = 0.1f;                          // 피해 간격 하한 (가속 연소 규칙과 같게)

    // 소용돌이 고리 그림 ErnStray_FireStormRing (2026-11-20 사용자 전달 1장, 1254px 정사각, 원본 webp = Art/Incoming/ErnStray/FireStorm_Ring_source.webp)
    // 생성: 플레이어 위치에서 점점 커져 판정 크기에 맞춤 → 지속 중 시계 반대 회전 → 폭발: 커지면서 사라짐 (2026-11-20 사용자)
    const string RingSprite = "ErnStray_FireStormRing";
    const float RingCanvasPx = 1254f;
    const float RingMidPx = 380f;       // 측정: 고리 안쪽 경계 약 275px · 바깥 불꽃 끝 약 485px 의 중간 → 이 반지름 = 소용돌이 판정 반경
    const float RingGrowTime = 0.3f;    // [임시] 생성 시 0 → 판정 크기까지 커지는 시간
    const float RingSpin = 180f;        // [임시] 회전 속도 (도/초, + = 시계 반대)
    const float RingBurstTime = 0.3f;   // [임시] 폭발 때 판정 크기 → 폭발 범위까지 커지며 사라지는 시간

    // ---------- 특수 강화 · 조합 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Giant, title = "거대한 폭풍", desc = "범위 +30%, 지속 시간 +1초" },
                new SpecialUpgradeOption { id = Accel, title = "가속 회전", desc = "피해 간격 -50%" },
                new SpecialUpgradeOption { id = Eve, title = "폭풍전야", desc = "소용돌이 최종 피해량 -75%, 폭발 최종 피해량 +100%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = FixedStorm, title = "고정 폭풍", desc = "소용돌이가 더이상 플레이어를 따라오지 않습니다. 범위 +50%, 지속 시간 +100%" },
                new SpecialUpgradeOption { id = Chase, title = "추적 폭풍", keywords = new[] { "추적", "유도" }, desc = "소용돌이가 적을 추적 및 유도합니다. 쿨타임 감소 +25%" },
                new SpecialUpgradeOption { id = Ember, title = "타오르는 불씨", desc = "화상 상태 적 적중 시 받는 최종 피해량 5% 부여, 최대 스택 10" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Collapse, title = "폭심 붕괴", desc = "지속 시간 -75%, 최종 피해량 +200%" },
                new SpecialUpgradeOption { id = Twin, title = "쌍둥이 태풍", desc = "주위 무작위 장소에 추가 소용돌이를 생성합니다." },
                new SpecialUpgradeOption { id = Overdrive, title = "초가속", desc = "공격 간격 -75%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboFixed, title = "천염폭풍", recipe = "고정 폭풍 + 쌍둥이 태풍", requires = new[] { FixedStorm, Twin },
            specs = new[] { "폭발 삭제", "무작위 장소에 10개의 추가 소용돌이 생성", "'범위' -50%", "'최종 피해량' +50%", "'쿨타임' +25%" },
        },
        new SpecialComboOption
        {
            id = ComboChase, title = "천염폭풍", recipe = "추적 폭풍 + 쌍둥이 태풍", requires = new[] { Chase, Twin },
            specs = new[] { "폭발 삭제", "무작위 장소에 10개의 추가 소용돌이 생성", "'범위' -50%", "'최종 피해량' +50%", "'쿨타임' +25%" },
        },
    };

    bool Combo => HasCombo(ComboFixed) || HasCombo(ComboChase);

    // ---------- 수치 ----------
    /// <summary>지속 시간 = (기본 + 고정 증가) × (1 + % 증가 합) — 화염의 검과 같은 규칙. 최소 0.3초</summary>
    float DurationNow
    {
        get
        {
            float flat = Duration + (HasSpecial(Giant) ? GiantDuration : 0f);
            float pct = (HasSpecial(FixedStorm) ? FixedDurationPct : 0f) + (HasSpecial(Collapse) ? CollapseDurationPct : 0f);
            return Mathf.Max(0.3f, flat * (1f + pct));
        }
    }

    /// <summary>피해 간격 기본값 (가속 회전 · 초가속은 서로 다른 효과라 곱연산 [해석]). 가속 연소는 이 값에서 더 줄임</summary>
    float BaseInterval => TickInterval * (HasSpecial(Accel) ? AccelInterval : 1f) * (HasSpecial(Overdrive) ? OverdriveInterval : 1f);

    protected override float SpecialRangeBonus =>
        (HasSpecial(Giant) ? GiantRange : 0f) + (HasSpecial(FixedStorm) ? FixedRange : 0f) + (Combo ? ComboRange : 0f);
    /// <summary>소용돌이 · 폭발 공통 최종 피해량 (폭심 붕괴 · 천염폭풍). 폭풍전야는 둘이 달라 따로</summary>
    protected override float SpecialFinalDamageMul => (HasSpecial(Collapse) ? CollapseFinal : 1f) * (Combo ? ComboFinal : 1f);
    protected override float SpecialCooldownMul => (HasSpecial(Chase) ? ChaseCooldown : 1f) * (Combo ? ComboCooldown : 1f);

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    // ---------- 타오르는 불씨: 받는 최종 피해량 증가 부여 (대상마다 스택) ----------
    readonly Dictionary<Enemy, int> embers = new Dictionary<Enemy, int>();
    EmberModifier emberMod;

    sealed class EmberModifier : CombatModifier
    {
        readonly FireStorm owner;
        public EmberModifier(FireStorm o) { owner = o; }
        public override float ReceivedMul(Enemy target, in DamageInfo d)
            => owner != null && owner.embers.TryGetValue(target, out int n) && n > 0 ? 1f + EmberPerStack * n : 1f;
    }

    void AddEmber(Enemy e)
    {
        if (e == null || e.hp <= 0) return;
        if (emberMod == null) { emberMod = new EmberModifier(this); CombatMods.Add(emberMod); }
        embers.TryGetValue(e, out int n);
        embers[e] = Mathf.Min(EmberMaxStacks, n + 1);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (emberMod != null) { CombatMods.Remove(emberMod); emberMod = null; }
        embers.Clear();
    }

    // ---------- 발동 ----------
    static readonly List<Enemy> scan = new List<Enemy>();

    protected override bool Fire()
    {
        // 인식 범위 없음: 주변에 적이 없어도 쿨타임마다 상시 발동 (사용자 2026-11-22)
        var cast = DirectCast;
        Vector2 me = P.transform.position;
        // 천염폭풍: 폭발 삭제, 추가 소용돌이 10개. 쌍둥이 태풍: 추가 소용돌이 1개
        StartCoroutine(Storm(me, !HasSpecial(FixedStorm) && !HasSpecial(Chase), HasSpecial(Chase), cast, false, !Combo));
        int extras = Combo ? ComboCount : (HasSpecial(Twin) ? TwinCount : 0);
        for (int i = 0; i < extras; i++)
        {
            float ang = Random.Range(0f, Mathf.PI * 2f), dist = Random.Range(ExtraMinDist, ExtraMaxDist);
            Vector2 at = me + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * dist;
            StartCoroutine(Storm(at, false, HasSpecial(Chase), cast.AsIndirect, true, !Combo));   // 추가 소용돌이: 그 자리에 남음 (추적 폭풍이면 같이 추적), 간접 사용
        }
        return true;
    }

    /// <summary>
    /// 소용돌이 1개: 지속 시간 동안 틱마다 범위 안 지속 피해 + 감속 → 끝나면 폭발 (explode). follow = 플레이어를 따라감, chase = 가장 가까운 적을 추적.
    /// extra = 추가 소용돌이 (모두 간접 사용). 폭풍전야는 소용돌이와 폭발의 최종 피해량을 따로 조정
    /// </summary>
    IEnumerator Storm(Vector2 startPos, bool follow, bool chase, CastRef cast, bool extra, bool explode)
    {
        // 화염 소용돌이 = 간접 사용 · 지속 피해 (틱마다 내림), 마지막 폭발 = 직접 사용 · 직접 피해 (추가 소용돌이의 폭발은 간접 사용)
        DamageInfo perTick = MakeDamage(DotDamage, extra ? cast : cast.AsIndirect, 0f, HasSpecial(Eve) ? EveVortexFinal : 1f, DamageType.DoT);
        float rSmall = Range(Tier.Short) * RangeMul;
        float duration = DurationNow;

        Vector2 pos = follow ? (Vector2)P.transform.position : startPos;
        var ringFrames = SpriteEffect.LoadFrames(RingSprite, 1, 1, 1f);
        FireStormRingFx vortex = null;
        FxShape ring = null;
        if (ringFrames != null) vortex = FireStormRingFx.Spawn(ringFrames[0], pos, rSmall * RingCanvasPx / RingMidPx, RingGrowTime, RingSpin);
        else ring = Fx.GroundCircle(pos, rSmall, Fx.FireDeep, duration);   // 그림이 없으면 장판 도형 (바닥 레이어)

        float tickT = 0f;   // 틱 타이머: 시작 즉시 첫 틱 (기존과 같게)
        bool first = true;
        for (float elapsed = 0f; elapsed < duration - 1e-4f;)
        {
            float dt = Time.deltaTime;
            if (follow && P != null) pos = P.transform.position;
            else if (chase) pos = ChaseStep(pos, dt);
            if (vortex != null) vortex.transform.position = pos;
            else if (ring != null) ring.transform.position = pos;

            float iv = Mathf.Max(MinTickInterval, CombatMods.DotInterval(BaseInterval));
            tickT += dt;
            if (first || tickT >= iv)
            {
                if (!first) tickT -= iv;
                first = false;
                if (CheatDisabled) break;
                TickAt(pos, rSmall, perTick, iv);
            }
            elapsed += dt;
            yield return null;
        }
        if (ring != null) Destroy(ring.gameObject);

        float rBig = Range(Tier.Mid) * RangeMul;
        if (!explode)
        {
            // 천염폭풍: 폭발 삭제 — 고리는 그대로 옅어지며 사라짐
            if (vortex != null) vortex.Burst(rSmall * RingCanvasPx / RingMidPx, RingBurstTime);
            yield break;
        }
        // 폭발: 고리가 폭발 범위까지 커지며 사라짐 (그림이 없으면 도형 폭발)
        if (vortex != null) vortex.Burst(rBig * RingCanvasPx / RingMidPx, RingBurstTime);
        else Fx.Disc(pos, rBig, Fx.Fire, 0.3f);
        HitDebug.Circle(pos, rBig, HitDebug.BlastColor);
        DamageInfo blast = MakeDamage(ExplosionDamage, cast, 0f, HasSpecial(Eve) ? EveBlastFinal : 1f);   // 추가 소용돌이의 cast 는 이미 간접 사용
        HitWithEmber(pos, rBig, blast, Knock);
        CameraFollow.Shake(0.1f, 0.1f);
    }

    /// <summary>소용돌이 틱 1회: 범위 안 지속 피해 + 감속 (+ 타오르는 불씨)</summary>
    void TickAt(Vector2 pos, float r, DamageInfo perTick, float iv)
    {
        HitWithEmber(pos, r, perTick, 0.3f, e => e.SustainSlow(SlowMul, iv + 0.1f));   // 범위 안에 있는 동안 유지 (누적 없음)
    }

    /// <summary>범위 피해 + 타오르는 불씨 (피해가 들어가기 전에 화상이던 적에게 받는 최종 피해량 +5% 부여)</summary>
    void HitWithEmber(Vector2 pos, float r, DamageInfo dmg, float knock, System.Action<Enemy> also = null)
    {
        HashSet<Enemy> burning = null;
        if (HasSpecial(Ember))
        {
            burning = new HashSet<Enemy>();
            foreach (var e in SkillHits.InCircle(pos, r, scan)) if (FlameSword.IsBurning(e)) burning.Add(e);
        }
        SkillHits.Circle(pos, r, dmg, knock, e =>
        {
            also?.Invoke(e);
            if (burning != null && burning.Contains(e)) AddEmber(e);
        });
    }

    /// <summary>추적 폭풍: 가장 가까운 적 쪽으로 이동 (ChaseSpeed). 적이 없으면 제자리</summary>
    Vector2 ChaseStep(Vector2 pos, float dt)
    {
        var t = Nearest(pos, ChaseSearch);
        if (t == null) return pos;
        return Vector2.MoveTowards(pos, t.transform.position, ChaseSpeed * dt);
    }
}

/// <summary>
/// 파이어 스톰 소용돌이 고리 (그림 1장): 따라갈 대상 위치에서 0 → 목표 크기로 커짐 · 계속 회전 → Burst() 로 그 자리에서 커지며 사라짐.
/// 크기 = 정사각 그림 한 변의 월드 길이.
/// </summary>
public class FireStormRingFx : MonoBehaviour
{
    SpriteRenderer sr;
    float spin, size, growTime, t;
    const float MaxLife = 10f;   // 안전장치: 스킬 코루틴이 끊겨 Burst 가 오지 않아도 이 시간이 지나면 사라짐 (소용돌이 3초보다 충분히 김)
    // 폭발 (Burst) 상태
    bool bursting;
    float burstFrom, burstTo, burstTime, bt;

    /// <summary>worldPos 에 생성. 따라가는 것은 호출한 쪽이 transform.position 을 매 프레임 바꿔서 (플레이어 따라가기 · 추적 · 고정)</summary>
    public static FireStormRingFx Spawn(Sprite sprite, Vector2 worldPos, float size, float growTime, float spinDegPerSec)
    {
        var go = new GameObject("FX_FireStormRing");
        go.transform.position = worldPos;
        go.transform.localScale = Vector3.zero;
        var fx = go.AddComponent<FireStormRingFx>();
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = sprite;
        fx.sr.sortingOrder = 11;
        fx.size = size; fx.growTime = growTime; fx.spin = spinDegPerSec;
        return fx;
    }

    /// <summary>그 자리에 남기고 (따라가지 않음) 지금 크기 → toSize 로 커지며 투명해진 뒤 사라짐</summary>
    public void Burst(float toSize, float time)
    {
        if (this == null) return;
        transform.SetParent(null, true);
        bursting = true;
        burstFrom = transform.localScale.x; burstTo = toSize; burstTime = Mathf.Max(0.01f, time); bt = 0f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        transform.Rotate(0f, 0f, spin * dt);
        if (bursting)
        {
            bt += dt;
            float k = Mathf.Clamp01(bt / burstTime);
            transform.localScale = Vector3.one * Mathf.Lerp(burstFrom, burstTo, 1f - (1f - k) * (1f - k));   // 빠르게 커졌다가 느려짐
            var c = sr.color; c.a = 1f - k; sr.color = c;
            if (k >= 1f) Destroy(gameObject);
            return;
        }
        t += dt;
        if (t > MaxLife) { Destroy(gameObject); return; }
        if (t < growTime + dt)
        {
            float k = Mathf.Clamp01(t / growTime);
            transform.localScale = Vector3.one * size * (1f - (1f - k) * (1f - k));   // 빠르게 커졌다가 느려짐
        }
    }
}
