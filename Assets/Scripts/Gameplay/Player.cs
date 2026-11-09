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

    public readonly List<Weapon> weapons = new List<Weapon>();
    public Vector2 Facing { get; private set; } = Vector2.right;

    /// <summary>체력 바를 그릴 위치 (플레이어 기준 아래쪽 거리)</summary>
    public float HealthBarOffset { get; private set; } = 0.65f;

    float invuln;
    SpriteRenderer sr;
    Color baseColor;
    SpriteSheetAnimator anim;

    // 캐릭터 스프라이트 시트: Resources/Sprites/{CharacterId}_{동작}.png
    const string CharacterId = "ErnStray";
    public const string SlashH = "SlashH";   // 수평베기
    public const string SlashV = "SlashV";   // 수직베기
    public const string Thrust = "Thrust";   // 찌르기
    static readonly string[] AttackClips = { SlashH, SlashV, Thrust };

    // 기본 공격(수평베기) 화염 이펙트: 일반 / 치명타
    //  - 일반   ErnStray_FireSlash     2행 x 8열 (16프레임)
    //  - 치명타 ErnStray_FireSlashCrit 4행 x 6열 (24프레임)
    // 타이밍·거리·크기·속도는 설정 파일에서 조정
    Sprite[] fxSlash, fxSlashCrit;
    float fxTimer = -1f;
    bool fxCrit;

    /// <summary>
    /// 공격 모션 재생 (애니메이션만, 판정 없음). dir 이 0이면 현재 바라보는 방향.
    /// 이전 공격 모션이 끝나기 전에는 무시되고 false 를 돌려줍니다.
    /// 수평베기는 치명타 판정 후 일반/치명타 화염 이펙트를 함께 출력합니다.
    /// </summary>
    public bool PlayAttack(string clip, Vector2 dir = default)
    {
        if (anim == null || !anim.Play(clip, dir)) return false;
        if (clip == SlashH)
        {
            fxCrit = RollCrit();
            fxTimer = Cfg.fxDelay;
        }
        return true;
    }

    void SpawnSlashFx(bool crit)
    {
        var frames = crit ? fxSlashCrit : fxSlash;
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
        SpriteEffect.Spawn(frames, crit ? Cfg.fxCritFps : Cfg.fxFps, transform,
                           dir * Cfg.fxDistance + offset, angle, flip, 11,
                           crit ? Cfg.fxCritSize : Cfg.fxSize);
    }

    void Awake()
    {
        I = this;
        hp = maxHp;

        // Resources/Sprites/ErnStray_*.png 가 있으면 4방향 스프라이트 애니메이션 사용
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        anim = gameObject.AddComponent<SpriteSheetAnimator>();
        const float cell = 1.9f, pivotY = 0.4f;
        if (anim.LoadClip(SpriteSheetAnimator.Run, CharacterId + "_Run", 4, 8, cell, pivotY, 12f, true))
        {
            anim.LoadClip(SpriteSheetAnimator.Idle, CharacterId + "_Idle", 4, 8, cell, pivotY, 8f, true);
            foreach (var key in AttackClips)
                anim.LoadClip(key, CharacterId + "_" + key, 4, 8, cell, pivotY, Cfg.attackFps, false);
            // 크기는 Spawn 때 설정 값으로 맞추므로 1유닛 기준으로 자름
            fxSlash = SpriteEffect.LoadFrames(CharacterId + "_FireSlash", 2, 8, 1f);
            fxSlashCrit = SpriteEffect.LoadFrames(CharacterId + "_FireSlashCrit", 4, 6, 1f);
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
            foreach (var key in AttackClips) anim.SetClipFps(key, Cfg.attackFps);

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();
        if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
        if (anim != null) anim.SetMove(input);
        else if (Mathf.Abs(input.x) > 0.01f) sr.flipX = input.x < 0;

#if UNITY_EDITOR
        // 애니메이션 확인용 (에디터 전용): J 수평베기 / K 수직베기 / L 찌르기
        if (Input.GetKeyDown(KeyCode.J)) PlayAttack(SlashH);
        if (Input.GetKeyDown(KeyCode.K)) PlayAttack(SlashV);
        if (Input.GetKeyDown(KeyCode.L)) PlayAttack(Thrust);
#endif

        if (fxTimer >= 0f)
        {
            fxTimer -= Time.deltaTime;
            if (fxTimer < 0f) SpawnSlashFx(fxCrit);
        }

        if (regen > 0) hp = Mathf.Min(maxHp, hp + regen * Time.deltaTime);

        if (invuln > 0)
        {
            invuln -= Time.deltaTime;
            // 피격 시 깜빡임 (실제 스프라이트는 흰색이라 투명도로 표현)
            sr.color = Mathf.Repeat(invuln * 20f, 1f) > 0.5f ? new Color(1f, 0.55f, 0.55f, 0.45f) : baseColor;
            if (invuln <= 0) sr.color = baseColor;
        }

        foreach (var w in weapons) w.Tick(Time.deltaTime);
    }

    public void TakeDamage(float dmg)
    {
        if (DebugCheats.God || invuln > 0 || GameManager.I.State != GameState.Playing) return;
        hp -= dmg;
        invuln = 0.35f;
        CameraFollow.Shake(0.12f, 0.15f);
        if (hp <= 0)
        {
            hp = 0;
            GameManager.I.GameOver();
        }
    }

    public T GetWeapon<T>() where T : Weapon
    {
        foreach (var w in weapons) if (w is T t) return t;
        return null;
    }

    public void AddWeapon<T>() where T : Weapon
    {
        var existing = GetWeapon<T>();
        if (existing != null) { existing.LevelUp(); return; }
        var go = new GameObject(typeof(T).Name);
        go.transform.SetParent(transform, false);
        var w = go.AddComponent<T>();
        weapons.Add(w);
    }
}
