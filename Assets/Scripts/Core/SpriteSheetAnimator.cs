using System;
using System.Collections.Generic;
using UnityEngine;

public enum Facing4 { Down = 0, Left = 1, Right = 2, Up = 3 }

/// <summary>
/// 4방향 스프라이트 시트 애니메이터 (여러 클립 지원).
/// 시트 규칙: 행(위→아래) = 아래, 왼쪽, 오른쪽, 위 / 열 = 프레임
/// Resources/Sprites/{sheetName}.png 를 런타임에 잘라 사용합니다.
///
/// 클립 키 규칙
///  - "Run"  : 이동 중 반복 (필수)
///  - "Idle" : 정지 중 반복 (없으면 Run 첫 프레임 고정)
///  - 그 외   : Play(key) 로 한 번 재생하는 동작 (공격 등)
/// </summary>
public class SpriteSheetAnimator : MonoBehaviour
{
    public const string Run = "Run";
    public const string Idle = "Idle";

    class Clip
    {
        public Sprite[,] frames;   // [direction, frame]
        public int cols;
        public float fps;
        public bool loop;
    }

    readonly Dictionary<string, Clip> clips = new Dictionary<string, Clip>();
    SpriteRenderer sr;
    float time;
    bool moving;
    Clip current;   // 현재 재생 중인 이동/정지 클립

    // 한 번 재생 중인 클립
    Clip oneShot;
    float oneShotTime;
    Action oneShotEnd;

    // 측면 전용 모드: 시트 행이 1개 (왼쪽을 보는 옆면) 인 클립이 하나라도 있으면, 오른쪽은 좌우 반전으로 표시하고
    // 위 · 아래 방향은 마지막 좌우 방향을 유지한다 (가니악 1페이즈, 2026-11-16 — 카구츠치 2페이즈와 같은 방식).
    bool sideMode;

    public Facing4 Facing { get; private set; } = Facing4.Down;
    public bool IsPlayingOneShot => oneShot != null;

    // 자세 고정: 지정한 시간 동안 클립의 첫 프레임을 다른 모든 재생보다 우선해 보여 줌 (가니악 돌진 자세, 시간이 지나면 저절로 풀림 —
    // 패턴이 중간에 끊겨도 자세가 남지 않게)
    Clip heldPose;
    float heldUntil;

    /// <summary>key 클립의 첫 프레임을 seconds 초 동안 고정 표시. dir 쪽을 바라봄. 클립이 없으면 false</summary>
    public bool HoldPose(string key, Vector2 dir, float seconds)
    {
        if (!clips.TryGetValue(key, out var clip) || sr == null) return false;
        if (dir.sqrMagnitude > 0.01f) SetFacing(dir);
        heldPose = clip;
        heldUntil = Time.time + seconds;
        sr.sprite = FrameOf(clip, 0);
        return true;
    }

    public void ReleasePose() => heldPose = null;

    /// <summary>재생 중인 한 번 재생 동작 · 고정 자세를 끊음 (디버그 맵: 같은 스킬 모션을 바로 다시 보려고)</summary>
    public void StopOneShot()
    {
        oneShot = null; oneShotEnd = null; heldPose = null; current = null;
    }

    /// <summary>기존 호환용: 이동(Run) 클립 하나를 불러옵니다. 없으면 false.</summary>
    public bool Load(string sheetName, int rows, int columns, float cellWorldSize, float pivotY)
        => LoadClip(Run, sheetName, rows, columns, cellWorldSize, pivotY, 12f, true);

