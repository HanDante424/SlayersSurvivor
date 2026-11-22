using UnityEngine;

/// <summary>원 전체가 보이는 연속 베기 표시. 본체·내부 8개와 프레임을 재사용하고 전투 판정은 소유하지 않음.</summary>
public sealed class KagutsuchiComboSlashFx : MonoBehaviour
{
    const int FrameCount = 6, Columns = 3, Rows = 2, CellSize = 512;
    // 피크 프레임 원 맞춤 반경. 기존 반경에 원화의 크기를 맞추는 표시 보정.
    const float ArtRadiusPixels = 194f;
    const int InnerCount = 8;
    const float InnerMinScale = 0.2f, InnerMaxScale = 0.5f;
    sealed class Ring
    {
        public Transform Transform;
        public SpriteRenderer First, Opposite;
        public void SetFrame(Sprite frame) { First.sprite = Opposite.sprite = frame; }
        public void Reset(bool reverse)
        {
            First.color = Opposite.color = Color.white;
            First.flipX = Opposite.flipX = false;
            First.flipY = Opposite.flipY = reverse;
        }
    }
    readonly Ring[] innerRings = new Ring[InnerCount];
    Ring mainRing;
    Material innerMaterial;
    Sprite[] frames;
    GameObject root;
    float age, duration;
    bool active;

    void Awake()
    {
        var texture = SpriteLib.Load("Kagutsuchi_Fx_ComboSlash");
        if (texture == null || texture.width != Columns * CellSize || texture.height != Rows * CellSize) return;
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            frames[i] = Sprite.Create(texture,
                new Rect(i % Columns * CellSize, (Rows - 1 - i / Columns) * CellSize, CellSize, CellSize),
                new Vector2(0.5f, 0.5f), ArtRadiusPixels, 0, SpriteMeshType.FullRect);
        root = new GameObject("KagutsuchiComboSlashVisual");
        mainRing = CreateRing(root.transform, "MainRing", null, Fx.DefaultOrder);
        root.SetActive(false);
        var shader = Resources.Load<Shader>("Shaders/KagutsuchiSlashClip");
        if (shader == null)
        {
            Debug.LogError("KagutsuchiSlashClip 셰이더가 없어 내부 횡베기를 표시할 수 없습니다.");
            return;
        }
        innerMaterial = new Material(shader);
        for (int i = 0; i < InnerCount; i++)
            innerRings[i] = CreateRing(root.transform, "InnerRing_" + i, innerMaterial, Fx.DefaultOrder + 1);
    }

    static Ring CreateRing(Transform parent, string name, Material material, int order)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var ring = new Ring { Transform = go.transform };
        // 반원 원화를 같은 중심에서 180도씩 마주 놓아 원 전체 궤적을 표시. 원본/공용 캐시는 유지.
        for (int i = 0; i < 2; i++)
        {
            var half = new GameObject(i == 0 ? "FirstHalf" : "OppositeHalf");
            half.transform.SetParent(go.transform, false);
            half.transform.localRotation = Quaternion.Euler(0f, 0f, i * 180f);
            var sr = half.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            if (material != null) sr.sharedMaterial = material;
            if (i == 0) ring.First = sr; else ring.Opposite = sr;
        }
        return ring;
    }

    public bool Play(Vector2 center, Vector2 direction, float radius, float seconds)
    {
        if (frames == null || root == null || radius <= 0f || seconds <= 0f) return false;
        age = 0f; duration = seconds; active = true;
        root.transform.position = center;
        root.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        root.transform.localScale = Vector3.one * radius;
        mainRing.SetFrame(frames[0]);
        mainRing.Reset(Random.value < 0.5f);
        if (innerMaterial != null)
        {
            innerMaterial.SetVector("_ClipCenter", new Vector4(center.x, center.y, 0f, 0f));
            innerMaterial.SetFloat("_ClipRadius", radius);
            foreach (var ring in innerRings)
            {
                float scale = Random.Range(InnerMinScale, InnerMaxScale);
                float positionAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
                float distance = Mathf.Sqrt(Random.value) * (1f - scale);
                ring.Transform.localPosition = new Vector3(Mathf.Cos(positionAngle) * distance, Mathf.Sin(positionAngle) * distance, 0f);
                ring.Transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
                ring.Transform.localScale = Vector3.one * scale;
                ring.SetFrame(frames[0]);
                ring.Reset(Random.value < 0.5f);
            }
        }
        root.SetActive(true);
        return true;
    }

    void Update()
    {
        if (!active || GameManager.I == null || GameManager.I.State != GameState.Playing) return;
        age += Time.deltaTime;
        if (age >= duration) { Clear(); return; }
        var frame = frames[Mathf.Min(FrameCount - 1, (int)(age / duration * FrameCount))];
        mainRing.SetFrame(frame);
        foreach (var ring in innerRings) if (ring != null) ring.SetFrame(frame);
    }

    public void Clear()
    {
        active = false; age = 0f; duration = 0f;
        if (root != null) root.SetActive(false);
    }

    void OnDisable() => Clear();
    void OnDestroy()
    {
        Clear();
        if (root != null) Destroy(root);
        if (innerMaterial != null) Destroy(innerMaterial);
        if (frames != null) foreach (var frame in frames) if (frame != null) Destroy(frame);
    }
}
