using System.Collections.Generic;
using UnityEngine;

/// <summary>주먹·발차기용 측면 소닉붐. 화면 좌반은 보스 뒤, 우반은 앞에 표시. 피해 판정 없음.</summary>
public sealed class KagutsuchiSonicBoomFx : MonoBehaviour
{
    const int FrameCount = 6, Columns = 3, Rows = 2, CellSize = 512;
    const float FrontDistance = 2.5f; // 사용자: 직전1.5 앞에서 왼쪽 보기X-1/오른쪽 보기X+1 추가.
    const float Duration = 0.3f; // [임시] 제작 GIF 재생 시간.
    const float VisualHeight = 6.75f; // 사용자: 직전 높이4.5에서 가로·세로 추가50% 확대.
    sealed class Slot
    {
        public GameObject Root;
        public SpriteRenderer Back, Front;
        public float Age;
        public bool Active, MirrorX;
    }
    readonly List<Slot> slots = new List<Slot>();
    Sprite[] leftFrames, rightFrames;
    SpriteRenderer bodyVisual;

    void Awake()
    {
        bodyVisual = GetComponent<SpriteRenderer>();
        var texture = Resources.Load<Texture2D>("Sprites/Kagutsuchi_P2_Fx_SonicBoomSide_v2");
        if (texture == null || texture.width != Columns * CellSize || texture.height != Rows * CellSize)
        {
            Debug.LogError("측면 소닉붐 v2가 없거나1536×1024 규격과 다릅니다.");
            return;
        }
        leftFrames = new Sprite[FrameCount];
        rightFrames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            float x = i % Columns * CellSize, y = (Rows - 1 - i / Columns) * CellSize;
            // 원래 중심을 공통 원점으로 유지. 두 절반 경계가 x=0에서 만남.
            leftFrames[i] = Sprite.Create(texture, new Rect(x, y, CellSize / 2, CellSize),
                new Vector2(1f, 0.5f), CellSize, 0, SpriteMeshType.FullRect);
            rightFrames[i] = Sprite.Create(texture, new Rect(x + CellSize / 2, y, CellSize / 2, CellSize),
                new Vector2(0f, 0.5f), CellSize, 0, SpriteMeshType.FullRect);
        }
    }

    public void Play(Vector2 bossPosition, bool facingRight)
    {
        if (leftFrames == null || bodyVisual == null) return;
        Slot slot = slots.Find(item => !item.Active);
        if (slot == null)
        {
            var root = new GameObject("KagutsuchiSideSonicBoom");
            slot = new Slot { Root = root };
            var back = new GameObject("BackHalf");
            back.transform.SetParent(root.transform, false);
            slot.Back = back.AddComponent<SpriteRenderer>();
            var front = new GameObject("FrontHalf");
            front.transform.SetParent(root.transform, false);
            slot.Front = front.AddComponent<SpriteRenderer>();
            slots.Add(slot);
        }
        slot.Age = 0f; slot.Active = true;
        slot.MirrorX = facingRight; // 사용자: 직전 표시(!facingRight)를 X축 반전.
        slot.Root.transform.position = bossPosition + Vector2.right * (facingRight ? FrontDistance : -FrontDistance);
        slot.Root.transform.rotation = Quaternion.identity;
        slot.Root.transform.localScale = Vector3.one * VisualHeight;
        slot.Back.sortingLayerID = slot.Front.sortingLayerID = bodyVisual.sortingLayerID;
        slot.Back.sortingOrder = bodyVisual.sortingOrder - 2; // 본체 외곽선보다 뒤.
        slot.Front.sortingOrder = bodyVisual.sortingOrder + 1;
        slot.Back.color = slot.Front.color = Color.white;
        slot.Back.flipX = slot.Front.flipX = slot.MirrorX;
        slot.Back.flipY = slot.Front.flipY = false;
        SetFrame(slot, 0);
        slot.Root.SetActive(true);
    }

    void SetFrame(Slot slot, int frame)
    {
        // X반전 시 원화 좌우도 바꿔 배정해야 화면 기준 왼쪽 뒤·오른쪽 앞이 유지된다.
        slot.Back.sprite = slot.MirrorX ? rightFrames[frame] : leftFrames[frame];
        slot.Front.sprite = slot.MirrorX ? leftFrames[frame] : rightFrames[frame];
    }

    void Update()
    {
        if (GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) { Clear(); return; }
        if (GameManager.I.State != GameState.Playing) return;
        foreach (var slot in slots)
        {
            if (!slot.Active) continue;
            slot.Age += Time.deltaTime;
            if (slot.Age >= Duration) { slot.Active = false; slot.Root.SetActive(false); }
            else SetFrame(slot, Mathf.Min(FrameCount - 1, (int)(slot.Age / Duration * FrameCount)));
        }
    }

    public void Clear()
    {
        foreach (var slot in slots)
        { slot.Active = false; slot.Age = 0f; if (slot.Root != null) slot.Root.SetActive(false); }
    }
    void OnDisable() => Clear();
    void OnDestroy()
    {
        Clear();
        foreach (var slot in slots) if (slot.Root != null) Destroy(slot.Root);
        slots.Clear();
        if (leftFrames != null) foreach (var frame in leftFrames) if (frame != null) Destroy(frame);
        if (rightFrames != null) foreach (var frame in rightFrames) if (frame != null) Destroy(frame);
    }
}
