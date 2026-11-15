using System.Collections.Generic;
using UnityEngine;

/// <summary>2페이즈 지면 충격의 뒤/앞 동기 표시. 실행기 소유 슬롯 재사용, 전투 판정 없음.</summary>
public sealed class KagutsuchiPhysicalStompFx : MonoBehaviour
{
    const int FrameCount = 6, Columns = 3, Rows = 2, CellWidth = 512, CellHeight = 256;
    const float VisibleWidth = 491f; // 새 원화6프레임 alpha>16 최대 가시폭. 기존 판정 지름에 표시 폭을 맞춤.
    const float Fps = 12f; // [임시] 기존 스톰프의0.5초 재생 유지.
    const float VerticalOffset = -1.5f; // 사용자: 기존 Y-2.5 위치에서 1 올림.
    sealed class Slot
    {
        public GameObject Root;
        public SpriteRenderer Back, Front;
        public float Age;
        public bool Active;
    }
    readonly List<Slot> slots = new List<Slot>();
    Sprite[] backFrames, frontFrames;
    SpriteRenderer bodyVisual;

    void Awake()
    {
        bodyVisual = GetComponent<SpriteRenderer>();
        var back = Resources.Load<Texture2D>("Sprites/Kagutsuchi_P2_Fx_PhysicalStompDepth_Back");
        var front = Resources.Load<Texture2D>("Sprites/Kagutsuchi_P2_Fx_PhysicalStompDepth_Front");
        if (back == null || front == null || back.width != Columns * CellWidth || front.width != Columns * CellWidth
            || back.height != Rows * CellHeight || front.height != Rows * CellHeight)
        {
            Debug.LogError("앞뒤 분리용 스톰프 시트가 없거나 각1536×512 규격과 다릅니다.");
            return;
        }
        backFrames = new Sprite[FrameCount];
        frontFrames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            var rect = new Rect(i % Columns * CellWidth, (Rows - 1 - i / Columns) * CellHeight, CellWidth, CellHeight);
            // 절단선이 공통 지면 원점에 정확히 만남: 뒤는 아래쪽 피벗, 앞은 위쪽 피벗.
            backFrames[i] = Sprite.Create(back, rect, new Vector2(0.5f, 0f), VisibleWidth, 0, SpriteMeshType.FullRect);
            frontFrames[i] = Sprite.Create(front, rect, new Vector2(0.5f, 1f), VisibleWidth, 0, SpriteMeshType.FullRect);
        }
    }

    public void Play(Vector2 ground, float radius)
    {
        if (backFrames == null || bodyVisual == null || radius <= 0f) return;
        Slot slot = slots.Find(item => !item.Active);
        if (slot == null)
        {
            var root = new GameObject("PhysicalStompImpact");
            slot = new Slot { Root = root };
            var back = new GameObject("BackDust");
            back.transform.SetParent(root.transform, false);
            slot.Back = back.AddComponent<SpriteRenderer>();
            var front = new GameObject("FrontDust");
            front.transform.SetParent(root.transform, false);
            slot.Front = front.AddComponent<SpriteRenderer>();
            slots.Add(slot);
        }
        slot.Age = 0f; slot.Active = true;
        slot.Root.transform.position = ground + Vector2.up * VerticalOffset;
        slot.Root.transform.rotation = Quaternion.identity;
        slot.Root.transform.localScale = Vector3.one * (radius * 2f);
        slot.Back.sortingLayerID = slot.Front.sortingLayerID = bodyVisual.sortingLayerID;
        slot.Back.sortingOrder = bodyVisual.sortingOrder - 2; // 보스 외곽선보다도 뒤.
        slot.Front.sortingOrder = bodyVisual.sortingOrder + 1;
        slot.Back.color = slot.Front.color = Color.white;
        slot.Back.flipX = slot.Front.flipX = false;
        slot.Back.flipY = slot.Front.flipY = false;
        SetFrame(slot, 0);
        slot.Root.SetActive(true);
    }

    void SetFrame(Slot slot, int frame)
    {
        slot.Back.sprite = backFrames[frame];
        slot.Front.sprite = frontFrames[frame];
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
            int frame = (int)(slot.Age * Fps);
            if (frame >= FrameCount) { slot.Active = false; slot.Root.SetActive(false); }
            else SetFrame(slot, frame);
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
        if (backFrames != null) foreach (var frame in backFrames) if (frame != null) Destroy(frame);
        if (frontFrames != null) foreach (var frame in frontFrames) if (frame != null) Destroy(frame);
    }
}
