using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player I;

    [Header("기본 스탯")]
    public float maxHp = 100f;
    public float hp;
    public float regen = 0f;            // 초당 회복
    public float damageMul = 1f;
    public float cooldownMul = 1f;
    public float areaMul = 1f;
    /// <summary>받는 피해 배율 (잿불 갑주: 0.8 = -20%). 보호막보다 먼저 적용</summary>
    public float damageTakenMul = 1f;
    /// <summary>경험치 획득 배율 (불씨 수확: 1.2 = +20%)</summary>
    public float xpGainMul = 1f;
    /// <summary>보호막 (화염 보호막): 피해를 체력보다 먼저 받아 냄</summary>
    public float shield;
    /// <summary>모든 회복량 배율 (잿불 갑주). 체력 회복은 반드시 Heal() 로</summary>
    public float healMul = 1f;
    /// <summary>유물 상자 드랍 확률 배율 (불씨 수확, 일반 몹만 — 중간보스는 확정)</summary>
    public float relicChanceMul = 1f;

    /// <summary>체력 회복 (회복량 배율 적용, 최대 체력까지)</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        hp = Mathf.Min(maxHp, hp + amount * healMul);
    }

    [Header("업그레이드 배율")]
    public float moveSpeedMul = 1f;
    public float pickupMul = 1f;

    // 감각 조정 값은 설정 파일에서 (Assets/Resources/Config/ErnStrayConfig.asset)
    static PlayerConfig Cfg => Tuning.ErnStray;
    public float moveSpeed => Cfg.moveSpeed * moveSpeedMul * (bossSlowLeft > 0f ? bossSlowMul : ZoneSlowMul);

    float bossSlowLeft, bossSlowMul = 1f;

    /// <summary>기간형 보스 감속. 새 효과가 배율·남은 시간을 덮어쓰며 부여 후 수명은 Player가 소유.</summary>
    public void ApplySlowByBoss(float multiplier, float seconds)
    {
        if (DebugCheats.God || IsInvulnerable || GameManager.I.State != GameState.Playing || seconds <= 0f) return;
        bossSlowMul = Mathf.Clamp01(multiplier);
        bossSlowLeft = seconds;
    }

    // 장판 감속 (얼음 지대): 장판이 매 프레임 다시 걸어 줌, 안 걸리면 다음 프레임에 풀림
    float zoneSlowMul = 1f;
    int zoneSlowFrame = -10;
    float ZoneSlowMul => Time.frameCount - zoneSlowFrame <= 1 ? zoneSlowMul : 1f;

    /// <summary>이번 프레임 장판 감속 (여러 장판이면 가장 강한 것)</summary>
    public void ApplyZoneSlow(float mul)
    {
        if (Time.frameCount - zoneSlowFrame > 1) zoneSlowMul = mul;
        else zoneSlowMul = Mathf.Min(zoneSlowMul, mul);
        zoneSlowFrame = Time.frameCount;
    }
    public float pickupRadius => Cfg.pickupRadius * pickupMul;
    /// <summary>피격 판정 반경</summary>
    public float radius => Cfg.hurtRadius;
    /// <summary>피격 판정 중심 (월드 좌표)</summary>
    public Vector2 HurtCenter => (Vector2)transform.position + Cfg.hurtOffset;
    public float critChance => Cfg.critChance;

    /// <summary>치명타 판정 (critChance 확률로 true)</summary>
    public bool RollCrit() => Random.value < critChance;

    /// <summary>캐릭터 스킬 목록 (기획서 기준). 캐릭터가 늘면 선택된 캐릭터의 목록으로 바뀜</summary>
    /// <summary>고른 캐릭터의 스킬 목록 (캐릭터 선택 화면 → CharacterRoster.Selected)</summary>
    public List<SkillEntry> Catalog => CharacterRoster.Selected?.catalog ?? SkillCatalog.ErnStray;
    /// <summary>보유 스킬 (획득 순서). 수동 스킬은 획득 순서대로 J/K/L 에 배정</summary>
    public readonly List<Skill> skills = new List<Skill>();
    public Vector2 Facing { get; private set; } = Vector2.right;

    // ---------- 스킬이 쓰는 상태 ----------
    float skillInvuln;      // 스킬 무적 (피격 무적과 별개)
    float rootLeft;         // 이동 불가
    Vector2 dashVel;        // 돌진 속도
    float dashLeft;
    System.Action dashStep, dashEnd;

    /// <summary>돌진 중이거나 이동 불가 상태 → 다른 수동 스킬 사용 불가</summary>
    public bool IsBusy => bossHoldOwner != null || dashLeft > 0 || rootLeft > 0 || bossMoveLeft > 0;
    public bool IsInvulnerable => skillInvuln > 0 || dashLeft > 0 && dashInvuln;
    bool dashInvuln;

    public void SetInvulnerable(float seconds) => skillInvuln = Mathf.Max(skillInvuln, seconds);

    /// <summary>
    /// 피격 가로채기 (파이어 스트림 '화염의 창' 대기 중 반격): 피해를 받기 직전에 호출. true 를 돌려주면 그 피격 (피해 · 기절 · 밀려남) 을 막음.
    /// 무적 · 치트 · 게임 상태 검사를 통과한 '실제로 맞을 피격' 에서만 호출됨. 쓰는 쪽이 끝나면 null 로 되돌릴 것
    /// </summary>
    public System.Func<bool> HitCounter;
    bool CounterHit() => HitCounter != null && HitCounter();
    public void Root(float seconds) => rootLeft = Mathf.Max(rootLeft, seconds);

    // 홀드는 발동자가 소유하며, 높이는 표시 자식에만 적용한다.
    object bossHoldOwner;
    GameObject bossHoldVisual;
    SpriteRenderer bossHoldRenderer;
    bool beforeHoldEnabled, bossHoldProne;
    Facing4? bossHoldFacing;
    const float BossHoldProneRotation = -90f; // 사용자: 왼쪽 보기=시계방향90도, 오른쪽 보기=반시계방향90도.
    float bossHoldHeight;
    Vector2 bossHoldVisualOffset;
    float bossHoldOffsetWeight = 1f;
    public Transform BossHoldVisual => bossHoldVisual != null ? bossHoldVisual.transform : transform;
    // 정렬된 캐릭터 시트: 칸 256px·발 아래 16px·몸 200px (CharacterVisual 규격).
    const float VisualCellPixels = 256f, VisualBodyPixels = 200f, VisualFeetMarginPixels = 16f;
    public Vector2 BossHoldHeadPosition
    {
        get
        {
            float headY = anim != null
                ? Visual.cell * ((VisualFeetMarginPixels + VisualBodyPixels) / VisualCellPixels - Visual.pivotY)
                : (sr != null && sr.sprite != null ? sr.sprite.bounds.max.y : 0f);
            return BossHoldVisual.TransformPoint(Vector3.up * headY);
        }
    }
    public bool IsHeldByBoss(object owner) => bossHoldOwner == owner && owner != null;

    public bool TryBeginBossHold(object owner)
    {
        if (owner == null || bossHoldOwner != null || hp <= 0f || DebugCheats.God || IsInvulnerable
            || GameManager.I == null || GameManager.I.State != GameState.Playing) return false;
        bossHoldOwner = owner;
        bossHoldProne = false;
        bossHoldFacing = null;
        bossHoldVisualOffset = Vector2.zero;
        bossHoldOffsetWeight = 1f;
        bossMoveLeft = 0f;
        knockVel = Vector2.zero;
        dashLeft = 0f;
        var end = dashEnd;
        dashStep = dashEnd = null;
        end?.Invoke();
        beforeHoldEnabled = sr.enabled;
        bossHoldVisual = new GameObject("BossHeldPlayerVisual");
        bossHoldVisual.transform.SetParent(transform, false);
        bossHoldRenderer = bossHoldVisual.AddComponent<SpriteRenderer>();
        bossHoldRenderer.sharedMaterial = sr.sharedMaterial;
        bossHoldRenderer.sortingLayerID = sr.sortingLayerID;
        bossHoldRenderer.sortingOrder = sr.sortingOrder;
        sr.enabled = false;
        UpdateBossHoldVisual();
        return true;
    }

    public void SetBossHoldHeight(object owner, float height)
    {
        if (!IsHeldByBoss(owner)) return;
        bossHoldHeight = Mathf.Max(0f, height);
        UpdateBossHoldVisual();
    }

    /// <summary>지면 판정은 유지하면서 홀드된 그림의 머리를 월드 접촉점에 맞춘다.</summary>
    public void AlignBossHoldHead(object owner, Vector2 contactWorld)
    {
        if (!IsHeldByBoss(owner)) return;
        bossHoldOffsetWeight = 1f;
        bossHoldVisualOffset += contactWorld - BossHoldHeadPosition;
        UpdateBossHoldVisual();
    }

    /// <summary>잡기 소유자가 표시 방향을 지정한다. 원본 Facing/애니메이터 상태는 유지.</summary>
    public void SetBossHoldFacing(object owner, bool faceRight)
    {
        if (!IsHeldByBoss(owner)) return;
        bossHoldFacing = faceRight ? Facing4.Right : Facing4.Left;
        UpdateBossHoldVisual();
    }

    /// <summary>홀드 표시를 눕히고 표시 방향에 따라 수평으로 마주 보게 한다.</summary>
    public void SetBossHoldProne(object owner, bool prone)
    {
        if (!IsHeldByBoss(owner)) return;
        bossHoldProne = prone;
        UpdateBossHoldVisual();
    }

    public void SetBossHoldOffsetWeight(object owner, float weight)
    {
        if (!IsHeldByBoss(owner)) return;
        bossHoldOffsetWeight = Mathf.Clamp01(weight);
        UpdateBossHoldVisual();
    }

    public void SetBossHoldPosition(object owner, Vector2 position)
    {
        if (!IsHeldByBoss(owner)) return;
        Vector2 hurtOffset = HurtCenter - (Vector2)transform.position;
        transform.position = Stage.Bounded ? Stage.ClampInside(position + hurtOffset, radius) - hurtOffset : position;
    }

    public void EndBossHold(object owner)
    {
        if (!IsHeldByBoss(owner)) return;
        bossHoldOwner = null;
        bossHoldProne = false;
        bossHoldFacing = null;
        bossHoldHeight = 0f;
        bossHoldVisualOffset = Vector2.zero;
        bossHoldOffsetWeight = 1f;
        if (sr != null) sr.enabled = beforeHoldEnabled;
        if (bossHoldVisual != null) { bossHoldVisual.SetActive(false); Destroy(bossHoldVisual); }
        bossHoldVisual = null;
        bossHoldRenderer = null;
    }

    void UpdateBossHoldVisual()
    {
        if (bossHoldRenderer == null || sr == null) return;
        bool overrideFacing = bossHoldFacing.HasValue || bossHoldProne;
        Facing4 heldFacing = bossHoldFacing ?? Facing4.Left;
        var heldSprite = overrideFacing && anim != null ? anim.GetIdleFrame(heldFacing) : null;
        bossHoldRenderer.sprite = heldSprite != null ? heldSprite : sr.sprite;
        bossHoldRenderer.color = sr.color;
        bossHoldRenderer.flipX = overrideFacing ? (anim == null ? heldFacing == Facing4.Left : anim.IdleFrameNeedsFlip(heldFacing)) : sr.flipX;
        bossHoldRenderer.flipY = overrideFacing ? false : sr.flipY;
        // 반대 방향에서는 원화 방향과 회전 부호를 함께 바꿔 누운 결과도 좌우 반전.
        float proneRotation = heldFacing == Facing4.Left ? BossHoldProneRotation : -BossHoldProneRotation;
        bossHoldVisual.transform.localRotation = Quaternion.Euler(0f, 0f, bossHoldProne ? proneRotation : 0f);
        bossHoldVisual.transform.localPosition = Vector3.up * (bossHoldHeight / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.y)))
            + transform.InverseTransformVector((Vector3)(bossHoldVisualOffset * bossHoldOffsetWeight));
    }

    void LateUpdate() => UpdateBossHoldVisual();
    void OnDisable()
    {
        if (bossHoldOwner != null) EndBossHold(bossHoldOwner);
    }

    public void StunByBoss(float seconds)
    {
        if (DebugCheats.God || IsInvulnerable || GameManager.I == null
            || GameManager.I.State != GameState.Playing || seconds <= 0f) return;
        stunLeft = Mathf.Max(stunLeft, seconds);
        Root(seconds);
    }

    // ---------- 보스 공격: 기절 · 밀려남 ----------
    float stunLeft;
    Vector2 knockVel;
    Vector2 bossMoveTarget;
    float bossMoveLeft;

    /// <summary>보스 제어 효과: 지정 시간에 목적지까지 이동. 스킬 무적은 제어도 막음.</summary>
    public void MoveByBoss(Vector2 destination, float seconds)
    {
        if (DebugCheats.God || IsInvulnerable || GameManager.I.State != GameState.Playing || seconds <= 0f) return;
        bossMoveTarget = destination;
        bossMoveLeft = seconds;
        knockVel = Vector2.zero;
        Root(seconds);
    }
    /// <summary>기절: 이동·새 공격 발동 봉인. 이미 발동한 공격은 유지.</summary>
    public bool IsStunned => bossHoldOwner != null || stunLeft > 0f;

    /// <summary>
    /// 보스 공격 적중: 피해 + 기절(초) + 밀려남(거리, 방향). 무적(치트 · 스킬 무적) 중이면 아무 효과 없음.
    /// 피격 직후 0.35초 무적은 피해만 막고 기절 · 밀려남은 그대로 적용
    /// </summary>
    public void HitByBoss(float damage, float stun, Vector2 knockDir, float knockDistance)
    {
        if (DebugCheats.God || IsInvulnerable || GameManager.I.State != GameState.Playing) return;
        if (CounterHit()) return;
        TakeDamage(damage);
        if (stun > 0f) { stunLeft = Mathf.Max(stunLeft, stun); Root(stun); }
        if (knockDistance > 0f && knockDir.sqrMagnitude > 0.0001f) knockVel = knockDir.normalized * Enemy.KnockSpeed(knockDistance);
    }

    /// <summary>
    /// 돌진. distance 를 duration 동안 이동. onStep 은 매 프레임(위치 이동 후) 호출.
    /// </summary>
    public void Dash(Vector2 dir, float distance, float duration, bool invulnerable,
                     System.Action onStep = null, System.Action onEnd = null)
    {
        if (dir.sqrMagnitude < 0.01f) dir = Facing;
        dir.Normalize();
        dashVel = dir * (distance / Mathf.Max(0.01f, duration));
        dashLeft = duration;
        dashInvuln = invulnerable;
        dashStep = onStep;
        dashEnd = onEnd;
        Facing = dir;
        if (anim != null) anim.SetMove(dir);
    }

    /// <summary>돌진 중 방향을 입력 쪽으로 틀기 (속도는 그대로). 입력이 없으면 그대로 (파이어 차지 화염 질주)</summary>
    public void SteerDash(Vector2 input)
    {
        if (dashLeft <= 0f || input.sqrMagnitude < 0.01f) return;
        float speed = dashVel.magnitude;
        input.Normalize();
        dashVel = input * speed;
        Facing = input;
        if (anim != null) anim.SetMove(input);   // 캐릭터 그림도 이동 방향으로
    }

    public Skill GetSkill(string id)
    {
        foreach (var s in skills) if (s.Entry.id == id) return s;
        return null;
    }

    public int CountKind(SkillKind k)
    {
        int n = 0;
        foreach (var s in skills) if (s.Entry.kind == k) n++;
        return n;
    }

    /// <summary>
    /// 스킬 신규 획득 (Lv.1). 이미 보유 중이면 아무것도 하지 않음 —
    /// 보유 스킬 강화는 레벨업 화면의 기본 강화(Skill.ApplyBasicUpgrade) / 궁극기 강화(Skill.LevelUp) 경유.
    /// </summary>
    public Skill AddOrLevelSkill(SkillEntry entry)
    {
        var existing = GetSkill(entry.id);
        if (existing != null) return existing;
        var go = new GameObject("Skill_" + entry.id);
        go.transform.SetParent(transform, false);
        var s = (Skill)go.AddComponent(entry.type);
        s.Init(entry);
        skills.Add(s);
        return s;
    }

    /// <summary>수동 스킬 n번째 (J=0, K=1, L=2)</summary>
    public Skill ManualSlot(int i)
    {
        int n = 0;
        foreach (var s in skills)
            if (s.Entry.kind == SkillKind.Manual) { if (n == i) return s; n++; }
        return null;
    }

    public Skill FirstOfKind(SkillKind k)
    {
        foreach (var s in skills) if (s.Entry.kind == k) return s;
        return null;
    }

    /// <summary>체력 바를 그릴 위치 (플레이어 기준 아래쪽 거리)</summary>
    public float HealthBarOffset { get; private set; } = 0.65f;
    /// <summary>체력 바를 그릴 월드 위치. 보스가 눕혀 잡고 있을 때는 눕힌 그림 바로 아래 (사용자 2026-11-16), 아니면 발 아래</summary>
    public Vector3 HealthBarWorldPosition
    {
        get
        {
            if (bossHoldProne && bossHoldRenderer != null && bossHoldRenderer.sprite != null)
            {
                var b = bossHoldRenderer.bounds;
                return new Vector3(b.center.x, b.min.y - 0.15f, transform.position.z);
            }
            return transform.position + Vector3.down * HealthBarOffset;
        }
    }

    float invuln;
    SpriteRenderer sr;
    Color baseColor;
    SpriteSheetAnimator anim;

    // 캐릭터 스프라이트 시트: Resources/Sprites/{Visual.id}_{동작}.png
    /// <summary>
    /// 캐릭터 외형 1종의 시트 규격. 시트 행 = 아래·왼쪽·오른쪽·위 (SpriteSheetAnimator 규칙).
    /// cell = 칸 1개의 월드 크기, pivotY = 칸 안에서 transform 이 놓일 높이 (0 = 아래)
    /// </summary>
    public struct CharacterVisual
    {
        public string id;
        public float cell, pivotY;
        public int idleCols, runCols;
        public float idleFps, runFps;
        /// <summary>공격 모션 {id}_Slash: 프레임 수 (0 = 없음), 칸 월드 크기 · 피벗 (무기 때문에 칸이 더 큼)</summary>
        public int attackCols;
        public float attackCell, attackPivotY;
        /// <summary>공격 모션 재생 속도 (0 = 설정 파일 attackFps)</summary>
        public float attackFps;
        /// <summary>
        /// 측면 스탠드 한 장 {id}_Stand (왼쪽을 보는 3/4 옆면, 오른쪽은 좌우 반전 — 카구츠치 2페이즈 방식, Docs/Design/Sprite_Rules_v0.1.md 3장).
        /// 그림 픽셀 측정값 (0 = 스탠드 없음, 기존 {id}_Idle 사용): 몸 키 (머리카락 끝 ~ 발끝) px · 발끝이 그림 아래에서 뜬 px · 두 발 가운데 x px
        /// </summary>
        public float standBodyPx, standFootPx, standFootX;
        /// <summary>
        /// 측면 달리기 한 줄 시트 {id}_RunSide (왼쪽 보기, 정사각 칸 8프레임, 2026-11-18 v18): 착지 프레임 발끝이 칸 아래에서 뜬 px (0 = 없음, 기존 {id}_Run 사용)
        /// </summary>
        public float runSideFootPx;
    }

    /// <summary>
    /// [임시] 달리기 시트 크기: 월드 1유닛당 px. 스탠드 (머리 끝 ~ 턱 약 365px = 몸 1253px ↔ 1.62유닛, 773.5px/유닛) 와 달리기 프레임 (머리 약 138px) 의
    /// 머리 높이를 맞춘 눈대중 값 = 773.5 × 138/365 ≈ 291 → 칸 512px = 1.76유닛, 몸 약 454px = 1.56유닛. Play 로 보고 조정
    /// </summary>
    const float RunSidePpu = 291f;

    /// <summary>
    /// 연출용 포즈 한 장 (2026-11-21 사용자: 블레이징 스톰 연출에 쓰는 에른 포즈들). 전부 왼쪽을 보는 한 장 그림, 오른쪽은 좌우 반전 (뒷모습 · 검 뻗기는 검도 같이 뒤집힘).
    /// 이름 = 파일 이름에서 "ErnStray_" 뺀 것. scale = 포즈별 크기 배율 (1 = 기본, 사용자 2026-11-22 조정). 발 위치는 그림 픽셀 측정값 (알파 > 32): footPx = 가장 낮은 발끝이 그림 아래에서 뜬 px,
    /// footX = 맨 아래 8% 띠의 알파 무게중심 x (두 발 가운데). ShowPose("Atk_SlashReady") 처럼 이름으로 부름
    /// </summary>
    static readonly (string name, float footPx, float footX, float scale)[] ErnPoses =
    {
        ("Atk_SlashReady", 73f, 811f, 1.0f),
        ("Atk_HorizontalSlashReady", 90f, 774f, 1.0f),
        ("UltCharge", 18f, 1177f, 1.15f),
        ("OverheadRaise", 164f, 573f, 1.5f),
        ("Atk_VerticalSlash", 99f, 903f, 1.1f),
        ("Atk_OneHandSlash", 90f, 818f, 1.0f),
        ("Atk_HorizontalSlash", 72f, 534f, 1.2f),
        ("RearReach", 24f, 704f, 1.2f),
        ("RisingSlash_02", 136f, 510f, 1.6f),   // 2026-11-22 오버 드라이브 플레임 슬래시 내려베기. 1024 정사각 그림, 측정 (알파 > 32). 같은 날 비율 보정본으로 교체 (발 위치 · 기준점 같음 136 · 509.7, 몸 키 585 → 607px, 이후 색감 보정본 (v3) 으로 교체: 크기 · 발 위치 동일). 크기 배율 2.0 → 1.6 → 1.2 → 1.6 (2026-11-22)
    };
    /// <summary>연출 포즈 이름 목록 (디버그 맵의 플레이어 그림 메뉴)</summary>
    public static System.Collections.Generic.IEnumerable<string> PoseNames { get { foreach (var p in ErnPoses) yield return p.name; } }

    /// <summary>
    /// [임시] 연출 포즈 크기 (월드 1유닛당 px): 8장을 같은 배율로 맞춰 머리 크기가 거의 같게 보이는지 비교해 정함 (스탠드는 773.5, 달리기 291 은 칸이 달라 별개).
    /// 기 모으기 포즈는 머리카락 폭 기준 734 였음. Play 로 보고 포즈별로 어긋나면 ErnPoses 에 배율을 넣을 것
    /// </summary>
    const float PosePpu = 750f;

    /// <summary>
    /// 스탠드 · 연출 포즈를 1/3 로 줄인 복사본 ({이름}_s3.png, 알파 곱한 색으로 줄임) 사용 (2026-11-22 사용자 합의: 원본을 밉맵 + 무압축으로 두면 +60MB,
    /// 줄인 복사본 + 밉맵이면 모습은 거의 같고 용량은 약 1/9). false 로 바꾸면 원본 그림으로 즉시 되돌아감. 복사본이 없으면 자동으로 원본 사용.
    /// 복사본을 쓰면 그림 픽셀 좌표 (발 위치 · 발 가운데) 와 1유닛당 px 를 ShrinkFactor 로 나눠 계산
    /// </summary>
    const bool UseShrunkSprites = true;
    const float ShrinkFactor = 3f;
    const string ShrinkSuffix = "_s3";

    /// <summary>줄인 복사본이 켜져 있고 존재하면 그 그림을, 아니면 원본을 불러옴. 쓴 그림 이름 · 줄인 배율 (원본 = 1) 을 함께 돌려줌</summary>
    static Texture2D LoadShrinkable(string name, out string usedName, out float k)
    {
        if (UseShrunkSprites)
        {
            var small = SpriteLib.Load(name + ShrinkSuffix);
            if (small != null) { usedName = name + ShrinkSuffix; k = ShrinkFactor; return small; }
        }
        usedName = name; k = 1f;
        return SpriteLib.Load(name);
    }
    const string PoseKeyPrefix = "Pose_";

    /// <summary>스탠드 그림의 화면 키 (월드 유닛) — 기존 시트 규격 (몸 200px / 칸 256px × cell 2.07)과 같은 1.62</summary>
    const float StandBodyWorld = 1.62f;
    /// <summary>transform 이 놓이는 발 위 높이 (월드 유닛) — 기존 pivotY 0.372 계산과 같은 0.64</summary>
    const float StandFootToTransform = 0.64f;

    /// <summary>
    /// 에른 스트레이 (2026-11-11 새 리소스 ErnStray_Complete_v2): 정지 4방향 · 달리기 4×8 (10fps) · 대각 내려베기 4×8.
    /// Tools/normalize_character_sheets.py --cell 256 --height 200 --baseline 16 --action-cell 320 로 정렬
    /// (화면 키 약 1.62유닛 · transform = 발 위 0.64유닛 — 이전 에른 · 레니아와 같음).
    ///   정지·달리기: cell 2.07, pivotY = 16/256 + 0.64/2.07 ≈ 0.372
    ///   베기: 칸 320px (검이 칸 밖으로 나가지 않게) → cell 2.07 × 320/256 = 2.5875, pivotY = 16/320 + 0.64/2.5875 ≈ 0.297
    /// </summary>
    public static readonly CharacterVisual ErnStrayVisual = new CharacterVisual
    {
        id = "ErnStray", cell = 2.07f, pivotY = 0.372f, idleCols = 1, runCols = 8, idleFps = 1f, runFps = 10f,
        attackCols = 8, attackCell = 2.5875f, attackPivotY = 0.297f,
        // ErnStray_Stand.png 1199 × 1312: 몸 y 35~1287 (키 1253), 발끝 = 아래 24px, 두 발 가운데 x = (356+713)/2 = 534.5 (2026-11-17 측정)
        standBodyPx = 1253f, standFootPx = 24f, standFootX = 534.5f,
        // ErnStray_RunSide.png 4096 × 512 (칸 512 × 8): 착지 프레임 (1 · 2 · 3 · 5 · 6) 발끝 = 아래 20 ~ 21px, 공중 프레임 (4 · 7 · 8) 은 더 높음 (2026-11-18 측정)
        runSideFootPx = 21f,
    };

    /// <summary>
    /// 레니아 (외형 테스트): Tools/normalize_character_sheets.py 로 정렬한 시트 (칸 256px, 캐릭터 키 200px, 발끝 = 아래 16px).
    /// 에른과 화면 키(약 1.62유닛)·발 기준 transform 높이(발 위 0.64유닛)가 같도록:
    ///   cell = 1.62 / (200/256) ≈ 2.07,  pivotY = 16/256 + 0.64/2.07 ≈ 0.372
    /// 정지 = 방향마다 1장, 이동 = 8프레임 10fps.
    /// 공격 모션 = 스패너 휘두르기 Lenia_Slash 4×8 (2026-11-12, 칸 256px · 발끝 아래 16px 로 이동 시트와 같은 기준이라 cell · pivot 동일).
    /// 레니아의 모든 스킬 사용(투척 · 터렛 설치 · 수동 4종)에 같은 모션
    /// </summary>
    public static readonly CharacterVisual LeniaVisual = new CharacterVisual
    {
        id = "Lenia", cell = 2.07f, pivotY = 0.372f, idleCols = 1, runCols = 8, idleFps = 1f, runFps = 10f,
        attackCols = 8, attackCell = 2.07f, attackPivotY = 0.372f, attackFps = 12f,   // [임시] 원본 10fps → 투척 쿨타임(1.5초)에 맞춰 조금 빠르게
    };

    /// <summary>다음 게임에서 쓸 외형 (타이틀 화면에서 선택). 스킬은 아직 모두 에른 스트레이 것</summary>
    public static CharacterVisual Visual = ErnStrayVisual;
    static string CharacterId => Visual.id;
    /// <summary>공격 모션 (베기 1종). 예전 수평베기 · 수직베기 · 찌르기 3종을 하나로 통합 (2026-11-11)</summary>
    public const string Slash = "Slash";

    /// <summary>
    /// 공격 모션(베기) 재생 (애니메이션만, 판정 없음). dir 이 0이면 현재 바라보는 방향.
    /// 이전 공격 모션이 끝나기 전에는 무시되고 false 를 돌려줍니다 (연타 스킬은 첫 타에만 모션).
    /// </summary>
    public bool PlayAttack(Vector2 dir = default)
    {
        if (anim == null || !anim.Play(Slash, dir)) return false;
        return true;
    }

    /// <summary>
    /// 연출 포즈를 고정 표시 (ReleasePose 로 풀 때까지). 시간 대신 직접 풀게 한 이유: 궁극기 중에는 게임 시간이 느려져 Time.time 기준 유지 시간이 어긋남.
    /// 바라보는 방향은 좌우만 (위 · 아래를 보는 중이면 마지막 좌우 방향). 그림이 없으면 false (기존 모습 그대로)
    /// </summary>
    public bool ShowPose(string name)
    {
        if (anim == null) return false;
        if (Mathf.Abs(Facing.x) > 0.1f) lastPoseDir = new Vector2(Mathf.Sign(Facing.x), 0f);
        return anim.HoldPose(PoseKeyPrefix + name, lastPoseDir, 600f);
    }
    Vector2 lastPoseDir = Vector2.left;

    public void ReleasePose() => anim?.ReleasePose();

    /// <summary>이동 불가를 바로 풀음 (궁극기 종료)</summary>
    public void EndRoot() => rootLeft = 0f;

    /// <summary>디버그 맵: 재생 중인 공격 모션을 끊음 (같은 스킬을 연달아 눌러도 모션이 매번 나오게)</summary>
    public void DebugStopMotion() => anim?.StopOneShot();

    void Awake()
    {
        I = this;
        hp = maxHp;

        // Resources/Sprites/ErnStray_*.png 가 있으면 4방향 스프라이트 애니메이션 사용
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        anim = gameObject.AddComponent<SpriteSheetAnimator>();
        anim.ignoreWorldSlow = true;   // 플레이어 애니메이션은 궁극기 중에도 정상 속도
        var v = Visual;
        float cell = v.cell, pivotY = v.pivotY;
        // 4방향 달리기 · 정지 시트 ({id}_Run · {id}_Idle) 는 있으면 쓰는 기본값. 에른은 이 둘을 2026-11-22 에 뺐고 (측면 달리기 · 스탠드로 대체) 없어도 아래가 이어짐
        bool loaded = anim.LoadClip(SpriteSheetAnimator.Run, CharacterId + "_Run", 4, v.runCols, cell, pivotY, v.runFps, true);
        if (loaded) anim.LoadClip(SpriteSheetAnimator.Idle, CharacterId + "_Idle", 4, v.idleCols, cell, pivotY, v.idleFps, true);
        // 측면 달리기 (있으면 달리기를 이것으로 교체, 오른쪽은 좌우 반전). 없으면 위 {id}_Run (4방향) 그대로
        if (v.runSideFootPx > 0f)
        {
            var runSide = SpriteLib.Load(CharacterId + "_RunSide");
            if (runSide != null)
            {
                float rppu = RunSidePpu;
                float cellPx = runSide.height;   // 정사각 칸
                if (anim.LoadClip(SpriteSheetAnimator.Run, CharacterId + "_RunSide", 1, Mathf.RoundToInt(runSide.width / cellPx),
                                  cellPx / rppu, (v.runSideFootPx + rppu * StandFootToTransform) / cellPx, v.runFps, true,
                                  pivotX: 0.5f, globalSide: false)) loaded = true;
            }
        }
        if (loaded)
        {
            // 측면 스탠드 (있으면 정지 모습만 이것으로 교체, 달리기 · 베기는 기존 4방향 시트). 없으면 위 {id}_Idle 그대로
            if (v.standBodyPx > 0f)
            {
                var stand = LoadShrinkable(CharacterId + "_Stand", out string standName, out float sk);
                if (stand != null)
                {
                    float ppu = v.standBodyPx / StandBodyWorld / sk;   // 이 그림의 1유닛당 px (줄였으면 그만큼 작음)
                    anim.LoadClip(SpriteSheetAnimator.Idle, standName, 1, 1, stand.width / ppu,
                                  (v.standFootPx / sk + ppu * StandFootToTransform) / stand.height, 1f, true,
                                  pivotX: v.standFootX / sk / stand.width, globalSide: false);
                }
            }
            // 연출용 포즈들 (에른 전용 측정값, 그림이 없으면 건너뜀)
            if (CharacterId == "ErnStray")
                foreach (var pose in ErnPoses)
                {
                    var tex = LoadShrinkable(CharacterId + "_" + pose.name, out string poseName, out float pk);
                    if (tex == null) continue;
                    float ppu = PosePpu / pose.scale / pk;   // 포즈별 크기 배율 (사용자 2026-11-22). 배율이 크면 1유닛당 px 가 줄어 크게 보임, 발은 그대로 바닥에 닿음. pk = 줄인 배율
                    anim.LoadClip(PoseKeyPrefix + pose.name, poseName, 1, 1, tex.width / ppu,
                                  (pose.footPx / pk + ppu * StandFootToTransform) / tex.height, 1f, true,
                                  pivotX: pose.footX / pk / tex.width, globalSide: false);
                }
            // 공격 모션은 시트가 있는 캐릭터만 (없으면 LoadClip/LoadFrames 가 건너뜀)
            if (v.attackCols > 0)
                anim.LoadClip(Slash, CharacterId + "_" + Slash, 4, v.attackCols, v.attackCell, v.attackPivotY,
                             v.attackFps > 0f ? v.attackFps : Cfg.attackFps, false);
            transform.localScale = Vector3.one;
            HealthBarOffset = cell * (pivotY - 0.06f) + 0.15f; // 발 바로 아래
        }
        else
        {
            Destroy(anim);
            anim = null;
            sr.sprite = SpriteFactory.Get("Player", SpriteFactory.Shape.Circle);
            sr.color = new Color(0.35f, 0.75f, 1f);
            transform.localScale = Vector3.one * 0.9f;
        }
        baseColor = sr.color;

        HitboxView.Attach(transform, HitboxView.HurtColor, () => radius, () => HurtCenter);
        HitboxView.Attach(transform, HitboxView.PickupColor * new Color(1, 1, 1, 0.5f), () => pickupRadius);
    }

    void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 판정 범위 표시 켜기/끄기
        if (Input.GetKeyDown(KeyCode.F1)) HitboxView.Show = !HitboxView.Show;
