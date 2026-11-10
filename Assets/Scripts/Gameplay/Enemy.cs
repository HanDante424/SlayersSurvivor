using System;
using System.Collections.Generic;
using UnityEngine;

public enum EnemyKind { Bat, Runner, Tank, Boss }

public class Enemy : MonoBehaviour
{
    public static readonly List<Enemy> All = new List<Enemy>();

    /// <summary>
    /// 적 사망 이벤트 (적 1마리당 정확히 1번). 처치 연쇄 강화(죽음의 메아리 등)가 구독.
    /// 인자 = 죽은 적 (이 호출 직후 파괴됨 → 위치 등은 즉시 복사해서 쓸 것)
    /// </summary>
    public static event Action<Enemy> Killed;
    bool dead;

    public EnemyKind kind;
    public float hp, maxHp, speed, damage, radius;
    public int xp;

    SpriteRenderer sr;
    Color baseColor;
    float flash;
    Vector2 knockback;
    float baseScale = 1f;

    // 상태이상
    float stunLeft;
    float slowLeft, slowMul = 1f;
    public bool IsStunned => stunLeft > 0;

    // 뇌진탕 (솔라 슬래시 S5-B): 넉백된 뒤 window 동안 다른 적과 부딪히면 기절
    float concussLeft, concussStun;
    bool concussStunsOther;

    /// <summary>이 적이 넉백되는 동안 다른 적과 충돌하면 stun 초 기절 (충돌 1회로 끝)</summary>
    public void MarkConcussion(float window, float stun, bool stunOther)
    {
        if (kind == EnemyKind.Boss) return;   // 보스는 넉백되지 않음
        concussLeft = window; concussStun = stun; concussStunsOther = stunOther;
    }

    /// <summary>기절: 이동·접촉 공격 중지 (더 긴 쪽 유지)</summary>
    public void Stun(float seconds) => stunLeft = Mathf.Max(stunLeft, seconds);

    /// <summary>화상 표시 (파티클만 — 화상 피해 규칙은 기획 미정)</summary>
    public void ShowBurn(float seconds) => BurnFx.Show(transform, () => radius, seconds);

    /// <summary>슬로우: 이동 속도 배율 (더 강한 쪽 유지)</summary>
    public void Slow(float mul, float seconds)
    {
        if (slowLeft <= 0 || mul < slowMul) slowMul = mul;
        slowLeft = Mathf.Max(slowLeft, seconds);
    }

    /// <summary>Hit 의 knock 에 넘기면 넉백을 건드리지 않음 (지속 피해 틱 등). 0 은 "넉백 멈춤"</summary>
    public const float NoKnock = -1f;

    /// <summary>상태 효과 이동속도 배율 (끈적한 화염 등). 슬로우와 곱연산</summary>
    public float statusSpeedMul = 1f;

    /// <summary>넉백 속도가 초당 줄어드는 비율. 밀려나는 총 거리 = 넉백 속도 / 이 값</summary>
    public const float KnockDecay = 10f;

    /// <summary>밀려날 거리(유닛) → Hit/Push 에 넘길 넉백 속도</summary>
    public static float KnockSpeed(float distance) => distance * KnockDecay;

    /// <summary>피해 없이 밀어내기</summary>
    public void Push(Vector2 dir, float force)
    {
        if (kind == EnemyKind.Boss) return;
        knockback = dir.normalized * force;
    }

    public static Enemy Spawn(EnemyKind kind, Vector2 pos, float hpMul)
    {
        var go = new GameObject("Enemy_" + kind);
        go.transform.position = pos;
        var e = go.AddComponent<Enemy>();
        e.Setup(kind, hpMul);
        return e;
    }

