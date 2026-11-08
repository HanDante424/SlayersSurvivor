using UnityEngine;

/// <summary>떠오르는 데미지 숫자 (TextMesh 사용, 추가 패키지 불필요)</summary>
public class DamageText : MonoBehaviour
{
    float life = 0.5f;
    TextMesh tm;
    static int active;
    static Font font;
    const int MaxActive = 60;

    public static void Show(Vector2 pos, float dmg)
    {
        if (active >= MaxActive) return;
        var go = new GameObject("Dmg");
        go.transform.position = pos + new Vector2(Random.Range(-0.2f, 0.2f), 0.3f);
        var t = go.AddComponent<DamageText>();
        t.tm = go.AddComponent<TextMesh>();
        t.tm.text = Mathf.RoundToInt(dmg).ToString();
        t.tm.characterSize = 0.08f;
        t.tm.fontSize = 48;
        t.tm.anchor = TextAnchor.MiddleCenter;
        t.tm.color = Color.white;
        if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.tm.font = font;
        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = font.material;
        mr.sortingOrder = 50;
    }

    void OnEnable() => active++;
    void OnDisable() => active--;

    void Update()
    {
        life -= Time.deltaTime;
        transform.position += Vector3.up * 1.5f * Time.deltaTime;
        var c = tm.color; c.a = Mathf.Clamp01(life / 0.3f); tm.color = c;
        if (life <= 0) Destroy(gameObject);
    }
}
