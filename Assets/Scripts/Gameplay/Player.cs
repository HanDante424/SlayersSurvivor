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

    float invuln;
    SpriteRenderer sr;
    Color baseColor;

    void Awake()
    {
        I = this;
        hp = maxHp;
        sr = SpriteFactory.AddRenderer(gameObject, "Player", SpriteFactory.Shape.Circle, new Color(0.35f, 0.75f, 1f), 10);
        baseColor = sr.color;
        transform.localScale = Vector3.one * 0.9f;
    }

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;

        var input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        if (input.sqrMagnitude > 1f) input.Normalize();
        if (input.sqrMagnitude > 0.01f) Facing = input.normalized;
        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
        if (Mathf.Abs(input.x) > 0.01f) sr.flipX = input.x < 0;

        if (regen > 0) hp = Mathf.Min(maxHp, hp + regen * Time.deltaTime);

        if (invuln > 0)
        {
            invuln -= Time.deltaTime;
            sr.color = Mathf.Repeat(invuln * 20f, 1f) > 0.5f ? Color.white : baseColor;
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
