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
    const float Distance = 2.67f * 2.5f;   // [임시] 돌진 거리 (기획 미정). 카메라 시야 7 복귀에 맞춰 4 × 2/3, 사용자 2026-11-22: 2.5배
    const float DashTime = 0.25f;       // [임시]
    const float Knock = 16f;            // [임시] 사용자 2026-11-22: 넉백 거리 증가 (8 → 16)

    // 불꽃 망토 이펙트 (FireMantle_MoveLoop_v4 원본): 5열 × 4행 · 칸 768 × 512 · 20프레임 반복 · 오른쪽을 향함
    // 캐릭터 뒤 레이어 + 앞 레이어 (앞은 캐릭터 자리 알파 63 이하라 캐릭터가 비쳐 보임)
    // 앞 레이어는 프레임이 거의 같아 (알파 겹침 0.97) 한 장으로 줄임 (2026-11-22 사용자, 용량 절약): 프레임 2 한 장 + 코드 일렁임. 원본 20프레임 시트는 Art/Incoming/Retired/FxSheets
    // (되돌리려면 시트를 Resources 로 옮기고 MantleFrontSheet = "ErnStray_FireChargeMantleFront", MantleFrontRows/Cols = 4/5, 아래 Wobble 줄 삭제)
    const string MantleBackSheet = "ErnStray_FireChargeMantleBack", MantleFrontSheet = "ErnStray_FireChargeMantleFront_One";
    const int MantleFrontRows = 1, MantleFrontCols = 1;
    const int MantleRows = 4, MantleCols = 5;
    const float MantleFps = 20f;            // 초안 프레임 50ms
    const float MantleCellPx = 768f;
    const float MantlePivotOffsetPx = 96f;  // 캐릭터 기준점 (480, 256) = 칸 가운데에서 앞쪽(오른쪽)으로 96px
    const float MantleHeightPx = 295f;      // 그림 세로 범위 (프레임별 알파 범위 102~397px)
    const float MantleHeightWorld = 1.85f * 1.5f;  // [임시] 망토 세로 = 캐릭터 키(약 1.62) × 약 1.15, 사용자 2026-11-22: 크기 50% 증가
    const float CharCenterY = 0.17f;        // [해석] 캐릭터 몸 가운데 = 발 기준점 위 0.17 (칸 2.07 · 발 16px · 키 200px 에서 계산)
    const int MantleBackOrder = 9, MantleFrontOrder = 11;   // 플레이어 = 10

    // 특수 강화 id (파이어 차지)
    public const string Long = "FC5A", Smash = "FC5B", Trail = "FC5C";
    public const string Aura = "FC10A", Crush = "FC10B", Continuous = "FC10C";
    public const string Prep = "FC15A", Endless = "FC15B", Shock = "FC15C";
    public const string ComboRush = "FCC1";

    // 특수 강화 확정 수치 (노션 2026-11-22)
    const float LongRange = 0.5f;               // 장거리 돌진: 사거리 +50%
    const float SmashStun = 2f;                 // 강타: 적중 시 기절 +2초
    const float FireRatio = 2f;                 // 불타는 궤적 · 불의 장막 · 충격파: 파이어 차지 총 피해량의 200% 고정 피해
    const float BurnFinal = 1f;                 // 위 셋: 화상 상태 적에게 최종 피해량 +100%
    const float AuraTime = 3f;                  // 불의 장막: 지속 시간 3초
    const float CrushKnock = 0.5f, CrushStun = 1f;   // 분쇄 돌파: 넉백 +50% · 적중 시 기절 +1초
    const int ContinuousStacks = 1;             // 연속 돌진: 스킬 사용 최대 스택 +1
    const float ContinuousCooldownMul = 0.6f;   // 연속 돌진: 쿨타임 -40%
    const float PrepSeconds = 2.5f;             // 돌진 준비: 적중 시 남은 쿨타임 감소 2.5초
    const float EndlessRange = 1f, EndlessStun = 1f;   // 무진돌파: 사거리 +100% · 적중 시 기절 +1초
    const float ComboRange = 1f, ComboPerStack = 0.5f; // 화염 질주: 사거리 +100% · 무효화한 최대 스택 1당 사거리 +50%
    const float ComboSlow = 0.7f;              // 화염 질주: 이동 속도 -70% (사용자 2026-11-22). 같은 거리를 1/0.3 배 시간 동안 이동 [해석]
    // [임시] 노션에 없는 수치
    const float TrailTime = 3f;                 // 불타는 궤적: 장판 지속 시간 (불의 장막과 같게)
    const float FieldTick = 0.5f;               // 장판 · 오오라 지속 피해 간격
    const float AuraRadiusMul = 1f;             // 불의 장막 반경 = 범위 중간 × 이 값
    const float ShockRadiusMul = 1f;            // 충격파 반경 = 범위 중간 × 이 값

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Long, title = "장거리 돌진", desc = "사거리 +50%" },
                new SpecialUpgradeOption { id = Smash, title = "강타", desc = "적중 시 기절 +2초" },
                new SpecialUpgradeOption { id = Trail, title = "불타는 궤적", keywords = new[] { "간접 사용", "지속 피해", "고정 피해" }, desc = "지나간 경로에 화염 장판이 생성됩니다.",
                    specs = new[] { "'파이어 차지' '총 피해량'의 200% '고정 피해'", "화상 상태 적에게 '최종 피해량' +100%" } },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Aura, title = "불의 장막", keywords = new[] { "간접 사용", "지속 피해", "고정 피해" }, desc = "종료 시 불의 오오라가 생깁니다.",
                    specs = new[] { "'파이어 차지' '총 피해량'의 200% '고정 피해'", "'지속 시간' 3초", "화상 상태 적에게 '최종 피해량' +100%" } },
                new SpecialUpgradeOption { id = Crush, title = "분쇄 돌파", desc = "넉백 +50%, 적중 시 기절 +1초" },
                new SpecialUpgradeOption { id = Continuous, title = "연속 돌진", desc = "스킬 사용 최대 스택 +1, 쿨타임 -40%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Prep, title = "돌진 준비", desc = "적중 시 남은 쿨타임 2.5초 감소" },
                new SpecialUpgradeOption { id = Endless, title = "무진돌파", desc = "사거리 +100%, 적중 시 기절 +1초" },
                new SpecialUpgradeOption { id = Shock, title = "충격파", keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "넉백" }, desc = "종료 시 충격파가 발생합니다. 적중 시 넉백.",
                    specs = new[] { "'파이어 차지' '총 피해량'의 200% '고정 피해'", "화상 상태 적에게 '최종 피해량' +100%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboRush, title = "화염 질주", recipe = "연속 돌진 + 무진돌파", requires = new[] { Continuous, Endless },
            desc = "사용 중 자유롭게 이동할 수 있습니다. 스킬 사용 최대 스택 능력치가 무효화되고, 무효화 비율에 따라 사거리가 증가합니다.",
            specs = new[] { "'사거리' +100%", "최대 스택 능력치 무효화 1당 '사거리' +50%", "'이동 속도' -70%" },
        },
    };

    bool Rush => HasCombo(ComboRush);
    /// <summary>스킬 사용 최대 스택: 기본 1, 연속 돌진 +1 (화염 질주가 무효화하면 다시 1)</summary>
    int MaxStacks => 1 + (HasSpecial(Continuous) && !Rush ? ContinuousStacks : 0);
    float RangeBonusSum => (HasSpecial(Long) ? LongRange : 0f) + (HasSpecial(Endless) ? EndlessRange : 0f)
                         + (Rush ? ComboRange + ComboPerStack * ContinuousStacks : 0f);
    float StunSeconds => (HasSpecial(Smash) ? SmashStun : 0f) + (HasSpecial(Crush) ? CrushStun : 0f) + (HasSpecial(Endless) ? EndlessStun : 0f);

    protected override float SpecialCooldownMul => HasSpecial(Continuous) ? ContinuousCooldownMul : 1f;

    // ---------- 스택 (연속 돌진): 파이어 스트림과 같은 방식 · 최대 스택 1 이면 일반 쿨타임과 같음 ----------
    int stacks = 1;
    float recharge;
    float StackCooldown => CooldownSeconds;

    public override string Badge => MaxStacks > 1 ? stacks.ToString() : null;
    public override float CooldownFraction =>
        stacks > 0 ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));
    public override float CooldownRemaining => stacks > 0 ? 0f : Mathf.Max(0f, recharge);

    protected override void OnSpecialUpgrade(int lv, string id)
    {
        if (id == Continuous && !Rush) stacks = Mathf.Min(MaxStacks, stacks + ContinuousStacks);   // 최대 스택이 늘어난 만큼 바로 채움
    }

    public override void Tick(float dt)
    {
        if (DebugCheats.NoCooldown) { stacks = MaxStacks; recharge = 0f; return; }
        if (stacks >= MaxStacks) { stacks = MaxStacks; recharge = 0f; return; }
        if (recharge <= 0f) recharge = StackCooldown;
        recharge -= dt;
        if (recharge <= 0f) { stacks++; recharge = stacks < MaxStacks ? StackCooldown : 0f; }
    }

    /// <summary>남은 쿨타임 감소 (비율)</summary>
    public override void ReduceRemainingCooldown(float ratio)
    {
        if (stacks < MaxStacks && recharge > 0f) recharge *= Mathf.Clamp01(1f - ratio);
    }

    /// <summary>돌진 준비: 충전 중인 스택의 남은 쿨타임을 초 단위로 감소 (다 차면 스택 +1)</summary>
    void ReduceSeconds(float seconds)
    {
        while (seconds > 0f && stacks < MaxStacks)
        {
            if (recharge <= 0f) recharge = StackCooldown;
            if (recharge > seconds) { recharge -= seconds; return; }
            seconds -= recharge;
            stacks++;
            recharge = stacks < MaxStacks ? StackCooldown : 0f;
        }
    }

    public override void DebugResetForCast() { if (stacks <= 0) stacks = 1; recharge = stacks >= MaxStacks ? 0f : recharge; }

    public override bool TryActivate()
    {
        if (CheatDisabled) return false;
        if (stacks <= 0 || P.IsBusy || P.IsStunned) return false;
        if (stacks >= MaxStacks) recharge = StackCooldown;
        stacks--;
        BeginCast();
        Activate();
        return true;
    }

    DamageInfo FixedOf(float amount, CastRef cast)
    {
        var d = FixedDamage(amount, DamageType.Direct, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    /// <summary>화상 상태 적이면 고정 피해량 ×2 (노션: 최종 피해량 +100% — 고정 피해라 직접 곱함)</summary>
    static float BurnMul(Enemy e) => FlameSword.IsBurning(e) ? 1f + BurnFinal : 1f;

    /// <summary>선분 a-b 에서 halfWidth 안의 살아 있는 적</summary>
    static List<Enemy> NearSegment(Vector2 a, Vector2 b, float halfWidth)
    {
        var all = Enemy.CopyAll(new List<Enemy>());
        var res = new List<Enemy>();
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        foreach (var e in all)
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 p = e.transform.position;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            float rr = halfWidth + e.radius;
            if ((p - (a + ab * t)).sqrMagnitude < rr * rr) res.Add(e);
        }
        return res;
    }

    /// <summary>불타는 궤적: 지나간 경로에 장판 · 0.5초마다 고정 피해 (합계 = 총 피해량의 200%, 화상 상태 적은 ×2)</summary>
    IEnumerator TrailField(Vector2 a, Vector2 b, float halfWidth, float total, CastRef cast)
    {
        int ticks = Mathf.Max(1, Mathf.RoundToInt(TrailTime / FieldTick));
        float per = total * FireRatio / ticks;
        Vector2 mid = (a + b) * 0.5f, d = b - a;
        float len = d.magnitude;
        var shape = len > 0.01f ? Fx.Box(a, d / len, len, halfWidth * 2f, Fx.FireDeep, TrailTime) : null;
        for (int i = 0; i < ticks; i++)
        {
            foreach (var e in NearSegment(a, b, halfWidth))
                e.Hit(FixedOf(per * BurnMul(e), cast), d, Enemy.NoKnock);
            yield return new WaitForSeconds(FieldTick);
        }
    }

    /// <summary>불의 장막: 도착 지점에 오오라 3초 · 0.5초마다 고정 피해 (합계 = 총 피해량의 200%, 화상 상태 적은 ×2)</summary>
    IEnumerator AuraField(Vector2 at, float r, float total, CastRef cast)
    {
        int ticks = Mathf.Max(1, Mathf.RoundToInt(AuraTime / FieldTick));
        float per = total * FireRatio / ticks;
        Fx.GroundCircle(at, r, Fx.FireDeep, AuraTime);
        var list = new List<Enemy>();
        for (int i = 0; i < ticks; i++)
        {
            SkillHits.InCircle(at, r, list);
            foreach (var e in list) if (e != null && e.hp > 0) e.Hit(FixedOf(per * BurnMul(e), cast), (Vector2)e.transform.position - at, Enemy.NoKnock);
            yield return new WaitForSeconds(FieldTick);
        }
    }

    /// <summary>충격파: 도착 지점에서 한 번 · 고정 피해 (총 피해량의 200%, 화상 상태 적은 ×2) + 넉백</summary>
    void ShockWave(Vector2 at, float r, float total, CastRef cast, float knock)
    {
        Fx.Disc(at, r, Fx.FireBright, 0.3f);
        HitDebug.Circle(at, r, HitDebug.BlastColor);
        var list = new List<Enemy>();
        SkillHits.InCircle(at, r, list);
        foreach (var e in list)
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 d = (Vector2)e.transform.position - at;
            if (d.sqrMagnitude < 0.0001f) d = Random.insideUnitCircle;
            e.Hit(FixedOf(total * FireRatio * BurnMul(e), cast), d, 0f);
            e.Push(d, knock);
        }
        CameraFollow.Shake(0.12f, 0.12f);
    }

    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    /// <summary>돌진 방향으로 망토 뒤 · 앞 레이어를 띄움 (플레이어를 따라감). 그림이 없으면 null</summary>
    SpriteLoopEffect[] SpawnMantle(Vector2 dir)
    {
        float pxWorld = MantleHeightWorld / MantleHeightPx;
        float cellWorld = MantleCellPx * pxWorld;
        var back = SpriteEffect.LoadFrames(MantleBackSheet, MantleRows, MantleCols, cellWorld);
        var front = SpriteEffect.LoadFrames(MantleFrontSheet, MantleFrontRows, MantleFrontCols, cellWorld);
        if (back == null || front == null) return null;
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        // [해석] 왼쪽으로 갈 때 위아래가 뒤집히지 않도록 세로 반전 (윗꼬리는 계속 위)
        bool mirror = dir.x < 0f;
        Vector2 at = new Vector2(0f, CharCenterY) - dir * (MantlePivotOffsetPx * pxWorld);
        var fx = new[]
        {
            SpriteLoopEffect.Spawn(back, MantleFps, P.transform, at, MantleBackOrder, 1f),
            SpriteLoopEffect.Spawn(front, MantleFps, P.transform, at, MantleFrontOrder, 1f)?.Wobble(0.025f, 0.035f, 0.08f, 9f),   // [임시] 한 장 + 일렁임
        };
        foreach (var f in fx)
        {
            f.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            if (mirror) f.transform.localScale = new Vector3(1f, -1f, 1f);
            Destroy(f.gameObject, DashTime / (1f - ComboSlow) + 0.5f);   // 안전장치: 돌진이 끊겨 onEnd 가 안 와도 남지 않게
        }
        return fx;
    }

    /// <summary>망토를 돌진 방향 dir 에 맞춰 다시 돌림 · 옮김 (화염 질주: 방향이 실시간으로 바뀜)</summary>
    static void OrientMantle(SpriteLoopEffect[] fx, Vector2 dir)
    {
        if (fx == null || dir.sqrMagnitude < 0.01f) return;
        dir.Normalize();
        float pxWorld = MantleHeightWorld / MantleHeightPx;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 at = new Vector2(0f, CharCenterY) - dir * (MantlePivotOffsetPx * pxWorld);
        foreach (var f in fx)
        {
            if (f == null) continue;
            f.transform.localPosition = at;
            f.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            f.transform.localScale = new Vector3(1f, dir.x < 0f ? -1f : 1f, 1f);   // 왼쪽으로 갈 때 세로 반전 (윗꼬리는 계속 위)
        }
    }

    protected override void Activate()
    {
        hit.Clear();
        float r = Range(Tier.Mid) * 0.5f;   // [임시] 돌진 중 판정은 범위 중간의 절반
        var cast = DirectCast;
        DamageInfo dmg = MakeDamage(BaseDamage);
        float total = FinalDamageOf(BaseDamage);   // [해석] 파이어 차지 총 피해량 = 돌진 1회의 최종 피해량 (적중 1회분)
        float stun = StunSeconds;
        float knock = Knock * (1f + (HasSpecial(Crush) ? CrushKnock : 0f));
        float distance = Distance * (1f + RangeBonusSum);
        int hitCount = 0;
        bool rush = Rush;
        Vector2 dashDir = P.Facing.sqrMagnitude > 0.01f ? P.Facing.normalized : Vector2.right;
        Vector2 sideAxis = new Vector2(-dashDir.y, dashDir.x);   // 돌진 방향의 옆 (왼쪽)
        Vector2 start = P.transform.position;
        var mantle = SpawnMantle(P.Facing);
        if (mantle == null) Fx.Circle(P.transform.position, r, Fx.Fire, DashTime + 0.1f, P.transform, 0f);   // 그림이 없을 때만
        float dashTime = rush ? DashTime / (1f - ComboSlow) : DashTime;   // 화염 질주: 이동 속도 -70%
        P.Dash(P.Facing, distance, dashTime, true,
            onStep: () =>
            {
                if (rush) { P.SteerDash(new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"))); OrientMantle(mantle, P.Facing); }   // 화염 질주: 이동 방향에 따라 망토도 실시간으로 바뀜   // 화염 질주: 사용 중 자유 이동 [해석: 돌진 방향을 입력으로 틀 수 있음]
                Vector2 at = P.transform.position;
                foreach (var e in SkillHits.InCircle(at, r, found))
                {
                    if (!hit.Add(e)) continue;
                    hitCount++;
                    // 부딪힌 적은 돌진 방향 양옆으로 넉백: 경로의 어느 쪽에 있는지에 따라 그 쪽으로 (사용자 2026-11-22)
                    Vector2 d = (Vector2)e.transform.position - at;
                    float s = Vector2.Dot(d, sideAxis);
                    Vector2 side = sideAxis * (Mathf.Abs(s) < 0.01f ? (Random.value < 0.5f ? 1f : -1f) : Mathf.Sign(s));
                    e.Hit(dmg, side, 0f);
                    e.Push(side, knock);
                    if (stun > 0f) e.Stun(stun);
                }
            },
            onEnd: () =>
            {
                // 도착 지점의 원 이펙트 · 추가 밀치기는 없음 (사용자 2026-11-22: 밀치기는 돌진 중에만)
                if (mantle != null) foreach (var f in mantle) if (f != null) f.Stop();
                Vector2 end = P.transform.position;
                var ind = cast.AsIndirect;
                if (HasSpecial(Trail)) StartCoroutine(TrailField(start, end, r, total, ind));
                if (HasSpecial(Aura)) StartCoroutine(AuraField(end, Range(Tier.Mid) * AuraRadiusMul, total, ind));
                if (HasSpecial(Shock)) ShockWave(end, Range(Tier.Mid) * ShockRadiusMul, total, ind, knock);
                if (HasSpecial(Prep) && hitCount > 0) ReduceSeconds(PrepSeconds * hitCount);   // 돌진 준비: 적중마다 2.5초 (이번 사용이 끝날 때 합산)
            });
    }
}

/// <summary>
/// M03 오버 드라이브 플레임 슬래시 (구 오버 플레임 슬래시): 화염의 검을 내려쳐 기절 → 1초 뒤 같은 자리 폭발.
/// 화염의 검 초기 피해량 20 · 기절 2초, 폭발 딜레이 1초 · 초기 피해량 50, 쿨타임 20초
/// </summary>
public class OverFlameSlash : ActiveSkill
{
    const float BladeDamage = 20f, BaseStun = 2f;
    const float ExplosionDelay = 1f, ExplosionDamage = 50f;
    const float BaseCooldown = 20f;
    const float WidthRatio = 0.5f;      // [임시] 검 판정 폭 = 범위 좁음 × 비율
    const float ExplosionWidthMul = 2.2f;   // [임시] 폭발 판정 폭 = 검 판정 폭 × 이 값 (같은 길이)

    // 화염검 그림 (FlameBlade_Upright_v5 원본): 4열 × 4행 · 칸 256 × 768 · 16프레임 반복 · 칼끝 위 · 곧은 날 = 오른쪽
    // 한 장 (프레임 13, 알파 겹침 0.94) + 코드 일렁임 (2026-11-22 사용자, 용량 절약). 원본 16프레임 시트는 Art/Incoming/Retired/FxSheets (되돌리기: 시트 이름 · 4×4 로, Wobble 줄 삭제)
    const string BladeSheet = "ErnStray_OverFlameBlade_One";
    const int BladeRows = 1, BladeCols = 1;
    const float BladeFps = 20f;             // 초안 프레임 50ms
    const float BladeCellPx = 256f;
    const float BladeRootFromCenterPx = 340f;   // 뿌리 기준점 (128, 724) = 칸 가운데(384)에서 아래로 340px
    const float BladeLengthPx = 674f;       // 칼 길이 (알파 범위 54~728px)
    const int BladeOrder = 12;
    const float WindupTime = 0.35f;         // [임시] 칼을 위로 치켜들고 준비하는 시간 (그동안 이동 불가 [해석])
    const float SwingTime = 0.08f;          // [임시] 내려치는 시간 → 끝나는 순간 판정
    const float UpwardLean = 0f;            // 바로 위로 공격할 때 준비 자세를 공격 방향에서 뒤로 젖히는 각도 (도). 사용자 2026-11-22: 위로 쓸 때는 수직으로 듦 (45 → 0, 회전 없이 곧게 선 채 앞으로 내려옴). 0 보다 크면 그만큼 젖힌 자세에서 시작
    const float TraceLengthMul = 1.5f;      // 지면 잔흔 · 폭발 그림의 길이 배율 (사용자 2026-11-22: 인스펙터 Scale 1.5). 판정 길이는 그대로
    const float TraceCenterDist = 4f;       // 지면 잔흔 · 폭발 그림의 중심 거리 (플레이어에서 공격 방향으로, 사용자 2026-11-22: 인스펙터 X 4). 이전에는 판정 길이의 절반
    const float WindupLift = 1.4f;          // 대기 동작 동안 화염검 시작 피봇의 세로 위치 (플레이어 기준 월드, 사용자 2026-11-22: 디버그 맵 (플레이어 y -6) 에서 피봇 y -4.6 = 플레이어 +1.4 = 손 높이). 내려치면서 원래 자리 (플레이어) 로 옮겨 옴
    const float BladeForward = 1f;          // 내려친 화염검의 뿌리를 공격 방향으로 앞당기는 거리 (월드, 사용자 2026-11-22: 인스펙터 X 1). 그림만 옮김 — 판정 · 지면 잔흔은 그대로
    const float TraceDelay = 0.25f;         // 내려친 뒤 지면 잔흔이 생기기까지 (사용자 2026-11-22: 잔흔이 생기기 0.25초 전부터 화염검이 사라짐). 폭발 시각 (내려친 뒤 ExplosionDelay) 은 그대로라 잔흔은 그만큼 짧게 남음
    const float TraceFadeIn = 0.25f;        // 잔흔이 나타나는 시간 (사용자 2026-11-22: 천천히 → 0.25초)
    // 화염검이 사라질 때 흩어지는 불꽃 파티클 (FireParticlePop 시트 재사용, 사용자 2026-11-22). 모두 [임시]
    const int BladeSparkCount = 22;         // 사라지는 동안 터뜨리는 개수
    const float BladeSparkSize = 0.5f;      // 파티클 1개 크기 (월드, 기본 0.6 기준)
    const float BladeSparkRise = 1.6f;      // 위로 떠오르는 속도 (월드/초)
    const float BladeSparkSide = 0.8f;      // 옆으로 퍼지는 속도 (±)
    const float BladeFadeTime = TraceDelay; // 화염검이 사라지는 시간 = 0.25초 (잔흔이 생기는 순간 다 사라짐). 이전 0.7 → 0.35 → 0.15
    const float BladeLinger = 0.15f;        // [임시] 내려친 뒤 칼이 남는 시간

    // 지면 잔흔 (FlameBlade_GroundTrace_Sustain_v1 원본): 4열 × 6행 · 칸 768 × 192 · 24프레임 반복 · 가로선 + 위로 오르는 불씨
    // 한 장 (프레임 5, 알파 겹침 0.99) + 코드 일렁임 (2026-11-22 사용자, 용량 절약). 원본 24프레임 시트는 Art/Incoming/Retired/FxSheets (되돌리기: 시트 이름 · 6×4 로, Wobble 줄 삭제)
    const string TraceSheet = "ErnStray_OverFlameGroundTrace_One";
    const int TraceRows = 1, TraceCols = 1;
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

    // 특수 강화 id (오버 드라이브 플레임 슬래시)
    public const string Giant = "OS5A", Bind = "OS5B", QuickPrep = "OS5C";
    public const string Trace = "OS10A", Collapse = "OS10B", Impact = "OS10C";
    public const string Focus = "OS15A", Triple = "OS15B", Aftermath = "OS15C";
    public const string ComboBlazing = "OSC1";

    // 특수 강화 확정 수치 (노션 2026-11-22)
    const float GiantRange = 0.5f, GiantDamage = 0.25f;     // 거대한 검: 범위 +50% · 피해량 +25%
    const float BindStun = 2f;                              // 옭아매는 화염: 기절 +2초
    const float QuickPrepPerHit = 0.025f;                   // 빠른 준비: 적중 시 남은 쿨타임 감소 2.5%
    const float TraceRatio = 0.6f, TraceTime = 3f, TraceSlow = 0.3f;   // 불타는 잔흔: 총 피해량 60% 고정 피해 · 3초 · 이동 속도 70% 감소 (×0.3)
    const float CollapseFinal = 0.75f;                      // 붕괴의 일격: 기절 상태 적 최종 피해량 +75%
    const float ImpactStun = 2.5f, ImpactCooldownMul = 0.8f;   // 충격 집중: 기절 +2.5초 · 쿨타임 감소 +20%
    const float FocusFinal = 1f, FocusPerStunSec = 0.25f;   // 화염 집중: 최종 피해량 +100% · 무효화한 기절 1초당 +25%
    const float TripleAngle = 60f;                          // 삼연발: 양쪽 60도 간격으로 1회씩 추가 사용
    const float AftermathRange = 0.5f, AftermathFinal = 0.5f;   // 후폭풍: 범위 +50% · 최종 피해량 +50%
    const float ComboRange = 0.25f, ComboFinal = 0.5f, ComboPerExtra = 0.25f;   // 오버 드라이브 블레이징 슬래시: 범위 +25% · 최종 +50% · 무효화한 추가 사용 1당 +25%
    const int ExtraUses = 2;                                // 삼연발의 추가 사용 수 (조합이 무효화하는 수)
    // [임시] 노션에 없는 수치
    const float TraceTick = 0.5f;                           // 잔흔 지속 피해 간격
    const float GatherTime = 0.6f;                          // 조합: 양쪽 검이 돌며 적을 모으는 시간 (이후 본 검이 내려침)
    const float GatherStart = 270f;                         // 조합: 각 검이 도는 총 각도 (사용자 2026-11-22: 270도). 시작 = 보는 방향 기준 뒤 → 180도 돌아 앞까지 적을 모으고 → 90도 더 돌아 반대편 옆에서 끝남
    const float GatherFadeAt = 180f;                        // 조합: 이만큼 돌았을 때 검이 급격히 투명해지기 시작 (사용자 2026-11-22: 180도)
    const float GatherFadeTime = 0.12f;                     // [임시] 급격히 투명해지는 시간
    const float GatherBand = 1.2f;                          // 조합: 검날이 적을 쓸어 모으는 폭 (검 중심선에서 이 거리 안)

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Giant, title = "거대한 검", desc = "범위 +50%, 피해량 +25%" },
                new SpecialUpgradeOption { id = Bind, title = "옭아매는 화염", desc = "기절 +2초" },
                new SpecialUpgradeOption { id = QuickPrep, title = "빠른 준비", desc = "적중 시 남은 쿨타임 감소 2.5%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Trace, title = "불타는 잔흔", keywords = new[] { "간접 사용", "지속 피해", "고정 피해" },
                    desc = "종료 시 잔흔이 남아 지속 피해를 줍니다. 잔흔은 이동 속도 감소를 부여합니다.",
                    specs = new[] { "'오버 드라이브 플레임 슬래시' '총 피해량'의 60% '고정 피해'", "'지속 시간' 3초", "'이동 속도 감소' 70% 부여" } },
                new SpecialUpgradeOption { id = Collapse, title = "붕괴의 일격", desc = "화염의 검 기절 상태 적 적중 시 최종 피해량 +75%" },
                new SpecialUpgradeOption { id = Impact, title = "충격 집중", desc = "기절 +2.5초, 쿨타임 감소 +20%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Focus, title = "화염 집중", desc = "폭발이 사라지고 기절 능력치가 무효화됩니다. 무효화 비율에 따라 최종 피해량이 증가합니다.",
                    specs = new[] { "'최종 피해량' +100%", "기절 능력치 무효화 1초당 '최종 피해량' +25%" } },
                new SpecialUpgradeOption { id = Triple, title = "삼연발", desc = "양쪽 60도 간격으로 1회씩 추가 사용합니다." },
                new SpecialUpgradeOption { id = Aftermath, title = "후폭풍", desc = "폭발 적중 시 적들을 폭발 중심으로 모읍니다.", specs = new[] { "'범위' +50%", "'최종 피해량' +50%" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboBlazing, title = "오버 드라이브 블레이징 슬래시", recipe = "붕괴의 일격 + 삼연발", requires = new[] { Collapse, Triple },
            desc = "추가 사용이 사라집니다. 두 개의 검으로 전방향의 적을 한곳으로 모은 뒤, 강화된 검을 내려칩니다.",
            specs = new[] { "추가 사용 능력치 무효화", "'범위' +25%", "'최종 피해량' +50%", "추가 사용 능력치 무효화 1당 '최종 피해량' +25%" },
        },
    };

    bool Blazing => HasCombo(ComboBlazing);
    float StunSeconds => BaseStun + (HasSpecial(Bind) ? BindStun : 0f) + (HasSpecial(Impact) ? ImpactStun : 0f);
    float ActualStun => HasSpecial(Focus) ? 0f : StunSeconds;   // 화염 집중: 기절 무효화

    protected override float SpecialDamageBonus => HasSpecial(Giant) ? GiantDamage : 0f;
    protected override float SpecialRangeBonus =>
        (HasSpecial(Giant) ? GiantRange : 0f) + (HasSpecial(Aftermath) ? AftermathRange : 0f) + (Blazing ? ComboRange : 0f);
    protected override float SpecialCooldownMul => HasSpecial(Impact) ? ImpactCooldownMul : 1f;
    protected override float SpecialFinalDamageMul
    {
        get
        {
            float m = 1f;
            if (HasSpecial(Focus)) m *= 1f + FocusFinal + FocusPerStunSec * StunSeconds;   // 무효화한 기절 시간만큼
            if (HasSpecial(Aftermath)) m *= 1f + AftermathFinal;
            if (Blazing) m *= 1f + ComboFinal + ComboPerExtra * ExtraUses;
            return m;
        }
    }

    /// <summary>붕괴의 일격: 기절 상태 적에게 최종 피해량 +75% (대상 단계, 피해가 들어가기 전 기준)</summary>
    static float CollapseMul(Enemy e) => e.IsStunned ? 1f + CollapseFinal : 1f;

    DamageInfo Dmg(float initial, CastRef cast)
    {
        var d = MakeDamage(initial, cast);
        if (HasSpecial(Collapse)) d.perTarget = CollapseMul;
        return d;
    }

    DamageInfo FixedOf(float amount, CastRef cast)
    {
        var d = FixedDamage(amount, DamageType.Direct, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    /// <summary>사용 1회가 모으는 값 (빠른 준비의 적중 수)</summary>
    class UseState { public int hits; }

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 from = P.transform.position;
        Vector2 dir = P.Facing;
        var cast = DirectCast;
        Vector2 ndir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;

        if (Blazing)
        {
            // 조합: 추가 사용 대신 먼저 양쪽에서 두 검이 보는 방향으로 돌며 검날로 전방향의 적을 한곳으로 모은 뒤, 본 검을 내려침
            // 본 검은 시작부터 치켜든 채 (준비 시간 = 모으는 시간), 모으기가 끝나면 내려치는 시간은 기존과 같음 (사용자 2026-11-22)
            StartCoroutine(Gather(from, ndir));
            yield return Use(from, dir, cast, true, GatherTime);
            yield break;
        }
        else if (HasSpecial(Triple))
        {
            // 삼연발: 양쪽 60도로 1회씩 추가 사용 (같은 타이밍 · 간접 사용 · 포즈 · 이동 불가는 본 사용만)
            StartCoroutine(Use(from, Rotate(ndir, TripleAngle), cast.AsIndirect, false));
            StartCoroutine(Use(from, Rotate(ndir, -TripleAngle), cast.AsIndirect, false));
        }
        yield return Use(from, dir, cast, true);
    }

    static Vector2 Rotate(Vector2 v, float deg) => (Vector2)(Quaternion.Euler(0f, 0f, deg) * v);

    /// <summary>
    /// 조합: 양쪽에서 두 검이 (거의 뒤쪽에서) 보는 방향으로 돌며 검날에 닿은 적을 내려칠 자리 중심으로 쓸어 모음.
    /// 두 검이 앞에서 만나면 사라지고 본 검이 내려침
    /// </summary>
    IEnumerator Gather(Vector2 from, Vector2 dir)
    {
        float len = Reach(Tier.Long);
        float face = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Vector2 center = from + dir * len * 0.5f;
        var blades = new Transform[2];
        float[] side = { 1f, -1f };   // 왼쪽 검은 시계 방향 (각도 감소), 오른쪽 검은 시계 반대 방향
        for (int i = 0; i < 2; i++)
        {
            blades[i] = SpawnBlade(from, dir, len, out _, out _);
            if (blades[i] == null) continue;
            blades[i].position = from;
            if (blades[i].childCount > 0)   // 곧은 날이 앞장서도록: 시계 방향이면 그대로, 반대면 좌우 반전
                blades[i].GetChild(0).localScale = new Vector3(side[i] > 0f ? 1f : -1f, 1f, 1f);
        }
        var list = new List<Enemy>();
        bool fading = false;
        for (float t = 0f; t < GatherTime; t += Time.deltaTime)
        {
            float k = t / GatherTime;
            k = k * k * (3f - 2f * k);
            if (!fading && GatherStart * k >= GatherFadeAt)   // 180도 돌았을 때 급격히 투명해짐
            {
                fading = true;
                for (int i = 0; i < 2; i++)
                {
                    var fx = blades[i] != null ? blades[i].GetComponentInChildren<SpriteLoopEffect>() : null;
                    if (fx != null) fx.FadeOut(GatherFadeTime);
                }
            }
            SkillHits.InCircle(from, len, list);
            for (int i = 0; i < 2; i++)
            {
                float angle = face + side[i] * (GatherFadeAt - GatherStart * k);   // 뒤 (180도) 에서 시작 → 앞까지 모은 뒤 90도 더 돎
                if (blades[i] != null) blades[i].rotation = Quaternion.Euler(0f, 0f, angle - 90f);
                Vector2 bdir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
                foreach (var e in list)
                {
                    if (e == null || e.hp <= 0 || e.IsWall || e.Hidden || e.Scripted) continue;
                    Vector2 rel = (Vector2)e.transform.position - from;
                    float along = Vector2.Dot(rel, bdir);
                    float lateral = Mathf.Abs(rel.x * bdir.y - rel.y * bdir.x);
                    if (fading || along < 0f || lateral > GatherBand) continue;   // 검날에 닿은 적만 (투명해진 뒤에는 모으지 않음)
                    Vector2 to = center - (Vector2)e.transform.position;
                    float dist = to.magnitude;
                    if (dist > 0.3f) e.Push(to, Enemy.KnockSpeed(dist));
                }
            }
            yield return null;
        }
        for (int i = 0; i < 2; i++) if (blades[i] != null) Destroy(blades[i].gameObject);
    }

    /// <summary>
    /// 사용 1회 (삼연발의 추가 사용 포함). main = 본 사용 (플레이어 포즈 · 이동 불가 · 카메라 흔들림은 본 사용만).
    /// </summary>
    IEnumerator Use(Vector2 from, Vector2 dir, CastRef cast, bool main, float windup = WindupTime)
    {
        float len = Reach(Tier.Long);
        float width = Range(Tier.Short) * WidthRatio;
        var st = new UseState();
        float stun = ActualStun;
        bool focus = HasSpecial(Focus);
        System.Action<Enemy> onSword = e => { st.hits++; if (stun > 0f) e.Stun(stun); };
        System.Action<Enemy> onBlast = e => { st.hits++; };
        if (HasSpecial(Aftermath))
        {
            Vector2 center = from + (dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right) * len * 0.5f;
            onBlast += e =>
            {
                if (e == null || e.hp <= 0) return;
                Vector2 to = center - (Vector2)e.transform.position;
                float dist = to.magnitude;
                if (dist > 0.3f) e.Push(to, Enemy.KnockSpeed(dist));   // 후폭풍: 폭발에 맞은 적을 중심으로 모음
            };
        }

        // 화염검: 위로 치켜든 채 준비 → 공격 방향으로 내려침 (그림이 없으면 예전처럼 바로 판정)
        if (main) P.Root(windup + SwingTime);   // 준비 · 내려치는 동안 이동 불가 (그림이 없어도 동일, 사용자 2026-11-22)
        bool posed = main && P.ShowPose("OverheadRaise");   // 화염 검 준비 동작 (사용자 2026-11-22: ErnStray_OverheadRaise). 그림이 없으면 기존 모습 그대로
        Transform blade = SpawnBlade(from, dir, len, out float startAngle, out float sweep);
        if (blade != null)
        {
            Vector2 ndir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
            Vector2 bladeEnd = from + ndir * BladeForward;      // 내려친 화염검의 최종 자리 (공격 방향으로 BladeForward 앞)
            blade.position = from + Vector2.up * WindupLift;   // 대기 동작: 화염검 시작 피봇을 WindupLift 위치로
            yield return new WaitForSeconds(windup);
            if (posed) P.ShowPose("RisingSlash_02");   // 내려베기 (사용자 2026-11-22: ErnStray_RisingSlash_02)
            else if (main) P.PlayAttack(dir);
            for (float t = 0f; t < SwingTime; t += Time.deltaTime)
            {
                float k = t / SwingTime;
                blade.rotation = Quaternion.Euler(0f, 0f, startAngle + sweep * k * k - 90f);   // 가속하며 내려침
                blade.position = Vector2.Lerp(from + Vector2.up * WindupLift, bladeEnd, k * k);    // 대기 위치의 화염검이 같이 원래 자리로 옴
                yield return null;
            }
            blade.rotation = Quaternion.Euler(0f, 0f, startAngle + sweep - 90f);
            blade.position = bladeEnd;
            StartCoroutine(FadeBlade(blade, len));   // 내려친 직후부터 서서히 사라지며 불꽃 파티클이 흩어짐
            if (posed) StartCoroutine(ReleasePoseAfter(BladeLinger));   // 검이 남는 동안 내려벤 자세 유지 → 원래 모습
        }
        else
        {
            if (main)
            {
                if (posed) { yield return new WaitForSeconds(windup); P.ShowPose("RisingSlash_02"); StartCoroutine(ReleasePoseAfter(BladeLinger)); }
                else P.PlayAttack(dir);
            }
            Fx.Box(from, dir, len, width, Fx.FireBright, 0.3f);
        }
        SkillHits.Box(from, dir, len, width, Dmg(BladeDamage, cast), 2f, onSword);
        if (main) CameraFollow.Shake(0.08f, 0.08f);

        // [임시] 폭발 위치 = 검을 내려친 자리 (넓은 직사각형)
        // 내려친 자리에 지면 잔흔 → 폭발 때 제거. 그림이 없으면 예전 바닥 직사각형
        yield return new WaitForSeconds(TraceDelay);   // 화염검이 다 사라진 순간 잔흔이 생김
        if (!focus)   // 화염 집중: 폭발 삭제 (잔흔 · 폭발 연출도 없음)
        {
            Transform trace = SpawnGroundTrace(from, dir, len);
            if (trace == null) Fx.Box(from, dir, len, width * ExplosionWidthMul, Fx.FireDeep, ExplosionDelay - TraceDelay);
            yield return new WaitForSeconds(Mathf.Max(0f, ExplosionDelay - TraceDelay));
            if (trace != null) Destroy(trace.gameObject);
            if (!SpawnGroundBurst(from, dir, len)) Fx.Box(from, dir, len, width * ExplosionWidthMul, Fx.FireBright, 0.3f);
            StartCoroutine(SpawnParticlePops(from, dir, len, width * ExplosionWidthMul));
            SkillHits.Box(from, dir, len, width * ExplosionWidthMul, Dmg(ExplosionDamage, cast), 3f, onBlast);
            if (main) CameraFollow.Shake(0.15f, 0.15f);
        }
        else yield return new WaitForSeconds(Mathf.Max(0f, ExplosionDelay - TraceDelay));

        // 빠른 준비: 이번 사용의 적중 수만큼 남은 쿨타임 감소 (적중당 2.5%, 합계 90% 상한)
        if (HasSpecial(QuickPrep) && st.hits > 0) ReduceRemainingCooldown(Mathf.Min(0.9f, QuickPrepPerHit * st.hits));

        // 불타는 잔흔: 종료 시 남아 3초 동안 지속 피해 + 이동 속도 감소
        if (HasSpecial(Trace))
            yield return TraceField(from, dir, len, width * ExplosionWidthMul, cast);
    }

    /// <summary>불타는 잔흔: 폭발 자리에 3초 동안 남아 0.5초마다 고정 피해 · 이동 속도 70% 감소. 합계 = 총 피해량 (화염의 검 + 폭발 최종 피해량) 의 60% [해석]</summary>
    IEnumerator TraceField(Vector2 from, Vector2 dir, float len, float boxWidth, CastRef cast)
    {
        float total = FinalDamageOf(BladeDamage) + (HasSpecial(Focus) ? 0f : FinalDamageOf(ExplosionDamage));
        int ticks = Mathf.Max(1, Mathf.RoundToInt(TraceTime / TraceTick));
        DamageInfo per = FixedOf(total * TraceRatio / ticks, cast);
        Transform fx = SpawnGroundTrace(from, dir, len);
        if (fx == null) Fx.Box(from, dir, len, boxWidth, Fx.FireDeep, TraceTime);
        for (int i = 0; i < ticks; i++)
        {
            SkillHits.Box(from, dir, len, boxWidth, per, Enemy.NoKnock, e => e.SustainSlow(TraceSlow, TraceTick + 0.1f));
            yield return new WaitForSeconds(TraceTick);
        }
        if (fx != null) Destroy(fx.gameObject);
    }

    /// <summary>내려친 화염검: 내려친 직후부터 BladeFadeTime (= 잔흔이 생기기 TraceDelay 전) 동안 서서히 투명해져 사라짐</summary>
    IEnumerator FadeBlade(Transform blade, float len)
    {
        if (blade == null) yield break;
        var fx = blade.GetComponentInChildren<SpriteLoopEffect>();
        if (fx != null) fx.FadeOut(BladeFadeTime);
        Destroy(blade.gameObject, BladeFadeTime + 0.1f);

        // 사라지는 칼날 위 무작위 자리에서 불꽃 파티클이 시간차로 터져 위로 흩어짐 (칼날 방향 = 회전축의 위쪽)
        Vector2 root = blade.position, bdir = blade.up, side = new Vector2(-bdir.y, bdir.x);
        var frames = SpriteEffect.LoadFrames(PopSheet, PopRows, PopCols, PopCellPx * BladeSparkSize / PopSpanPx);
        if (frames == null) yield break;
        float t = 0f;
        for (int i = 0; i < BladeSparkCount; i++)
        {
            float at = BladeFadeTime * (i + Random.value) / BladeSparkCount;   // 사라지는 시간 안에 고르게
            if (at > t) { yield return new WaitForSeconds(at - t); t = at; }
            Vector2 pos = root + bdir * Random.Range(0.05f, 1f) * len + side * Random.Range(-0.15f, 0.15f);
            var spark = SpriteEffect.Spawn(frames, PopFps * 0.7f, null, pos, Random.Range(0f, 360f), Random.value < 0.5f, PopOrder,
                                           Random.Range(0.6f, 1.2f));
            if (spark != null)
            {
                var d = spark.gameObject.AddComponent<DriftUp>();
                d.velocity = new Vector2(Random.Range(-BladeSparkSide, BladeSparkSide), BladeSparkRise * Random.Range(0.7f, 1.3f));
                d.life = frames.Length / (PopFps * 0.7f);
            }
        }
    }

    IEnumerator ReleasePoseAfter(float seconds)
    {
        yield return new WaitForSeconds(seconds);
        P.ReleasePose();
    }

    /// <summary>
    /// 화염검을 뿌리 = from 에 세움. 반환 = 회전축(뿌리) Transform, 그림이 없으면 null.
    /// 칼 길이 = 판정 길이. 시작 각도 = 항상 위 (90°, 사용자 2026-11-22: 준비는 무조건 위에서). 거기서 공격 방향으로 내려침:
    /// 오른쪽 · 아래 공격은 시계 방향, 왼쪽 공격은 시계 반대 방향 (+ 좌우 반전 → 곧은 날이 앞장섬). sweep = 내려치는 회전량 (도, 부호 = 방향).
    /// 바로 위로 공격하면 회전량이 0 이라 위쪽으로 조금 기울인 자세에서 시작 (UpwardLean)
    /// </summary>
    Transform SpawnBlade(Vector2 from, Vector2 dir, float len, out float startAngle, out float sweep)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float endAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        bool left = dir.x < -0.01f;
        startAngle = 90f;
        sweep = endAngle - startAngle;
        if (!left) { while (sweep > 0f) sweep -= 360f; while (sweep <= -360f) sweep += 360f; }   // 시계 방향 (−)
        else { while (sweep < 0f) sweep += 360f; while (sweep >= 360f) sweep -= 360f; }         // 시계 반대 방향 (+)
        if (Mathf.Abs(sweep) < UpwardLean)   // 위쪽 공격: 위에서 시작하면 내려칠 거리가 없음 → 뒤로 젖힌 자세에서 시작
        {
            startAngle = endAngle + (left ? -UpwardLean : UpwardLean);
            sweep = endAngle - startAngle;
        }
        float pxWorld = len / BladeLengthPx;
        var frames = SpriteEffect.LoadFrames(BladeSheet, BladeRows, BladeCols, BladeCellPx * pxWorld);
        if (frames == null) return null;

        var pivot = new GameObject("FX_OverFlameBladePivot").transform;
        pivot.position = from;
        pivot.rotation = Quaternion.Euler(0f, 0f, startAngle - 90f);   // 그림은 칼끝이 위(90°)
        var loop = SpriteLoopEffect.Spawn(frames, BladeFps, pivot, new Vector2(0f, BladeRootFromCenterPx * pxWorld), BladeOrder, 1f).Wobble(0.03f, 0.012f, 0.1f, 11f);   // [임시] 한 장 + 일렁임
        if (left) loop.transform.localScale = new Vector3(-1f, 1f, 1f);   // [해석] 왼쪽으로 내려칠 땐 곧은 날이 앞(왼쪽)
        Destroy(pivot.gameObject, WindupTime + SwingTime + BladeLinger + BladeFadeTime + 1f);   // 안전장치
        return pivot;
    }

    /// <summary>
    /// 내려친 자리(검 판정 직사각형 중심선)에 지면 잔흔. 선 길이 = 판정 길이, 바닥 레이어.
    /// [해석] 왼쪽 공격이면 세로 반전 (불씨가 계속 위로). 반환 = 루트 Transform, 그림이 없으면 null
    /// </summary>
    Transform SpawnGroundTrace(Vector2 from, Vector2 dir, float len)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float pxWorld = len * TraceLengthMul / TraceLengthPx;
        var frames = SpriteEffect.LoadFrames(TraceSheet, TraceRows, TraceCols, TraceCellPx * pxWorld);
        if (frames == null) return null;

        var root = new GameObject("FX_OverFlameGroundTrace").transform;
        root.position = from + dir * TraceCenterDist;
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
        if (dir.x < -0.01f) root.localScale = new Vector3(1f, -1f, 1f);
        SpriteLoopEffect.Spawn(frames, TraceFps, root, new Vector2(0f, TracePivotBelowCenterPx * pxWorld), Fx.GroundOrder, 1f).Wobble(0.015f, 0.25f, 0.15f, 14f).FadeIn(TraceFadeIn);   // [임시] 한 장 + 일렁임 (가는 선이라 세로로 크게)
        Destroy(root.gameObject, TraceTime + ExplosionDelay + 1f);   // 안전장치 (불타는 잔흔은 3초 남음)
        return root;
    }

    /// <summary>잔흔 자리에서 폭발 (잔흔과 같은 위치 · 회전 · 축척). 그림이 없으면 false</summary>
    bool SpawnGroundBurst(Vector2 from, Vector2 dir, float len)
    {
        dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector2.right;
        float pxWorld = len * TraceLengthMul / TraceLengthPx;
        var frames = SpriteEffect.LoadFrames(BurstSheet, BurstRows, BurstCols, TraceCellPx * pxWorld);
        if (frames == null) return false;
        var fx = SpriteEffect.Spawn(frames, BurstFps, null, from + dir * TraceCenterDist,
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
    const float BaseStun = 1f;          // 기획서 확정
    const float BaseDelay = 2f;         // 기획서 확정 (폭발 사이 간격 = 폭발 딜레이 2초)
    const int BaseCount = 2;            // 공격 횟수 = 폭발 횟수 (최초 + 2초 뒤 추가 1회)

    // 특수 강화 id (파이어 브레이커)
    public const string Fissure = "FBK5A", Hot = "FBK5B", Fast = "FBK5C";
    public const string Chain = "FBK10A", Release = "FBK10B", Absorb = "FBK10C";
    public const string Stack = "FBK15A", Crust = "FBK15B", Gravity = "FBK15C";
    public const string ComboFlame = "FBKC1";

    // 특수 강화 확정 수치 (노션 2026-11-22)
    const float FissureRange = 0.5f, FissureStun = 1f;      // 균열 확장: 폭발마다 범위 +50% · 기절 +1초
    const float HotSlow = 0.5f;                             // 불타는 대지: 이동 속도 감소 50% (×0.5)
    const float FastInterval = 0.5f, FastFinal = 0.5f;      // 빠른 폭발: 공격 간격 -50% · 최종 피해량 +50%
    const int ChainBonus = 2;                               // 연쇄 분출: 공격 횟수 +2
    const float AbsorbHeal = 0.05f;                         // 충격 흡수: 마지막 폭발 적 하나당 최대 체력 5% 회복
    const float StackFinal = 0.2f;                          // 중첩 붕괴: 받는 최종 피해량 +20% (적중 시 부여)
    const int StackMax = 5;                                 // 최대 스택 5
    const int CrustBonus = 2;                               // 지각 붕괴: 공격 횟수 +2
    const float CrustInterval = 0.5f;                       // 지각 붕괴: 공격 간격 -50%
    const float GravityRange = 0.5f, GravityStun = 2f;      // 중력 붕괴: 공격 횟수 능력치 무효화 1당 범위 +50% · 기절 +2초
    const float ComboFinal = 0.4f;                          // 오버 드라이브 플레임 브레이커: 최종 피해량 -60% (×0.4)
    const int ComboBonus = 5;                               // 공격 횟수 +5
    // [임시] 노션에 없는 수치
    const float ReleaseKnock = 8f;                          // 방출: 적중 시 넉백 (기본 1)
    const float HotTime = 3f, HotStep = 0.25f;              // 불타는 대지: 장판 지속 시간 · 감속 갱신 간격 (화상 스택은 매 초)
    const float MinDelay = 0.25f;                           // 폭발 사이 간격 하한
    const float ExtraSearch = 14f, ExtraStep = 0.03f;       // 조합: 추가 사용 대상을 찾는 범위 · 추가 사용 사이 간격
    // 재발동 표시 고리 ErnStray_FireBreakerMark (2026-11-22 사용자 전달 단일 그림, 원본 1254px → 게임용 1024px 로 줄임). 시트 (2000×2000) 대신 코드로 연출 (GroundMarkFx)
    const string MarkSheet = "ErnStray_FireBreakerMark";
    const float MarkSrcPx = 1254f;                       // 원본 가로 (좌표 기준)
    const float MarkRingRadiusPx = 500f;                 // 측정: 얇은 고리의 반지름 (원본 px, 불투명 픽셀 반지름 중앙값 474 · 90% 506)
    static readonly Vector2 MarkCenterNorm = new Vector2(622f / 1254f, 629f / 1254f);   // 측정: 고리 중심 (그림 가운데에서 왼쪽 5px · 아래 2px, 아래쪽 기준)
    const float BaseDamage = 10f, BaseCooldown = 10f;   // [해석] 두 번째 폭발도 초기 피해량 10

    const float RangeMul = 2f;          // 범위 배율 (사용자 2026-11-22: 2배). 판정 · 이펙트 크기 모두
    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Fissure, title = "균열 확장", desc = "폭발마다 범위 +50%, 폭발마다 기절 +1초" },
                new SpecialUpgradeOption { id = Hot, title = "불타는 대지", desc = "종료 시 장판이 생성됩니다. 매 초 장판에 닿은 적에게 적용 중인 모든 화상 스택이 1 증가하고 이동 속도 감소를 부여합니다.", specs = new[] { "'이동 속도 감소' 50%" } },
                new SpecialUpgradeOption { id = Fast, title = "빠른 폭발", desc = "공격 간격 -50%, 최종 피해량 +50%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Chain, title = "연쇄 분출", desc = "공격 횟수 +2" },
                new SpecialUpgradeOption { id = Release, title = "방출", desc = "적중 시 넉백" },
                new SpecialUpgradeOption { id = Absorb, title = "충격 흡수", desc = "마지막 폭발 적중 시 적 하나당 최대 체력 5% 회복" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Stack, title = "중첩 붕괴", desc = "적중 시 받는 최종 피해량 +20%를 부여합니다. 최대 스택 5" },
                new SpecialUpgradeOption { id = Crust, title = "지각 붕괴", desc = "공격 횟수 +2, 공격 간격 -50%" },
                new SpecialUpgradeOption { id = Gravity, title = "중력 붕괴", desc = "공격 횟수 능력치가 무효화됩니다.", specs = new[] { "공격 횟수 능력치 무효화 1당 '범위' +50%", "공격 횟수 능력치 무효화 1당 '기절' +2초" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboFlame, title = "오버 드라이브 플레임 브레이커", recipe = "연쇄 분출 + 지각 붕괴", requires = new[] { Chain, Crust },
            desc = "매 공격 시 공격 횟수에 비례해 무작위 대상의 위치에 추가 사용합니다. 같은 적에게 중복 생성될 수 있습니다.",
            specs = new[] { "'최종 피해량' -60%", "공격 횟수 +5", "공격 횟수 1당 추가 사용 +1" },
        },
    };

    bool Combo => HasCombo(ComboFlame);
    /// <summary>중력 붕괴로 무효화되기 전의 공격 횟수</summary>
    int RawCount => BaseCount + (HasSpecial(Chain) ? ChainBonus : 0) + (HasSpecial(Crust) ? CrustBonus : 0) + (Combo ? ComboBonus : 0);
    float Delay => Mathf.Max(MinDelay, BaseDelay * (HasSpecial(Fast) ? FastInterval : 1f) * (HasSpecial(Crust) ? CrustInterval : 1f));

    protected override float SpecialFinalDamageMul => (HasSpecial(Fast) ? 1f + FastFinal : 1f) * (Combo ? ComboFinal : 1f);

    // ---------- 중첩 붕괴: 받는 최종 피해량 증가 부여 (대상마다 스택, 사망하면 정리) ----------
    readonly Dictionary<Enemy, int> stacks = new Dictionary<Enemy, int>();
    StackModifier stackMod;

    sealed class StackModifier : CombatModifier
    {
        readonly FireBreaker owner;
        public StackModifier(FireBreaker o) { owner = o; }
        public override float ReceivedMul(Enemy target, in DamageInfo d)
            => owner != null && owner.stacks.TryGetValue(target, out int n) && n > 0 ? 1f + StackFinal * n : 1f;
    }

    void AddStack(Enemy e)
    {
        if (e == null || e.hp <= 0) return;
        if (stackMod == null) { stackMod = new StackModifier(this); CombatMods.Add(stackMod); }
        stacks.TryGetValue(e, out int n);
        stacks[e] = Mathf.Min(StackMax, n + 1);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (stackMod != null) { CombatMods.Remove(stackMod); stackMod = null; }
        stacks.Clear();
    }

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        Vector2 at = P.transform.position;   // 같은 위치에서 재발동
        var cast = DirectCast;
        int raw = RawCount;
        bool gravity = HasSpecial(Gravity);
        int count = gravity ? 1 : raw;          // 중력 붕괴: 공격 횟수 능력치 무효화 → 폭발 1회
        int nullified = gravity ? raw - 1 : 0;  // [해석] 무효화된 횟수 = 원래 공격 횟수 - 1 (폭발 1회는 남음)
        float delay = Delay;
        P.PlayAttack();
        float r = ExplosionRadius(0, nullified);
        float stun = ExplosionStun(0, nullified);
        float rLast = r;
        for (int i = 0; i < count; i++)
        {
            r = ExplosionRadius(i, nullified);
            stun = ExplosionStun(i, nullified);
            rLast = r;
            bool last = i == count - 1;
            if (!Combo) Burst(at, r, cast, stun, last, true);   // 특수 조합: 기본 폭발 제거 (추가 사용만 발생, 사용자 2026-11-22)
            if (Combo) StartCoroutine(Extras(at, r, cast.AsIndirect, stun, count, last));
            if (last) break;
            // 다음 폭발 위치 표시 = 바닥 레이어. 고리 그림 한 장을 코드로 차오르게 함 (시트 대신, 용량 절약). 그림이 없으면 예전 단색 원
            float rNext = ExplosionRadius(i + 1, nullified);
            var markTex = Combo ? null : SpriteLib.Load(MarkSheet);   // 조합은 기본 폭발이 없어 표시도 없음
            GameObject mark = null;
            if (Combo) { }
            else if (markTex != null)
            {
                var fx = GroundMarkFx.Spawn(markTex, MarkCenterNorm, MarkRingRadiusPx, MarkSrcPx, at, rNext, delay, Fx.GroundOrder);
                if (fx != null) mark = fx.gameObject;
            }
            else { var shape = Fx.GroundCircle(at, rNext, Fx.FireDeep, delay); if (shape != null) mark = shape.gameObject; }
            yield return new WaitForSeconds(delay);
            if (mark != null) Destroy(mark);
        }
        if (HasSpecial(Hot)) yield return HotField(at, rLast);
    }

    /// <summary>i 번째 폭발 (0부터) 의 범위: 균열 확장은 폭발마다 +50%, 중력 붕괴는 무효화 1당 +50%</summary>
    float ExplosionRadius(int i, int nullified) =>
        Range(Tier.Mid) * RangeMul * (1f + (HasSpecial(Fissure) ? FissureRange * i : 0f) + GravityRange * nullified);

    /// <summary>i 번째 폭발의 기절: 균열 확장은 폭발마다 +1초, 중력 붕괴는 무효화 1당 +2초</summary>
    float ExplosionStun(int i, int nullified) =>
        BaseStun + (HasSpecial(Fissure) ? FissureStun * i : 0f) + GravityStun * nullified;

    /// <summary>
    /// 조합: 폭발 한 번마다 공격 횟수만큼 무작위 적의 위치에 추가 폭발 (같은 적에게 중복 가능). 대상이 없으면 없음.
    /// 추가 사용은 본 폭발과 같은 범위 · 기절
    /// </summary>
    IEnumerator Extras(Vector2 around, float r, CastRef cast, float stun, int n, bool lastRound)
    {
        var list = new List<Enemy>();
        SkillHits.InCircle(around, ExtraSearch, list);
        if (list.Count == 0) yield break;
        for (int k = 0; k < n; k++)
        {
            var e = list[Random.Range(0, list.Count)];
            if (e != null && e.hp > 0) Burst(e.transform.position, r, cast, stun, lastRound, false);   // 마지막 공격의 추가 사용이 마지막 폭발 (충격 흡수)
            yield return new WaitForSeconds(ExtraStep);
        }
    }

    /// <summary>불타는 대지: 종료 시 마지막 폭발 자리에 장판. 매 초 닿은 적의 화상 스택 +1 (화상 중인 적만), 이동 속도 50% 감소</summary>
    IEnumerator HotField(Vector2 at, float r)
    {
        var shape = Fx.GroundCircle(at, r, Fx.FireDeep, HotTime);
        var list = new List<Enemy>();
        float acc = 1f;   // 첫 틱은 바로
        for (float t = 0f; t < HotTime; t += HotStep)
        {
            SkillHits.InCircle(at, r, list);
            bool stackTick = acc >= 1f;
            if (stackTick) acc = 0f;
            foreach (var e in list)
            {
                if (e == null || e.hp <= 0) continue;
                e.SustainSlow(1f - HotSlow, HotStep + 0.1f);
                if (stackTick && FlameSword.IsBurning(e) && FlameSword.I != null) FlameSword.I.AddStacks(e, 1);
            }
            acc += HotStep;
            yield return new WaitForSeconds(HotStep);
        }
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

    /// <param name="main">본 폭발 (카메라 흔들림 · 충격 흡수 · 중첩 붕괴 대상)</param>
    void Burst(Vector2 at, float r, CastRef cast, float stun, bool lastBurst, bool main)
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
        bool stack = HasSpecial(Stack), absorb = HasSpecial(Absorb) && lastBurst;
        SkillHits.Circle(at, r, MakeDamage(BaseDamage, cast), HasSpecial(Release) ? ReleaseKnock : 1f, e =>
        {
            e.Stun(stun);
            if (stack) AddStack(e);   // 중첩 붕괴: 이번 피해가 들어간 뒤부터 받는 최종 피해량 증가
            if (absorb && P != null) P.hp = Mathf.Min(P.maxHp, P.hp + P.maxHp * AbsorbHeal);   // 충격 흡수: 마지막 폭발 적 하나당 최대 체력 5%
        });
        if (main) CameraFollow.Shake(0.1f, 0.1f);
    }
}