    /// <summary>시트를 불러와 클립으로 등록. 시트가 없으면 false.</summary>
    /// <param name="cellWorldSize">한 칸이 월드에서 차지할 크기(유닛)</param>
    /// <param name="pivotY">칸 안에서 transform 위치가 될 세로 지점 (0=아래, 1=위)</param>
    /// <param name="pivotX">칸 안에서 transform 위치가 될 가로 지점 (0.5 = 가운데). 좌우 반전은 이 점을 기준으로 뒤집힘</param>
    /// <param name="globalSide">true (기본) = 1행 시트이면 애니메이터 전체를 측면 전용 모드로. false = 이 클립만 좌우 반전 (같은 애니메이터의
    /// 다른 4행 클립은 기존처럼 위 · 아래 행 사용 — 플레이어 측면 스탠드 + 기존 4방향 달리기 · 베기 혼용)</param>
    public bool LoadClip(string key, string sheetName, int rows, int columns,
                         float cellWorldSize, float pivotY, float fps, bool loop,
                         float pivotX = 0.5f, bool globalSide = true)
    {
        var tex = Resources.Load<Texture2D>("Sprites/" + sheetName);
        if (tex == null) return false;

        float cw = tex.width / (float)columns, ch = tex.height / (float)rows;
        float ppu = cw / cellWorldSize;
        var frames = new Sprite[rows, columns];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < columns; c++)
        {
            // 텍스처 좌표는 아래에서 위로 → 시트의 r행은 (rows-1-r)
            var rect = new Rect(c * cw, (rows - 1 - r) * ch, cw, ch);
            frames[r, c] = Sprite.Create(tex, rect, new Vector2(pivotX, pivotY), ppu, 0, SpriteMeshType.FullRect);
            frames[r, c].name = $"{sheetName}_{r}_{c}";
        }
        clips[key] = new Clip { frames = frames, cols = columns, fps = fps, loop = loop };
        if (rows == 1 && globalSide) sideMode = true;

        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        }
        if (sr.sprite == null) sr.sprite = frames[0, 0];
        return true;
    }

    public bool HasClip(string key) => clips.ContainsKey(key);

    /// <summary>애니메이터 상태를 바꾸지 않고 지정 방향의 정지 표시 프레임을 빌려준다.</summary>
    public Sprite GetIdleFrame(Facing4 facing)
    {
        if (!clips.TryGetValue(Idle, out var clip) && !clips.TryGetValue(Run, out clip)) return null;
        return clip.frames[Row(clip, facing), 0];
    }

    /// <summary>시트 행 번호 (행이 1개면 항상 0 = 왼쪽을 보는 옆면)</summary>
    static int Row(Clip clip, Facing4 facing) => Mathf.Clamp((int)facing, 0, clip.frames.GetLength(0) - 1);

    Sprite FrameOf(Clip clip, int frame)
    {
        // 1행 (왼쪽 옆면) 클립 = 오른쪽일 때 좌우 반전, 위 · 아래는 마지막 좌우 방향 유지. 4행 클립 (기존 달리기 · 베기) 은 행으로 방향 표현
        bool oneRow = clip.frames.GetLength(0) == 1;
        sr.flipX = oneRow && lastSide == Facing4.Right;
        return clip.frames[Row(clip, Facing), frame];
    }

    /// <summary>대기 (없으면 달리기) 클립의 표시 프레임이 dir 방향에서 좌우 반전이 필요한가 (1행 옆면 클립 + 오른쪽)</summary>
    public bool IdleFrameNeedsFlip(Facing4 facing)
    {
        if (!clips.TryGetValue(Idle, out var clip) && !clips.TryGetValue(Run, out clip)) return false;
        return clip.frames.GetLength(0) == 1 && facing == Facing4.Right;
    }

    Facing4 lastSide = Facing4.Left;   // 마지막으로 바라본 좌우 방향 (1행 옆면 클립의 반전 기준)

    /// <summary>방향 갱신 (측면 전용 모드에서는 위 · 아래가 와도 좌우 방향을 유지)</summary>
    void SetFacing(Vector2 dir)
    {
        var f = ToFacing(dir);
        if (f == Facing4.Left || f == Facing4.Right) lastSide = f;
        // 대각선 입력 (위 + 왼쪽 등) 은 세로가 우세 (또는 같음) 로 판정돼 좌우가 갱신되지 않았음 → 1행 옆면 클립이 반대쪽을 보고 이동.
        // 플레이어 (측면 클립 + 4행 클립 혼용, sideMode 아님) 는 가로 성분이 있으면 그 쪽으로 반전. 측면 전용 모드 (보스) 는 기존 동작 유지
        else if (!sideMode && Mathf.Abs(dir.x) > 0.01f) lastSide = dir.x < 0f ? Facing4.Left : Facing4.Right;
        if (sideMode && (f == Facing4.Up || f == Facing4.Down)) { if (Facing != Facing4.Left && Facing != Facing4.Right) Facing = Facing4.Left; return; }
        Facing = f;
    }

    /// <summary>클립 재생 속도 변경 (실행 중 조정용)</summary>
    public void SetClipFps(string key, float fps)
    {
        if (clips.TryGetValue(key, out var c)) c.fps = Mathf.Max(0.1f, fps);
    }

    /// <summary>매 프레임 이동 방향을 넘겨주세요. 0 벡터면 정지.</summary>
    public void SetMove(Vector2 dir)
    {
        moving = dir.sqrMagnitude > 0.01f;
        if (!moving) return;
        // 동작 재생 중에도 이동 방향을 따라 돌아봄 (같은 프레임에서 행만 바뀜)
        SetFacing(dir);
    }

    /// <summary>멈춰 있을 때 바라볼 방향 (동작 재생 중에는 무시 — 휘두르는 도중 돌아보지 않게)</summary>
    public void Look(Vector2 dir)
    {
        if (oneShot == null && dir.sqrMagnitude > 0.01f) SetFacing(dir);
    }

    /// <summary>
    /// 클립을 한 번 재생합니다. 재생 중 이동하면 그 방향으로 돌아보고, 끝나면 이동/정지 애니메이션으로 돌아갑니다.
    /// 이미 다른 동작이 재생 중이면 무시하고 false 를 돌려줍니다 (interrupt=true 면 끊고 새로 시작).
    /// </summary>
    /// <param name="dir">바라볼 방향. 0 벡터면 현재 방향 유지</param>
    public bool Play(string key, Vector2 dir = default, Action onEnd = null, bool interrupt = false)
    {
        if (oneShot != null && !interrupt) return false;
        if (!clips.TryGetValue(key, out var clip)) return false;
        if (dir.sqrMagnitude > 0.01f) SetFacing(dir);
        oneShot = clip;
        oneShotTime = 0f;
        oneShotEnd = onEnd;
        sr.sprite = FrameOf(clip, 0);
        return true;
    }

    /// <summary>재생 길이(초)</summary>
    public float GetDuration(string key)
        => clips.TryGetValue(key, out var c) ? c.cols / c.fps : 0f;

    static Facing4 ToFacing(Vector2 dir)
    {
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) + 0.01f) return dir.x < 0 ? Facing4.Left : Facing4.Right;
        return dir.y < 0 ? Facing4.Down : Facing4.Up;
    }

    /// <summary>true = 궁극기 중 WorldSlow 로 전체가 느려져도 이 애니메이터는 정상 속도 (플레이어용)</summary>
    public bool ignoreWorldSlow;

    void Update()
    {
        if (sr == null) return;
        float dt = ignoreWorldSlow ? WorldSlow.PlayerDelta : Time.deltaTime;

        // 0) 고정 자세 (돌진 등)
        if (heldPose != null)
        {
            if (Time.time < heldUntil) { sr.sprite = FrameOf(heldPose, 0); return; }
            heldPose = null;
        }

        // 1) 한 번 재생 중인 동작
        if (oneShot != null)
        {
            oneShotTime += dt;
            int f = (int)(oneShotTime * oneShot.fps);
            if (f < oneShot.cols || oneShot.loop)
            {
                sr.sprite = FrameOf(oneShot, f % oneShot.cols);
                return;
            }
            var end = oneShotEnd;
            oneShot = null;
            oneShotEnd = null;
            current = null;   // 동작 후 이동/정지 클립을 처음부터
            end?.Invoke();
            if (oneShot != null) return;   // 콜백에서 다음 동작을 이어서 재생한 경우
        }

        // 2) 이동 / 정지
        Clip clip;
        bool frozen = false;
        if (moving && clips.TryGetValue(Run, out clip)) { }
        else if (!moving && clips.TryGetValue(Idle, out clip)) { }
        else if (clips.TryGetValue(Run, out clip)) frozen = true;   // 정지 전용 클립이 없으면 달리기 첫 프레임 고정
        else return;

        // 클립이 바뀌면 첫 프레임부터
        if (clip != current || frozen) { current = clip; time = 0f; }
        int frame = frozen ? 0 : (int)(time * clip.fps) % clip.cols;
        if (!frozen) time += dt;

        sr.sprite = FrameOf(clip, frame);
    }
}
