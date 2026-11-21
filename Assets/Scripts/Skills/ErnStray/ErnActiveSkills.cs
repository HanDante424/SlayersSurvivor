using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 수동 5종 · 이동 1종 · 궁극기 1종 (기획서 5~7장, 수치 = 2026-11-11 스킬 설명 중간본)
// 방향은 플레이어가 마지막으로 이동한 방향(Facing) 기준.
// 기획서에 없는 값은 각 클래스 맨 위 "[임시]" 상수.

/// <summary>M02 파이어 차지: 주변 적을 밀치며 돌진 · 사용 중 무적 · 초기 피해량 4 · 쿨타임 10초 (노션 2026-11-20)</summary>
public class FireCharge : ActiveSkill
{
    const float BaseDamage = 4f, BaseCooldown = 10f;
    const float Distance = 2.67f;       // [임시] 돌진 거리 (기획 미정). 카메라 시야 7 복귀에 맞춰 4 × 2/3
    const float DashTime = 0.25f;       // [임시]
    const float Knock = 8f;             // [임시]

    // 불꽃 망토 이펙트 (FireMantle_MoveLoop_v4 원본): 5열 × 4행 · 칸 768 × 512 · 20프레임 반복 · 오른쪽을 향함
    // 캐릭터 뒤 레이어 + 앞 레이어 (앞은 캐릭터 자리 알파 63 이하라 캐릭터가 비쳐 보임)
    const string MantleBackSheet = "ErnStray_FireChargeMantleBack", MantleFrontSheet = "ErnStray_FireChargeMantleFront";
    const int MantleRows = 4, MantleCols = 5;
    const float MantleFps = 20f;            // 초안 프레임 50ms
    const float MantleCellPx = 768f;
    const float MantlePivotOffsetPx = 96f;  // 캐릭터 기준점 (480, 256) = 칸 가운데에서 앞쪽(오른쪽)으로 96px
    const float MantleHeightPx = 295f;      // 그림 세로 범위 (프레임별 알파 범위 102~397px)
    const float MantleHeightWorld = 1.85f;  // [임시] 망토 세로 = 캐릭터 키(약 1.62) × 약 1.15
    const float CharCenterY = 0.17f;        // [해석] 캐릭터 몸 가운데 = 발 기준점 위 0.17 (칸 2.07 · 발 16px · 키 200px 에서 계산)
    const int MantleBackOrder = 9, MantleFrontOrder = 11;   // 플레이어 = 10

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    /// <summary>돌진 방향으로 망토 뒤 · 앞 레이어를 띄움 (플레이어를 따라감). 그림이 없으면 null</summary>
    SpriteLoopEffect[] SpawnMantle(Vector2 dir)
    {
        float pxWorld = MantleHeightWorld / MantleHeightPx;
        float cellWorld = MantleCellPx * pxWorld;
        var back = SpriteEffect.LoadFrames(MantleBackSheet, MantleRows, MantleCols, cellWorld);
        var front = SpriteEffect.LoadFrames(MantleFrontSheet, MantleRows, MantleCols, cellWorld);
        if (back == null || front == null) return null;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        // [해석] 왼쪽으로 갈 때 위아래가 뒤집히지 않도록 세로 반전 (윗꼬리는 계속 위)
        bool mirror = dir.x < 0f;
        Vector2 at = new Vector2(0f, CharCenterY) - dir * (MantlePivotOffsetPx * pxWorld);
        var fx = new[]
        {
            SpriteLoopEffect.Spawn(back, MantleFps, P.transform, at, MantleBackOrder, 1f),
            SpriteLoopEffect.Spawn(front, MantleFps, P.transform, at, MantleFrontOrder, 1f),
        };
        foreach (var f in fx)
        {
            f.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (mirror) f.transform.localScale = new Vector3(1f, -1f, 1f);
            Destroy(f.gameObject, DashTime + 0.5f);   // 안전장치: 돌진이 끊겨 onEnd 가 안 와도 남지 않게
        }
        return fx;
    }