/// <summary>
/// M05 파이어 엑스큐션: 빠른 속도로 주변을 베어냄 · 초기 피해량 5 × 공격 횟수 30 · 쿨타임 30초 · 발동 중 이동 불가
/// 특수 강화 9종 + 특수 조합 플레임 디멘션 블레이드 (노션 2026-11-22 기준)
/// </summary>
public class FireExecution : ActiveSkill
{
    const float RangeMul = 3f;          // 범위 배율 (사용자 2026-11-22: 50% 증가 → 거기서 다시 2배 = 3배). 판정 · 이펙트 크기 모두
    const float FadeOutTime = 0.5f;     // [임시] 끝날 때 고리 이펙트가 서서히 사라지는 시간 (사용자 2026-11-22)
    const int BaseHits = 30;
    const float HitInterval = 0.025f;   // 베기 1회 간격. 사용자 2026-11-22: 속도 2배 (30회 0.75초 = 0.025초 간격). 공격 횟수가 늘어도 간격은 그대로 [해석]
    const float BaseDamage = 5f, BaseCooldown = 30f;

    // 특수 강화 id (파이어 엑스큐션)
    public const string Slaughter = "FE5A", Flow = "FE5B", Focus = "FE5C";
    public const string Quick = "FE10A", Wind = "FE10B", Scorch = "FE10C";
    public const string Endless = "FE15A", Afterimage = "FE15B", Blazing = "FE15C";
    public const string ComboDimension = "FEC1";

