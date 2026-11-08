using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    static CameraFollow inst;
    float shakeTime, shakeAmp;
    Transform ground;

    void Awake()
    {
        inst = this;
        var g = new GameObject("Ground");
        var sr = g.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("Ground", SpriteFactory.Shape.Ground);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(60, 40);
        sr.sortingOrder = -100;
        ground = g.transform;
    }

    public static void Shake(float time, float amp)
    {
        if (inst == null) return;
        inst.shakeTime = Mathf.Max(inst.shakeTime, time);
        inst.shakeAmp = Mathf.Max(inst.shakeAmp, amp);
    }

    void LateUpdate()
    {
        if (Player.I == null) return;
        Vector3 p = Player.I.transform.position;
        Vector3 offset = Vector3.zero;
        if (shakeTime > 0)
        {
            shakeTime -= Time.unscaledDeltaTime;
            offset = (Vector3)Random.insideUnitCircle * shakeAmp;
            if (shakeTime <= 0) shakeAmp = 0;
        }
        transform.position = new Vector3(p.x, p.y, -10f) + offset;
        // 바닥 타일을 1칸 단위로 따라오게 해서 무한 맵처럼 보이게
        ground.position = new Vector3(Mathf.Round(p.x), Mathf.Round(p.y), 0f);
    }
}