    void Setup(EnemyKind k, float hpMul)
    {
        kind = k;
        Color col; float scale; SpriteFactory.Shape shape = SpriteFactory.Shape.Circle;
        switch (k)
        {
            default:
            case EnemyKind.Bat:    maxHp = 10;  speed = 2.2f; damage = 8;  xp = 1;  scale = 0.6f; col = new Color(0.85f, 0.3f, 0.35f); break;
            case EnemyKind.Runner: maxHp = 7;   speed = 3.6f; damage = 6;  xp = 2;  scale = 0.5f; col = new Color(1f, 0.6f, 0.2f); shape = SpriteFactory.Shape.Diamond; break;
            case EnemyKind.Tank:   maxHp = 60;  speed = 1.4f; damage = 15; xp = 5;  scale = 1.1f; col = new Color(0.55f, 0.3f, 0.7f); shape = SpriteFactory.Shape.Square; break;
            case EnemyKind.Boss:   maxHp = 900; speed = 1.8f; damage = 25; xp = 60; scale = 2.2f; col = new Color(0.9f, 0.1f, 0.15f); break;
        }
        maxHp *= hpMul;
        hp = maxHp;
        baseScale = scale;
        radius = baseScale * Tuning.Enemy.HitboxRatio(kind);
        transform.localScale = Vector3.one * scale;
        HitboxView.Attach(transform, HitboxView.EnemyColor, () => radius);
        sr = SpriteFactory.AddRenderer(gameObject, "Enemy_" + k, shape, col, k == EnemyKind.Boss ? 6 : 5);
        baseColor = sr.color;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>
    /// All 의 사본을 buffer 에 채워 돌려줍니다.
    /// 순회 중 Hit() 으로 적이 죽어 All 이 바뀌어도 안전하게 돌 수 있습니다. (죽은 적은 hp &lt;= 0)
    /// </summary>
    public static List<Enemy> CopyAll(List<Enemy> buffer)
    {
        buffer.Clear();
        buffer.AddRange(All);
        return buffer;
    }

    void Update()
    {
        // 판정 크기는 설정 파일 값으로 매 프레임 갱신 (Play 중 조정 반영)
        radius = baseScale * Tuning.Enemy.HitboxRatio(kind);
        if (GameManager.I.State != GameState.Playing) return;
        var p = Player.I;
        if (p == null) return;

        Vector2 pos = transform.position;
        Vector2 toPlayer = (Vector2)p.transform.position - pos;
        float dist = toPlayer.magnitude;
        Vector2 dir = dist > 0.001f ? toPlayer / dist : Vector2.zero;

        if (slowLeft > 0) { slowLeft -= Time.deltaTime; if (slowLeft <= 0) slowMul = 1f; }
        bool stunned = stunLeft > 0;
        if (stunned) stunLeft -= Time.deltaTime;

        if (concussLeft > 0) CheckConcussion(pos);

        float moveSpeed = stunned ? 0f : speed * slowMul * statusSpeedMul;
        pos += (dir * moveSpeed + knockback) * Time.deltaTime;
        knockback = Vector2.Lerp(knockback, Vector2.zero, KnockDecay * Time.deltaTime);
        transform.position = pos;
        if (!stunned) sr.flipX = dir.x < 0;

        // 기절 중에는 접촉 공격 없음
        float touch = radius + p.radius;
        if (!stunned && (p.HurtCenter - pos).sqrMagnitude < touch * touch) p.TakeDamage(damage);

        // 너무 멀어지면 플레이어 근처로 재배치 (뱀서 방식)
        // 기준은 카메라 시야에 맞춰 자동 (화면 밖 생성 거리 + 6 넘으면 화면 밖 생성 거리로)
        float far = GameManager.OffscreenRadius;
        if (dist > far + 6f) transform.position = (Vector2)p.transform.position + UnityEngine.Random.insideUnitCircle.normalized * far;

        if (flash > 0)
        {
            flash -= Time.deltaTime;
            sr.color = flash > 0 ? Color.white : baseColor;
        }
        else
        {
            // 상태 표시: 기절 = 노랑, 슬로우 = 파랑
            sr.color = stunLeft > 0 ? Color.Lerp(baseColor, new Color(1f, 0.95f, 0.3f), 0.6f)
                     : slowLeft > 0 ? Color.Lerp(baseColor, new Color(0.4f, 0.6f, 1f), 0.5f)
                     : baseColor;
        }
    }

    void CheckConcussion(Vector2 pos)
    {
        concussLeft -= Time.deltaTime;
        if (knockback.sqrMagnitude < 0.25f) { if (concussLeft <= 0) concussLeft = 0; return; }   // 거의 안 밀리는 중
        foreach (var o in All)
        {
            if (o == this || o.hp <= 0) continue;
            Vector2 d = (Vector2)o.transform.position - pos;
            float min = radius + o.radius;
            // 밀려나는 방향 앞쪽에서 닿은 적 = 충돌
            if (d.sqrMagnitude < min * min && Vector2.Dot(d, knockback) > 0f)
            {
                Stun(concussStun);
                if (concussStunsOther) o.Stun(concussStun);
                concussLeft = 0;
                return;
            }
        }
    }

    /// <summary>
    /// 피해 전달 후 (체력 감소 직후, 사망 처리 전). 인자 = 맞은 적 · 피해 정보 · 실제 피해(내림 후).
    /// 특수 피해는 발생하지 않음 → 트리거 연쇄 차단. 구독자는 e.hp &lt;= 0 (이번 피해로 사망)을 확인해서 쓸 것.
    /// </summary>
    public static event Action<Enemy, DamageInfo, int> Damaged;

    /// <summary>
    /// Damaged 구독자가 모두 끝난 뒤 (같은 피해의 화상 부여 등이 반영된 상태). 강제 발화처럼 "이번 타격까지 포함한 상태"를 보는 효과용.
    /// 특수 피해는 발생하지 않음
    /// </summary>
    public static event Action<Enemy, DamageInfo, int> AfterDamaged;

    /// <summary>
    /// 체력 감소 직후, Damaged 보다 먼저. 같은 피해의 트리거(화상 부여 등)가 반영되기 **전** 상태를 봐야 하는 효과용 (강제 발화).
    /// 특수 피해는 발생하지 않음
    /// </summary>
    public static event Action<Enemy, DamageInfo, int> Damaging;

    /// <summary>
    /// 피해 전달 = ② 대상 단계 (대상 의존 최종 피해량 · 받는 최종 피해량 → 내림) → 체력 감소 → ③ Damaged 트리거 → 사망.
    /// 숫자만 넘기는 예전 호출은 출처 없는 직접 피해 (DamageInfo 암시적 변환).
    /// </summary>
    public void Hit(DamageInfo info, Vector2 fromDir, float knock = 3f)
    {
        if (hp <= 0) return;
        float dmg = DamageMath.Target(this, info);
        if (DebugCheats.OneHitKill) dmg = Mathf.Max(dmg, hp);   // 테스트: 한 방에 처치
        if (!DebugCheats.ImmortalEnemies) hp -= dmg;            // 테스트: 피해 숫자만 표시
        flash = 0.08f;
        if (kind != EnemyKind.Boss && knock >= 0f) knockback = fromDir.normalized * knock;   // NoKnock = 진행 중인 넉백 유지
        DamageText.Show(transform.position, dmg);
        if (info.type != DamageType.Special)
        {
            Damaging?.Invoke(this, info, (int)dmg);
            Damaged?.Invoke(this, info, (int)dmg);
            AfterDamaged?.Invoke(this, info, (int)dmg);
        }
        if (hp <= 0) Die();
    }

    void Die()
    {
        if (dead) return;   // 사망 처리는 한 번만 (메아리 무한 재발동 방지)
        dead = true;
        GameManager.I.OnEnemyKilled(this);
        Killed?.Invoke(this);
        if (!DebugCheats.NoXpDrop) Gem.Spawn(transform.position, xp);   // 치트 F9
        Destroy(gameObject);
    }

    // 간단한 밀어내기: 적끼리 겹치지 않게 (매 프레임 일부만 처리)
    public static void Separate()
    {
        int n = All.Count;
        if (n < 2) return;
        int start = Time.frameCount % 2;
        for (int i = start; i < n; i += 2)
        {
            var a = All[i];
            Vector2 pa = a.transform.position;
            Vector2 push = Vector2.zero;
            int checks = 0;
            for (int j = 0; j < n && checks < 40; j++)
            {
                if (i == j) continue;
                var b = All[j];
                Vector2 d = pa - (Vector2)b.transform.position;
                float min = a.radius + b.radius;
                float sq = d.sqrMagnitude;
                if (sq < min * min && sq > 0.0001f)
                {
                    float m = Mathf.Sqrt(sq);
                    push += d / m * (min - m);
                    checks++;
                }
            }
            if (push != Vector2.zero) a.transform.position = pa + push * 0.5f;
        }
    }
}
