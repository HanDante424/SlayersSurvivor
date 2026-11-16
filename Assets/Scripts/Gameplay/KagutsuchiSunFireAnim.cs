using System;
using UnityEngine;

/// <summary>
/// 초거대 화염구 (SunFire) 반복 애니메이션. 특수 패턴 1 · 2 가 공유.
/// 시트 Kagutsuchi_SunFire_Loop: 칸 320×320, 4×4 (원본 제작 시트 512 칸에서 가시 원 둘레만 잘라 용량을 줄임, 그림 픽셀 변경 없음).
/// 프레임 0~12 가 서로 다른 그림이고 13 은 0 과 같아서 반복에서 뺌. 제작 제안 속도 80ms (12.5fps) [임시].
/// 원이 칸의 약 91.6% (293/320) 라 기존 정지 그림 (칸의 85.9%) 과 같은 원 크기가 되도록 CellScale 을 곱해서 쓴다.
/// </summary>
public sealed class KagutsuchiSunFireAnim : IDisposable
{
    const string Sheet = "Kagutsuchi_SunFire_Loop";
    const int Cell = 320, Columns = 4, FrameCount = 13;
    const float Fps = 12.5f;
    const float RotationDegreesPerSecond = 7.5f;  // [임시] 사용자: 시계 방향으로 천천히, 15 → 50% 감소 (48초에 한 바퀴)
    /// <summary>기존 정지 그림 기준 칸 크기에 곱할 값: (1077/1254) / (293/320). 원의 월드 크기를 그대로 유지.</summary>
    public const float CellScale = 0.938f;

    // 프레임별 불투명 (알파>128) 질량중심 px (칸 안 좌상단 기준) 실측. 프레임 0~12.
    static readonly float[] MassCenterX = { 156.7f, 158.3f, 158.4f, 152.9f, 158.1f, 152.8f, 154.2f, 158.6f, 158.4f, 152.9f, 158.6f, 154.2f, 153.9f };
    static readonly float[] MassCenterY = { 162.1f, 162.9f, 163.3f, 162.6f, 162.6f, 163.0f, 162.9f, 163.3f, 162.3f, 163.0f, 162.0f, 162.1f, 162.2f };
    readonly Sprite[] frames;
    float age;
    public bool Valid => frames != null;
    public Sprite First => frames != null ? frames[0] : null;

    public KagutsuchiSunFireAnim()
    {
        var texture = Resources.Load<Texture2D>("Sprites/" + Sheet);
        if (texture == null || texture.width != Cell * Columns || texture.height != Cell * Columns)
        {
            Debug.LogError("태양 화염 반복 시트가 없거나 1280×1280 규격과 다릅니다. 정지 그림으로 대체합니다.");
            return;
        }
        frames = new Sprite[FrameCount];
        for (int i = 0; i < FrameCount; i++)
        {
            var rect = new Rect(i % Columns * Cell, (Columns - 1 - i / Columns) * Cell, Cell, Cell);
            // 프레임마다 원 (불투명 질량) 중심을 pivot 으로 → 제작 시트의 프레임별 가로 최대 6px · 세로 1px 의 중심 어긋남 (상하좌우 꿈틀거림) 제거.
            // 칸 폭이 1 유닛이 되게 ppu = 320 (호출쪽이 bounds.size.x 로 나눠 크기를 맞춤).
            var pivot = new Vector2(MassCenterX[i] / Cell, 1f - MassCenterY[i] / Cell);
            frames[i] = Sprite.Create(texture, rect, pivot, Cell, 0, SpriteMeshType.FullRect);
        }
    }

    public void Tick(float dt, SpriteRenderer target)
    {
        if (frames == null || target == null) return;
        age += dt;
        target.sprite = frames[(int)(age * Fps) % FrameCount];
        // 시계 방향으로 천천히 회전 (Unity z 회전은 반시계가 +). pivot = 구체 중심이라 제자리에서 돈다.
        target.transform.rotation = Quaternion.Euler(0f, 0f, -age * RotationDegreesPerSecond % 360f);
    }

    public void Dispose()
    {
        if (frames == null) return;
        foreach (var frame in frames) if (frame != null) UnityEngine.Object.Destroy(frame);
    }
}