    protected override void Activate()
    {
        hit.Clear();
        float r = Range(Tier.Mid) * 0.5f;   // [임시] 돌진 중 판정은 범위 중간의 절반
        DamageInfo dmg = MakeDamage(BaseDamage);
        var mantle = SpawnMantle(P.Facing);
        if (mantle == null) Fx.Circle(P.transform.position, r, Fx.Fire, DashTime + 0.1f, P.transform, 0f);   // 그림이 없을 때만
        P.Dash(P.Facing, Distance, DashTime, true,
            onStep: () =>
            {
                Vector2 at = P.transform.position;
                foreach (var e in SkillHits.InCircle(at, r, found))
                {
                    if (!hit.Add(e)) continue;
                    Vector2 d = (Vector2)e.transform.position - at;
                    e.Hit(dmg, d, 0f);
                    e.Push(d, Knock);
                }
            },
            onEnd: () =>
            {
                if (mantle != null) foreach (var f in mantle) if (f != null) f.Stop();
                // 도착 지점에서 주변 적 밀치기
                Vector2 at = P.transform.position;
                Fx.Disc(at, Range(Tier.Mid), Fx.FireDeep, 0.2f);
                foreach (var e in SkillHits.InCircle(at, Range(Tier.Mid), found))
                    e.Push((Vector2)e.transform.position - at, Knock);
            });
    }
}

/// <summary>
/// M03 오버 드라이브 플레임 슬래시 (구 오버 플레임 슬래시): 화염의 검을 내려쳐 기절 → 1초 뒤 같은 자리 폭발.
/// 화염의 검 초기 피해량 20 · 기절 2초, 폭발 딜레이 1초 · 초기 피해량 50, 쿨타임 20초
/// </summary>
public class OverFlameSlash : ActiveSkill
{
    const float BladeDamage = 20f, StunSeconds = 2f;
    const float ExplosionDelay = 1f, ExplosionDamage = 50f;
    const float BaseCooldown = 20f;
    const float WidthRatio = 0.5f;      // [임시] 검 판정 폭 = 범위 좁음 × 비율
    const float ExplosionWidthMul = 2.2f;   // [임시] 폭발 판정 폭 = 검 판정 폭 × 이 값 (같은 길이)

    // 화염검 그림 (FlameBlade_Upright_v5 원본): 4열 × 4행 · 칸 256 × 768 · 16프레임 반복 · 칼끝 위 · 곧은 날 = 오른쪽
    const string BladeSheet = "ErnStray_OverFlameBlade";
    const int BladeRows = 4, BladeCols = 4;
    const float BladeFps = 20f;             // 초안 프레임 50ms
    const float BladeCellPx = 256f;
    const float BladeRootFromCenterPx = 340f;   // 뿌리 기준점 (128, 724) = 칸 가운데(384)에서 아래로 340px
    const float BladeLengthPx = 674f;       // 칼 길이 (알파 범위 54~728px)
    const int BladeOrder = 12;
    const float WindupTime = 0.35f;         // [임시] 칼을 위로 치켜들고 준비하는 시간 (그동안 이동 불가 [해석])
    const float SwingTime = 0.08f;          // [임시] 내려치는 시간 → 끝나는 순간 판정
    const float BladeLinger = 0.15f;        // [임시] 내려친 뒤 칼이 남는 시간

    // 지면 잔흔 (FlameBlade_GroundTrace_Sustain_v1 원본): 4열 × 6행 · 칸 768 × 192 · 24프레임 반복 · 가로선 + 위로 오르는 불씨
    const string TraceSheet = "ErnStray_OverFlameGroundTrace";
    const int TraceRows = 6, TraceCols = 4;
    const float TraceFps = 20f;             // 초안 프레임 50ms
    const float TraceCellPx = 768f;
    const float TraceLengthPx = 660f;       // 선 길이 (알파 범위 54~714px)
    const float TracePivotBelowCenterPx = 28f;  // 기준점 (384, 124) = 칸 가운데(96)에서 아래로 28px = 지면선

