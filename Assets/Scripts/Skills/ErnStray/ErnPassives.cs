using System.Collections.Generic;
using UnityEngine;

// 에른 스트레이 패시브 (전투 통합 기획서 v1.0 7~10장 + 결정 기록 v1.0 6장)
// 패시브는 공통 기본 강화 대신 고유 강화 3종 (Skill.PassiveBasicOptions)

/// <summary>
/// P01 화염의 검: 직접 피해마다 화상 1스택. 화상 = '지속 피해' · 태그 '화상' · 화염의 검 소유의 독립 피해
/// (화상을 건 스킬의 강화는 받지 않음).
///
/// 화상 1틱 = 초기 피해량 (초당 피해 × 스택) → 피해량 (기름붓기 · 핵융합 · 영원의 불꽃 · 화염 낙인 · 전역 '화상'/'지속 피해')
///          → 최종 피해량 (전역: 옥염 등) → 대상 단계 (받는 최종 피해량: 융해) → 내림.
/// 부여 · 갱신: 재부여 시 모든 스택 지속시간 갱신, 피해 간격 타이머는 초기화하지 않음. 첫 틱 = 부여 후 피해 간격.
/// 종료: 지속시간이 끝나면 모든 스택 삭제 (화염 낙인도 함께).
/// 재귀 방지: 화상·폭탄·일촉즉발은 '지속 피해' → 화상 부여 · 살아있는 폭탄 · 화염 낙인(직접 피해 트리거)을 다시 발동하지 않음.
/// </summary>
public class FlameSword : Skill
{
    /// <summary>보유 중인 화염의 검 (없으면 null). 다른 스킬이 화상 상태를 조회할 때 사용</summary>
    public static FlameSword I { get; private set; }

    // 기본 수치 (기획 v1.0 확정)
    const float BaseDps = 2f;           // 화상 초당 초기 피해량 (스택당)
    const float BaseDuration = 3f;      // 화상 지속시간
    const int BaseMaxStacks = 5;
    const float BaseInterval = 1f;      // 지속 피해 기본 피해 간격

    // 고유 기본 강화
    const float UpDps = 2f;             // 화상 피해: 초기 피해량 +2
    const int UpStacks = 1;             // 화상 최대 스택 +1
    const float UpDuration = 1f;        // 화상 지속시간 +1초

    // 특수 강화 id
    public const string Melt = "B5A", Oil = "B5B", Spread = "B5C";
    public const string Detonate = "B10A", Bomb = "B10B", Sticky = "B10C";
    public const string Eternal = "B15A", Fusion = "B15B", Brand = "B15C";

    // 특수 강화 확정 수치
    const float MeltPerStack = 0.02f;   // 융해: 화상 스택당 받는 최종 피해량 +2%
    const float OilDamage = -0.25f;     // 기름붓기: 화상 피해량 -25%
    const float OilDuration = 1f;       //           화상 지속시간 +100%
    const int SpreadStacks = 10;        // 확산되는 불길: 최대 스택 +10
    const float StickyMul = 0.5f;       // 끈적한 화염: 이동속도 50% 감소 (곱연산)
    const float EternalPerSec = 0.5f;   // 영원의 불꽃: 지속시간 1초당 화상 피해량 +50%
    const int EternalStacks = 10;       //               최대 스택 +10
    const float FusionDuration = 3f;    // 핵융합: 지속시간 3초로
    const float FusionPerSec = 1f;      //         줄어든 1초당 화상 피해량 +100%
    const float BrandPerStack = 0.1f;   // 화염 낙인: 낙인 1스택당 그 적의 화상 피해량 +10%
    const int BrandMax = 30;

    int dpsUps, stackUps, durUps;

    class Burn
    {
        public Enemy e;
        public int stacks;
        public float left;      // 남은 지속시간 (영원의 불꽃이면 사용 안 함)
        public float tickIn;    // 다음 틱까지
        public int brand;       // 화염 낙인 스택 (화상과 별도)
        public bool slowed;     // 끈적한 화염 감속을 걸었는지
    }

    readonly Dictionary<Enemy, Burn> burns = new Dictionary<Enemy, Burn>();
    readonly List<Burn> iter = new List<Burn>();
    readonly List<Vector2> spreadQueue = new List<Vector2>();
    readonly List<Enemy> around = new List<Enemy>();
    MeltModifier melt;

