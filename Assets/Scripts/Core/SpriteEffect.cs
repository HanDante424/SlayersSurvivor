using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 한 번 재생하고 사라지는 스프라이트 시트 이펙트.
/// 시트: Resources/Sprites/{sheetName}.png, 프레임 순서는 행(위→아래) → 열(왼→오).
/// </summary>
public class SpriteEffect : MonoBehaviour
{
    static readonly Dictionary<string, Sprite[]> cache = new Dictionary<string, Sprite[]>();

    Sprite[] frames;
    float fps;
    float t;
    SpriteRenderer sr;
    /// <summary>true = 궁극기 중 WorldSlow 로 전체가 느려져도 정상 속도로 재생 (궁극기 연출용)</summary>
    public bool ignoreWorldSlow;

    /// <summary>시트를 잘라 프레임 배열로 (시트 이름별 캐시). 시트가 없으면 null.</summary>
    /// <param name="cellWorldWidth">한 칸의 가로 길이(월드 유닛). 같은 시트는 처음 값으로 캐시됩니다.</param>
    /// <param name="frameCount">실제 프레임 수 (마지막 칸들이 비어 있는 시트용). 0 이면 rows × cols 전부</param>
    public static Sprite[] LoadFrames(string sheetName, int rows, int cols, float cellWorldWidth, int frameCount = 0)
    {
        // 씬 전환 시 Unity 가 안 쓰는 스프라이트를 정리하므로 파괴됐으면 다시 만듦
        if (cache.TryGetValue(sheetName, out var cached) && cached != null && cached[0] != null) return cached;

        var tex = SpriteLib.Load(sheetName);
        if (tex == null) return null;

        float cw = tex.width / (float)cols, ch = tex.height / (float)rows;
        float ppu = cw / cellWorldWidth;
        int n = frameCount > 0 ? Mathf.Min(frameCount, rows * cols) : rows * cols;
        var f = new Sprite[n];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < cols; c++)
        {
            if (r * cols + c >= n) break;
            // 텍스처 좌표는 아래에서 위로 → 시트의 r행은 (rows-1-r)
            var rect = new Rect(c * cw, (rows - 1 - r) * ch, cw, ch);
            var s = Sprite.Create(tex, rect, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            s.name = $"{sheetName}_{r * cols + c}";
            f[r * cols + c] = s;
        }
        cache[sheetName] = f;
        return f;
    }

    /// <summary>이펙트를 만들어 재생. parent 가 있으면 따라 움직입니다.</summary>
    public static SpriteEffect Spawn(Sprite[] frames, float fps, Transform parent, Vector2 localPos,
                                     float angleDeg, bool flipX, int sortingOrder, float scale = 1f)
    {
        if (frames == null || frames.Length == 0) return null;
        var go = new GameObject("FX_" + frames[0].name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0, 0, angleDeg);
        go.transform.localScale = Vector3.one * scale;

        var fx = go.AddComponent<SpriteEffect>();
        fx.frames = frames;
        fx.fps = fps;
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = frames[0];
        fx.sr.flipX = flipX;
        fx.sr.sortingOrder = sortingOrder;
        return fx;
    }

    void Update()
    {
        t += ignoreWorldSlow ? WorldSlow.EffectDelta : Time.deltaTime;
        int i = (int)(t * fps);
        if (i >= frames.Length) { Destroy(gameObject); return; }
        sr.sprite = frames[i];
    }
}

/// <summary>
/// 멈출 때까지 반복 재생하는 스프라이트 시트 이펙트 (선택: 계속 회전). 끝낼 때는 Stop().
/// 예: 솔라 슬래시 중간 고리 (반복 + 시계 반대 회전)
/// </summary>
public class SpriteLoopEffect : MonoBehaviour
{
    Sprite[] frames;
    float fps, spin, t;
    SpriteRenderer sr;