    // 특수 강화 확정 수치 (노션)
    const int SlaughterBonus = 15;          // 난도질: 공격 횟수 +15
    const float FlowSlow = 0.5f;            // 유동 검무: 시전 중 이동 속도 -50% (이동 불가 삭제)
    const float FocusRange = -0.5f, FocusDamage = 0.5f;   // 집중 절삭: 범위 -50% · 피해량 +50%
    const float QuickTimeMul = 0.25f;       // 고속 절단: 공격 시간 -75%
    const float QuickCooldownMul = 0.75f;   // 고속 절단: 쿨타임 감소 +25% (독립 곱 0.75)
    const int WindEvery = 5, WindCount = 8; // 검풍: 베기 5회마다 검기 8개
    const float WindRatio = 0.4f;           // 검풍: 총 피해량의 40% 고정 피해
    const float ScorchFinal = 0.5f;         // 작열 절삭: 화상 상태 적 최종 피해량 +50%
    const int EndlessBonus = 30;            // 무진참: 공격 횟수 +30
    const float EndlessCooldownMul = 1.25f; // 무진참: 쿨타임 +25%
    const int AfterimageCount = 6;          // 잔영: 종료 시 소형 연속 베기 6개
    const float AfterimageRatio = 0.6f;     // 잔영: 총 피해량의 60% 고정 피해
    const int BlazingEvery = 5;             // 타오르는 검: 화상 상태 적 5회 적중 시 폭발
    const float BlazingRatio = 1f;          // 타오르는 검: 폭발 화상 총 피해량의 100% 고정 피해
    const int ComboBonus = 30;              // 플레임 디멘션 블레이드: 공격 횟수 +30
    const int ComboWaveEvery = 3;           // 플레임 디멘션 블레이드: 공격 횟수 3당 검기 1개