    // 잔흔 폭발 (FlameBlade_GroundTrace_Burst_Overhead_v3 원본): 5열 × 4행 · 칸 768 × 384 · 20프레임 (마지막 = 투명) · 1회
    // 기준점 = 칸 가운데 = 지면선, 첫 프레임이 잔흔과 같은 모습 (같은 축척 · 선 길이 660px)
    const string BurstSheet = "ErnStray_OverFlameGroundBurst";
    const int BurstRows = 4, BurstCols = 5;
    const float BurstFps = 25f;             // [해석] 초안 프레임 30~50ms (합계 0.8초) → 20프레임 / 0.8초
    const int BurstOrder = 12;

    // 폭발 파티클 (FireExplosion_ParticlePop_v1 원본): 4열 × 3행 · 칸 256 · 12프레임 (마지막 = 투명) · 1회 · 제자리
    const string PopSheet = "ErnStray_FireParticlePop";
    const int PopRows = 3, PopCols = 4;
    const float PopFps = 33.3f;             // 초안 프레임 30ms (0.36초)
    const float PopCellPx = 256f, PopSpanPx = 175f;   // 가장 클 때 그림 폭 (알파 범위 47~226px)
    const float PopSize = 0.6f;             // [임시] 파티클 1개 크기 (월드)
    const int PopCount = 10;                // [임시] 폭발 1회당 개수
    const float PopSpread = 0.4f;           // [임시] 파티클이 나타나는 시간 폭 (폭발 시작부터, 초)
    const int PopOrder = 13;                // 폭발 그림 위

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 from = P.transform.position;
        Vector2 dir = P.Facing;
        float len = Reach(Tier.Long);
        float width = Range(Tier.Short) * WidthRatio;
        var cast = DirectCast;

        // 화염검: 위로 치켜든 채 준비 → 공격 방향으로 내려침 (그림이 없으면 예전처럼 바로 판정)
        Transform blade = SpawnBlade(from, dir, len, out float startAngle, out float endAngle);
        if (blade != null)
        {
            P.Root(WindupTime + SwingTime);
            yield return new WaitForSeconds(WindupTime);
            P.PlayAttack(dir);
            for (float t = 0f; t < SwingTime; t += Time.deltaTime)
            {
                float k = t / SwingTime;
                blade.rotation = Quaternion.Euler(0f, 0f, Mathf.LerpAngle(startAngle, endAngle, k * k) - 90f);   // 가속하며 내려침
                yield return null;
            }
            blade.rotation = Quaternion.Euler(0f, 0f, endAngle - 90f);
            Destroy(blade.gameObject, BladeLinger);
        }
        else
        {
            P.PlayAttack(dir);
            Fx.Box(from, dir, len, width, Fx.FireBright, 0.3f);
        }
        SkillHits.Box(from, dir, len, width, MakeDamage(BladeDamage, cast), 2f, e => e.Stun(StunSeconds));
        CameraFollow.Shake(0.08f, 0.08f);