    // ---------- 수치 ----------
    float Dps => BaseDps + UpDps * dpsUps;
    public int MaxStacks => BaseMaxStacks + UpStacks * stackUps
                          + (HasSpecial(Spread) ? SpreadStacks : 0) + (HasSpecial(Eternal) ? EternalStacks : 0);
    /// <summary>지속시간 스탯 = (기본 + 고정 증가) × (1 + % 증가 합) — 결정 기록 E6</summary>
    public float DurationStat => (BaseDuration + UpDuration * durUps) * (1f + (HasSpecial(Oil) ? OilDuration : 0f));
    /// <summary>실제 지속시간 (핵융합이면 3초)</summary>
    float Duration => HasSpecial(Fusion) ? Mathf.Min(DurationStat, FusionDuration) : DurationStat;
    bool Permanent => HasSpecial(Eternal);
    /// <summary>피해 간격 (가속 연소 등 전역 감소 반영, 최소 0.1초)</summary>
    public float Interval => CombatMods.DotInterval(BaseInterval);

    /// <summary>화염의 검 화상 '피해량' % (대상 무관 부분)</summary>
    float BurnPct =>
        (HasSpecial(Oil) ? OilDamage : 0f)
        + (HasSpecial(Fusion) ? Mathf.Max(0f, DurationStat - FusionDuration) * FusionPerSec : 0f)
        + (HasSpecial(Eternal) ? DurationStat * EternalPerSec : 0f);   // 영원의 불꽃: 실시간 (이후 지속시간 증가 반영)

    // ---------- 조회 (다른 스킬·패시브용) ----------
    /// <summary>이 적의 화염의 검 화상 스택 (없으면 0)</summary>
    public static int StacksOn(Enemy e) =>
        I != null && e != null && I.burns.TryGetValue(e, out var b) ? b.stacks : 0;
    public static bool IsBurning(Enemy e) => StacksOn(e) > 0;

    /// <summary>페이즈 변경 등 명시적인 상태 초기화. 처치·확산 이벤트는 발생시키지 않음.</summary>
    public static void RemoveBurn(Enemy e)
    {
        if (I != null && e != null && I.burns.TryGetValue(e, out var burn)) I.Clear(burn, true);
    }

    /// <summary>
    /// 강제 발화용: 이 적의 화상이 **전체 지속시간** 동안 줄 총 피해 = 틱 수 × 내림한 1틱 피해 (실제 화상과 같은 내림, 결정 2026-11-10).
    /// 1틱 = 공격자 단계까지 (화상 강화 · 옥염 반영, 받는 최종 피해량은 고정 피해라 미반영).
    /// 전체 지속시간 = 실제 지속시간 (핵융합 3초), 영원의 불꽃이면 지속시간 스탯 (무한대 취급 금지)
    /// </summary>
    public static float TotalBurnDamage(Enemy e)
    {
        if (I == null || e == null || !I.burns.TryGetValue(e, out var b) || b.stacks <= 0) return 0f;
        float full = I.Permanent ? I.DurationStat : I.Duration;
        int ticks = Mathf.FloorToInt(full / I.Interval + 1e-4f);
        int perTick = Mathf.FloorToInt(I.TickDamage(b).amount + 1e-4f);
        return (float)ticks * perTick;
    }