    // [임시] 노션에 없는 수치
    const float WaveSpeed = 14f, WaveRange = 10f, WaveSize = 0.55f;   // 검기 속도 · 사거리 · 크기
    const float SeekTurn = 540f;            // 유도 검기가 방향을 트는 속도 (도/초)
    const float SeekRange = 12f;            // 유도 검기가 적을 찾는 거리
    const float AfterimageMin = 2.5f, AfterimageMax = 7f;   // 잔영이 생기는 거리 (플레이어 기준)
    const float SmallComboScale = 0.35f;    // 소형 연속 베기 반경 = 본 베기 반경 × 이 값
    const int SmallComboHits = 8;           // 소형 연속 베기 1개의 베기 횟수
    const float SmallComboTime = 0.3f;      // 소형 연속 베기가 이어지는 시간
    const float BlazingRadiusMul = 1f;      // 타오르는 검 폭발 반경 = 범위 좁음 × 이 값

    // 연속 베기 고리 이펙트 (FireSlash_OmniCombo_v2 원본): 6열 × 5행 · 칸 640px · 30프레임 · 반복 재생
    const string SlashLoopSheet = "ErnStray_FireSlashOmniCombo";
    const int SlashLoopRows = 5, SlashLoopCols = 6;
    const float SlashLoopFps = 50f;       // 초안 프레임 40ms (한 바퀴 1.2초). 사용자 2026-11-22: 속도 2배 (25 → 50)
    const float SlashLoopCellPx = 640f;
    const float SlashLoopRadiusPx = 290f; // 그림 범위 반지름 (프레임별 알파 범위 34~623px 에서 잰 값)
    const int SlashLoopOrder = 11;

