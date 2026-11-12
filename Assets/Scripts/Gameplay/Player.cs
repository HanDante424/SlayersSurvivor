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

    [Header("업그레이드 배율")]
    public float moveSpeedMul = 1f;
    public float pickupMul = 1f;

    // 감각 조정 값은 설정 파일에서 (Assets/Resources/Config/ErnStrayConfig.asset)
    static PlayerConfig Cfg => Tuning.ErnStray;
    public float moveSpeed => Cfg.moveSpeed * moveSpeedMul;
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
    public bool IsBusy => dashLeft > 0 || rootLeft > 0;
    public bool IsInvulnerable => skillInvuln > 0 || dashLeft > 0 && dashInvuln;
    bool dashInvuln;

    public void SetInvulnerable(float seconds) => skillInvuln = Mathf.Max(skillInvuln, seconds);
    public void Root(float seconds) => rootLeft = Mathf.Max(rootLeft, seconds);

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
    }

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

    // 기본 공격 화염 이펙트: ErnStray_FireSlash 2행 x 8열 (16프레임)
    // 치명타여도 같은 이펙트 (치명타 전용 이펙트 교체는 취소됨)
    // 타이밍·거리·크기·속도는 설정 파일에서 조정
    Sprite[] fxSlash;
    float fxTimer = -1f;

    /// <summary>
    /// 공격 모션(베기) 재생 (애니메이션만, 판정 없음). dir 이 0이면 현재 바라보는 방향.
    /// 이전 공격 모션이 끝나기 전에는 무시되고 false 를 돌려줍니다 (연타 스킬은 첫 타에만 모션).
    /// baseFx = true 면 기본 공격 화염 이펙트를 함께 출력 (스킬 전용 이펙트가 없을 때만 켬).
    /// </summary>
    public bool PlayAttack(Vector2 dir = default, bool baseFx = false)
    {
        if (anim == null || !anim.Play(Slash, dir)) return false;
        if (baseFx) fxTimer = Cfg.fxDelay;
        return true;
    }

    void SpawnSlashFx()
    {
        var frames = fxSlash;
        if (frames == null) return;

        // 이펙트 원본은 오른쪽으로 휘두르는 모양 → 방향에 맞게 회전/반전
        float angle = 0f; bool flip = false; Vector2 dir;
        switch (anim.Facing)
        {
            case Facing4.Left:  dir = Vector2.left;  flip = true; break;
            case Facing4.Up:    dir = Vector2.up;    angle = 90f; break;
            case Facing4.Down:  dir = Vector2.down;  angle = -90f; break;
            default:            dir = Vector2.right; break;
        }
        // fxOffset 은 오른쪽 기준 → 바라보는 방향에 맞춰 회전 (왼쪽은 좌우 반전)
        var o = Cfg.fxOffset;
        Vector2 offset = anim.Facing == Facing4.Left ? new Vector2(-o.x, o.y)
                       : (Vector2)(Quaternion.Euler(0, 0, angle) * o);
        SpriteEffect.Spawn(frames, Cfg.fxFps, transform,
                           dir * Cfg.fxDistance + offset, angle, flip, 11, Cfg.fxSize);
    }

    void Awake()
    {
        I = this;
        hp = maxHp;

        // Resources/Sprites/ErnStray_*.png 가 있으면 4방향 스프라이트 애니메이션 사용
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        anim = gameObject.AddComponent<SpriteSheetAnimator>();
        var v = Visual;
        float cell = v.cell, pivotY = v.pivotY;
        if (anim.LoadClip(SpriteSheetAnimator.Run, CharacterId + "_Run", 4, v.runCols, cell, pivotY, v.runFps, true))
        {
            anim.LoadClip(SpriteSheetAnimator.Idle, CharacterId + "_Idle", 4, v.idleCols, cell, pivotY, v.idleFps, true);
            // 공격 모션 · 기본 공격 이펙트는 시트가 있는 캐릭터만 (없으면 LoadClip/LoadFrames 가 건너뜀)
            if (v.attackCols > 0)
                anim.LoadClip(Slash, CharacterId + "_" + Slash, 4, v.attackCols, v.attackCell, v.attackPivotY,
                             v.attackFps > 0f ? v.attackFps : Cfg.attackFps, false);
            // 크기는 Spawn 때 설정 값으로 맞추므로 1유닛 기준으로 자름
            fxSlash = SpriteEffect.LoadFrames(CharacterId + "_FireSlash", 2, 8, 1f);
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

        float dt = Time.deltaTime;
        if (skillInvuln > 0) skillInvuln -= dt;
        if (rootLeft > 0) rootLeft -= dt;

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();

        if (dashLeft > 0)
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

        // 스킬 입력 [임시 키: SkillTuning]
        var T = Tuning.Skills;
        for (int i = 0; i < T.manualKeys.Length; i++)
            if (Input.GetKeyDown(T.manualKeys[i])) ManualSlot(i)?.TryActivate();
        if (Input.GetKeyDown(T.movementKey)) FirstOfKind(SkillKind.Movement)?.TryActivate();
        if (Input.GetKeyDown(T.ultimateKey)) FirstOfKind(SkillKind.Ultimate)?.TryActivate();

        if (fxTimer >= 0f)
        {
            fxTimer -= Time.deltaTime;
            if (fxTimer < 0f) SpawnSlashFx();
        }

        if (regen > 0) hp = Mathf.Min(maxHp, hp + regen * Time.deltaTime);

        if (invuln > 0)
        {
            invuln -= Time.deltaTime;
            // 피격 시 깜빡임 (실제 스프라이트는 흰색이라 투명도로 표현)
            sr.color = Mathf.Repeat(invuln * 20f, 1f) > 0.5f ? new Color(1f, 0.55f, 0.55f, 0.45f) : baseColor;
            if (invuln <= 0) sr.color = baseColor;
        }

        // 스킬 쿨타임/자동 발동 (획득·강화로 목록이 바뀔 수 있어 인덱스로 순회)
        for (int i = 0; i < skills.Count; i++) skills[i].Tick(dt);

        // 스킬 무적 표시: 살짝 밝게
        if (invuln <= 0) sr.color = IsInvulnerable ? new Color(1f, 0.85f, 0.6f, 0.8f) : baseColor;
    }

    public void TakeDamage(float dmg)
    {
        if (DebugCheats.God || invuln > 0 || IsInvulnerable || GameManager.I.State != GameState.Playing) return;
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