        // [임시] 폭발 위치 = 검을 내려친 자리 (넓은 직사각형)
        // 내려친 자리에 지면 잔흔 → 폭발 때 제거. 그림이 없으면 예전 바닥 직사각형
        Transform trace = SpawnGroundTrace(from, dir, len);
        if (trace == null) Fx.Box(from, dir, len, width * ExplosionWidthMul, Fx.FireDeep, ExplosionDelay);
        yield return new WaitForSeconds(ExplosionDelay);
        if (trace != null) Destroy(trace.gameObject);
        if (!SpawnGroundBurst(from, dir, len)) Fx.Box(from, dir, len, width * ExplosionWidthMul, Fx.FireBright, 0.3f);
        StartCoroutine(SpawnParticlePops(from, dir, len, width * ExplosionWidthMul));
        SkillHits.Box(from, dir, len, width * ExplosionWidthMul, MakeDamage(ExplosionDamage, cast), 3f);
        CameraFollow.Shake(0.15f, 0.15f);
    }

    /// <summary>
    /// 화염검을 뿌리 = from 에 세움. 반환 = 회전축(뿌리) Transform, 그림이 없으면 null.
    /// 칼 길이 = 판정 길이. 시작 각도 = 공격 방향에서 90° 뒤 (오른쪽 공격이면 위, 왼쪽 공격이면 위 + 좌우 반전 → 곧은 날이 앞장섬)
    /// </summary>
    Transform SpawnBlade(Vector2 from, Vector2 dir, float len, out float startAngle, out float endAngle)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        endAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bool left = dir.x < -0.01f;
        startAngle = endAngle + (left ? -90f : 90f);
        float pxWorld = len / BladeLengthPx;
        var frames = SpriteEffect.LoadFrames(BladeSheet, BladeRows, BladeCols, BladeCellPx * pxWorld);
        if (frames == null) return null;

        var pivot = new GameObject("FX_OverFlameBladePivot").transform;
        pivot.position = from;
        pivot.rotation = Quaternion.Euler(0f, 0f, startAngle - 90f);   // 그림은 칼끝이 위(90°)
        var loop = SpriteLoopEffect.Spawn(frames, BladeFps, pivot, new Vector2(0f, BladeRootFromCenterPx * pxWorld), BladeOrder, 1f);
        if (left) loop.transform.localScale = new Vector3(-1f, 1f, 1f);   // [해석] 왼쪽으로 내려칠 땐 곧은 날이 앞(왼쪽)
        Destroy(pivot.gameObject, WindupTime + SwingTime + BladeLinger + 1f);   // 안전장치
        return pivot;
    }

    /// <summary>
    /// 내려친 자리(검 판정 직사각형 중심선)에 지면 잔흔. 선 길이 = 판정 길이, 바닥 레이어.
    /// [해석] 왼쪽 공격이면 세로 반전 (불씨가 계속 위로). 반환 = 루트 Transform, 그림이 없으면 null
    /// </summary>
    Transform SpawnGroundTrace(Vector2 from, Vector2 dir, float len)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float pxWorld = len / TraceLengthPx;
        var frames = SpriteEffect.LoadFrames(TraceSheet, TraceRows, TraceCols, TraceCellPx * pxWorld);
        if (frames == null) return null;

        var root = new GameObject("FX_OverFlameGroundTrace").transform;
        root.position = from + dir * (len * 0.5f);
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        if (dir.x < -0.01f) root.localScale = new Vector3(1f, -1f, 1f);
        SpriteLoopEffect.Spawn(frames, TraceFps, root, new Vector2(0f, TracePivotBelowCenterPx * pxWorld), Fx.GroundOrder, 1f);
        Destroy(root.gameObject, ExplosionDelay + 1f);   // 안전장치
        return root;
    }

    /// <summary>잔흔 자리에서 폭발 (잔흔과 같은 위치 · 회전 · 축척). 그림이 없으면 false</summary>
    bool SpawnGroundBurst(Vector2 from, Vector2 dir, float len)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float pxWorld = len / TraceLengthPx;
        var frames = SpriteEffect.LoadFrames(BurstSheet, BurstRows, BurstCols, TraceCellPx * pxWorld);
        if (frames == null) return false;
        var fx = SpriteEffect.Spawn(frames, BurstFps, null, from + dir * (len * 0.5f),
                                    Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, false, BurstOrder);
        if (dir.x < -0.01f) fx.transform.localScale = new Vector3(1f, -1f, 1f);   // [해석] 잔흔과 같이 세로 반전
        return true;
    }

    /// <summary>폭발 판정 직사각형 안 무작위 위치에 작은 불꽃 파티클을 시간차로 터뜨림 (크기 · 회전 · 좌우 무작위)</summary>
    IEnumerator SpawnParticlePops(Vector2 from, Vector2 dir, float len, float boxWidth)
    {
        var frames = SpriteEffect.LoadFrames(PopSheet, PopRows, PopCols, PopCellPx * PopSize / PopSpanPx);
        if (frames == null) yield break;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        Vector2 side = new Vector2(-dir.y, dir.x);
        var times = new float[PopCount];
        for (int i = 0; i < PopCount; i++) times[i] = Random.Range(0f, PopSpread);
        System.Array.Sort(times);
        float t = 0f;
        foreach (float at in times)
        {
            if (at > t) { yield return new WaitForSeconds(at - t); t = at; }
            Vector2 pos = from + dir * Random.Range(0f, len) + side * Random.Range(-0.5f, 0.5f) * boxWidth;
            SpriteEffect.Spawn(frames, PopFps, null, pos, Random.Range(0f, 360f), Random.value < 0.5f, PopOrder,
                               Random.Range(0.75f, 1.3f));   // [임시] 크기 편차
        }
    }
}

