using System.Collections.Generic;
using UnityEngine;

/// <summary>2페이즈 실행기 소유 지면 충격 표시. 재생 슬롯 재사용, 전투 판정 없음.</summary>
public sealed class KagutsuchiPhysicalStompFx : MonoBehaviour
{
    const int FrameCount = 6, CellWidth = 512, CellHeight = 256, SortingOrder = 2;
    const float VisibleWidth = 383f, Fps = 12f; // [임시] 원화 미리보기 속도, 0.5초 한 번 재생.
    sealed class Slot { public SpriteRenderer Visual; public float Age; public bool Active; }
    readonly List<Slot> slots = new List<Slot>();
    Sprite[] frames;

    void Awake()
    {
        var texture = Resources.Load<Texture2D>("Sprites/Kagutsuchi_P2_Fx_PhysicalStomp");
        if (texture == null || texture.width != CellWidth * FrameCount || texture.height != CellHeight) return;
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
            frames[i] = Sprite.Create(texture, new Rect(i * CellWidth, 0f, CellWidth, CellHeight),
                                      new Vector2(0.5f, 0.5f), VisibleWidth, 0, SpriteMeshType.FullRect);
    }

    public void Play(Vector2 ground, float radius)
    {
        if (frames == null || radius <= 0f) return;
        Slot slot = slots.Find(item => !item.Active);
        if (slot == null)
        {
            var go = new GameObject("PhysicalStompImpact");
            slot = new Slot { Visual = go.AddComponent<SpriteRenderer>() };
            slot.Visual.sortingOrder = SortingOrder;
            slots.Add(slot);
        }
        slot.Age = 0f;
        slot.Active = true;
        slot.Visual.transform.position = ground;
        slot.Visual.transform.rotation = Quaternion.identity;
        slot.Visual.transform.localScale = Vector3.one * (radius * 2f);
        slot.Visual.color = Color.white;
        slot.Visual.sprite = frames[0];
        slot.Visual.gameObject.SetActive(true);
    }

    void Update()
    {
        if (GameManager.I == null || GameManager.I.State != GameState.Playing) return;
        foreach (var slot in slots)
        {
            if (!slot.Active) continue;
            slot.Age += Time.deltaTime;
            int frame = (int)(slot.Age * Fps);
            if (frame >= FrameCount) { slot.Active = false; slot.Visual.gameObject.SetActive(false); }
            else slot.Visual.sprite = frames[frame];
        }
    }

    public void Clear()
    {
        foreach (var slot in slots)
        { slot.Active = false; slot.Age = 0f; if (slot.Visual != null) slot.Visual.gameObject.SetActive(false); }
    }
    void OnDisable() => Clear();
    void OnDestroy()
    {
        foreach (var slot in slots) if (slot.Visual != null) Destroy(slot.Visual.gameObject);
        slots.Clear();
        if (frames != null) foreach (var frame in frames) if (frame != null) Destroy(frame);
    }
}
