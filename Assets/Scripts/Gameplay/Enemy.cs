using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 몬스터 종류 (2026-11-12 사용자: 고블린 계열). 기본 몹 4종 + [임시] 보스.
/// 중간보스 고블린 장로 · 홉 고블린 기사는 패턴 설계 후 추가 (Docs/Design/Monsters_v1.md)
/// </summary>
public enum EnemyKind
{
    /// <summary>고블린: 기본 몹</summary>
    Goblin,
    /// <summary>고블린 궁수: 원거리, 공격할 때 멈춤</summary>
    GoblinArcher,
    /// <summary>홉 고블린: 중형</summary>
    Hobgoblin,
    /// <summary>고블린 라이더: 돌진</summary>
    GoblinRider,
    /// <summary>고블린 장로: 마법 · 중간보스 (GoblinElderAI)</summary>
    GoblinElder,
    /// <summary>홉 고블린 기사: 중형 · 중간보스 (HobgoblinKnightAI)</summary>
    HobgoblinKnight,
    /// <summary>[임시] 예전 보스 — 지금은 나오지 않음 (홉 고블린 기사 설계 후 정리)</summary>
    Boss,
    /// <summary>가니악 벤데스트라: 레이드 보스 (레이드 스테이지 전용, GaniacAI · Docs/Design/Raid_Ganiac_Plan_v0.1.md)</summary>
    Ganiac,
    /// <summary>가니악의 검 (특수 패턴 3): 움직이지 않는 파괴 대상 · 접촉 피해 없음 · 경험치 없음</summary>
    GaniacSword,
    /// <summary>카구츠치: 레이드 보스 (레이드 스테이지 전용, KagutsuchiAI · Docs/Design/Raid_Kagutsuchi_Plan_v0.1.md)</summary>
    Kagutsuchi,
    /// <summary>야타의 거울 (카구츠치 주변 궤도, KagutsuchiMirror): 체력 = 카구츠치 × 15 · 접촉 피해 없음 · 경험치 없음</summary>
    KagutsuchiMirror,
    /// <summary>아메노무라쿠모 (카구츠치의 검, KagutsuchiBlade): 독립 체력 · 접촉 피해 없음 · 경험치 없음</summary>
    KagutsuchiSword,
}

