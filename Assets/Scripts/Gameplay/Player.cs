using System.Collections.Generic;
using UnityEngine;

public class Player : MonoBehaviour
{
    public static Player I;

    [Header("기본 스탯")]
    public float maxHp = 100f;
    public float hp;
    public float moveSpeed = 4.5f;
    public float regen = 0f;            // 초당 회복
    public float pickupRadius = 2.0f;
    public float damageMul = 1f;
    public float cooldownMul = 1f;
    public float areaMul = 1f;
    public float radius = 0.4f;

    public readonly List<Weapon> weapons = new List<Weapon>();
    public Vector2 Facing { get; private set; } = Vector2.right;

    /// <summary>체력 바를 그릴 위치 (플레이어 기준 아래쪽 거리)</summary>
    public float HealthBarOffset { get; private set; } = 0.65f;

    float invuln;
    SpriteRenderer sr;
    Color baseColor;
    SpriteSheetAnimator anim;

    void Awake()
    {
        I = this;
        hp = maxHp;

        // Resources/Sprites/Player_Run.png 가 있으면 4방향 달리기 애니메이션 사용
        sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sortingOrder = 10;
        anim = gameObject.AddComponent<SpriteSheetAnimator>();
        const float cell = 1.9f, pivotY = 0.4f;
        if (anim.Load("Player_Run", 4, 8, cell, pivotY))
        {
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
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();
        if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
        if (anim != null) anim.SetMove(input);
        else if (Mathf.Abs(input.x) > 0.01f) sr.flipX = input.x < 0;

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
        if (invuln > 0 || GameManager.I.State != GameState.Playing) return;
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
