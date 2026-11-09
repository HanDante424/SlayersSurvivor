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

    /// <summary>시트를 잘라 프레임 배열로 (시트 이름별 캐시). 시트가 없으면 null.</summary>
    /// <param name="cellWorldWidth">한 칸의 가로 길이(월드 유닛). 같은 시트는 처음 값으로 캐시됩니다.</param>
    /// <param name="frameCount">실제 프레임 수 (마지막 칸들이 비어 있는 시트용). 0 이면 rows × cols 전부</param>
    public static Sprite[] LoadFrames(string sheetName, int rows, int cols, float cellWorldWidth, int frameCount = 0)
    {
        // 씬 전환 시 Unity 가 안 쓰는 스프라이트를 정리하므로 파괴됐으면 다시 만듦
        if (cache.TryGetValue(sheetName, out var cached) && cached != null && cached[0] != null) return cached;

        var tex = Resources.Load<Texture2D>("Sprites/" + sheetName);
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
        t += Time.deltaTime;
        int i = (int)(t * fps);
        if (i >= frames.Length) { Destroy(gameObject); return; }
        sr.sprite = frames[i];
    }
}
