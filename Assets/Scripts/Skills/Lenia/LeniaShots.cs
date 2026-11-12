using System;
using System.Collections.Generic;
using UnityEngine;

// 레니아 투사체 공통 규칙 (2026-11-12 사용자 수정 사항)
// - '투사체 증가': 추가 투사체는 0.2초 간격으로 차례로 발사. 같은 사용 안에서는 이미 노린 대상을 다시 노리지 않음 (범위 안에 다른 대상이 없으면 허용)
// - '도탄': 적중한 뒤 주변 가까운 적(BounceRadius)에게 같은 투사체를 다시 발사, 도탄 수만큼 반복.
//           지금 맞은 대상은 노리지 않지만 그 이전 대상은 다시 노릴 수 있음 (적 2마리면 도탄 수만큼 왕복)
// - '도탄' · '투사체 증가'로 생긴 추가 피해 = '간접 사용' · '직접 피해' (같은 사용 번호)
// - "같은 피해" = 원래 피해의 '최종 피해량'만큼 '고정 피해' (부메랑 귀환 등)
// - 적중 시 처리 순서: 대상 상태 확인(기절 여부) → 조건부 피해 증가(DamageInfo.perTarget) → 피해 → 상태이상 부여 (onHit, 가장 늦게)
// - '특수 피해'는 적중 시 효과를 발동시키지 않음 (onHit 은 투사체 적중에서만 호출)

/// <summary>투사체 1발에 붙는 강화 (관통 · 도탄 · 부메랑 · 적중 시 효과)</summary>
public class ShotMods
{
    /// <summary>관통: 추가로 지나갈 수 있는 적 수</summary>
    public int pierce;
    /// <summary>도탄: 남은 도탄 횟수</summary>
    public int bounces;
    /// <summary>도탄 대상 탐색 반경 (기본 강화 '범위' 반영 값을 스킬이 넣음)</summary>
    public float bounceRange = LeniaShots.BounceRadius;
    /// <summary>부메랑: 사거리 끝(또는 마지막 적중)에서 플레이어에게 돌아오며 다시 피해</summary>
    public bool boomerang;
    /// <summary>적중 후 호출: 맞은 적 · 이 피해 전에 기절 상태였는지 · 적용된 피해. 상태이상 부여는 여기서</summary>
    public Action<Enemy, bool, DamageInfo> onHit;

    public ShotMods Clone() => (ShotMods)MemberwiseClone();
}

public static class LeniaShots
{
    /// <summary>'투사체 증가' 추가 투사체 발사 간격</summary>
    public const float Interval = 0.2f;
    /// <summary>도탄 대상 탐색 반경 (수치 지정용, 화면에는 표기하지 않음)</summary>
    public const float BounceRadius = 3f;

    /// <summary>
    /// 같은 사용 안의 다음 대상: 아직 노리지 않은 가장 가까운 적, 없으면 이미 노린 적 중 가장 가까운 적.
    /// 고른 대상은 used 에 추가됨
    /// </summary>
    public static Enemy NextTarget(Vector2 from, float range, HashSet<Enemy> used)
    {
        var t = LeniaFx.Nearest(from, range, used);
        if (t == null) t = LeniaFx.Nearest(from, range);
        if (t != null) used.Add(t);
        return t;
    }

    /// <summary>"같은 피해": d 의 최종 피해량만큼 고정 피해 (간접 사용, 같은 사용 번호)</summary>
    public static DamageInfo SameAsFixed(in DamageInfo d, float ratio = 1f, DamageType type = DamageType.Direct)
    {
        var f = new DamageInfo(d.amount * ratio, type, d.source, CastKind.Indirect, isFixed: true);
        f.castId = d.castId;
        return f;
    }

    /// <summary>같은 피해 정보를 간접 사용으로 (도탄 · 추가 투사체)</summary>
    public static DamageInfo AsIndirect(in DamageInfo d) { var c = d; c.cast = CastKind.Indirect; return c; }
}

/// <summary>
/// 직진 투사체 (스패너 · 망치 · 총알). spin ≠ 0 이면 회전하며 날아가고, 0 이면 그림을 진행 방향으로 돌림.
/// 관통 · 도탄 · 부메랑은 ShotMods 로.
/// </summary>
public class LeniaProjectile : MonoBehaviour
{
    Vector2 dir;
    float speed, spin, distLeft, hitRadius, knock;
    DamageInfo dmg, returnDmg;
    ShotMods mods;
    Sprite sprite;
    int order;
    bool returning;
    readonly HashSet<Enemy> hitThisPass = new HashSet<Enemy>();
    static readonly List<Enemy> buffer = new List<Enemy>();