    protected override float CooldownSeconds => CooldownOf(BaseCooldown);

    // ---------- 특수 강화 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Slaughter, title = "난도질", desc = "공격 횟수 +15" },
                new SpecialUpgradeOption { id = Flow, title = "유동 검무", desc = "이동 불가가 사라집니다. 시전 중 이동 속도 -50%" },
                new SpecialUpgradeOption { id = Focus, title = "집중 절삭", desc = "범위 -50%, 피해량 +50%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Quick, title = "고속 절단", desc = "공격 시간 -75%, 쿨타임 감소 +25%" },
                new SpecialUpgradeOption { id = Wind, title = "검풍", keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "투사체", "원거리" },
                    desc = "베기 5회마다 8개의 검기를 난사합니다.", specs = new[] { "'파이어 엑스큐션' '총 피해량'의 40% '고정 피해'" } },
                new SpecialUpgradeOption { id = Scorch, title = "작열 절삭", desc = "화상 상태 적 적중 시 최종 피해량 +50%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Endless, title = "무진참", desc = "공격 횟수 +30, 쿨타임 +25%" },
                new SpecialUpgradeOption { id = Afterimage, title = "잔영", keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "투사체", "원거리" },
                    desc = "종료 시 무작위 위치에 소형 연속 베기 6개를 생성합니다.", specs = new[] { "'파이어 엑스큐션' '총 피해량'의 60% '고정 피해'" } },
                new SpecialUpgradeOption { id = Blazing, title = "타오르는 검", desc = "화상 상태 적에게 5회 적중하면 폭발이 발생합니다.", specs = new[] { "폭발: 화상 '총 피해량'의 100% '고정 피해'" } },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboDimension, title = "플레임 디멘션 블레이드", recipe = "검풍 + 잔영", requires = new[] { Wind, Afterimage },
            keywords = new[] { "간접 사용", "직접 피해", "고정 피해", "투사체", "원거리" },
            desc = "연속 베기가 사라지고 공격 횟수가 늘어납니다. 공격 횟수에 비례해 유도 검기를 발사하고, 검기가 적중하면 연속 베기가 발생합니다.",
            specs = new[] { "연속 베기 삭제", "공격 횟수 +30", "공격 횟수 3당 검기 1개 발사", "검기 유도", "검기 적중 시 연속 베기 발생" },
        },
    };

    bool Dimension => HasCombo(ComboDimension);
    int TotalHits => BaseHits + (HasSpecial(Slaughter) ? SlaughterBonus : 0) + (HasSpecial(Endless) ? EndlessBonus : 0) + (Dimension ? ComboBonus : 0);
    float Interval => HitInterval * (HasSpecial(Quick) ? QuickTimeMul : 1f);

    protected override float SpecialDamageBonus => HasSpecial(Focus) ? FocusDamage : 0f;
    protected override float SpecialRangeBonus => HasSpecial(Focus) ? FocusRange : 0f;
    protected override float SpecialCooldownMul =>
        (HasSpecial(Quick) ? QuickCooldownMul : 1f) * (HasSpecial(Endless) ? EndlessCooldownMul : 1f);

    /// <summary>작열 절삭: 화상 상태 적에게 최종 피해량 +50% (대상 단계, 피해가 들어가기 전 화상 기준)</summary>
    static float ScorchMul(Enemy e) => FlameSword.IsBurning(e) ? 1f + ScorchFinal : 1f;

    DamageInfo FixedOf(float amount, CastRef cast)
    {
        var d = FixedDamage(amount, DamageType.Direct, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    // ---------- 이동 속도 감소 (유동 검무) ----------
    bool slowed;
    void BeginSlow() { if (slowed) return; slowed = true; P.moveSpeedMul *= 1f - FlowSlow; }
    void EndSlow() { if (!slowed) return; slowed = false; P.moveSpeedMul /= 1f - FlowSlow; }

    protected override void OnDisable()
    {
        base.OnDisable();
        EndSlow();
    }

    protected override void Activate() => StartCoroutine(Run());

    IEnumerator Run()
    {
        int hits = TotalHits;
        float interval = Interval;
        float duration = hits * interval;
        bool flow = HasSpecial(Flow);
        bool dimension = Dimension;
        var cast = DirectCast;
        var indirect = cast.AsIndirect;

        if (flow) BeginSlow(); else P.Root(duration);

        // [해석] 노션 키워드 '고정 피해' 는 검풍 · 잔영 · 조합 쪽 → 본 베기는 일반 직접 피해
        DamageInfo per = MakeDamage(BaseDamage, cast);
        if (HasSpecial(Scorch)) per.perTarget = ScorchMul;

        // [해석] '총 피해량' = 이번 사용의 베기 전체 최종 피해량 (최종 피해량 × 공격 횟수). 검풍 · 잔영 · 조합은 그 x% 를 개수로 나눠 줌
        float totalDamage = FinalDamageOf(BaseDamage) * hits;
        int waves = dimension ? hits / ComboWaveEvery : (HasSpecial(Wind) ? hits / WindEvery * WindCount : 0);
        float waveDamage = waves > 0 ? totalDamage * WindRatio / waves : 0f;
        float smallComboTotal = dimension
            ? (waves > 0 ? totalDamage * AfterimageRatio / waves : 0f)
            : totalDamage * AfterimageRatio / AfterimageCount;

        // 그림 반지름 = 판정 반경 (범위 중간). 발동 동안 플레이어를 따라감. 조합은 연속 베기 삭제
        SpriteLoopEffect loop = null;
        float r0 = Range(Tier.Mid) * RangeMul;
        if (!dimension)
        {
            var frames = SpriteEffect.LoadFrames(SlashLoopSheet, SlashLoopRows, SlashLoopCols, 1f);
            loop = SpriteLoopEffect.Spawn(frames, SlashLoopFps, P.transform, Vector2.zero, SlashLoopOrder,
                                          r0 * SlashLoopCellPx / SlashLoopRadiusPx);
        }

        var burnHits = new Dictionary<Enemy, int>();   // 타오르는 검: 화상 상태에서 맞은 횟수
        bool blazing = HasSpecial(Blazing);
        bool wind = HasSpecial(Wind) && !dimension;

        float t = 0f;
        int done = 0;
        while (done < hits)
        {
            t += Time.deltaTime;
            int due = Mathf.Min(hits, Mathf.FloorToInt(t / interval + 0.0001f) + 1);   // 첫 베기는 바로. 간격이 한 프레임보다 짧으면 한 프레임에 여러 번
            while (done < due)
            {
                int i = done++;
                bool last = i == hits - 1;
                Vector2 at = P.transform.position;
                float r = Range(Tier.Mid) * RangeMul;
                P.PlayAttack();
                if (dimension)
                {
                    // 연속 베기 없음: 공격 횟수 3당 유도 검기 1개
                    if ((i + 1) % ComboWaveEvery == 0) FireWave(at, RandomDir(), waveDamage, indirect, true, smallComboTotal, r);
                }
                else
                {
                    if (loop == null) Fx.Circle(at, r, last ? Fx.FireBright : Fx.Fire, 0.25f);   // 그림이 없을 때만 예전 원
                    SkillHits.Circle(at, r, per, last ? 6f : 1f, blazing ? (System.Action<Enemy>)(e => CountBurn(e, burnHits, indirect)) : null);
                    if (wind && (i + 1) % WindEvery == 0)
                    {
                        float a0 = Random.value * 360f;
                        for (int k = 0; k < WindCount; k++)
                        {
                            float a = (a0 + k * 360f / WindCount) * Mathf.Deg2Rad;
                            FireWave(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)), waveDamage, indirect, false, 0f, r);
                        }
                    }
                }
                if (last) CameraFollow.Shake(0.15f, 0.15f);
            }
            yield return null;
        }

        if (loop != null) loop.FadeOut(FadeOutTime);   // 갑자기 끊지 않고 서서히 사라짐 (플레이어를 따라가는 채로)
        EndSlow();

        // 잔영: 종료 시 무작위 위치에 소형 연속 베기 6개
        if (HasSpecial(Afterimage) && !dimension)
        {
            Vector2 c = P.transform.position;
            float sr = Range(Tier.Mid) * RangeMul * SmallComboScale;
            for (int k = 0; k < AfterimageCount; k++)
            {
                Vector2 pos = c + RandomDir() * Random.Range(AfterimageMin, AfterimageMax);
                StartCoroutine(SmallCombo(pos, sr, smallComboTotal, indirect));
            }
        }
    }

    static Vector2 RandomDir()
    {
        float a = Random.value * Mathf.PI * 2f;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    }

    /// <summary>타오르는 검: 화상 상태의 적을 5번 맞히면 그 자리에서 폭발 (화상 총 피해량의 100% 고정 피해)</summary>
    void CountBurn(Enemy e, Dictionary<Enemy, int> counts, CastRef cast)
    {
        if (e == null || e.hp <= 0 || !FlameSword.IsBurning(e)) return;
        counts.TryGetValue(e, out int n);
        if (++n < BlazingEvery) { counts[e] = n; return; }
        counts.Remove(e);
        Vector2 at = e.transform.position;
        float r = Range(Tier.Short) * BlazingRadiusMul;
        float burn = FlameSword.TotalBurnDamage(e) * BlazingRatio;
        Fx.Disc(at, r, Fx.FireBright, 0.25f);
        HitDebug.Circle(at, r, HitDebug.BlastColor);
        SkillHits.Circle(at, r, FixedOf(burn, cast), 1f);
    }

    /// <summary>검기 1개: 고정 피해 · 1회 적중 · 조합이면 적을 따라 휘고 적중 위치에 소형 연속 베기</summary>
    void FireWave(Vector2 from, Vector2 dir, float damage, CastRef cast, bool seek, float comboTotal, float baseRadius)
    {
        var p = Projectile.Spawn(from, dir, WaveSpeed, FixedOf(damage, cast), 1, WaveRange / WaveSpeed,
                                 "FireBall", new Color(1f, 0.5f, 0.12f), WaveSize);
        if (seek)
        {
            var s = p.gameObject.AddComponent<SwordWaveSeeker>();
            s.projectile = p; s.turn = SeekTurn; s.range = SeekRange;
            float sr = baseRadius * SmallComboScale;
            p.onHit = (e, pos) => StartCoroutine(SmallCombo(pos, sr, comboTotal, cast));
        }
    }

    /// <summary>소형 연속 베기: 그 자리에서 짧게 여러 번 베기 (고정 피해, 합계 total)</summary>
    IEnumerator SmallCombo(Vector2 at, float r, float total, CastRef cast)
    {
        var frames = SpriteEffect.LoadFrames(SlashLoopSheet, SlashLoopRows, SlashLoopCols, 1f);
        var fx = SpriteLoopEffect.Spawn(frames, SlashLoopFps, null, at, SlashLoopOrder, r * SlashLoopCellPx / SlashLoopRadiusPx);
        if (fx == null) Fx.Circle(at, r, Fx.Fire, SmallComboTime);
        DamageInfo d = FixedOf(total / SmallComboHits, cast);
        float step = SmallComboTime / SmallComboHits;
        for (int i = 0; i < SmallComboHits; i++)
        {
            SkillHits.Circle(at, r, d, 0.5f);
            yield return new WaitForSeconds(step);
        }
        if (fx != null) fx.FadeOut(0.15f);
    }
}

