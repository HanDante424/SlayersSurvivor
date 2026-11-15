using UnityEngine;

/// <summary>특수 패턴 2의 직선 베기 표시. 판정과 수명은 기존 RaidLaser가 소유.</summary>
public sealed class KagutsuchiStraightSlashFx : MonoBehaviour
{
    const int FrameCount = 6, CellWidth = 1536, CellHeight = 160;
    Sprite[] frames;
    SpriteRenderer source, visual;
    SpriteRenderer[] original;
    float age, duration;

    public static void Attach(RaidLaser laser, float seconds)
    {
        var texture = Resources.Load<Texture2D>("Sprites/Kagutsuchi_Fx_StraightSlash");
        if (laser == null || texture == null || texture.width != CellWidth || texture.height != CellHeight * FrameCount) return;
        var fx = laser.gameObject.AddComponent<KagutsuchiStraightSlashFx>();
        fx.original = laser.GetComponentsInChildren<SpriteRenderer>();
        if (fx.original.Length == 0) { Destroy(fx); return; }
        // Line 직후 첫 렌더러는 판정 길이·폭·페이드를 따라가는 코어 표시다.
        fx.source = fx.original[0];
        fx.duration = Mathf.Max(0.0001f, seconds);
        fx.frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            fx.frames[i] = Sprite.Create(texture, new Rect(0f, (FrameCount - 1 - i) * CellHeight, CellWidth, CellHeight),
                                         new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        var go = new GameObject("StraightSlashVisual");
        go.transform.SetParent(laser.transform, false);
        fx.visual = go.AddComponent<SpriteRenderer>();
        fx.visual.sortingLayerID = fx.source.sortingLayerID;
        fx.visual.sortingOrder = fx.source.sortingOrder;
        foreach (var renderer in fx.original) renderer.enabled = false;
        fx.Apply();
    }

    void Apply()
    {
        var size = source.transform.localScale;
        visual.transform.localScale = new Vector3(size.x / CellWidth, size.y / CellHeight, 1f);
        visual.sprite = frames[Mathf.Min(FrameCount - 1, (int)(age / duration * FrameCount))];
        visual.color = new Color(1f, 1f, 1f, source.color.a);
    }

    void LateUpdate()
    {
        if (source == null || visual == null) return;
        if (GameManager.I != null && GameManager.I.State == GameState.Playing) age += Time.deltaTime;
        Apply();
    }

    void OnDestroy()
    {
        if (original != null) foreach (var renderer in original) if (renderer != null) renderer.enabled = true;
        if (visual != null) Destroy(visual.gameObject);
        if (frames != null) foreach (var frame in frames) if (frame != null) Destroy(frame);
    }
}
