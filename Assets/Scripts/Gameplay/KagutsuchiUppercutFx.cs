using UnityEngine;

/// <summary>2페이즈 어퍼컷 풍압. 실행기 소유의 앞뒤 표시를 재사용하며 전투 판정은 만들지 않는다.</summary>
public sealed class KagutsuchiUppercutFx : MonoBehaviour
{
    const int FrameCount = 6, CellWidth = 512, CellHeight = 768;
    const float Duration = 0.5f;
    const float VisualScale = 3f; // 사용자: 앞뒤 어퍼컷 풍압을 기존 크기의3배로 확대.
    const float GroundPixelY = 652f, PreviewBossHeight = 420f; // 사용자 승인: 제작 미리보기의 발 원점과 크기 비율.
    const int BackOrderOffset = -2, FrontOrderOffset = 1;
    const string BackSheet = "Sprites/Kagutsuchi_P2_Fx_UppercutWindDepth_v9_Back";
    const string FrontSheet = "Sprites/Kagutsuchi_P2_Fx_UppercutWindDepth_v9_Front";
    Enemy owner;
    SpriteRenderer bodyVisual, backVisual, frontVisual;
    Sprite[] backFrames, frontFrames;
    GameObject root;
    float age;
    bool active;

    void Awake()
    {
        owner = GetComponent<Enemy>();
        bodyVisual = GetComponent<SpriteRenderer>();
        var back = Resources.Load<Texture2D>(BackSheet);
        var front = Resources.Load<Texture2D>(FrontSheet);
        if (owner == null || bodyVisual == null || back == null || front == null
            || back.width != FrameCount * CellWidth || front.width != FrameCount * CellWidth
            || back.height != CellHeight || front.height != CellHeight)
        {
            Debug.LogError("어퍼컷 풍압 v9의 앞뒤 시트 또는 본체가 없거나 시트가3072×768 규격과 다릅니다.");
            return;
        }
        backFrames = new Sprite[FrameCount];
        frontFrames = new Sprite[FrameCount];
        // 원화는 좌상단 좌표, Unity 피벗은 좌하단 좌표. 같은 발 원점을 앞뒤에 공유한다.
        var pivot = new Vector2(0.5f, 1f - GroundPixelY / CellHeight);
        for (int i = 0; i < FrameCount; i++)
        {
            var rect = new Rect(i * CellWidth, 0f, CellWidth, CellHeight);
            backFrames[i] = Sprite.Create(back, rect, pivot, PreviewBossHeight, 0, SpriteMeshType.FullRect);
            frontFrames[i] = Sprite.Create(front, rect, pivot, PreviewBossHeight, 0, SpriteMeshType.FullRect);
        }
        root = new GameObject("KagutsuchiUppercutWind");
        var backObject = new GameObject("BackWind");
        backObject.transform.SetParent(root.transform, false);
        backVisual = backObject.AddComponent<SpriteRenderer>();
        var frontObject = new GameObject("FrontParticles");
        frontObject.transform.SetParent(root.transform, false);
        frontVisual = frontObject.AddComponent<SpriteRenderer>();
        root.SetActive(false);
    }

    public void Play(Vector2 bossPosition, bool facingRight)
    {
        if (root == null || bodyVisual == null || bodyVisual.sprite == null) return;
        var bounds = bodyVisual.sprite.bounds;
        // 모든 2페이즈 포즈는 본체와 같은 키·발 피벗. 잡기에서 본체 렌더러가 숨겨져도 같은 기준을 쓴다.
        float bossHeight = bounds.size.y * Mathf.Abs(bodyVisual.transform.lossyScale.y);
        Vector2 footOffset = bodyVisual.transform.TransformVector(new Vector3(0f, bounds.min.y, 0f));
        root.transform.position = bossPosition + footOffset;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one * (bossHeight * VisualScale);
        backVisual.sortingLayerID = frontVisual.sortingLayerID = bodyVisual.sortingLayerID;
        backVisual.sortingOrder = bodyVisual.sortingOrder + BackOrderOffset;
        frontVisual.sortingOrder = bodyVisual.sortingOrder + FrontOrderOffset;
        backVisual.color = frontVisual.color = Color.white;
        backVisual.flipX = frontVisual.flipX = facingRight;
        backVisual.flipY = frontVisual.flipY = false;
        backVisual.enabled = frontVisual.enabled = true;
        age = 0f;
        active = true;
        SetFrame(0);
        root.SetActive(true);
    }

    void SetFrame(int frame)
    {
        backVisual.sprite = backFrames[frame];
        frontVisual.sprite = frontFrames[frame];
    }

    void Update()
    {
        if (!active) return;
        if (owner == null || owner.hp <= 0f || owner.Hidden) { Clear(); return; }
        if (GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) { Clear(); return; }
        if (GameManager.I.State != GameState.Playing) return;
        age += Time.deltaTime;
        if (age >= Duration) Clear();
        else SetFrame(Mathf.Min(FrameCount - 1, (int)(age / Duration * FrameCount)));
    }

    public void Clear()
    {
        active = false;
        age = 0f;
        if (root != null) root.SetActive(false);
    }
    void OnDisable() => Clear();
    void OnDestroy()
    {
        Clear();
        if (root != null) Destroy(root);
        if (backFrames != null) foreach (var frame in backFrames) if (frame != null) Destroy(frame);
        if (frontFrames != null) foreach (var frame in frontFrames) if (frame != null) Destroy(frame);
    }
}
