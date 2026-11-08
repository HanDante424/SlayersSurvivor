using System.Collections.Generic;
using UnityEngine;

public class Gem : MonoBehaviour
{
    public static readonly List<Gem> All = new List<Gem>();
    public int value;
    bool attracted;
    float speed;

    public static void Spawn(Vector2 pos, int value)
    {
        var go = new GameObject("Gem");
        go.transform.position = pos;
        var g = go.AddComponent<Gem>();
        g.value = value;
        Color c = value >= 20 ? new Color(1f, 0.3f, 0.3f) : value >= 5 ? new Color(0.3f, 1f, 0.5f) : new Color(0.3f, 0.7f, 1f);
        SpriteFactory.AddRenderer(go, "Gem", SpriteFactory.Shape.Diamond, c, 2);
        go.transform.localScale = Vector3.one * (value >= 20 ? 0.45f : value >= 5 ? 0.35f : 0.28f);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public void Attract() => attracted = true;

    void Update()
    {
        if (GameManager.I.State != GameState.Playing) return;
        var p = Player.I;
        Vector2 to = p.transform.position - transform.position;
        float d = to.magnitude;
        if (!attracted && d < p.pickupRadius) attracted = true;
        if (attracted)
        {
            speed += 25f * Time.deltaTime;
            transform.position += (Vector3)(to.normalized * Mathf.Min(speed * Time.deltaTime, d));
            if (d < 0.3f)
            {
                GameManager.I.AddXp(value);
                Destroy(gameObject);
            }
        }
    }
}