/// <summary>적 1마리. 종류별 행동(궁수 사격 · 라이더 돌진)은 EnemyBehaviors.cs (partial)</summary>
public partial class Enemy : MonoBehaviour
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

    /// <summary>화면 표시 이름 (보스 UI 등)</summary>
    public string DisplayName => kind switch
    {
        EnemyKind.Goblin => "고블린",
        EnemyKind.GoblinArcher => "고블린 궁수",
        EnemyKind.Hobgoblin => "홉 고블린",
        EnemyKind.GoblinRider => "고블린 라이더",
        EnemyKind.GoblinElder => "고블린 장로",
        EnemyKind.HobgoblinKnight => "홉 고블린 기사",
        EnemyKind.Ganiac => "가니악 벤데스트라",
        EnemyKind.GaniacSword => "가니악의 검",
        EnemyKind.Kagutsuchi => "카구츠치",
        EnemyKind.KagutsuchiMirror => "야타의 거울",
        EnemyKind.KagutsuchiSword => "아메노무라쿠모",
        _ => "보스",
    };

    /// <summary>보스급 (중간보스 포함): 넉백 · 뇌진탕 없음, 소각 보스 비율</summary>
    public bool IsBoss => kind == EnemyKind.Boss || kind == EnemyKind.GoblinElder || kind == EnemyKind.HobgoblinKnight || kind == EnemyKind.Ganiac || kind == EnemyKind.Kagutsuchi;
    /// <summary>맵에서 이탈 중 (홉 고블린 기사 뛰어오르기): 맞지 않음 · 조준 안 됨 · 접촉 피해 없음</summary>
    public bool Hidden { get; private set; }
    public void SetHidden(bool on) { Hidden = on; if (sr != null) sr.enabled = !on; }
    /// <summary>
    /// 연출 중 (카구츠치 입장 등): 보이지만 맞지 않음 · 조준 안 됨 · 기절 안 됨 · 접촉 피해 없음 · 맵 경계 무시 (맵 밖에서 걸어 들어올 수 있음)
    /// </summary>
    public bool Scripted { get; private set; }
    public void SetScripted(bool on) => Scripted = on;
    /// <summary>그림 투명도 배율 (가니악 맵 이탈 · 등장 시 흐려짐). 1 = 보통</summary>
    public float alpha = 1f;
    /// <summary>중간보스 포위 고블린 (움직이지 않는 벽, MidBossArena)</summary>
    public bool IsWall { get; private set; }
    /// <summary>밀리지 않음 (넉백 · 밀어내기 · 적끼리 밀기)</summary>
    bool Immovable => IsBoss || IsWall || kind == EnemyKind.GaniacSword || kind == EnemyKind.KagutsuchiMirror || kind == EnemyKind.KagutsuchiSword;
    GoblinElderAI elderAI;
    HobgoblinKnightAI knightAI;
    GaniacAI ganiacAI;

    // ---------- 4방향 동작 시트 (가니악, 2026-11-13) ----------
    // Resources/Sprites/Ganiac_Idle (4×1) · Ganiac_Run (4×8) · Ganiac_Slash (4×8), 행 = 아래 · 왼쪽 · 오른쪽 · 위 (오른쪽 = 왼쪽 반전).
    // 시트 규격: 정지 · 걷기 칸 384px · 베기 칸 480px, 몸 키 300px, 발끝 = 칸 아래 24 · 30px (Docs/Design/Raid_Ganiac_Plan_v0.1.md 3-2)
    public const string SlashClip = "Slash";
    public const float SlashFps = 14f;                       // [임시] 베기 8프레임 ≈ 0.57초
    public const float SlashLead = 3f / SlashFps;            // 베기 4번째 프레임(칼이 지나가는 순간)까지 걸리는 시간 → 판정 순간에 맞춰 미리 재생
    const float GaniacHeight = 2.3f;                         // [임시] 화면 키 (유닛, 플레이어 약 1.62)
    const float GaniacPivotAboveFeet = 1.0f;                 // transform(판정 원 중심) = 발 위 1유닛 (판정 반경 약 1)
    const float MoveAnimSpeed = 0.3f;                        // 이 속도 이상 움직이면 걷기
    SpriteSheetAnimator anim;
    Vector2 animLastPos, lastFace = Vector2.down;

    /// <summary>방향별 동작 시트 불러오기 (시트가 없으면 기존 한 장 그림 · 도형 그대로)</summary>
    void SetupSheets(string id, float height, float pivotAboveFeet)
    {
        var a = gameObject.AddComponent<SpriteSheetAnimator>();
        float ppu = 300f / height;                                     // 시트 px / 월드 유닛
        float s = Mathf.Max(0.0001f, baseScale);                       // 오브젝트 크기 배율로 나눠 칸 크기를 맞춤
        float cellIdle = 384f / ppu / s, cellSlash = 480f / ppu / s;
        float pivotIdle = (24f + ppu * pivotAboveFeet) / 384f, pivotSlash = (30f + ppu * pivotAboveFeet) / 480f;
        var placeholder = sr.sprite;
        sr.sprite = null;   // 첫 프레임부터 시트 그림 (도형이 한 프레임 보이지 않게)
        if (!a.LoadClip(SpriteSheetAnimator.Run, id + "_Run", 4, 8, cellIdle, pivotIdle, 10f, true)) { sr.sprite = placeholder; Destroy(a); return; }
        a.LoadClip(SpriteSheetAnimator.Idle, id + "_Idle", 4, 1, cellIdle, pivotIdle, 1f, true);
        a.LoadClip(SlashClip, id + "_Slash", 4, 8, cellSlash, pivotSlash, SlashFps, false);
        anim = a;
        sr.color = baseColor = Color.white;   // 도형 대체 색을 지움
        sr.flipX = false;                    // 방향은 시트 행으로
        animLastPos = transform.position;
    }

    /// <summary>베기 동작 재생 (시트가 없으면 아무것도 안 함). 진행 중인 동작은 끊고 새로</summary>
    public void PlayAttackAnim(Vector2 dir)
    {
        if (anim != null) anim.Play(SlashClip, dir, null, interrupt: true);
    }

    // 실제 위치 변화로 걷기 / 정지 (패턴이 transform 을 직접 옮기는 돌진 · 전진도 걷기로 보임). 멈춰 있으면 바라볼 방향
    void LateUpdate()
    {
        if (anim == null) return;
        Vector2 pos = transform.position;
        float dt = Time.deltaTime;
        Vector2 v = dt > 0f ? (pos - animLastPos) / dt : Vector2.zero;
        animLastPos = pos;
        if (GameManager.I != null && GameManager.I.State != GameState.Playing) return;
        bool moving = v.sqrMagnitude > MoveAnimSpeed * MoveAnimSpeed && v.sqrMagnitude < 2500f;   // 순간이동(한 프레임 큰 이동)은 걷기 아님
        anim.SetMove(moving ? v : Vector2.zero);
        if (!moving) anim.Look(lastFace);
    }
    KagutsuchiAI kagutsuchiAI;

    SpriteRenderer sr;
    Color baseColor;
    float flash;
    /// <summary>일반 몹 4종 이동 속도 배율 (아래 표의 값 × 0.9). 라이더 돌진 · 궁수 화살 속도는 그대로</summary>
    public const float MobSpeedMul = 0.9f;
    /// <summary>[임시] 이미지 적의 피격 깜빡임 색</summary>
    static readonly Color HitFlashTint = new Color(1f, 0.45f, 0.45f);
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
        if (Immovable) return;   // 보스 · 포위 고블린은 넉백되지 않음
        concussLeft = window; concussStun = stun; concussStunsOther = stunOther;
    }

    /// <summary>
    /// 기절: 이동·접촉 공격 중지. 스택이 없는 상태이상 → 지속 시간 누적 (2026-11-12 사용자 규칙)
    /// </summary>
    /// <param name="indirect">'간접 사용'에서 나온 기절 (터렛 공격 · 추가 투사체 · 충격파 · 범위 충격). 범위 충격 연쇄 방지용</param>
    public void Stun(float seconds, bool indirect = false)
    {
        if (seconds <= 0f || Hidden || Scripted) return;
        bool was = IsStunned;
        seconds *= CombatMods.StunDurationMul();               // 충격 역학 '기절' '지속 시간' +%
        if (was) seconds += CombatMods.StunReapplyAdd();       // 재기절
        if (!was) StunTotal = 0f;
        stunLeft = Mathf.Max(0f, stunLeft) + seconds;
        StunTotal += seconds;
        Stunned?.Invoke(this, seconds, was, indirect);
    }

    /// <summary>이번 기절의 총 지속 시간 (기절이 시작된 뒤 누적된 값, 남은 시간 아님)</summary>
    public float StunTotal { get; private set; }

    /// <summary>기절 부여 직후: 적 · 실제 부여 시간 · 이미 기절 중이었는지 · 간접 사용 여부</summary>
    public static event Action<Enemy, float, bool, bool> Stunned;
    /// <summary>기절이 끝남 (살아 있는 적만)</summary>
    public static event Action<Enemy> StunEnded;

    /// <summary>화상 표시 (파티클만 — 화상 피해 규칙은 기획 미정)</summary>
    public void ShowBurn(float seconds) => BurnFx.Show(transform, () => radius, seconds);

    /// <summary>
    /// '이동 속도 감소' 부여: 피해 없는 계수형 상태이상 → 계수는 곱연산, 지속 시간은 합연산 (2026-11-12 사용자 규칙).
    /// 예: 20% · 3초 두 번 = 이동 속도 ×0.8×0.8, 지속 6초
    /// </summary>
    public void Slow(float mul, float seconds)
    {
        if (seconds <= 0f) return;
        if (slowLeft > 0) { slowMul *= mul; slowLeft += seconds; }
        else { slowMul = mul; slowLeft = seconds; }
    }

    /// <summary>
    /// 장판 안에 있는 동안 유지되는 감속 (매 틱 다시 걸어도 쌓이지 않음 — 더 강한 쪽 · 더 긴 쪽 유지).
    /// 부여형(Slow)과 달리 '범위 안에 있는 동안' 효과용
    /// </summary>
    public void SustainSlow(float mul, float seconds)
    {
        if (slowLeft <= 0 || mul < slowMul) slowMul = Mathf.Min(slowLeft > 0 ? slowMul : 1f, mul);
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
        if (Immovable) return;
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
            // [임시] 수치 — 기획 대기. scale = 그림 긴 변(유닛), 플레이어 키 약 1.62
            default:
            case EnemyKind.Goblin:       maxHp = 10;  speed = 2.2f; damage = 8;  xp = 1;  scale = 0.8f; col = new Color(0.45f, 0.75f, 0.3f); break;
            case EnemyKind.GoblinArcher: maxHp = 8;   speed = 2.0f; damage = 5;  xp = 2;  scale = 0.8f; col = new Color(0.85f, 0.8f, 0.3f); shape = SpriteFactory.Shape.Diamond; break;
            case EnemyKind.Hobgoblin:    maxHp = 60;  speed = 1.4f; damage = 15; xp = 5;  scale = 1.3f; col = new Color(0.3f, 0.5f, 0.25f); shape = SpriteFactory.Shape.Square; break;
            case EnemyKind.GoblinRider:  maxHp = 16;  speed = 2.6f; damage = 10; xp = 3;  scale = 1.2f; col = new Color(0.75f, 0.45f, 0.25f); shape = SpriteFactory.Shape.Diamond; break;
            case EnemyKind.GoblinElder:  maxHp = 500; speed = 1.0f; damage = 15; xp = 40; scale = 1.4f; col = new Color(0.6f, 0.35f, 0.85f); break;
            case EnemyKind.HobgoblinKnight: maxHp = 900; speed = 0.9f; damage = 20; xp = 60; scale = 1.8f; col = new Color(0.35f, 0.45f, 0.55f); shape = SpriteFactory.Shape.Square; break;
            case EnemyKind.Boss:         maxHp = 900; speed = 1.8f; damage = 25; xp = 60; scale = 2.2f; col = new Color(0.9f, 0.1f, 0.15f); break;
            // [임시] 가니악 (레이드 보스): 수치 미정 · 이동 속도 느림 (사용자). 그림 Enemy_Ganiac 없으면 짙은 보라 원
            case EnemyKind.Ganiac:       maxHp = 3000; speed = 1.0f; damage = 20; xp = 100; scale = 2.25f; col = new Color(0.35f, 0.12f, 0.45f); break;   // 크기 3 → 2.25 (사용자: 0.75배)
            // [임시] 카구츠치: 수치 전부 미정 (가니악 값을 확정으로 쓰지 않음 — 테스트용 임시). 그림 Enemy_Kagutsuchi 없으면 주황 원
            case EnemyKind.Kagutsuchi:   maxHp = 3000; speed = 1.0f; damage = 20; xp = 100; scale = 4f; col = new Color(0.9f, 0.4f, 0.15f); break;   // 그림 Enemy_Kagutsuchi (옥좌에 앉은 정면 치비, 2026-11-13) 옥좌 포함 키 4 [임시] (그림 전 도형 크기 2.25)
            // 야타의 거울: 체력은 생성 때 카구츠치 최대 체력 × 15 로 덮어씀. [임시] 크기 1.6 (그림 Enemy_KagutsuchiMirror 2026-11-13, 보스 키 4 에 맞춰 1.1 → 1.6), 그림 없으면 밝은 원
            // 아메노무라쿠모: 체력은 생성 때 덮어씀. 크기 (길이 · 두께) 는 KagutsuchiBlade 가 정함. 흰색 · 은색 (사용자)
            case EnemyKind.KagutsuchiSword: maxHp = 1; speed = 0f; damage = 0; xp = 0; scale = 1f; col = new Color(0.92f, 0.94f, 0.98f, 0.97f); shape = SpriteFactory.Shape.Square; break;
            case EnemyKind.KagutsuchiMirror: maxHp = 1; speed = 0f; damage = 0; xp = 0; scale = 1.6f; col = new Color(0.85f, 0.92f, 1f, 0.95f); break;
            // [임시] 가니악의 검: 체력 미정. 그림 Enemy_GaniacSword 없으면 보라 마름모
            case EnemyKind.GaniacSword:  maxHp = 200; speed = 0f; damage = 0; xp = 0; scale = 2.4f;   // 그림 Enemy_GaniacSword (땅에 박힌 검, 2026-11-13) 키 2.4 [임시]
                                          col = new Color(0.6f, 0.3f, 0.9f); shape = SpriteFactory.Shape.Diamond; break;
        }
        // 일반 몹 이동 속도 -10% (2026-11-13 플레이 테스트 피드백: 피로도). 보스 · 중간보스는 그대로
        if (k == EnemyKind.Goblin || k == EnemyKind.GoblinArcher || k == EnemyKind.Hobgoblin || k == EnemyKind.GoblinRider) speed *= MobSpeedMul;
        maxHp *= hpMul;
        hp = maxHp;
        baseScale = scale;
        radius = baseScale * Tuning.Enemy.HitboxRatio(kind);
        transform.localScale = Vector3.one * scale;
        HitboxView.Attach(transform, HitboxView.EnemyColor, () => radius);
        sr = SpriteFactory.AddRenderer(gameObject, "Enemy_" + k, shape, col, IsBoss ? 6 : 5);
        baseColor = sr.color;
        if (IsBoss) BossOutline.Attach(gameObject, sr);   // 보스 붉은 테두리 (몹 사이에서 잘 보이게)
        if (k == EnemyKind.GoblinElder) elderAI = gameObject.AddComponent<GoblinElderAI>();
        if (k == EnemyKind.HobgoblinKnight) knightAI = gameObject.AddComponent<HobgoblinKnightAI>();
        if (k == EnemyKind.Ganiac) { ganiacAI = gameObject.AddComponent<GaniacAI>(); SetupSheets("Ganiac", GaniacHeight, GaniacPivotAboveFeet); }
        if (k == EnemyKind.Kagutsuchi) kagutsuchiAI = gameObject.AddComponent<KagutsuchiAI>();
    }

    /// <summary>중간보스 포위 고블린 1마리: 고블린 모습 · 체력 hp · 움직이지 않음</summary>
    public static Enemy SpawnWall(Vector2 pos, float hp)
    {
        var e = Spawn(EnemyKind.Goblin, pos, 1f);
        e.name = "ArenaGoblin";
        e.IsWall = true;
        e.maxHp = e.hp = hp;
        e.xp = MidBossArena.WallXp;
        return e;
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
        if (stunned)
        {
            stunLeft -= Time.deltaTime;
            if (stunLeft <= 0f) StunEnded?.Invoke(this);
        }

        if (concussLeft > 0) CheckConcussion(pos);

        // 종류별 행동 (기절하면 준비 중인 공격 취소)
        float speedMul = slowMul * statusSpeedMul;
        Vector2 vel = Vector2.zero, face = dir;
        if (stunned) CancelAttack();
        else vel = Behave(pos, dir, dist, speedMul, ref face);
        pos += (vel + knockback) * Time.deltaTime;
        knockback = Vector2.Lerp(knockback, Vector2.zero, KnockDecay * Time.deltaTime);
        if (Stage.Bounded && !Scripted) pos = Stage.ClampInside(pos, radius);   // 제한된 맵 (레이드): 경계 밖으로 나가지 않음
        transform.position = pos;
        if (anim != null) { if (!stunned) lastFace = face; }   // 방향은 4방향 시트가 정함
        else if (kind == EnemyKind.Kagutsuchi) { }   // 카구츠치 = 정면 그림만 (2026-11-13 사용자) → 좌우 반전 없음
        else if (!stunned && Mathf.Abs(face.x) > 0.01f) sr.flipX = face.x < 0;

        // 기절 중에는 접촉 공격 없음
        float touch = radius + p.radius;
        if (damage > 0f && !stunned && !Hidden && !Scripted &&(p.HurtCenter - pos).sqrMagnitude < touch * touch) p.TakeDamage(damage);   // 접촉 피해 0 = 닿아도 피격 무적이 생기지 않음

        // 너무 멀어지면 플레이어 근처로 재배치 (뱀서 방식)
        // 기준은 카메라 시야에 맞춰 자동 (화면 밖 생성 거리 + 6 넘으면 화면 밖 생성 거리로)
        float far = GameManager.OffscreenRadius;
        if (!Immovable && dist > far + 6f)
            transform.position = Stage.ClampInside((Vector2)p.transform.position + UnityEngine.Random.insideUnitCircle.normalized * far, radius);

        if (flash > 0)
        {
            flash -= Time.deltaTime;
            // 도형(색 칠함)은 흰색으로, 이미지(원래 색 = 흰색)는 붉게 깜빡임 (곱하기라 밝게는 못 함)
            sr.color = flash > 0 ? (baseColor == Color.white ? HitFlashTint : Color.white) : baseColor;
        }
        else
        {
            // 상태 표시: 기절 = 노랑, 슬로우 = 파랑
            sr.color = stunLeft > 0 ? Color.Lerp(baseColor, new Color(1f, 0.95f, 0.3f), 0.6f)
                     : slowLeft > 0 ? Color.Lerp(baseColor, new Color(0.4f, 0.6f, 1f), 0.5f)
                     : baseColor;
        }
        if (alpha < 1f) { var c = sr.color; c.a *= Mathf.Clamp01(alpha); sr.color = c; }
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
        if (hp <= 0 || Hidden || Scripted) return;   // 맵 이탈 · 연출 중에는 맞지 않음
        float dmg = DamageMath.Target(this, info);
        if (knightAI != null && !info.isFixed) dmg = Mathf.Floor(dmg * knightAI.DamageMul(fromDir) + 1e-4f);   // 방패 막기 (고정 피해 제외)
        if (DebugCheats.OneHitKill) dmg = Mathf.Max(dmg, hp);   // 테스트: 한 방에 처치
        if (!DebugCheats.ImmortalEnemies) hp -= dmg;            // 테스트: 피해 숫자만 표시
        flash = 0.08f;
        if (!Immovable && knock >= 0f) knockback = fromDir.normalized * knock;   // NoKnock = 진행 중인 넉백 유지
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
        if (xp > 0 && !DebugCheats.NoXpDrop) Gem.Spawn(transform.position, xp);   // 치트 F9 · 경험치 0 (가니악의 검) 은 드랍 없음
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
            if (a.Immovable) continue;   // 포위 고블린 · 보스 · 가니악의 검은 밀리지 않음 (다른 적만 밀려남)
            Vector2 pa = a.transform.position;
            Vector2 push = Vector2.zero;
            int checks = 0;
            for (int j = 0; j < n && checks < 40; j++)
            {
                if (i == j) continue;
                var b = All[j];
                if (b.Hidden) continue;
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
