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

    public Facing4 Facing { get; private set; } = Facing4.Down;
    public bool IsPlayingOneShot => oneShot != null;

    /// <summary>기존 호환용: 이동(Run) 클립 하나를 불러옵니다. 없으면 false.</summary>
    public bool Load(string sheetName, int rows, int columns, float cellWorldSize, float pivotY)
        => LoadClip(Run, sheetName, rows, columns, cellWorldSize, pivotY, 12f, true);

    /// <summary>시트를 불러와 클립으로 등록. 시트가 없으면 false.</summary>
    /// <param name="cellWorldSize">한 칸이 월드에서 차지할 크기(유닛)</param>
    /// <param name="pivotY">칸 안에서 transform 위치가 될 세로 지점 (0=아래, 1=위)</param>
    public bool LoadClip(string key, string sheetName, int rows, int columns,
                         float cellWorldSize, float pivotY, float fps, bool loop)
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
            frames[r, c] = Sprite.Create(tex, rect, new Vector2(0.5f, pivotY), ppu, 0, SpriteMeshType.FullRect);
            frames[r, c].name = $"{sheetName}_{r}_{c}";
        }
        clips[key] = new Clip { frames = frames, cols = columns, fps = fps, loop = loop };

        if (sr == null)
        {
            sr = GetComponent<SpriteRenderer>();
            if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        }
        if (sr.sprite == null) sr.sprite = frames[0, 0];
        return true;
    }

    public bool HasClip(string key) => clips.ContainsKey(key);

    /// <summary>매 프레임 이동 방향을 넘겨주세요. 0 벡터면 정지.</summary>
    public void SetMove(Vector2 dir)
    {
        moving = dir.sqrMagnitude > 0.01f;
        if (!moving || oneShot != null) return;   // 동작 재생 중에는 방향 고정
        Facing = ToFacing(dir);
    }

    /// <summary>
    /// 클립을 한 번 재생합니다. 재생 중에는 방향이 고정되고, 끝나면 이동/정지 애니메이션으로 돌아갑니다.
    /// 이미 다른 동작이 재생 중이면 무시하고 false 를 돌려줍니다 (interrupt=true 면 끊고 새로 시작).
    /// </summary>
    /// <param name="dir">바라볼 방향. 0 벡터면 현재 방향 유지</param>
    public bool Play(string key, Vector2 dir = default, Action onEnd = null, bool interrupt = false)
    {
        if (oneShot != null && !interrupt) return false;
        if (!clips.TryGetValue(key, out var clip)) return false;
        if (dir.sqrMagnitude > 0.01f) Facing = ToFacing(dir);
        oneShot = clip;
        oneShotTime = 0f;
        oneShotEnd = onEnd;
        sr.sprite = clip.frames[(int)Facing, 0];
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

    void Update()
    {
        if (sr == null) return;

        // 1) 한 번 재생 중인 동작
        if (oneShot != null)
        {
            oneShotTime += Time.deltaTime;
            int f = (int)(oneShotTime * oneShot.fps);
            if (f < oneShot.cols || oneShot.loop)
            {
                sr.sprite = oneShot.frames[(int)Facing, f % oneShot.cols];
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
        if (!frozen) time += Time.deltaTime;

        sr.sprite = clip.frames[(int)Facing, frame];
    }
}