    public static LeniaProjectile Spawn(Sprite sprite, Vector2 from, Vector2 dir, float speed, float spinDeg, float range,
                                        float hitRadius, DamageInfo dmg, float knock, ShotMods mods = null, int order = 11,
                                        Enemy ignore = null)
    {
        var go = new GameObject("LeniaShot");
        go.transform.position = from;
        var p = go.AddComponent<LeniaProjectile>();
        p.dir = dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.right;
        p.speed = speed; p.spin = spinDeg; p.distLeft = range; p.hitRadius = hitRadius;
        p.dmg = dmg; p.knock = knock; p.mods = mods != null ? mods.Clone() : new ShotMods();   // 관통 횟수는 투사체마다 따로
        p.sprite = sprite; p.order = order;
        if (ignore != null) p.hitThisPass.Add(ignore);   // 도탄: 지금 맞은 대상은 다시 맞지 않음
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = order;
        p.Face();
        return p;
    }

    void Face()
    {
        if (spin == 0f) transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        float step = speed * Time.deltaTime;
        if (returning)
        {
            if (Player.I == null) { Destroy(gameObject); return; }
            Vector2 to = (Vector2)Player.I.transform.position - (Vector2)transform.position;
            if (to.magnitude <= step + 0.2f) { Destroy(gameObject); return; }
            dir = to.normalized;
            Face();
        }
        transform.position += (Vector3)(dir * step);
        if (spin != 0f) transform.Rotate(0, 0, spin * Time.deltaTime);
        if (!returning)
        {
            distLeft -= step;
            if (distLeft <= 0f) { EndFlight(); return; }
        }

        Vector2 pos = transform.position;
        Enemy.CopyAll(buffer);
        foreach (var e in buffer)
        {
            if (e == null || e.hp <= 0 || hitThisPass.Contains(e)) continue;
            float rr = hitRadius + e.radius;
            if (((Vector2)e.transform.position - pos).sqrMagnitude > rr * rr) continue;
            hitThisPass.Add(e);
            bool wasStunned = e.IsStunned;            // ① 대상 상태 확인
            var d = returning ? returnDmg : dmg;      // ② 조건부 피해 증가 = d.perTarget (피해 계산 안에서)
            e.Hit(d, dir, knock);
            mods.onHit?.Invoke(e, wasStunned, d);     // ③ 상태이상 부여 (가장 늦게)
            if (returning) continue;                  // 귀환 중에는 모두 관통
            if (mods.pierce > 0) { mods.pierce--; continue; }
            Bounce(e, pos);
            EndFlight();
            return;
        }
    }

    /// <summary>도탄: 지금 맞은 대상을 뺀 가장 가까운 적에게 같은 투사체 (간접 사용)</summary>
    void Bounce(Enemy current, Vector2 at)
    {
        if (mods.bounces <= 0) return;
        var next = LeniaFx.Nearest(at, mods.bounceRange, new[] { current });
        if (next == null) return;
        var m = mods.Clone();
        m.bounces--;
        m.boomerang = false;   // 도탄된 투사체는 돌아오지 않음
        Vector2 d = (Vector2)next.transform.position - at;
        Spawn(sprite, at, d, speed, spin, mods.bounceRange * 1.5f, hitRadius, LeniaShots.AsIndirect(dmg), knock, m, order, current);
    }

    void EndFlight()
    {
        if (mods.boomerang && !returning)
        {
            returning = true;
            returnDmg = LeniaShots.SameAsFixed(dmg);   // 돌아오며 같은 피해 = 최종 피해량만큼 고정 피해
            hitThisPass.Clear();
            return;
        }
        Destroy(gameObject);
    }
}

/// <summary>
/// 스택형 '받는 최종 피해량' 증가 상태이상: 적중 시 1스택, 스택당 +perStack, 스택이 쌓이면 지속 시간 갱신, 최대 maxStacks.
/// 같은 인스턴스끼리 스택 공유 (스패너 · 망치 타박상 = Bruise 하나)
/// </summary>
public class StackedVulnerability
{
    public readonly float perStack, duration;
    public readonly int maxStacks;

    class State { public int stacks; public float until; }
    readonly Dictionary<Enemy, State> map = new Dictionary<Enemy, State>();
    Mod mod;

    public StackedVulnerability(float perStack, float duration, int maxStacks)
    {
        this.perStack = perStack; this.duration = duration; this.maxStacks = maxStacks;
    }