/// <summary>
/// M04 파이어 브레이커: 군중제어 · 범위 중간 · 피해 중간 · 쿨 중간
/// 최초 발동 범위 피해 + 1초 기절 → 2초 후 같은 위치에서 자동 재발동 (피해 + 1초 기절)
/// </summary>
public class FireBreaker : ActiveSkill
{
    const float StunSeconds = 1f;       // 기획서 확정
    const float RecastDelay = 2f;       // 기획서 확정 (폭발 딜레이 2초)
    const float BaseDamage = 10f, BaseCooldown = 10f;   // [해석] 두 번째 폭발도 초기 피해량 10

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 at = P.transform.position;   // 같은 위치에서 재발동
        float r = Range(Tier.Mid);
        var cast = DirectCast;
        P.PlayAttack();
        Burst(at, r, cast);
        var mark = Fx.GroundCircle(at, r, Fx.FireDeep, RecastDelay);   // 재발동 위치 표시 = 바닥 레이어
        yield return new WaitForSeconds(RecastDelay);
        if (mark != null) Destroy(mark.gameObject);
        Burst(at, r, cast);
    }

    // 지면 불꽃 분출 (FireEruption_GroundFan_v1 원본): 4열 × 4행 · 칸 512 · 16프레임 (마지막 = 투명) · 1회 · 옆에서 본 부채꼴
    const string EruptionSheet = "ErnStray_FireBreakerEruption";
    const string EruptionParticleSheet = "ErnStray_FireBreakerEruptionParticles";
    const int EruptionRows = 4, EruptionCols = 4;
    const float EruptionFps = 25f;          // [해석] 초안 프레임 20~60ms (합계 0.64초) → 16프레임 / 0.64초
    const float EruptionCellPx = 512f;
    const float EruptionWidthPx = 400f;     // 가장 넓을 때 폭 (알파 범위 56~457px)
    const float EruptionRootBelowCenterPx = 164f;   // 뿌리 (256, 420) = 칸 가운데(256)에서 아래로 164px
    const float EruptionRootDrop = 0.5f;    // [임시] 뿌리 위치 = 판정 원 중심에서 반지름 × 이 값만큼 아래
    const int EruptionOrder = 12;

    // 지면 원형 폭발 (FireExplosion_GroundCircle_v1 원본): 4열 × 4행 · 칸 512 · 16프레임 (마지막 = 투명) · 1회 · 위에서 본 고리
    const string GroundRingSheet = "ErnStray_FireBreakerGroundRing";
    const float GroundRingFps = 22f;        // [해석] 초안 프레임 30~70ms (합계 0.73초) → 16프레임 / 0.73초
    const float GroundRingRadiusPx = 215f;  // 다 퍼졌을 때 고리 반지름 (알파 가중 평균, 12~15번 프레임 약 215px)

    void Burst(Vector2 at, float r, CastRef cast)
    {
        // 바닥 고리: 다 퍼진 고리 반지름 = 판정 반경, 바닥 레이어 (캐릭터 · 적 아래)
        var ring = SpriteEffect.LoadFrames(GroundRingSheet, EruptionRows, EruptionCols, EruptionCellPx * r / GroundRingRadiusPx);
        if (ring != null) SpriteEffect.Spawn(ring, GroundRingFps, null, at, 0f, false, Fx.GroundOrder);

        // [임시] 부채꼴 폭 = 판정 원 지름
        float pxWorld = r * 2f / EruptionWidthPx;
        var frames = SpriteEffect.LoadFrames(EruptionSheet, EruptionRows, EruptionCols, EruptionCellPx * pxWorld);
        if (frames != null)
        {
            Vector2 root = at + Vector2.down * (r * EruptionRootDrop);
            Vector2 center = root + Vector2.up * (EruptionRootBelowCenterPx * pxWorld);
            SpriteEffect.Spawn(frames, EruptionFps, null, center, 0f, false, EruptionOrder);
            // 같이 쓰는 파티클 (FireEruption_GroundFan_Particles_v1): 같은 격자 · 뿌리 · 프레임 타이밍 → 같은 자리 · 크기로 위에 겹침
            var particles = SpriteEffect.LoadFrames(EruptionParticleSheet, EruptionRows, EruptionCols, EruptionCellPx * pxWorld);
            if (particles != null) SpriteEffect.Spawn(particles, EruptionFps, null, center, 0f, false, EruptionOrder + 1);
        }
        else Fx.Disc(at, r, Fx.Fire, 0.3f);   // 그림이 없을 때만
        SkillHits.Circle(at, r, MakeDamage(BaseDamage, cast), 1f, e => e.Stun(StunSeconds));
        CameraFollow.Shake(0.1f, 0.1f);
    }
}