    /// <param name="spinDegPerSec">회전 속도 (도/초, + = 시계 반대 방향)</param>
    public static SpriteLoopEffect Spawn(Sprite[] frames, float fps, Transform parent, Vector2 localPos,
                                         int sortingOrder, float scale, float spinDegPerSec = 0f)
    {
        if (frames == null || frames.Length == 0) return null;
        var go = new GameObject("FXLoop_" + frames[0].name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localScale = Vector3.one * scale;
        var fx = go.AddComponent<SpriteLoopEffect>();
        fx.frames = frames; fx.fps = fps; fx.spin = spinDegPerSec;
        fx.sr = go.AddComponent<SpriteRenderer>();
        fx.sr.sprite = frames[0];
        fx.sr.sortingOrder = sortingOrder;
        return fx;
    }

    public void Stop() { if (this != null) Destroy(gameObject); }

    float fadeTime, fadeT = -1f;
    /// <summary>계속 재생하면서 time 초에 걸쳐 서서히 투명해진 뒤 사라짐 (Stop 의 부드러운 버전)</summary>
    public void FadeOut(float time)
    {
        if (this == null) return;
        fadeTime = Mathf.Max(0.01f, time); fadeT = 0f;
    }

    float fadeInTime, fadeInT = -1f;
    /// <summary>만든 직후 time 초에 걸쳐 서서히 나타남 (투명 → 원래 투명도). 일렁임 · 사라지기와 함께 쓸 수 있음</summary>
    public SpriteLoopEffect FadeIn(float time)
    {
        fadeInTime = Mathf.Max(0.01f, time); fadeInT = 0f;
        return this;
    }

    float shrinkTime, shrinkT = -1f;
    /// <summary>계속 재생하면서 time 초에 걸쳐 중심 (이 오브젝트의 위치) 으로 줄어들며 투명해진 뒤 사라짐</summary>
    public void ShrinkOut(float time)
    {
        if (this == null) return;
        shrinkTime = Mathf.Max(0.01f, time); shrinkT = 0f;
        if (!baseSet) { baseScale = transform.localScale; baseSet = true; }
    }

    // 일렁임 (2026-11-22): 프레임이 거의 같은 반복 시트를 한 장 (_One) 으로 줄이고, 프레임 교체 대신 크기 · 투명도를 코드로 흔들어 살아 있게 함 (용량 절약)
    bool wobble, baseSet;
    float wobSx, wobSy, wobAlpha, wobSpeed, fadeMul = 1f;
    Vector3 baseScale;

    /// <summary>크기 (가로 · 세로 비율) 와 투명도를 속도 speed 로 불규칙하게 흔듦. 예: (0.02, 0.02, 0.08, 10)</summary>
    public SpriteLoopEffect Wobble(float scaleX, float scaleY, float alpha, float speed)
    {
        wobble = true; wobSx = scaleX; wobSy = scaleY; wobAlpha = alpha; wobSpeed = speed;
        return this;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (frames.Length > 1) sr.sprite = frames[(int)(t * fps) % frames.Length];
        if (spin != 0f) transform.Rotate(0f, 0f, spin * Time.deltaTime);
        if (fadeT >= 0f)
        {
            fadeT += Time.deltaTime;
            float k = Mathf.Clamp01(fadeT / fadeTime);
            fadeMul = 1f - k * k;   // 처음엔 천천히, 끝으로 갈수록 빨리 옅어짐
            if (k >= 1f) { Destroy(gameObject); return; }
        }
        float shrinkMul = 1f;
        if (shrinkT >= 0f)
        {
            shrinkT += Time.deltaTime;
            float k = Mathf.Clamp01(shrinkT / shrinkTime);
            if (k >= 1f) { Destroy(gameObject); return; }
            shrinkMul = 1f - Mathf.SmoothStep(0f, 1f, k);
            fadeMul = Mathf.Min(fadeMul, 1f - k * k);
        }
        float inMul = 1f;
        if (fadeInT >= 0f)
        {
            fadeInT += Time.deltaTime;
            inMul = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeInT / fadeInTime));
        }
        float wa = 1f;
        if (wobble)
        {
            if (!baseSet) { baseScale = transform.localScale; baseSet = true; }   // 만든 직후 바뀐 크기 (좌우 반전 등) 를 기준으로
            float n1 = Mathf.Sin(t * wobSpeed) * 0.6f + Mathf.Sin(t * wobSpeed * 1.73f + 1.3f) * 0.4f;
            float n2 = Mathf.Sin(t * wobSpeed * 1.31f + 0.7f) * 0.6f + Mathf.Sin(t * wobSpeed * 2.1f + 2.2f) * 0.4f;
            transform.localScale = new Vector3(baseScale.x * (1f + wobSx * n1) * shrinkMul, baseScale.y * (1f + wobSy * n2) * shrinkMul, baseScale.z);
            wa = 1f + wobAlpha * n1;
        }
        else if (shrinkT >= 0f) transform.localScale = baseScale * shrinkMul;
        if (wobble || fadeT >= 0f || shrinkT >= 0f || fadeInT >= 0f) { var c = sr.color; c.a = Mathf.Clamp01(fadeMul * wa * inMul); sr.color = c; }
    }
}