    public void Apply(Enemy e)
    {
        if (e == null || e.hp <= 0) return;
        if (mod == null) mod = new Mod(this);
        CombatMods.Add(mod);   // 새 판에서 CombatMods.Clear 된 뒤에도 다시 등록 (중복 등록 없음)
        if (map.Count > 256) Prune();
        if (!map.TryGetValue(e, out var s) || s.until <= Time.time) map[e] = s = new State();
        s.stacks = Mathf.Min(maxStacks, s.stacks + 1);
        s.until = Time.time + duration;   // 스택형 = 지속 시간 갱신
    }

    public int Stacks(Enemy e) => e != null && map.TryGetValue(e, out var s) && s.until > Time.time ? s.stacks : 0;

    readonly List<Enemy> dead = new List<Enemy>();
    void Prune()
    {
        dead.Clear();
        foreach (var kv in map) if (kv.Key == null || kv.Value.until <= Time.time) dead.Add(kv.Key);
        foreach (var k in dead) map.Remove(k);
    }

    class Mod : CombatModifier
    {
        readonly StackedVulnerability v;
        public Mod(StackedVulnerability v) { this.v = v; }
        public override float ReceivedMul(Enemy target, in DamageInfo d) => 1f + v.perStack * v.Stacks(target);
    }
}

/// <summary>
/// 스택이 없는 계수형 '받는 최종 피해량' 증가 상태이상 (방어구 파괴):
/// 피해 없는 계수형 → 계수는 곱연산, 지속 시간은 합연산 (2026-11-12 사용자 규칙)
/// </summary>
public class CoefVulnerability
{
    class State { public float mul = 1f, until; public int stacks; }
    readonly Dictionary<Enemy, State> map = new Dictionary<Enemy, State>();
    readonly int maxStacks;
    Mod mod;

    /// <param name="maxStacks">0 = 스택 없음 (계수 곱 · 지속 시간 합). 1 이상 = 스택형 (계수 곱은 최대 maxStacks 번까지 · 지속 시간 갱신)</param>
    public CoefVulnerability(int maxStacks = 0) { this.maxStacks = maxStacks; }

    public void Apply(Enemy e, float mul, float seconds)
    {
        if (e == null || e.hp <= 0 || seconds <= 0f) return;
        if (mod == null) mod = new Mod(this);
        CombatMods.Add(mod);
        if (map.Count > 256) Prune();
        if (map.TryGetValue(e, out var s) && s.until > Time.time)
        {
            if (maxStacks <= 0) { s.mul *= mul; s.until += seconds; }
            else
            {
                if (s.stacks < maxStacks) { s.mul *= mul; s.stacks++; }
                s.until = Time.time + seconds;   // 스택형 = 지속 시간 갱신
            }
        }
        else map[e] = new State { mul = mul, until = Time.time + seconds, stacks = 1 };
    }

    public float Mul(Enemy e) => e != null && map.TryGetValue(e, out var s) && s.until > Time.time ? s.mul : 1f;

    readonly List<Enemy> dead = new List<Enemy>();
    void Prune()
    {
        dead.Clear();
        foreach (var kv in map) if (kv.Key == null || kv.Value.until <= Time.time) dead.Add(kv.Key);
        foreach (var k in dead) map.Remove(k);
    }

    class Mod : CombatModifier
    {
        readonly CoefVulnerability v;
        public Mod(CoefVulnerability v) { this.v = v; }
        public override float ReceivedMul(Enemy target, in DamageInfo d) => v.Mul(target);
    }
}

/// <summary>'타박상': 스택당 받는 최종 피해량 +5%, 5초, 최대 10 (스패너 · 망치 공유)</summary>
public static class Bruise
{
    static readonly StackedVulnerability s = new StackedVulnerability(0.05f, 5f, 10);
    public static void Apply(Enemy e) => s.Apply(e);
    public static int Stacks(Enemy e) => s.Stacks(e);
}

/// <summary>'융해' (터렛 과부화 Lv10): 스택당 받는 최종 피해량 +5%, 3초, 최대 10</summary>
public static class Melt
{
    static readonly StackedVulnerability s = new StackedVulnerability(0.05f, 3f, 10);
    public static void Apply(Enemy e) => s.Apply(e);
}

/// <summary>'방어구 파괴' (지면 강타 Lv10): 받는 최종 피해량 ×1.2, 5초, '스택' 최대 5 (계수 곱 · 스택형이라 지속 시간 갱신)</summary>
public static class ArmorBreak
{
    public const float Mul = 1.2f, Duration = 5f;
    public const int MaxStacks = 5;
    static readonly CoefVulnerability s = new CoefVulnerability(MaxStacks);
    public static void Apply(Enemy e) => s.Apply(e, Mul, Duration);
}