/// <summary>M05 파이어 엑스큐션: 빠른 속도로 주변을 베어냄 · 초기 피해량 5 × 공격 횟수 30 · 쿨타임 30초 · 발동 중 이동 불가</summary>
public class FireExecution : ActiveSkill
{
    const float Duration = 1.5f;        // [임시] 공격 지속시간 (기획 미정) → 30회면 0.05초 간격
    const int Hits = 30;
    const float BaseDamage = 5f, BaseCooldown = 30f;

    // 연속 베기 고리 이펙트 (FireSlash_OmniCombo_v2 원본): 6열 × 5행 · 칸 640px · 30프레임 · 반복 재생
    const string SlashLoopSheet = "ErnStray_FireSlashOmniCombo";
    const int SlashLoopRows = 5, SlashLoopCols = 6;
    const float SlashLoopFps = 25f;       // 초안 프레임 40ms (한 바퀴 1.2초)
    const float SlashLoopCellPx = 640f;
    const float SlashLoopRadiusPx = 290f; // 그림 범위 반지름 (프레임별 알파 범위 34~623px 에서 잰 값)
    const int SlashLoopOrder = 11;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        P.Root(Duration);
        // [해석] 키워드 '고정' '피해' 의 의미 미확정 → 일반 직접 피해로 처리
        DamageInfo per = MakeDamage(BaseDamage, DirectCast);
        // [임시] 그림 반지름 = 판정 반경 (범위 중간). 발동 동안 플레이어를 따라감
        var frames = SpriteEffect.LoadFrames(SlashLoopSheet, SlashLoopRows, SlashLoopCols, 1f);
        float r0 = Range(Tier.Mid);
        var loop = SpriteLoopEffect.Spawn(frames, SlashLoopFps, P.transform, Vector2.zero, SlashLoopOrder,
                                          r0 * SlashLoopCellPx / SlashLoopRadiusPx);
        for (int i = 0; i < Hits; i++)
        {
            Vector2 at = P.transform.position;
            float r = Range(Tier.Mid);
            P.PlayAttack();
            if (loop == null) Fx.Circle(at, r, i == Hits - 1 ? Fx.FireBright : Fx.Fire, 0.25f);   // 그림이 없을 때만 예전 원
            SkillHits.Circle(at, r, per, i == Hits - 1 ? 6f : 1f);
            if (i == Hits - 1) CameraFollow.Shake(0.15f, 0.15f);
            yield return new WaitForSeconds(Duration / Hits);
        }
        if (loop != null) loop.Stop();
    }
}