/// <summary>유도 검기 (플레임 디멘션 블레이드): 가장 가까운 적 쪽으로 방향을 틀며 날아감</summary>
public class SwordWaveSeeker : MonoBehaviour
{
    public Projectile projectile;
    public float turn, range;

    void Update()
    {
        if (projectile == null) return;
        Vector2 me = transform.position;
        Enemy best = null;
        float bestSq = range * range;
        foreach (var e in Enemy.All)
        {
            if (e.hp <= 0 || e.IsWall || e.Hidden || e.Scripted || projectile.HasHit(e)) continue;
            float sq = ((Vector2)e.transform.position - me).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; best = e; }
        }
        if (best == null) return;
        Vector3 want = ((Vector2)best.transform.position - me).normalized;
        projectile.Direction = Vector3.RotateTowards(projectile.Direction, want, turn * Mathf.Deg2Rad * Time.deltaTime, 0f);
    }
}

/// <summary>
/// D01 파이어 스트림: 빠른 돌진 · 이동 경로 피해 · 무적 · 초기 피해량 3 · 스택 3 · 스택 쿨타임 8초 (노션 2026-11-20)
/// </summary>
public class FireStream : Skill
{
    const int BaseMaxStacks = 3;        // 기획서 확정
    const float DashTime = 0.12f;       // [임시] 이동 시간 (속도 '매우 빠름')
    // [임시 해석] 스택은 한 번에 하나씩 순서대로 회복

