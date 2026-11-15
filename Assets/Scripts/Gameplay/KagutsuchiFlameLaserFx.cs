using UnityEngine;

/// <summary>RaidLaser의 판정·수명은 그대로 두고 카구츠치 화염 빔의 표시만 교체.</summary>
public sealed class KagutsuchiFlameLaserFx : MonoBehaviour
{
    const int FrameCount = 6, CellWidth = 128, CellHeight = 2048;
    const float VisibleWidth = 80f, VisibleHeight = 1731f;
    const float Fps = 12f; // [임시] 제작된 미리보기와 같은 반복 속도.
    const int VisibleLeft = 24, VisibleBottom = 159;
    Sprite[] frames;
    SpriteRenderer source, visual;
    SpriteRenderer[] original;
    float age;

    public static void Attach(RaidLaser laser)
    {
        var texture = Resources.Load<Texture2D>("Sprites/Kagutsuchi_P2_Fx_FlameLaser");
        if (laser == null || texture == null || texture.width != CellWidth * FrameCount || texture.height != CellHeight) return;
        var fx = laser.gameObject.AddComponent<KagutsuchiFlameLaserFx>();
        fx.original = laser.GetComponentsInChildren<SpriteRenderer>();
        if (fx.original.Length == 0) { Destroy(fx); return; }
        // Line 생성 직후 첫 표시가 코어이며 크기·알파는 RaidLaser가 계속 갱신한다.
        fx.source = fx.original[0];
        fx.frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            fx.frames[i] = Sprite.Create(texture, new Rect(i * CellWidth + VisibleLeft, VisibleBottom, VisibleWidth, VisibleHeight),
                                         new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        var go = new GameObject("FlameLaserVisual");
        go.transform.SetParent(laser.transform, false);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);
        fx.visual = go.AddComponent<SpriteRenderer>();
        fx.visual.sortingLayerID = fx.source.sortingLayerID;
        fx.visual.sortingOrder = fx.source.sortingOrder;
        foreach (var renderer in fx.original) renderer.enabled = false;
        fx.Apply();
    }

    void Apply()
    {
        var size = source.transform.localScale;
        visual.transform.localScale = new Vector3(size.y / VisibleWidth, size.x / VisibleHeight, 1f);
        visual.sprite = frames[(int)(age * Fps) % FrameCount];
        visual.color = new Color(1f, 1f, 1f, source.color.a);
    }

    void LateUpdate()
    {
        if (source == null || visual == null) return;
        if (GameManager.I != null && GameManager.I.State == GameState.Playing) age += Time.deltaTime;
        Apply(); // 회전·길이·폭 펼침·페이드는 부모 RaidLaser의 최신 표시를 따른다.
    }

    void OnDestroy()
    {
        if (original != null) foreach (var renderer in original) if (renderer != null) renderer.enabled = true;
        if (visual != null) Destroy(visual.gameObject);
        if (frames != null) foreach (var frame in frames) if (frame != null) Destroy(frame);
    }
}