/// <summary>
/// D01 파이어 스트림: 빠른 돌진 · 이동 경로 피해 · 무적 · 초기 피해량 3 · 스택 3 · 스택 쿨타임 8초 (노션 2026-11-20)
/// </summary>
public class FireStream : Skill
{
    const int MaxStacks = 3;            // 기획서 확정
    const float DashTime = 0.12f;       // [임시] 이동 시간 (속도 '매우 빠름')
    // [임시 해석] 스택은 한 번에 하나씩 순서대로 회복

    int stacks = MaxStacks;
    float recharge;
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    const float BaseDamage = 3f, BaseCooldown = 8f;    // 스택 쿨타임 8초 (노션)

    // 불꽃 줄기 (ErnStray_FireStream_StraightDash_v4 원본): 4열 × 6행 · 칸 768 × 512 · 24프레임 (마지막 = 투명) · 1회
    // 오른쪽으로 곧게 자라는 나선 불꽃 (월드에 고정, 캐릭터를 따라가지 않음) · 뒤 레이어 + 앞 레이어
    const string StreamRearSheet = "ErnStray_FireStreamRear", StreamFrontSheet = "ErnStray_FireStreamFront";
    const int StreamRows = 6, StreamCols = 4;
    const float StreamFps = 25f;            // 초안 프레임 40ms (1초)
    const float StreamCellPx = 768f;
    const float StreamStartPx = 118f;       // 출발점 = 1번 프레임 점화 자리 가운데 (알파 범위 86~151px)
    const float StreamEndPx = 733f;         // 도착점 = 다 자랐을 때 오른쪽 끝 (알파 범위 최대 733px)
    const float StreamGroundBelowCenterPx = 55f;   // 지면선 (기준점 y 311) = 칸 가운데(256)에서 아래로 55px
    const int StreamRearOrder = 9, StreamFrontOrder = 11;   // 플레이어 = 10

    /// <summary>
    /// 돌진 경로에 불꽃 줄기: 출발점 → 도착점 길이 = 돌진 거리, 돌진 방향으로 회전 (왼쪽이면 세로 반전 [해석]).
    /// 월드에 고정되어 1초 동안 자라고 사그라듦. 그림이 없으면 false
    /// </summary>
    bool SpawnStream(Vector2 from, Vector2 dir, float distance)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float pxWorld = distance / (StreamEndPx - StreamStartPx);
        var rear = SpriteEffect.LoadFrames(StreamRearSheet, StreamRows, StreamCols, StreamCellPx * pxWorld);
        var front = SpriteEffect.LoadFrames(StreamFrontSheet, StreamRows, StreamCols, StreamCellPx * pxWorld);
        if (rear == null || front == null) return false;