    int stacks = BaseMaxStacks;
    float recharge;
    readonly HashSet<Enemy> hit = new HashSet<Enemy>();
    readonly List<Enemy> found = new List<Enemy>();

    const float BaseDamage = 3f, BaseCooldown = 8f;    // 스택 쿨타임 8초 (노션)
    const float HitRadius = 0.4f;       // [임시] 이동 경로 판정 반경 (카메라 시야 7 복귀에 맞춰 0.6 × 2/3)

    // ---------- 특수 강화 (노션 2026-11-23 편집본) ----------
    public const string Continuous = "FS5A", Trail = "FS5B", Veil = "FS5C";
    public const string Sprint = "FS10A", Reflux = "FS10B", Pierce = "FS10C";
    public const string Lance = "FS15A", Backdraft = "FS15B", Endless = "FS15C";
    public const string ComboIlseom = "FSC1";

    // 확정 수치 (노션)
    const int ContinuousStacks = 1;             // 연속 돌진: 최대 스택 +1
    const float ContinuousCooldown = 1.2f;      // 연속 돌진: 쿨타임 +20%
    const float TrailRatio = 2f;                // 불타는 궤적: 파이어 스트림 총 피해량의 200% 고정 피해
    const float BurnFinal = 1f;                 // 불타는 궤적: 화상 상태 적에게 최종 피해량 +100%
    const float VeilInvuln = 2f;                // 화염 장막: 종료 시 2초간 무적 유지
    const float SprintRange = 0.5f;             // 고속 질주: 사거리 +50%
    const float SprintCooldown = 0.75f;         // 고속 질주: 쿨타임 감소 +25% (배율 0.75)
    const float RefluxPerHit = 0.025f;          // 화염 환류: 적중 시 남은 쿨타임 감소 2.5%
    const float PierceFinal = 3f;               // 꿰뚫는 화염: 최종 피해량 +200% (배율 3) — [해석] 추가 사용에만 적용
    const float LanceWaitTime = 1f;             // 화염의 창: 누르고 있을 때 최대 1초 대기
    const float LanceFinal = 5f;                // 화염의 창: 피격 시 최종 피해량 +400% (배율 5) — 그 사용에만
    const float LanceCooldownCut = 0.2f;        // 화염의 창: 피격 시 남은 쿨타임 감소 20%
    const float BackdraftFinal = 2f;            // 후폭풍: 최종 피해량 +100% (배율 2) — [해석] 추가 사용에만 적용
    const int EndlessStacks = 2;                // 무한 기동: 최대 스택 +2
    const float EndlessCooldown = 0.5f;         // 무한 기동: 쿨타임 감소 +50% (배율 0.5)
    const float EndlessFinal = 0.25f;           // 무한 기동: 최종 피해량 -75%
    const float IlseomFinal = 10f;              // 화염 일섬: 최종 피해량 +1000%
    const float IlseomPerReduction = 5f;        // 화염 일섬: 쿨타임 감소 능력치 무효화 1% 당 최종 피해량 +5% (전환율 1 = 100% → +500%)
    const float IlseomPerStack = 1.5f;          // 화염 일섬: 최대 스택 능력치 무효화 1당 최종 피해량 +150%
    // [임시] 노션에 없는 수치
    const float TrailTime = 3f;                 // 불타는 궤적: 장판 지속 시간 (파이어 차지 불타는 궤적과 같게)
    const float TrailTick = 0.5f;               // 불타는 궤적: 지속 피해 간격
    const bool LanceBlocksHit = true;           // [해석] 화염의 창 대기 중 맞은 공격은 막음 (반격 자세). false = 피해는 받고 돌진만
    const float BackdraftDelay = 0.05f;         // 후폭풍: 돌진이 끝난 뒤 지나온 경로를 훑기까지