#endif
        if (GameManager.I.State != GameState.Playing) return;

        if (anim != null)
            anim.SetClipFps(Slash, Cfg.attackFps);

        float dt = WorldSlow.PlayerDelta;   // 궁극기 중 느려지는 동안에도 플레이어는 정상 속도
        Vector2 beforeMovement = transform.position;
        if (bossSlowLeft > 0f) bossSlowLeft = Mathf.Max(0f, bossSlowLeft - dt);
        if (skillInvuln > 0) skillInvuln -= dt;
        if (rootLeft > 0) rootLeft -= dt;
        if (stunLeft > 0) stunLeft -= dt;

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (bossHoldOwner != null)
        {
            if (anim != null) anim.SetMove(Vector2.zero);
        }
        else if (bossMoveLeft > 0f)
        {
            float step = Mathf.Min(dt, bossMoveLeft);
            transform.position = Vector3.Lerp(transform.position, bossMoveTarget, step / bossMoveLeft);
            bossMoveLeft -= step;
            if (anim != null) anim.SetMove(Vector2.zero);
        }
        else if (dashLeft > 0)
        {
            // 돌진 중: 입력 무시하고 정해진 속도로 이동
            float step = Mathf.Min(dt, dashLeft);
            transform.position += (Vector3)(dashVel * step);
            dashLeft -= dt;
            dashStep?.Invoke();
            if (dashLeft <= 0) { var end = dashEnd; dashStep = dashEnd = null; end?.Invoke(); }
        }
        else if (rootLeft > 0)
        {
            // 이동 불가: 방향 전환만 허용
            if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
            if (anim != null) anim.SetMove(Vector2.zero);
        }
        else
        {
            if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
            transform.position += (Vector3)(input * moveSpeed * dt);
            if (anim != null) anim.SetMove(input);
            else if (Mathf.Abs(input.x) > 0.01f) sr.flipX = input.x < 0;
        }

        // 보스 공격 밀려남
        if (knockVel.sqrMagnitude > 0.01f)
        {
            transform.position += (Vector3)(knockVel * dt);
            knockVel = Vector2.Lerp(knockVel, Vector2.zero, Enemy.KnockDecay * dt);
        }

        // 중간보스 포위 고블린 사이로 지나갈 수 없음 (돌진 포함)
        MidBossArena.ConstrainPlayer(this);
        KagutsuchiSolarArena.ConstrainPlayer(this, beforeMovement);
        KagutsuchiPillarField.ConstrainPlayer(this, beforeMovement);   // 특수 패턴 2 돌기둥

        // 제한된 맵 (레이드): 피격 판정 원이 경계 밖으로 나가지 않음 (돌진 · 밀려남 포함)
        if (Stage.Bounded)
        {
            Vector2 c = HurtCenter, inside = Stage.ClampInside(c, radius);
            if (inside != c) transform.position += (Vector3)(inside - c);
        }

        // 스킬 입력 [임시 키: SkillTuning]
        var T = Tuning.Skills;
        for (int i = 0; i < T.manualKeys.Length; i++)
            if (Input.GetKeyDown(T.manualKeys[i])) ManualSlot(i)?.TryActivate();
        if (Input.GetKeyDown(T.movementKey)) FirstOfKind(SkillKind.Movement)?.TryActivate();
        if (Input.GetKeyDown(T.ultimateKey)) FirstOfKind(SkillKind.Ultimate)?.TryActivate();

        if (regen > 0) Heal(regen * dt);

        if (invuln > 0)
        {
            invuln -= dt;
            // 피격 시 깜빡임 (실제 스프라이트는 흰색이라 투명도로 표현)
            sr.color = Mathf.Repeat(invuln * 20f, 1f) > 0.5f ? new Color(1f, 0.55f, 0.55f, 0.45f) : baseColor;
            if (invuln <= 0) sr.color = baseColor;
        }

        // 스킬 쿨타임/자동 발동 (획득·강화로 목록이 바뀔 수 있어 인덱스로 순회)
        for (int i = 0; i < skills.Count; i++) skills[i].Tick(dt);

        // 스킬 무적 표시: 살짝 밝게
        if (invuln <= 0) sr.color = IsInvulnerable ? new Color(1f, 0.85f, 0.6f, 0.8f)
                                   : IsStunned ? new Color(1f, 0.95f, 0.45f) : baseColor;   // 기절 = 노랑
    }

    /// <param name="ignoreHitInvuln">피격 직후 0.35초 무적을 무시 (빠른 연타 보스 공격 — 가니악 연속 베기). 치트 · 스킬 무적은 그대로 막음</param>
    public void TakeDamage(float dmg, bool ignoreHitInvuln = false)
    {
        if (DebugCheats.God || (invuln > 0 && !ignoreHitInvuln) || IsInvulnerable || GameManager.I.State != GameState.Playing) return;
        if (CounterHit()) return;
        dmg *= damageTakenMul;
        if (shield > 0f)   // 보호막이 먼저 받아 냄
        {
            float absorbed = Mathf.Min(shield, dmg);
            shield -= absorbed;
            dmg -= absorbed;
        }
        hp -= dmg;
        invuln = 0.35f;
        CameraFollow.Shake(0.12f, 0.15f);
        if (hp <= 0)
        {
            hp = 0;
            GameManager.I.GameOver();
        }
    }

}