        var root = new GameObject("FX_FireStream").transform;
        root.position = from;
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        if (dir.x < -0.01f) root.localScale = new Vector3(1f, -1f, 1f);
        Vector2 local = new Vector2((StreamCellPx * 0.5f - StreamStartPx) * pxWorld, StreamGroundBelowCenterPx * pxWorld);
        SpriteEffect.Spawn(rear, StreamFps, root, local, 0f, false, StreamRearOrder);
        SpriteEffect.Spawn(front, StreamFps, root, local, 0f, false, StreamFrontOrder);
        Destroy(root.gameObject, StreamRows * StreamCols / StreamFps + 0.2f);   // 두 장이 끝난 뒤 빈 루트 정리
        return true;
    }
    float StackCooldown => CooldownOf(BaseCooldown);
    float Distance => Reach(Tier.Short);

    public override string Badge => stacks.ToString();
    public override float CooldownFraction =>
        stacks >= MaxStacks ? 0f : stacks > 0 ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));
    public override float CooldownRemaining => stacks > 0 ? 0f : Mathf.Max(0f, recharge);
    /// <summary>다음 스택 회복 진행도 (HUD)</summary>
    public float RechargeFraction => stacks >= MaxStacks ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));

    /// <summary>남은 쿨타임 감소 (불길의 순환): 지금 충전 중인 스택 기준</summary>
    public override void ReduceRemainingCooldown(float ratio)
    {
        if (stacks < MaxStacks && recharge > 0f) recharge *= Mathf.Clamp01(1f - ratio);
    }

    public override void Tick(float dt)
    {
        if (stacks >= MaxStacks) { recharge = 0f; return; }
        if (recharge <= 0f) recharge = StackCooldown;
        recharge -= dt;
        if (recharge <= 0f) { stacks++; recharge = stacks < MaxStacks ? StackCooldown : 0f; }
    }

    public override void DebugResetForCast() { if (stacks <= 0) stacks = 1; }   // 디버그 맵 버튼: 스택이 없어도 사용

    public override bool TryActivate()
    {
        if (CheatDisabled) return false;   // 치트: 스킬별 사용 금지
        if (stacks <= 0 || P.IsBusy || P.IsStunned) return false;
        if (stacks == MaxStacks) recharge = StackCooldown;
        stacks--;
        BeginCast();

        hit.Clear();
        DamageInfo dmg = MakeDamage(BaseDamage);
        float r = 0.4f;                 // [임시] 이동 경로 판정 반경 (카메라 시야 7 복귀에 맞춰 0.6 × 2/3)
        bool drawn = SpawnStream(P.transform.position, P.Facing, Distance);
        if (!drawn) Fx.Circle(P.transform.position, r, Fx.FireBright, 0.2f);   // 그림이 없을 때만
        P.Dash(P.Facing, Distance, DashTime, true, onStep: () =>
        {
            Vector2 at = P.transform.position;
            if (!drawn) Fx.Disc(at, r * 0.6f, Fx.Fire, 0.18f, 0f);
            foreach (var e in SkillHits.InCircle(at, r, found))
                if (hit.Add(e)) e.Hit(dmg, (Vector2)e.transform.position - at, 1f);
        });
        return true;
    }
}

/// <summary>
/// U01 블레이징 스톰: 궁극기 · 카메라 전체 · 피해 매우 높음 · 사용 종료까지 무적 · 피해 후 모든 적 기절
/// 게이지(적 처치 수 기반)가 가득 찼을 때 수동 발동
/// </summary>
public class BlazingStorm : Skill
{
    const float Duration = 1.5f;        // [임시] 연출 시간 (기획 미정)
    const int Hits = 1;                 // 초기 피해량 500 을 한 번에
    const float BaseDamage = 500f;
    const float StunSeconds = 2f;       // [임시] 기절 시간 (기획 미정, 보스 규칙 미정 → 동일 적용)

    readonly List<Enemy> targets = new List<Enemy>();
    bool running;

    public override float CooldownFraction => running ? 1f : 1f - GameManager.I.UltimateGaugeFraction;
    public override string Badge => running ? null : $"{Mathf.FloorToInt(GameManager.I.UltimateGaugeFraction * 100)}%";

    public override bool TryActivate()
    {
        if (CheatDisabled) return false;   // 치트: 스킬별 사용 금지
        if (running || P.IsBusy || P.IsStunned || !GameManager.I.UltimateReady) return false;
        GameManager.I.ConsumeUltimateGauge();
        BeginCast();
        StartCoroutine(Run());
        return true;
    }

    IEnumerator Run()
    {
        running = true;
        P.SetInvulnerable(Duration + 0.1f);
        DamageInfo per = MakeDamage(BaseDamage);
        Fx.ScreenFlash(new Color(1f, 0.45f, 0.1f, 0.45f), Duration);
        for (int i = 0; i < Hits; i++)
        {
            CameraFollow.Shake(0.2f, 0.25f);
            foreach (var e in SkillHits.OnScreen(targets))
            {
                Fx.Disc(e.transform.position, 0.4f, Fx.Fire, 0.25f);   // 카메라 시야 7 복귀에 맞춰 0.6 × 2/3
                e.Hit(per, (Vector2)(e.transform.position - P.transform.position), 0.5f);
            }
            // 마지막 피해 직후 모든 적 기절 (연출 시간이 끝날 때까지 기다리지 않음)
            if (i == Hits - 1) foreach (var e in SkillHits.OnScreen(targets)) e.Stun(StunSeconds);
            yield return new WaitForSeconds(Duration / Hits);
        }
        running = false;
    }
}