    // 불꽃 줄기 (ErnStray_FireStream_StraightDash_v4 원본): 4열 × 6행 · 칸 768 × 512 · 24프레임 (마지막 = 투명) · 1회
    // 오른쪽으로 곧게 자라는 나선 불꽃 (월드에 고정, 캐릭터를 따라가지 않음) · 뒤 레이어 + 앞 레이어
    const string StreamRearSheet = "ErnStray_FireStreamRear", StreamFrontSheet = "ErnStray_FireStreamFront";
    const int StreamRows = 6, StreamCols = 4;
    const float StreamFps = 50f;            // 초안 프레임 40ms (1초). 사용자 2026-11-22: 재생 시간 절반 (25 → 50fps = 0.5초)
    const float StreamCellPx = 768f;
    const float StreamStartPx = 118f;       // 출발점 = 1번 프레임 점화 자리 가운데 (알파 범위 86~151px)
    const float StreamEndPx = 733f;         // 도착점 = 다 자랐을 때 오른쪽 끝 (알파 범위 최대 733px)
    const float StreamGroundBelowCenterPx = 55f;   // 지면선 (기준점 y 311) = 칸 가운데(256)에서 아래로 55px
    const int StreamRearOrder = 9, StreamFrontOrder = 11;   // 플레이어 = 10
    const float StreamDrop = 1.5f;          // 좌우 돌진 때 이펙트를 월드 아래로 내리는 거리 (사용자 2026-11-22: 2 → 0.5 더 높여 1.5)
    const float StreamMidAboveGroundPx = 87.5f;   // 측정: 불꽃 그림 세로 범위 110~337px, 지면선 311px → 가운데가 지면선 위 87.5px
    const float DistanceMul = 2.5f;         // 돌진 거리 배율 (사용자 2026-11-22: 2.5배)
    const float StreamThickness = 3f;       // 불꽃 굵기 배율 (사용자 2026-11-22: 3배)

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
        // 앞에서부터 뒤로 나옴 (사용자 2026-11-22): 그림은 점화 자리에서 오른쪽으로 자라므로, 점화 자리를 돌진 도착점에 두고 돌진 반대 방향으로 자라게 함
        Vector2 grow = -dir;
        // 높이 보정은 좌우로 돌진할 때만 (위 · 아래로 돌진할 때 아래로 내리면 경로에서 벗어나 멀리 떨어져 보임, 사용자 2026-11-22)
        float horiz = Mathf.Abs(grow.x), vert = Mathf.Abs(grow.y);
        root.position = from + dir * distance + Vector2.down * (StreamDrop * horiz);
        root.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(grow.y, grow.x) * Mathf.Rad2Deg);
        // 크기 3배 (사용자 2026-11-22): 길이 = 돌진 거리 그대로, 굵기 (불꽃 높이) 만 3배. 지면선이 루트 원점이라 땅에 붙은 채 위로 커짐
        float side = grow.x < -0.01f ? -1f : 1f;   // 자라는 방향이 왼쪽이면 세로 반전 (불꽃이 계속 위로)
        root.localScale = new Vector3(1f, side * StreamThickness, 1f);
        // 위 · 아래로 돌진하면 불꽃이 옆으로 서서 경로 한쪽에 치우침 → 그림의 가운데 (지면선 위 StreamMidAboveGroundPx) 가 경로에 오도록 보정
        Vector2 local = new Vector2((StreamCellPx * 0.5f - StreamStartPx) * pxWorld, (StreamGroundBelowCenterPx - StreamMidAboveGroundPx * vert) * pxWorld);
        SpriteEffect.Spawn(rear, StreamFps, root, local, 0f, false, StreamRearOrder);
        SpriteEffect.Spawn(front, StreamFps, root, local, 0f, false, StreamFrontOrder);
        Destroy(root.gameObject, StreamRows * StreamCols / StreamFps + 0.2f);   // 두 장이 끝난 뒤 빈 루트 정리
        return true;
    }
    float StackCooldown => CooldownOf(BaseCooldown);
    float Distance => Reach(Tier.Short) * DistanceMul * (1f + (HasSpecial(Sprint) ? SprintRange : 0f));

    // ---------- 특수 강화 선택지 ----------
    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Continuous, title = "연속 돌진", desc = "최대 스택 +1, 쿨타임 +20%" },
                new SpecialUpgradeOption { id = Trail, title = "불타는 궤적", keywords = new[] { "간접 사용", "지속 피해", "고정 피해" }, desc = "지나간 경로에 화염 장판이 생성됩니다.",
                    specs = new[] { "'파이어 스트림' '총 피해량'의 200% '고정 피해'", "화상 상태 적에게 '최종 피해량' +100%" } },
                new SpecialUpgradeOption { id = Veil, title = "화염 장막", desc = "종료 시 2초간 무적이 유지됩니다." },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Sprint, title = "고속 질주", desc = "사거리 +50%, 쿨타임 감소 +25%" },
                new SpecialUpgradeOption { id = Reflux, title = "화염 환류", desc = "적중 시 남은 쿨타임 감소 2.5%" },
                new SpecialUpgradeOption { id = Pierce, title = "꿰뚫는 화염", desc = "종료 시 진행 방향으로 한 번 더 사용합니다.", specs = new[] { "추가 사용 '최종 피해량' +200%" } },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Lance, title = "화염의 창", desc = "누르고 있으면 1초간 대기합니다. 대기 중 피격당하거나 1초가 지나면 돌진합니다.\n피격 시 남은 쿨타임 감소 및 최종 피해량 증가.",
                    specs = new[] { "피격 시 '최종 피해량' +400%", "피격 시 남은 쿨타임 감소 20%" } },
                new SpecialUpgradeOption { id = Backdraft, title = "후폭풍", desc = "종료 시 지나온 경로에 한 번 더 사용합니다.", specs = new[] { "추가 사용 '최종 피해량' +100%" } },
                new SpecialUpgradeOption { id = Endless, title = "무한 기동", desc = "최대 스택 +2, 쿨타임 감소 +50%, 최종 피해량 -75%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboIlseom, title = "화염 일섬", recipe = "꿰뚫는 화염 + 화염의 창", requires = new[] { Pierce, Lance },
            desc = "쿨타임 감소 · 최대 스택 능력치가 무효화되고, 무효화한 만큼 최종 피해량이 증가합니다.",
            specs = new[] { "'최종 피해량' +1000%", "쿨타임 감소 능력치 무효화 1% 당 '최종 피해량' +5%", "최대 스택 능력치 무효화 1당 '최종 피해량' +150%" },
        },
    };

    bool Ilseom => HasCombo(ComboIlseom);
    /// <summary>특수 강화로 늘어난 최대 스택 (화염 일섬이 무효화하는 양) — [해석] 기본 3 스택은 그대로 (파이어 차지 화염 질주와 같은 해석)</summary>
    int BonusStacks => (HasSpecial(Continuous) ? ContinuousStacks : 0) + (HasSpecial(Endless) ? EndlessStacks : 0);
    int MaxStacks => BaseMaxStacks + (Ilseom ? 0 : BonusStacks);

    /// <summary>특수 강화의 쿨타임 '감소' 배율만 (고속 질주 · 무한 기동). 화염 일섬이 무효화</summary>
    float SpecialReductionMul => (HasSpecial(Sprint) ? SprintCooldown : 1f) * (HasSpecial(Endless) ? EndlessCooldown : 1f);
    /// <summary>특수 강화의 쿨타임 '증가' 배율 (연속 돌진). 무효화 대상 아님</summary>
    float SpecialIncreaseMul => HasSpecial(Continuous) ? ContinuousCooldown : 1f;
    protected override float SpecialCooldownMul => SpecialReductionMul * SpecialIncreaseMul;

    /// <summary>
    /// 화염 일섬이 무효화한 쿨타임 감소율 (0.3 = 30%): 기본 강화 · 전역 · 플레이어 · 고속 질주 · 무한 기동. 90% 상한 그대로.
    /// [해석] 츠바메가에시와 같이 '감소' 만 무효화 — 연속 돌진의 쿨타임 +20% 는 그대로 곱함
    /// </summary>
    float IlseomConvertedReduction
    {
        get
        {
            float m = Mathf.Max(1f - T.cooldownReductionCap, BasicCooldownMul * SpecialReductionMul * CombatMods.CooldownMul(this) * P.cooldownMul);
            return Mathf.Round(Mathf.Max(0f, 1f - m) * 10000f) / 10000f;
        }
    }

    protected override float CooldownMultiplier => Ilseom ? SpecialIncreaseMul : GeneralCooldownMultiplier;
    /// <summary>무한 기동 ×0.25 · 화염 일섬 (1 + 10 + 전환 쿨감 × 5 + 무효화 스택 × 1.5) 를 곱한 최종 배율</summary>
    protected override float SpecialFinalDamageMul =>
        (HasSpecial(Endless) ? EndlessFinal : 1f)
        * (Ilseom ? 1f + IlseomFinal + IlseomConvertedReduction * IlseomPerReduction + BonusStacks * IlseomPerStack : 1f);

    protected override void OnSpecialUpgrade(int lv, string id)
    {
        if (id == Continuous || id == Endless) stacks = Mathf.Min(MaxStacks, stacks + (id == Continuous ? ContinuousStacks : EndlessStacks));   // 늘어난 만큼 바로 채움
    }
    protected override void OnComboChanged(string id, bool on) { stacks = Mathf.Min(stacks, MaxStacks); }

    public override string Badge => stacks.ToString();
    public override float CooldownFraction =>
        stacks >= MaxStacks ? 0f : stacks > 0 ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));
    public override float CooldownRemaining => stacks > 0 ? 0f : Mathf.Max(0f, recharge);
    /// <summary>다음 스택 회복 진행도 (HUD)</summary>
    public float RechargeFraction => stacks >= MaxStacks ? 0f : Mathf.Clamp01(recharge / Mathf.Max(0.01f, StackCooldown));

    /// <summary>남은 쿨타임 감소 (불길의 순환 · 화염 환류 · 화염의 창): 지금 충전 중인 스택 기준.
    /// [해석] 화염 일섬은 '쿨타임 감소 능력치' 만 무효화 — 남은 쿨타임 감소는 그대로 받음 (조합 재료인 화염의 창 효과라서)</summary>
    public override void ReduceRemainingCooldown(float ratio)
    {
        if (stacks < MaxStacks && recharge > 0f) recharge *= Mathf.Clamp01(1f - ratio);
    }

    public override void Tick(float dt)
    {
        if (DebugCheats.NoCooldown) { stacks = MaxStacks; recharge = 0f; return; }
        if (stacks >= MaxStacks) { stacks = MaxStacks; recharge = 0f; return; }
        if (recharge <= 0f) recharge = StackCooldown;
        recharge -= dt;
        if (recharge <= 0f) { stacks++; recharge = stacks < MaxStacks ? StackCooldown : 0f; }
    }

    public override void DebugResetForCast() { if (stacks <= 0) stacks = 1; }   // 디버그 맵 버튼: 스택이 없어도 사용

    bool running;   // 화염의 창 대기 ~ 추가 사용까지 (중복 사용 방지)

    public override bool TryActivate()
    {
        if (CheatDisabled) return false;   // 치트: 스킬별 사용 금지
        if (running || stacks <= 0 || P.IsBusy || P.IsStunned) return false;
        if (stacks >= MaxStacks) recharge = StackCooldown;
        stacks--;
        BeginCast();
        StartCoroutine(Run());
        return true;
    }

    void OnDisable()
    {
        if (running && P != null) P.HitCounter = null;
        running = false;
    }

    IEnumerator Run()
    {
        running = true;
        var cast = DirectCast;
        float useFinal = 1f;

        // 화염의 창: 이동 키를 누르고 있는 동안 최대 1초 대기 (방향 전환만 가능). 대기 중 피격 → 즉시 돌진 + 최종 피해량 +400% · 남은 쿨타임 -20%
        // [해석] 키를 일찍 떼면 그때 바로 돌진. 디버그 맵 버튼처럼 누르고 있지 않으면 대기 없이 돌진
        if (HasSpecial(Lance) && Input.GetKey(T.movementKey))
        {
            bool countered = false;
            P.Root(LanceWaitTime);
            P.HitCounter = () => { countered = true; return LanceBlocksHit; };
            var mark = Fx.Circle(P.transform.position, HitRadius * 2f, Fx.FireBright, LanceWaitTime, P.transform, 0f);   // [임시] 대기 표시
            for (float t = 0f; t < LanceWaitTime && !countered && Input.GetKey(T.movementKey); t += Time.deltaTime) yield return null;
            P.HitCounter = null;
            P.EndRoot();
            if (mark != null) Destroy(mark.gameObject);
            if (countered)
            {
                useFinal = LanceFinal;
                ReduceRemainingCooldown(LanceCooldownCut);
                CameraFollow.Shake(0.1f, 0.12f);
            }
        }

        Vector2 dir = P.Facing.sqrMagnitude > 0.01f ? P.Facing.normalized : Vector2.right;
        Vector2 start = P.transform.position;
        float distance = Distance;
        bool veil = HasSpecial(Veil);

        yield return DashOnce(dir, distance, cast, useFinal);

        // 꿰뚫는 화염: 끝난 자리에서 같은 방향으로 한 번 더 (간접 사용, 스택 소모 없음)
        if (HasSpecial(Pierce) && P != null && !P.IsStunned)
            yield return DashOnce(dir, distance, cast.AsIndirect, useFinal * PierceFinal);

        Vector2 end = P.transform.position;
        float total = FinalDamageOf(BaseDamage) * useFinal;   // [해석] 파이어 스트림 총 피해량 = 돌진 1회의 최종 피해량 (적중 1회분)

        if (veil) P.SetInvulnerable(VeilInvuln);   // 화염 장막: 종료 시 2초 무적
        if (HasSpecial(Trail)) StartCoroutine(TrailField(start, end, HitRadius, total, cast.AsIndirect));
        if (HasSpecial(Backdraft))
        {
            yield return new WaitForSeconds(BackdraftDelay);
            Backdraft_Sweep(end, start, cast.AsIndirect, useFinal * BackdraftFinal);
        }
        running = false;
    }

    /// <summary>돌진 1회: 경로의 적에게 한 번씩 피해 (화염 환류: 적중마다 남은 쿨타임 -2.5%)</summary>
    IEnumerator DashOnce(Vector2 dir, float distance, CastRef cast, float extraFinal)
    {
        hit.Clear();
        DamageInfo dmg = MakeDamage(BaseDamage, cast, 0f, extraFinal);
        bool drawn = SpawnStream(P.transform.position, dir, distance);
        if (!drawn) Fx.Circle(P.transform.position, HitRadius, Fx.FireBright, 0.2f);   // 그림이 없을 때만
        bool done = false;
        P.Dash(dir, distance, DashTime, true,
            onStep: () =>
            {
                Vector2 at = P.transform.position;
                if (!drawn) Fx.Disc(at, HitRadius * 0.6f, Fx.Fire, 0.18f, 0f);
                foreach (var e in SkillHits.InCircle(at, HitRadius, found))
                {
                    if (!hit.Add(e)) continue;
                    e.Hit(dmg, (Vector2)e.transform.position - at, 1f);
                    if (HasSpecial(Reflux)) ReduceRemainingCooldown(RefluxPerHit);
                }
            },
            onEnd: () => done = true);
        // 돌진이 끊겨 (보스 잡기 등) onEnd 가 오지 않아도 멈추지 않게 시간으로도 끝냄
        for (float t = 0f; !done && t < DashTime + 0.3f; t += Time.deltaTime) yield return null;
    }

    /// <summary>후폭풍: 지나온 경로 (도착 → 출발) 를 한 번 훑음. 플레이어는 움직이지 않음 [해석]</summary>
    void Backdraft_Sweep(Vector2 from, Vector2 to, CastRef cast, float extraFinal)
    {
        Vector2 d = to - from;
        float len = d.magnitude;
        if (len < 0.01f) return;
        DamageInfo dmg = MakeDamage(BaseDamage, cast, 0f, extraFinal);
        if (!SpawnStream(from, d / len, len)) Fx.Box(from, d / len, len, HitRadius * 2f, Fx.FireBright, 0.25f);
        foreach (var e in NearSegment(from, to, HitRadius))
        {
            e.Hit(dmg, d, 1f);
            if (HasSpecial(Reflux)) ReduceRemainingCooldown(RefluxPerHit);
        }
        HitDebug.Circle(from + d * 0.5f, len * 0.5f, HitDebug.BlastColor);
    }

    /// <summary>선분 a-b 에서 halfWidth 안의 살아 있는 적 (파이어 차지와 같은 계산)</summary>
    static List<Enemy> NearSegment(Vector2 a, Vector2 b, float halfWidth)
    {
        var all = Enemy.CopyAll(new List<Enemy>());
        var res = new List<Enemy>();
        Vector2 ab = b - a;
        float len2 = ab.sqrMagnitude;
        foreach (var e in all)
        {
            if (e == null || e.hp <= 0) continue;
            Vector2 p = e.transform.position;
            float t = len2 > 0.0001f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2) : 0f;
            float rr = halfWidth + e.radius;
            if ((p - (a + ab * t)).sqrMagnitude < rr * rr) res.Add(e);
        }
        return res;
    }

    DamageInfo FixedOf(float amount, CastRef cast)
    {
        var d = FixedDamage(amount, DamageType.Direct, CastKind.Indirect);
        d.castId = cast.id;
        return d;
    }

    /// <summary>불타는 궤적: 지나간 경로에 장판 · 0.5초마다 고정 피해 (합계 = 총 피해량의 200%, 화상 상태 적은 ×2) — 노션 키워드 '지속 피해'</summary>
    IEnumerator TrailField(Vector2 a, Vector2 b, float halfWidth, float total, CastRef cast)
    {
        int ticks = Mathf.Max(1, Mathf.RoundToInt(TrailTime / TrailTick));
        float per = total * TrailRatio / ticks;
        Vector2 d = b - a;
        float len = d.magnitude;
        if (len > 0.01f) Fx.Box(a, d / len, len, halfWidth * 2f, Fx.FireDeep, TrailTime);
        for (int i = 0; i < ticks; i++)
        {
            foreach (var e in NearSegment(a, b, halfWidth))
            {
                var dmg = FixedOf(per * (FlameSword.IsBurning(e) ? 1f + BurnFinal : 1f), cast);
                dmg.type = DamageType.DoT;
                e.Hit(dmg, d, Enemy.NoKnock);
            }
            yield return new WaitForSeconds(TrailTick);
        }
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
    const string TornadoRibbonSheet = "ErnStray_OverFlameBlade_One";   // 2026-11-22: 화염검 시트를 한 장 (_One) 으로 줄여서 이 시험판도 그 한 장을 씀
    const int TornadoRibbonRows = 1, TornadoRibbonCols = 1;
    const float TornadoCellW = 256f, TornadoCellH = 768f;
    const float TornadoRibbonX = 88f, TornadoRibbonW = 74f;    // 칼 알파 범위 x 88~162
    const float TornadoRibbonTop = 54f, TornadoRibbonH = 674f; // 칼 알파 범위 y 54~728 (위 기준)

    static RibbonTornadoFx.Source LoopSource(string sheet, float weight, bool single = false)
    {
        var tex = SpriteLib.Load(sheet);
        if (tex == null) return null;
        int cols = single ? 1 : 4, rows = single ? 1 : 3;   // single = 칸 한 장짜리 그림
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
        var tex = SpriteLib.Load(sheet);
        if (tex == null) return null;
        var frames = new[] { new Rect(x0 / tex.width, 0f, (x1 - x0) / tex.width, 1f) };
        // 칸 세로가 288 이 아니라 192 라서 같은 굵기로 보이게 두께를 맞춤
        return new RibbonTornadoFx.Source { texture = tex, frames = frames, lengthAlongY = false, weight = weight, thickMul = StripThick * StillH / LoopCellH };
    }

    static RibbonTornadoFx.Source SwordSource()
    {
        var tex = SpriteLib.Load(TornadoRibbonSheet);
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
        Add(LoopSource("ErnStray_StormHook_One", 1f, true));   // 한 장 (프레임 4, 2026-11-22 용량 절약, 원본 12프레임은 Art/Incoming/Retired/FxSheets)
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
        var tex = SpriteLib.Load("ErnStray_StormStillCurl");
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
