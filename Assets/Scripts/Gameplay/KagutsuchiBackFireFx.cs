using UnityEngine;

/// <summary>1페이즈 옥좌 뒤의 반복 화염. 보스 AI가 수명을 소유하며 피해 판정은 만들지 않음.</summary>
public sealed class KagutsuchiBackFireFx : MonoBehaviour
{
    const int Columns = 4, Rows = 2, FrameCount = 8, CellWidth = 384, CellHeight = 512;
    const float GroundPivotY = 40f / CellHeight, ContentHeight = 466f; // 원화 alpha>16 밑단·최대 높이 실측.
    const float FramesPerSecond = 12.5f; // [임시] 제작 GIF의 80ms/프레임 재사용.
    const float HeightScale = 1.25f; // [임시] 옥좌 위·양옆으로 보이도록 본체 높이의 125%.
    const int OrderBehindBody = 2; // 본체 바로 뒤 외곽선보다도 아래.
    Enemy owner;
    SpriteRenderer source, visual;
    Sprite[] frames;
    GameObject root;
    float age;
    bool disposed;

    public static KagutsuchiBackFireFx Attach(Enemy body)
    {
        var texture = Resources.Load<Texture2D>("Sprites/Kagutsuchi_P1_Fx_BackFire_Loop");
        if (body == null || texture == null || texture.width != Columns * CellWidth || texture.height != Rows * CellHeight)
        {
            Debug.LogError("카구츠치 후방 화염 시트가 없거나 1536×1024 규격과 다릅니다.");
            return null;
        }
        var source = body.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null) return null;
        var fx = body.gameObject.AddComponent<KagutsuchiBackFireFx>();
        fx.owner = body; fx.source = source;
        fx.frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            fx.frames[i] = Sprite.Create(texture,
                new Rect(i % Columns * CellWidth, (Rows - 1 - i / Columns) * CellHeight, CellWidth, CellHeight),
                new Vector2(0.5f, GroundPivotY), ContentHeight, 0, SpriteMeshType.FullRect);
        fx.root = new GameObject("KagutsuchiPhase1BackFire");
        fx.root.transform.SetParent(body.transform, false);
        var bounds = source.sprite.bounds;
        fx.root.transform.localPosition = new Vector3(bounds.center.x, bounds.min.y, 0f);
        fx.root.transform.localScale = Vector3.one * (bounds.size.y * HeightScale);
        fx.visual = fx.root.AddComponent<SpriteRenderer>();
        fx.visual.sprite = fx.frames[0];
        fx.visual.sortingLayerID = source.sortingLayerID;
        fx.visual.sortingOrder = source.sortingOrder - OrderBehindBody;
        fx.root.SetActive(false);
        return fx;
    }

    void LateUpdate()
    {
        if (disposed || root == null) return;
        bool show = owner != null && owner.hp > 0f && !owner.Hidden
            && source != null && source.enabled && RaidStage.Phase == 1 && !RaidStage.InTransition;
        root.SetActive(show);
        if (!show) return;
        if (GameManager.I != null && GameManager.I.State == GameState.Playing)
            age = (age + Time.deltaTime) % (FrameCount / FramesPerSecond);
        visual.sprite = frames[Mathf.Min(FrameCount - 1, (int)(age * FramesPerSecond))];
        visual.color = new Color(1f, 1f, 1f, source.color.a);
    }

    void OnDisable() { if (root != null) root.SetActive(false); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (root != null) { root.SetActive(false); Destroy(root); root = null; }
        if (frames != null) foreach (var frame in frames) if (frame != null) Destroy(frame);
    }
    void OnDestroy() => Dispose();
}
