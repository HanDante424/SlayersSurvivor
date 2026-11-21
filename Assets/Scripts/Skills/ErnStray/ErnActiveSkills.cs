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
    // 연출 길이 (기획 미정, 모두 [임시]). 기 모으기 → ① 토네이도 생성 → 밑단 확장 → 낙하 → ② 지면 폭발 (피해 · 기절은 폭발 순간)
    // ① 토네이도 시간 = Tornado1Life, ② 지면 폭발 토네이도 시간 = BigLife. Duration = 기 모으기 뒤 전체 (무적 길이)
    const float ConnectTail = 0.35f;            // [임시] 지면에 닿은 뒤 첫 토네이도가 남아 서서히 사라지는 시간 (후속 토네이도가 자라는 동안 겹침 → 자연스러운 연결). 0.8 로 늘렸다가 사용자 요청으로 0.35 로 되돌림
    const float Tornado1Life = FallStart + FallTime + ConnectTail;   // 낙하가 끝난 뒤 ConnectTail 동안 겹치고 사라짐
    const float BigLife = 1.6f;                 // 3.2 → 1.6 (사용자: 후속 토네이도 지속시간 절반). 생성 · 떠오름 · 사라짐도 같은 비율로 줄임
    // 연출 포즈 (2026-11-21 사용자): 준비 동작 (포즈당 0.25초) → 기 모으기 (UltCharge) → 토네이도 생성 (OverheadRaise) → 낙하 (VerticalSlash) → 지면에 닿은 뒤 마무리 동작 (포즈당 0.25초) → 궁극기 종료
    const float PoseStep = 0.15f;               // 포즈 하나가 보이는 시간 (사용자 2026-11-21: 각 스프라이트 0.25 → 0.15초)
    const float OutroStep = 0.1f;               // 마무리 동작 앞 3장의 포즈당 시간 (사용자 2026-11-21: 0.15 → 0.1초). 준비 동작은 PoseStep 그대로
    const float RearReachHold = 1.0f;           // 마무리 마지막 포즈 (RearReach) 만 1초 (사용자 2026-11-21)
    const float PoseIntro = PoseStep * 3f;      // 준비 동작: Atk_SlashReady → Atk_HorizontalSlashReady → UltCharge (3장 = 0.45초)
    const float PoseOutro = OutroStep * 3f + RearReachHold;   // 마무리 동작: Atk_OneHandSlash → Atk_HorizontalSlashReady → Atk_HorizontalSlash (각 0.1초) → RearReach (1초)
    // 궁극기 (플레이어 멈춤 · 무적 · 슬로우) 가 이어지는 시간: 준비 + 기 모으기 + 첫 토네이도가 닿을 때까지 + 마무리. 후속 토네이도 연출은 그 뒤로 계속됨
    // + 후속 토네이도가 끝나면 바로 마무리 동작 PoseOutro (Atk_VerticalSlash 는 토네이도가 끝나는 순간까지만)
    const float FocusZoom = 0.6f;               // [임시] 플레이어 확대 시야 배율 (1 = 기본, 작을수록 확대). 준비 동작 ~ 기 모으기, 토네이도가 끝난 뒤 마무리에서 사용
    const float FinishZoomLead = 1.0f;          // 후속 토네이도가 끝나기 이만큼 전부터 플레이어 확대 (사용자 2026-11-21: 끝난 뒤 0.5초 → 끝나기 1초 전)
    const float CastTotal = PoseIntro + ChargeTime + FallStart + FallTime + BigLife + PoseOutro;
    const int Hits = 1;                 // 초기 피해량 500 을 한 번에
    const float BaseDamage = 500f;
    const float StunSeconds = 2f;       // [임시] 기절 시간 (기획 미정, 보스 규칙 미정 → 동일 적용)

    readonly List<Enemy> targets = new List<Enemy>();
    bool running;

    // ---------- 불꽃 토네이도 (RibbonTornadoFx) ----------
    // 곧은 불꽃 줄기 그림 여러 종류를 섞어 원뿔 나선으로 휘어 돌림 (2026-11-21 재구성, 사용자가 모은 재료 4묶음 사용).
    // 재료 원본 = Art/Incoming/ErnStray/BlazingStorm/ (01 조각 · 02 굵기 팩 · 03 · 04 낱장), 게임용 사본 = Resources/Sprites/ErnStray_Storm*.png (원본 그대로 복사).
    // 크기 · 굵기 · 가중치 값은 모두 [임시] (카메라 세로 반 = 7). 재료가 하나도 없으면 예전 시험판 (화염검 시트) 으로 대체.
    // 2026-11-21 사용자: "매우 빽빽해서 정말 거대한 화염 폭풍" → 리본 수 · 두께 · 크기 · 겹 수를 크게 늘림 (모두 [임시], 한 줄씩 조정)
    // 후속 (지면 폭발) 거대 토네이도 생성 시간 (2026-11-21 사용자: 늘려라). 가운데에서 바깥으로 퍼지는 시간 + 밑단부터 차오르는 시간
    const float BigFromRadius = 0.3f;           // [임시] 후속 토네이도가 자라기 시작할 때 반지름 비율 (0.04 → 0.3: 막 닿은 첫 토네이도 밑단 (반지름 9) 과 비슷한 크기에서 시작해 바깥으로 퍼지며 이어짐)
    const float BigGrowTime = 0.65f;            // [임시] 반지름이 작게 → 원래 크기로 퍼지는 시간 (0.45 → 1.3 → 0.65, 지속시간 절반에 맞춤)
    const float BigBuildUpTime = 0.35f;         // [임시] 맨 아래 리본 시작 → 맨 위 리본 시작
    const float BigBuildUpGrow = 0.25f;         // [임시] 리본 하나가 나타나는 시간
    const float BigRiseDistance = 36f;          // [임시] 떠오르는 거리 (월드). 16 → 36: 더 빠르게 상승 (사용자)
    const float BigRiseEase = 1.15f;            // [임시] 떠오르는 가속 지수 (1.6 → 1.15: 처음부터 빠르게, 1 = 일정 속도)
    // 후속 토네이도를 화면 쪽으로 눕힘 (2026-11-21 사용자: 화면 평면에서 옆으로 기울이는 게 아니라 화면 쪽으로 각도를 돌림). 축이 보는 사람 쪽으로 기울면
    // ① 보는 각도가 올라가 고리가 더 둥글게 열려 보이고 (squash = sin 각도) ② 세로 길이가 짧아 보임 (cos 비율). 첫 토네이도는 10도 그대로
    const float BigViewAngle = 35f;             // [임시] 후속 토네이도를 보는 각도 (도). 10 → 35. 90 에 가까울수록 위에서 내려다본 원
    static readonly float BigVerticalScale = Mathf.Cos(35f * Mathf.Deg2Rad) / Mathf.Cos(10f * Mathf.Deg2Rad);   // 세로 단축 (BigViewAngle 와 같은 각도로)
    const float BigSpreadScale = 1.9f;          // [임시] 떠오르는 동안 반지름이 이 배율까지 더 퍼짐
    const float BigFadeTime = 0.55f;            // [임시] 끝에서 이 시간 동안 서서히 사라짐 (오르는 동안 같이)
    const int BigRibbons = 280;                 // [임시] 지면 폭발 토네이도 리본 수 (더 크고 빽빽하게)
    const int TornadoRibbons = 180;             // [임시] 리본 수 (처음 28 → 34 → 150 → 180, 토네이도가 커져서 밀도 유지)
    const float LoopFps = 16f;                  // [임시] 반복 시트 재생 속도 (제작 초안 70ms ≈ 14fps 보다 약간 빠르게)
    // 보는 각도 (2026-11-21 사용자: 더 2D 느낌으로, 완전 평면은 안 되고 10도 가량 남김). 위에서 본 원을 옆에서 볼 때 세로로 납작해지는 비 = sin(각도).
    // 0도 = 완전 평면 (정면에서 본 선), 90도 = 위에서 본 원. 처음 값은 약 17.5도 (squash 0.3)
    const float TornadoViewAngle = 10f;
    // 시전 연출 (2026-11-21 사용자): 카메라를 축소하고 토네이도를 플레이어 위로 5 올림
    // 연출 순서 (2026-11-21 사용자): ① 기 모으기 (화염 소용돌이가 플레이어를 타고 올라가며 사라짐) → ② 급격하게 본 토네이도 생성 + 피해
    const float ChargeTime = 1.5f;              // 기 모으기 길이 (사용자 지정 1.5초, 천천히). 이 동안 무적, 피해는 ② 시작 때
    const float ChargeRise = 1.5f;              // [임시] 소용돌이 줄기 하나가 발밑에서 머리 위까지 오르는 시간 (0.55 → 1.0 → 1.5: 상승 속도 3.8/1.5 ≈ 2.5 월드/초). 줄기가 끊기지 않고 계속 피어오르도록 시작 시각을 전체에 고르게 나눔 (끝 무렵 시작한 줄기는 오르던 중에 토네이도로 넘어감)
    const int ChargeRibbons = 44;               // [임시] 느려진 만큼 줄여 한꺼번에 많이 보이지 않게
    // 기 모으는 동안 조금씩 피어오르는 불꽃 파티클 (FireParticlePop 재사용, 위로 천천히 떠오르며 사라짐). 뒤로 갈수록 많이 나옴
    const int EmberCount = 40;                  // [임시]
    const float EmberFps = 14f;                 // [임시] 12프레임 → 약 0.86초 (원래 33fps 0.36초를 늘림)
    const float EmberRiseSpeed = 1.3f;          // [임시] 떠오르는 속도 (월드/초)
    const float EmberRadius = 1.5f;             // [임시] 플레이어 둘레 퍼짐
    const float EmberSizeMin = 0.45f, EmberSizeMax = 0.95f;   // [임시] 크기 배율 (1 = 폭발 파티클 기본 0.6 월드)
    const float ChargeHeight = 3.8f;            // [임시] 오르는 높이 (발밑 -0.9 ~ 머리 위 약 2.9)
    // 기 모으기 → 토네이도 자연스러운 연결 (2026-11-21 사용자 요청)
    // 밑단부터 자연스럽게 생성 (2026-11-21 사용자: 시간은 상관없음): 아래 리본이 먼저, 위로 갈수록 늦게 시작해 각자 작게 → 크게 나타남 → 아래에서 위로 차오르는 모양
    const float BuildUpTime = 0.5f;             // [임시] 맨 아래 리본이 시작한 뒤 맨 위 리본이 시작하기까지 (토네이도가 다 만들어지는 데 걸리는 시간). 1.0 → 0.5 (사용자: 생성 시간 절반)
    const float BuildUpGrow = 0.175f;           // [임시] 리본 하나가 작게 → 원래 크기로 커지는 시간 (0.35 → 0.175, 절반)
    const float BurstFromRadius = 0.2f;         // [임시] 리본이 나타나기 시작할 때 반지름 비율 (0.08 → 0.2)
    // 밑단 확장 → 낙하 → 지면 폭발 (2026-11-21 사용자): 토네이도 밑단이 넓어지고, 지면으로 떨어지고, 닿으면 더 큰 토네이도가 지면에서 바깥으로 퍼지며 터지듯 생성
    const float ExpandStart = 0.65f;            // [임시] ① 시작 뒤 밑단이 넓어지기 시작하는 시각 (생성이 거의 끝난 뒤)
    const float ExpandTime = 0.5f;              // [임시] 밑단이 넓어지는 시간
    const float ExpandedBottomRadius = 9f;      // [임시] 넓어진 밑단 반지름 (처음 2.2)
    const float FallGap = 1.0f;                 // 토네이도가 다 만들어진 뒤 (BuildUpTime) 낙하가 시작하기까지의 간격 (사용자 2026-11-21: 0.5 → 0.75 → 1.0초)
    const float FallStart = BuildUpTime + FallGap;   // ① 시작 뒤 낙하가 시작하는 시각 = 1.5초 (밑단 확장이 끝나기 전에 겹쳐 시작)
    const float FallTime = 0.18f;               // [임시] 낙하 시간 (점점 빨라짐). 0.35 → 0.18: 더 빠르게 내려옴 (사용자)
    const float GroundAnchor = 0.7f;            // [해석] 낙하가 끝났을 때 토네이도 기준점 높이 (플레이어 기준). 밑단 -1.6 + 0.7 = -0.9 = 발밑 근처 지면
    const float LiftTime = 0.35f;               // [임시] 토네이도가 플레이어 몸에서 TornadoLift 위치로 솟아오르는 시간 (0.35 → 0.7 → 0.35: 생성 시간과 같이 절반)
    const float ChargeTail = 0.35f;             // [임시] 기 모으던 줄기가 토네이도가 터진 뒤에도 이만큼 더 올라가다 사라짐 (끊기지 않게 토네이도 속으로 겹침)
    const float ZoomLead = 0.35f;               // [임시] 토네이도가 터지기 이만큼 전에 카메라 축소를 시작 (축소가 미리 진행되어 터질 때 시야가 갑자기 바뀌지 않게)
    const float CastZoomOut = 2f;               // [임시] 시전 중 시야 배율 (세로 반 7 → 14). 끝나면 원래대로
    const float TornadoLift = 3f;               // 토네이도 기준점을 플레이어 위로 올린 거리 (월드). 5 → 2.5 → 3 (사용자). 위쪽 끝은 플레이어 위 약 21.4 = 시전 시야 (위쪽 14) 를 크게 넘어감
    const float StripThick = 3.2f;              // [임시] 불꽃이 칸 두께의 24~41% 만 차지해서 리본 두께를 키움 (2.2 → 3.2)

    // 반복 시트 (01 · 02): 4열 x 3행 12프레임, 칸 384 x 288, 오른쪽이 머리 (머리 끝 x 336~340), 꼬리 x 19~24 부터
    const float LoopCellW = 384f, LoopCellH = 288f;
    const float LoopAlongX0 = 18f, LoopAlongX1 = 342f;
    // 낱장 (03 · 04): 960 x 192 한 장, 오른쪽이 머리
    const float StillH = 192f;

    // [임시] 예전 시험판 재료 = 오버 드라이브 화염검 시트 (칼끝 = 머리). 새 재료가 없을 때만
    const string TornadoRibbonSheet = "ErnStray_OverFlameBlade";
    const int TornadoRibbonRows = 4, TornadoRibbonCols = 4;
    const float TornadoCellW = 256f, TornadoCellH = 768f;
    const float TornadoRibbonX = 88f, TornadoRibbonW = 74f;    // 칼 알파 범위 x 88~162
    const float TornadoRibbonTop = 54f, TornadoRibbonH = 674f; // 칼 알파 범위 y 54~728 (위 기준)

    static RibbonTornadoFx.Source LoopSource(string sheet, float weight)
    {
        var tex = Resources.Load<Texture2D>("Sprites/" + sheet);
        if (tex == null) return null;
        const int cols = 4, rows = 3;
        var frames = new Rect[cols * rows];
        for (int k = 0; k < frames.Length; k++)
        {
            int col = k % cols, row = k / cols;
            float yBottom = tex.height - (row + 1) * LoopCellH;   // 텍스처 좌표는 아래 기준
            frames[k] = new Rect((col * LoopCellW + LoopAlongX0) / tex.width, yBottom / tex.height,
                                 (LoopAlongX1 - LoopAlongX0) / tex.width, LoopCellH / tex.height);
        }
        return new RibbonTornadoFx.Source { texture = tex, frames = frames, lengthAlongY = false, fps = LoopFps, weight = weight, thickMul = StripThick };
    }

    /// <summary>낱장 줄기: 불꽃이 있는 가로 구간 x0~x1 만 잘라 꼬리 → 머리로 씀</summary>
    static RibbonTornadoFx.Source StillSource(string sheet, float x0, float x1, float weight)
    {
        var tex = Resources.Load<Texture2D>("Sprites/" + sheet);
        if (tex == null) return null;
        var frames = new[] { new Rect(x0 / tex.width, 0f, (x1 - x0) / tex.width, 1f) };
        // 칸 세로가 288 이 아니라 192 라서 같은 굵기로 보이게 두께를 맞춤
        return new RibbonTornadoFx.Source { texture = tex, frames = frames, lengthAlongY = false, weight = weight, thickMul = StripThick * StillH / LoopCellH };
    }

    static RibbonTornadoFx.Source SwordSource()
    {
        var tex = Resources.Load<Texture2D>("Sprites/" + TornadoRibbonSheet);
        if (tex == null) return null;
        int n = TornadoRibbonRows * TornadoRibbonCols;
        var frames = new Rect[n];
        for (int k = 0; k < n; k++)
        {
            int col = k % TornadoRibbonCols, row = k / TornadoRibbonCols;
            float x = col * TornadoCellW + TornadoRibbonX;
            float yBottom = tex.height - (row * TornadoCellH + TornadoRibbonTop + TornadoRibbonH);   // 텍스처 좌표는 아래 기준
            frames[k] = new Rect(x / tex.width, yBottom / tex.height, TornadoRibbonW / tex.width, TornadoRibbonH / tex.height);
        }
        return new RibbonTornadoFx.Source { texture = tex, frames = frames, lengthAlongY = true, fps = 20f };
    }

    /// <summary>토네이도와 기 모으기가 함께 쓰는 줄기 재료 목록 (없으면 null)</summary>
    static List<RibbonTornadoFx.Source> StormSources()
    {
        var list = new List<RibbonTornadoFx.Source>();
        void Add(RibbonTornadoFx.Source x) { if (x != null) list.Add(x); }
        // 움직이는 줄기 (일렁임): 굵기 3단계가 주력, 갈고리 · 불씨는 포인트
        Add(LoopSource("ErnStray_StormStreamNarrow", 3f));
        Add(LoopSource("ErnStray_StormStreamMedium", 3f));
        Add(LoopSource("ErnStray_StormStreamWide", 2f));
        Add(LoopSource("ErnStray_StormHook", 1f));
        Add(LoopSource("ErnStray_StormEmbers", 1f));
        // 낱장 줄기: 불꽃이 있는 가로 구간 (원본 알파 범위 ±2px)
        Add(StillSource("ErnStray_StormStillThin", 39f, 926f, 1f));
        Add(StillSource("ErnStray_StormStillMedium", 40f, 928f, 1f));
        Add(StillSource("ErnStray_StormStillWide", 35f, 927f, 1f));
        Add(StillSource("ErnStray_StormStillCrest", 31f, 929f, 2f));
        Add(StillSource("ErnStray_StormStillRising", 32f, 698f, 1f));
        Add(StillSource("ErnStray_StormStillCurl", 32f, 304f, 1f));
        Add(StillSource("ErnStray_StormStillDip", 31f, 499f, 1f));
        if (list.Count == 0) Add(SwordSource());   // 새 재료가 하나도 없으면 예전 시험판
        return list.Count == 0 ? null : list;
    }

    /// <summary>② 본 토네이도: 밑단부터 차례로 생성 (BuildUpTime · BuildUpGrow · BurstFromRadius)</summary>
    static RibbonTornadoFx.Settings TornadoSettings(float life)
    {
        var list = StormSources();
        if (list == null) return null;
        return new RibbonTornadoFx.Settings
        {
            sources = list,
            ribbons = TornadoRibbons, baseY = -1.6f, height = 20f, bottomRadius = 2.2f, topRadius = 12f, squash = Mathf.Sin(TornadoViewAngle * Mathf.Deg2Rad),
            radiusJitter = 0.22f,   // 같은 높이에서도 안팎으로 겹겹이
            arcMin = 90f, arcMax = 210f, thickBottom = 0.45f, thickTop = 1.6f, endThickness = 0.4f, riseJitter = 0.8f,
            spin = 440f, spinJitter = 0.45f, segments = 16,
            growTime = 0f, buildUpTime = BuildUpTime, buildUpGrow = BuildUpGrow, growFromRadius = BurstFromRadius, life = life,
            fadeFromTop = true, fadeTime = FallTime + ConnectTail,   // 내려오기 시작할 때부터 위쪽 리본부터 차례로 사라져 아래가 마지막에 사라짐 (사용자 2026-11-21)
            bottomRadiusEnd = ExpandedBottomRadius, expandStart = ExpandStart, expandTime = ExpandTime,
            spawnOffset = new Vector2(0f, -TornadoLift), offsetTime = LiftTime,   // 플레이어 몸에서 솟아오름
        };
    }

    /// <summary>
    /// ③ 지면 폭발 토네이도: 지면에 닿은 자리에서 첫 토네이도보다 훨씬 크게, 가운데에서 바깥으로 퍼지며 터지듯 생성 (전체가 작은 반지름 → 원래 반지름으로 빠르게 커짐).
    /// 밑단이 지면에 붙어 있고 위쪽 반지름이 더 큼. 크기 · 두께 · 높이 모두 [임시]
    /// </summary>
    static RibbonTornadoFx.Settings BigTornadoSettings(float life)
    {
        var list = StormSources();
        if (list == null) return null;
        return new RibbonTornadoFx.Settings
        {
            sources = list,
            // 하나의 거대한 토네이도: 밑단 반지름 14 (지면을 따라 24 까지 더 번짐) → 위쪽 반지름 110, 높이 32 (보는 각도 35도로 세로 약 25 로 보임) 로 극단적으로 넓게 퍼지는 깔때기 (벌어진 각도 약 75°, 72 일 때 약 66°, 처음 토네이도는 약 21°)
            ribbons = BigRibbons, baseY = -0.9f, height = 32f, bottomRadius = 14f, topRadius = 110f, squash = Mathf.Sin(BigViewAngle * Mathf.Deg2Rad),
            radiusJitter = 0.25f,
            arcMin = 90f, arcMax = 210f, thickBottom = 1.1f, thickTop = 6.5f, endThickness = 0.4f, riseJitter = 1.0f,
            spin = 380f, spinJitter = 0.45f, segments = 16,
            // 생성 시간을 길게 (사용자 2026-11-21): 가운데에서 바깥으로 퍼지는 것 (growTime) 과 밑단부터 차오르는 것 (buildUp) 을 함께 천천히
            growTime = BigGrowTime, growFromRadius = BigFromRadius, buildUpTime = BigBuildUpTime, buildUpGrow = BigBuildUpGrow, life = life,
            // 생성이 끝난 뒤 (BigGrowTime) 위로 떠오르며 더 퍼지고 서서히 사라짐 (사용자 2026-11-21)
            driftStart = BigGrowTime, driftDistance = BigRiseDistance, driftEase = BigRiseEase, spreadEndScale = BigSpreadScale, fadeTime = BigFadeTime,
            verticalScale = BigVerticalScale,   // 화면 쪽으로 눕혀 세로가 짧아 보임
            bottomRadiusEnd = 24f, expandStart = 0.15f, expandTime = 0.5f,   // 밑단이 지면을 따라 더 넓게 번짐 (12 → 18)
        };
    }

    /// <summary>
    /// ① 기 모으기: 플레이어 발밑에서 작은 화염 소용돌이 줄기들이 시간차로 하나씩 플레이어를 감싸고 머리 위까지 타고 올라가며 사라짐.
    /// 위로 갈수록 반지름이 줄어 (기를 모으듯 몸으로 빨려 드는 느낌). 보는 각도는 본 토네이도와 같음.
    /// </summary>
    static RibbonTornadoFx.Settings ChargeSettings(float life)
    {
        var list = StormSources();
        if (list == null) return null;
        return new RibbonTornadoFx.Settings
        {
            sources = list, climb = true, riseTime = ChargeRise,
            climbSpread = 0.8f * ChargeTime / life,   // 줄기 시작 시각 분포는 기 모으는 시간 (ChargeTime) 안에서 그대로. life 는 ChargeTail 만큼 더 길다
            ribbons = ChargeRibbons, baseY = -0.9f, height = ChargeHeight, bottomRadius = 1.7f, topRadius = 0.7f, squash = Mathf.Sin(TornadoViewAngle * Mathf.Deg2Rad),
            radiusJitter = 0.25f,
            arcMin = 140f, arcMax = 260f, thickBottom = 0.1f, thickTop = 0.06f, endThickness = 0.4f, riseJitter = 0.2f,
            spin = 520f, spinJitter = 0.3f, segments = 14,
            growTime = 0f, fadeTime = 0f, life = life,
        };
    }

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

    // 마무리 동작의 반시계 불꽃 줄기 (OrbitFlameFx). 그림 = ErnStray_StormStillCurl (03/04 묶음 낱장 줄기, 머리 = 오른쪽, 불꽃 구간 x 32~304). 모두 [임시]
    const float OrbitLength = 2.2f;             // 줄기 하나의 길이 (월드)
    const float OrbitEmitTime = 0.15f;           // 파티클 생성 총 시간 (사용자 2026-11-21: 0.3 → 0.21 → 0.15초). 마무리 시작부터 Atk_HorizontalSlash 끝 (0.3초) 안에 모두 생성
    const float OrbitSparkStart = 0.10f;        // 불티가 시작되는 시점 = 생성 총 시간 (0.15초) 의 마지막 0.05초. 줄기 파티클은 그대로 0.15초 내내 나오고 불티를 추가로 흩뿌림 (사용자 2026-11-21)
    const string OrbitSlashSheet = "ErnStray_SkillTsubameGaeshi";   // 휘두를 때 이펙트 (기존 줄기 대신, 5프레임 1x5)
    const float OrbitSlashFps = 20f / 0.7f;      // 5프레임 = 0.175초 (사용자 2026-11-21: 지속시간 30% 단축, 원래 20fps = 0.25초)
    static readonly Vector2 OrbitSlashOffset = new Vector2(-0.75f, 0.6f);   // 플레이어 기준 위치 (사용자 2026-11-21: x -0.2 → -0.5 → -1 → -0.75, y 0.6 은 이전 계산값 유지)
    const float OrbitSlashRotation = -170f;     // 회전 z (도, 사용자 2026-11-21 확정)
    const float OrbitSlashWidth = 2.6f;         // [임시] 참격 한 칸 가로 (월드)
    const int OrbitSparkCount = 48;             // [임시] 마지막 0.05초에 흩뿌리는 불티 수 (16 → 3배 = 48)
    const float OrbitSparkScaleMin = 0.08f, OrbitSparkScaleMax = 0.18f;   // [임시] 불티 크기 배율 (아주 작게)
    const float OrbitSparkSpeed = 3.5f;         // [임시] 불티가 바깥으로 튀는 속도 (월드/초)
    const float OrbitSparkFps = 40f;            // [임시] 불티 재생 속도 (12프레임 = 0.3초)
    const float OrbitDegPerParticle = 6f;       // 각도 몇 도당 파티클 1개 (사용자 2026-11-21: 4 → 6도당 1개 = 55개)
    const float OrbitRate = OrbitSweepDegrees / OrbitDegPerParticle / OrbitEmitTime;   // 초당 발사 수: 발사 시간 (0.15초) 안에 55개 = 약 367개/초 
    const float OrbitLife = 0.05f;              // 초반 줄기 수명 (사용자 2026-11-21: 초반 0.05 · 중반 0.1 · 후반 0.15초)
    const float OrbitLifeMid = 0.1f, OrbitLifeLate = 0.15f;   // 중반 (발사 시간의 1/3~2/3) · 후반 (2/3~)
    const float OrbitScaleMin = 0.35f, OrbitScaleMax = 0.65f;   // 작은 파티클 크기 배율 (줄기 길이 2.2 x 이 값)
    const float OrbitOmegaMin = 0f, OrbitOmegaMax = 0f;     // 파티클 하나가 제자리에서 도는 속도 (도/초, 반시계). 작게 해서 금방 사라지기 전에 조금만 번짐
    const float OrbitJitter = 10f;              // 발사 각도 흔들림 (±도)
    const float OrbitSweepStart = 240f;         // 시작 = 7시 (3시 = 0도, 12시 = 90도, 반시계가 +)
    const float OrbitSweepDegrees = 330f;       // 반시계로 330도 훑어 끝 = 570 = 210도 = 8시
    const float OrbitRadiusMin = 1.6f, OrbitRadiusMax = 3.4f;   // 플레이어 중심에서의 시작 거리 (사용자 2026-11-21: 폭 1.4 → 0.7, 외곽 2.6 → 1.9 부터 절반)
    const float OrbitSpreadRandom = 0.6f;      // 퍼지는 거리 무작위 폭 (0.4~1.6 배)
    const float OrbitSpread = 2.0f;             // [임시] 수명 동안 바깥쪽으로 퍼지는 거리 (월드). 줄기가 바깥 방향을 향하며 퍼져 나감
    const float OrbitSquash = 0.5f;             // 타원 세로 / 가로
    static Sprite orbitSprite;

    static Sprite OrbitSprite()
    {
        if (orbitSprite != null) return orbitSprite;
        var tex = Resources.Load<Texture2D>("Sprites/ErnStray_StormStillCurl");
        if (tex == null) return null;
        const float x0 = 32f, x1 = 304f;
        orbitSprite = Sprite.Create(tex, new Rect(x0, 0f, x1 - x0, tex.height), new Vector2(0.5f, 0.5f), (x1 - x0) / OrbitLength, 0, SpriteMeshType.FullRect);
        return orbitSprite;
    }

    /// <summary>포즈를 이름 순서대로 각자의 시간 (실제 시간, 슬로우와 무관) 동안 보임</summary>
    IEnumerator PoseSequence(string[] names, float[] seconds)
    {
        for (int i = 0; i < names.Length; i++)
        {
            P.ShowPose(names[i]);
            yield return new WaitForSecondsRealtime(seconds[i]);
        }
    }

    IEnumerator Run()
    {
        running = true;
        WorldSlow.Begin();   // 궁극기 중 플레이어 · 연출만 정상 속도, 나머지 전부 96% 감소 (집중용)
        P.Root(CastTotal + 0.2f);   // 궁극기 사용 중 플레이어 멈춤 (이동 불가, 사용 중 다른 스킬도 불가 — 사용자 2026-11-21). 끝나면 EndRoot 로 바로 풀음
        P.SetInvulnerable(CastTotal + 0.1f);
        DamageInfo per = MakeDamage(BaseDamage);

        CameraFollow.SetZoomOut(FocusZoom);   // 준비 동작 ~ 기 모으기: 플레이어 확대 (토네이도가 생기기 직전에 서서히 축소로 전환)

        // ① 준비 동작 (포즈당 0.25초): Atk_SlashReady → Atk_HorizontalSlashReady → UltCharge
        yield return PoseSequence(new[] { "Atk_SlashReady", "Atk_HorizontalSlashReady", "UltCharge" }, new[] { PoseStep, PoseStep, PoseStep });

        // ② 기 모으기 (UltCharge 유지): 화염 소용돌이가 플레이어를 타고 올라가며 사라짐 (재료가 없으면 생략)
        RibbonTornadoFx.Spawn(ChargeSettings(ChargeTime + ChargeTail), P.transform, Vector2.zero);   // 줄기가 토네이도 속으로 이어지게 ChargeTail 더 길게
        CameraFollow.Shake(ChargeTime, 0.04f);   // [임시] 아주 약한 떨림
        StartCoroutine(RisingEmbers());
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, ChargeTime - ZoomLead));
        CameraFollow.SetZoomOut(CastZoomOut);    // 터지기 직전부터 카메라가 서서히 물러남
        yield return new WaitForSecondsRealtime(Mathf.Min(ZoomLead, ChargeTime));

        // ③ 토네이도 생성 (밑단부터, OverheadRaise) → 밑단 확장 (Settings 의 expand*)
        P.ShowPose("OverheadRaise");
        Fx.ScreenFlash(new Color(1f, 0.45f, 0.1f, 0.35f), 0.6f);
        CameraFollow.Shake(0.25f, 0.3f);   // [임시]
        var fx1 = RibbonTornadoFx.Spawn(TornadoSettings(Tornado1Life), P.transform, new Vector2(0f, TornadoLift));   // 플레이어를 따라감, 위로 TornadoLift (재료가 없으면 생략)
        yield return new WaitForSecondsRealtime(FallStart);

        // ④ 낙하 (Atk_VerticalSlash): 점점 빨라지며 TornadoLift → GroundAnchor
        P.ShowPose("Atk_VerticalSlash");
        for (float tt = 0f; tt < FallTime; )
        {
            tt += WorldSlow.EffectDelta;
            float k = Mathf.Clamp01(tt / FallTime);
            if (fx1 != null) fx1.transform.localPosition = new Vector3(0f, Mathf.Lerp(TornadoLift, GroundAnchor, k * k), 0f);
            yield return null;
        }

        // ⑤ 지면에 닿음 → 더 큰 토네이도가 지면에서 바깥으로 퍼지며 터지듯 생성 + 피해 · 기절
        Fx.ScreenFlash(new Color(1f, 0.45f, 0.1f, 0.5f), 0.9f);
        CameraFollow.Shake(0.4f, 0.6f);   // [임시] 지면에 닿는 순간 가장 강하게
        RibbonTornadoFx.Spawn(BigTornadoSettings(BigLife), P.transform, Vector2.zero);
        for (int i = 0; i < Hits; i++)
        {
            foreach (var e in SkillHits.OnScreen(targets))
            {
                Fx.Disc(e.transform.position, 0.4f, Fx.Fire, 0.25f);   // 카메라 시야 7 복귀에 맞춰 0.6 × 2/3
                e.Hit(per, (Vector2)(e.transform.position - P.transform.position), 0.5f);
            }
            if (i == Hits - 1) foreach (var e in SkillHits.OnScreen(targets)) e.Stun(StunSeconds);
        }

        // ⑥ 후속 토네이도가 끝나기 FinishZoomLead (1초) 전까지 Atk_VerticalSlash 유지 (낙하 때 바뀐 포즈 그대로) → 그때부터 플레이어 확대 → 토네이도가 끝날 때까지
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, BigLife - FinishZoomLead));
        CameraFollow.SetZoomOut(FocusZoom);
        yield return new WaitForSecondsRealtime(Mathf.Min(FinishZoomLead, BigLife));

        // ⑦ 마무리 동작: Atk_OneHandSlash → Atk_HorizontalSlashReady → Atk_HorizontalSlash (각 0.1초) → RearReach (1초) (후속 토네이도가 끝나면 바로. Atk_VerticalSlash 는 토네이도 종료 후 더 유지하지 않음 — 사용자 2026-11-21)
        // 휘두르는 불꽃 줄기 파티클 대신, 마지막 0.05초에 츠바메가에시 참격 그림 + 작은 불티를 흩뿌림 (OrbitSparks, 사용자 2026-11-21)
        StartCoroutine(OrbitSparks());
        yield return PoseSequence(new[] { "Atk_OneHandSlash", "Atk_HorizontalSlashReady", "Atk_HorizontalSlash", "RearReach" },
                                  new[] { OutroStep, OutroStep, OutroStep, RearReachHold });

        // 궁극기 종료: 포즈 · 멈춤 · 슬로우 해제, 시야 복귀
        running = false;
        P.ReleasePose();
        P.EndRoot();
        WorldSlow.End();
        CameraFollow.SetZoomOut(1f);
    }

    /// <summary>생성 총 시간의 마지막 구간 (OrbitSparkStart ~ OrbitEmitTime) 에, 훑는 경로의 남은 각도 쪽으로 (줄기와 별개로 추가) 아주 작은 불티 (폭발 파티클 그림) 를 흩뿌림</summary>
    IEnumerator OrbitSparks()
    {
        var frames = SpriteEffect.LoadFrames("ErnStray_FireParticlePop", 3, 4, 256f * 0.6f / 175f);
        if (frames == null) yield break;
        yield return new WaitForSecondsRealtime(OrbitSparkStart);
        float span = OrbitEmitTime - OrbitSparkStart;
        float a0 = OrbitSweepStart + OrbitSweepDegrees * (OrbitSparkStart / OrbitEmitTime);   // 불티 시작 시점의 각도부터
        float a1 = OrbitSweepStart + OrbitSweepDegrees;                                      // 훑기 끝 각도 (8시)

        // 마지막 구간에만 츠바메가에시 참격 하나: 참격의 끝점이 8시 (훑기 끝 각도) 에 오도록, 반시계 접선 방향으로 놓음
        var slash = SpriteEffect.LoadFrames(OrbitSlashSheet, 1, 5, 1f);
        if (slash != null)
        {
            // 사용자가 Play 에서 직접 맞춘 값 (2026-11-21): 플레이어 기준 x -0.2, 회전 z -170. y 는 직전 계산값 (0.6) 유지
            var sfx = SpriteEffect.Spawn(slash, OrbitSlashFps, null, (Vector2)P.transform.position + OrbitSlashOffset, OrbitSlashRotation, false, 11, OrbitSlashWidth);
            if (sfx != null) sfx.ignoreWorldSlow = true;
        }

        for (int i = 0; i < OrbitSparkCount; i++)
        {
            if (!running) yield break;
            float a = Random.Range(a0, a1) * Mathf.Deg2Rad;
            float r = Random.Range(OrbitRadiusMin, OrbitRadiusMax);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * OrbitSquash);
            Vector2 pos = (Vector2)P.transform.position + dir * r;
            var fx = SpriteEffect.Spawn(frames, OrbitSparkFps, null, pos, Random.Range(0f, 360f), Random.value < 0.5f, 12,
                                        Random.Range(OrbitSparkScaleMin, OrbitSparkScaleMax));
            if (fx != null)
            {
                fx.ignoreWorldSlow = true;
                var d = fx.gameObject.AddComponent<DriftUp>();
                d.velocity = dir.normalized * OrbitSparkSpeed * Random.Range(0.6f, 1.4f);
                d.life = frames.Length / OrbitSparkFps;
            }
            yield return new WaitForSecondsRealtime(span / OrbitSparkCount);
        }
    }

    /// <summary>기 모으는 동안 플레이어 둘레에서 불꽃 파티클이 조금씩 피어올라 위로 떠오르며 사라짐 (뒤로 갈수록 자주)</summary>
    IEnumerator RisingEmbers()
    {
        var frames = SpriteEffect.LoadFrames("ErnStray_FireParticlePop", 3, 4, 256f * 0.6f / 175f);   // 폭발 파티클과 같은 자르기 (캐시 공유, 12프레임 중 마지막은 투명)
        if (frames == null) yield break;
        var times = new float[EmberCount];
        for (int i = 0; i < EmberCount; i++) times[i] = ChargeTime * Mathf.Pow(Random.value, 0.6f);   // 0.6 제곱: 뒤쪽에 몰림
        System.Array.Sort(times);
        float t = 0f;
        foreach (float at in times)
        {
            if (at > t) { yield return new WaitForSecondsRealtime(at - t); t = at; }
            if (!running) yield break;
            Vector2 around = Random.insideUnitCircle * EmberRadius;
            Vector2 pos = (Vector2)P.transform.position + new Vector2(around.x, -0.7f + around.y * 0.35f);   // 발 근처에서 시작
            var fx = SpriteEffect.Spawn(frames, EmberFps, null, pos, Random.Range(0f, 360f), Random.value < 0.5f, 13,
                                        Random.Range(EmberSizeMin, EmberSizeMax));
            if (fx != null)
            {
                fx.ignoreWorldSlow = true;   // 느려지는 동안에도 정상 속도로 피어오름
                var d = fx.gameObject.AddComponent<DriftUp>();
                d.velocity = new Vector2(Random.Range(-0.15f, 0.15f), EmberRiseSpeed * Random.Range(0.8f, 1.2f));
                d.life = frames.Length / EmberFps;
            }
        }
    }

    // 도중에 스킬이 꺼져도 (사망 · 씬 전환 등) 시야가 줄어든 채 남지 않게
    void OnDisable() { CameraFollow.SetZoomOut(1f); WorldSlow.End(); if (P != null) { P.ReleasePose(); if (running) P.EndRoot(); } }
}

/// <summary>스프라이트 이펙트를 천천히 위로 띄우고 끝 무렵 서서히 투명하게 (기 모으기 불꽃 파티클용)</summary>
public class DriftUp : MonoBehaviour
{
    public Vector2 velocity;
    public float life = 1f;
    float t;
    SpriteRenderer sr;

    void Awake() => sr = GetComponent<SpriteRenderer>();

    void Update()
    {
        float dt = WorldSlow.EffectDelta;   // 기 모으기 파티클은 느려지는 동안에도 정상 속도
        t += dt;
        transform.position += (Vector3)(velocity * dt);
        if (sr != null && life > 0f)
        {
            float k = t / life;
            var c = sr.color; c.a = k < 0.55f ? 1f : Mathf.Clamp01(1f - (k - 0.55f) / 0.45f);   // 앞 55% 는 그대로, 끝까지 서서히
            sr.color = c;
        }
    }
}