    /// <summary>화상 1틱 피해 (공격자 단계, 내림 전) — 살아있는 폭탄 · 일촉즉발 · 강제 발화가 참조</summary>
    DamageInfo TickDamage(Burn b, float scale = 1f)
    {
        var d = new DamageInfo(0f, DamageType.DoT, this, CastKind.Indirect, tag: DamageInfo.BurnTag);
        float pct = BurnPct + (HasSpecial(Brand) ? b.brand * BrandPerStack : 0f);
        d.amount = DamageMath.Attacker(d, Dps * b.stacks * scale, pct, 0f, 1f);
        return d;
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption { title = $"화상 피해  <size=16>초당 초기 피해량 +{UpDps:0}</size>", desc = $"스택당 초당 {Dps:0} → {Dps + UpDps:0}" },
        new PassiveUpgradeOption { title = $"화상 최대 스택  <size=16>+{UpStacks}</size>", desc = $"최대 {MaxStacks} → {MaxStacks + UpStacks}스택" },
        new PassiveUpgradeOption { title = $"화상 지속시간  <size=16>+{UpDuration:0}초</size>", desc = $"지속시간 스탯 {DurationStat:0.#}초 → {(BaseDuration + UpDuration * (durUps + 1)) * (1f + (HasSpecial(Oil) ? OilDuration : 0f)):0.#}초" },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: dpsUps++; break;
            case 1: stackUps++; break;
            case 2: durUps++; break;
        }
    }

    public override string BasicSummary => $"초당 {Dps:0} · 최대 {MaxStacks}스택 · {DurationStat:0.#}초";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Melt, title = "융해", desc = "화상 스택당 대상이 받는 최종 피해량 +2%" },
                new SpecialUpgradeOption { id = Oil, title = "기름붓기", desc = "모든 화상 피해량 -25% · 모든 화상 지속시간 +100%" },
                new SpecialUpgradeOption { id = Spread, title = "확산되는 불길", desc = "화상 상태의 적이 죽으면 주변 적에게 화상 1스택 · 최대 스택 +10" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Detonate, title = "일촉즉발", desc = "화상이 최대 스택에 도달하면 모든 스택을 소모하고 남은 화상 피해를 즉시 적용" },
                new SpecialUpgradeOption { id = Bomb, title = "살아있는 폭탄", desc = "화상 상태의 적이 직접 피해를 받을 때마다 주변에 화상 초당 피해만큼 폭발 (지속 피해)" },
                new SpecialUpgradeOption { id = Sticky, title = "끈적한 화염", desc = "화상 상태의 적 이동속도 50% 감소 (다른 감속과 곱연산)" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Eternal, title = "영원의 불꽃", desc = "화상이 영구 지속 · 지속시간 1초당 화상 피해량 +50% · 최대 스택 +10" },
                new SpecialUpgradeOption { id = Fusion, title = "핵융합", desc = "화상 지속시간 3초로 감소 · 줄어든 1초당 모든 화상 피해량 +100%" },
                new SpecialUpgradeOption { id = Brand, title = "화염 낙인", desc = "화상 상태의 적에게 직접 피해마다 그 적의 화상 피해량 +10% (최대 30스택, 화상이 꺼지면 사라짐)" },
            };
        }
        return base.SpecialOptions(lv);
    }

    protected override void OnSpecialUpgrade(int lv, string id)
    {
        if (id == Melt && melt == null) { melt = new MeltModifier(); CombatMods.Add(melt); }
        if (id == Eternal) foreach (var b in burns.Values) ShowFx(b);   // 이미 붙은 화상도 영구 표시로
    }

    /// <summary>융해: 화상 스택당 '받는 최종 피해량' +2% (대상 단계, 고정 피해 제외)</summary>
    class MeltModifier : CombatModifier
    {
        public override float ReceivedMul(Enemy target, in DamageInfo d) => 1f + MeltPerStack * StacksOn(target);
    }

    // ---------- 수명 ----------
    void OnEnable()
    {
        I = this;
        Enemy.Damaged += OnDamaged;
        Enemy.Killed += OnKilled;
    }

    void OnDisable()
    {
        if (I == this) I = null;
        Enemy.Damaged -= OnDamaged;
        Enemy.Killed -= OnKilled;
        if (melt != null) { CombatMods.Remove(melt); melt = null; }
        foreach (var b in burns.Values) Clear(b, removeFromMap: false);
        burns.Clear();
        spreadQueue.Clear();
    }

    // ---------- 트리거 ----------
    /// <summary>피해 전달 직후: 직접 피해(또는 화상 예외)면 낙인 · 폭탄 → 화상 1스택</summary>
    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        bool direct = d.type == DamageType.Direct;
        if (!direct && !d.canBurn) return;   // 지속·아이템 피해로는 화상 누적 불가 (타오름만 예외)

        if (direct && burns.TryGetValue(e, out var b) && b.stacks > 0)
        {
            if (HasSpecial(Brand)) b.brand = Mathf.Min(BrandMax, b.brand + 1);
            if (HasSpecial(Bomb)) Explode(e, b);   // 이번 피해로 죽었어도 폭발
        }
        if (e.hp > 0) AddStacks(e, 1);
    }

    /// <summary>확산되는 불길: 화상 상태로 죽은 적 → 다음 Tick 에 주변 1스택 (연쇄가 한 프레임 재귀로 쌓이지 않게)</summary>
    void OnKilled(Enemy e)
    {
        if (!burns.TryGetValue(e, out var b)) return;
        if (HasSpecial(Spread) && b.stacks > 0) spreadQueue.Add(e.transform.position);
        Clear(b, removeFromMap: true);
    }

    /// <summary>화상 n스택 부여 (최대 스택까지) + 모든 스택 지속시간 갱신. 일촉즉발이면 최대 도달 시 즉시 폭발</summary>
    public void AddStacks(Enemy e, int n)
    {
        if (e == null || e.hp <= 0 || n <= 0) return;
        if (!burns.TryGetValue(e, out var b))
        {
            b = new Burn { e = e, tickIn = Interval };   // 첫 틱 = 부여 후 피해 간격
            burns[e] = b;
        }
        int max = MaxStacks;
        b.stacks = Mathf.Min(max, b.stacks + n);
        b.left = Duration;                              // 갱신: 피해 간격 타이머는 그대로
        ShowFx(b);
        if (HasSpecial(Detonate) && b.stacks >= max) DetonateNow(b);
    }

    /// <summary>일촉즉발: 남은 화상 피해 (남은 틱 수 × 틱 피해)를 지속 피해로 즉시, 모든 스택 소모</summary>
    void DetonateNow(Burn b)
    {
        // [해석] 영원의 불꽃이면 남은 시간 = 지속시간 스탯 (무한대 취급 금지)
        float remain = Permanent ? DurationStat : Mathf.Max(0f, b.left);
        var dmg = TickDamage(b, remain / Interval);
        var e = b.e;
        Clear(b, removeFromMap: true);
        e.Hit(dmg, Vector2.zero, Enemy.NoKnock);
    }

    /// <summary>살아있는 폭탄: 그 적 위치에서 범위 폭발 = 그 적의 화상 1틱 피해 ('화상' 지속 피해)</summary>
    void Explode(Enemy e, Burn b)
    {
        Vector2 at = e.transform.position;
        float r = T.flameBombRadius;
        Fx.Disc(at, r, Fx.Fire, 0.2f);
        HitDebug.Circle(at, r, HitDebug.BlastColor);
        SkillHits.Circle(at, r, TickDamage(b), Enemy.NoKnock);
    }

    void ShowFx(Burn b)
    {
        if (b.e != null) BurnFx.Show(b.e.transform, () => b.e != null ? b.e.radius : 0.3f, Permanent ? 9999f : b.left);
    }

    void Clear(Burn b, bool removeFromMap)
    {
        if (b.e != null)
        {
            BurnFx.Stop(b.e.transform);
            if (b.slowed) b.e.statusSpeedMul /= StickyMul;   // 끈적한 화염이 건 몫만 되돌림
        }
        b.slowed = false;
        b.stacks = 0;
        b.brand = 0;
        // 파괴된 적도 키(참조)로 제거 — Unity 의 == null 비교를 거치지 않도록 object 로.
        // 같은 적에게 새로 만든 화상(일촉즉발 직후 재부여 등)은 지우지 않음
        if (removeFromMap && !ReferenceEquals(b.e, null) && burns.TryGetValue(b.e, out var cur) && cur == b) burns.Remove(b.e);
    }

    // ---------- 진행 (Playing 상태에서만 불림) ----------
    public override void Tick(float dt)
    {
        // 확산되는 불길 (지난 프레임의 사망분)
        if (spreadQueue.Count > 0)
        {
            var points = spreadQueue.ToArray();
            spreadQueue.Clear();
            foreach (var at in points)
                foreach (var o in SkillHits.InCircle(at, T.flameSpreadRadius, around)) AddStacks(o, 1);
        }

        iter.Clear();
        iter.AddRange(burns.Values);
        float interval = Interval;
        bool sticky = HasSpecial(Sticky);
        foreach (var b in iter)
        {
            var e = b.e;
            if (e == null || e.hp <= 0 || b.stacks <= 0) { Clear(b, removeFromMap: true); continue; }
            // 끈적한 화염: 다른 상태 효과와 곱연산 → 자기 몫만 곱하고 끝날 때 나눔
            if (sticky && !e.ControlImmune && !b.slowed) { e.statusSpeedMul *= StickyMul; b.slowed = true; }

            if (!Permanent) b.left -= dt;
            if (b.tickIn > interval) b.tickIn = interval;   // 간격이 줄면(가속 연소) 예약된 다음 틱도 새 간격 안으로
            b.tickIn -= dt;
            // 지속시간 끝과 틱이 겹치면 틱 먼저 (3초 · 1초 간격 = 3틱)
            while (b.tickIn <= 1e-4f && b.stacks > 0 && e != null && e.hp > 0)
            {
                e.Hit(TickDamage(b), Vector2.zero, Enemy.NoKnock);
                b.tickIn += interval;
            }
            if (e == null || e.hp <= 0) continue;   // 사망 처리는 OnKilled 에서
            if (!Permanent && b.left <= 1e-4f) Clear(b, removeFromMap: true);
        }
    }
}
