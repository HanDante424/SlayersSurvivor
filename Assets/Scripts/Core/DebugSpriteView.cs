using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 디버그 맵 "스프라이트 보기": 패턴 · 애니메이션과 상관없이 대상의 그림 하나를 그 자리에 고정 표시 (2026-11-16 사용자).
/// 매 프레임 LateUpdate 에서 SpriteRenderer 의 그림을 덮어써서 애니메이터 · AI 가 바꾸는 것보다 늦게 적용된다.
/// 크기 기준: 시트 (가니악) = 원래 그림과 같은 픽셀 밀도 · 발 여백만 보정, 한 장 그림 = 원래 그림과 같은 키.
/// </summary>
public sealed class DebugSpriteView : MonoBehaviour
{
    public sealed class Def
    {
        public string Label, Sheet;
        public int Rows = 1, Cols = 1;
        public bool Matrix;                 // true = 4방향 시트 (행 = 아래 · 왼 · 오 · 위, 열 = 프레임)
        public float Margin, RefMargin;     // 칸 아래 여백 px (이 시트 / 기준이 되는 원래 그림)
    }

    static readonly Dictionary<EnemyKind, List<Def>> cache = new Dictionary<EnemyKind, List<Def>>();
    SpriteRenderer sr;
    Sprite basis, shown;

    public bool Active => shown != null;

    public static DebugSpriteView Of(Enemy e)
    {
        if (e == null) return null;
        return e.TryGetComponent<DebugSpriteView>(out var v) ? v : e.gameObject.AddComponent<DebugSpriteView>();
    }

    /// <summary>대상 종류별로 볼 수 있는 그림 목록</summary>
    public static List<Def> DefsFor(Enemy e)
    {
        if (e == null) return new List<Def>();
        if (cache.TryGetValue(e.kind, out var cached)) return cached;
        var list = new List<Def>();
        if (e.kind == EnemyKind.Ganiac)
        {
            // Enemy.SetupSheets 와 같은 규격: 대기 4×1 · 이동 4×8 (칸 384, 아래 여백 24) · 베기 4×8 (칸 480, 아래 여백 30)
            list.Add(new Def { Label = "대기", Sheet = "Ganiac_Idle", Rows = 4, Cols = 1, Matrix = true, Margin = 24f, RefMargin = 24f });
            list.Add(new Def { Label = "이동", Sheet = "Ganiac_Run", Rows = 4, Cols = 8, Matrix = true, Margin = 24f, RefMargin = 24f });
            list.Add(new Def { Label = "베기", Sheet = "Ganiac_Slash", Rows = 4, Cols = 8, Matrix = true, Margin = 30f, RefMargin = 24f });
        }
        else
        {
            AddSingle(list, "Enemy_" + e.kind);
            if (e.kind == EnemyKind.Kagutsuchi) AddSingle(list, "Enemy_Kagutsuchi_P2");
        }
        cache[e.kind] = list;
        return list;
    }

    static void AddSingle(List<Def> list, string sheet)
    {
        if (SpriteLib.Load(sheet) == null) return;
        foreach (var d in list) if (d.Sheet == sheet) return;
        list.Add(new Def { Label = sheet.Replace("Enemy_", ""), Sheet = sheet });
    }

    public void Show(Def d, int row, int col)
    {
        if (sr == null) sr = GetComponent<SpriteRenderer>();
        if (sr == null || d == null) return;
        if (shown == null) basis = sr.sprite;     // 처음 고를 때의 원래 그림 (크기 · 발 기준)
        if (basis == null) return;
        var tex = SpriteLib.Load(d.Sheet);
        if (tex == null) return;
        row = Mathf.Clamp(row, 0, d.Rows - 1); col = Mathf.Clamp(col, 0, d.Cols - 1);
        float cw = tex.width / (float)d.Cols, ch = tex.height / (float)d.Rows;
        var rect = new Rect(col * cw, (d.Rows - 1 - row) * ch, cw, ch);
        float ppu, pivotY;
        if (d.Matrix)
        {
            ppu = basis.pixelsPerUnit;
            pivotY = (basis.pivot.y - d.RefMargin + d.Margin) / ch;
        }
        else
        {
            ppu = tex.height / Mathf.Max(0.0001f, basis.bounds.size.y);
            pivotY = basis.pivot.y / Mathf.Max(1f, basis.rect.height);
        }
        var sprite = Sprite.Create(tex, rect, new Vector2(0.5f, pivotY), ppu, 0, SpriteMeshType.FullRect);
        sprite.name = $"{d.Sheet}_{row}_{col}";
        if (shown != null) Destroy(shown);
        shown = sprite;
        sr.sprite = shown;
        sr.flipX = false;
    }

    public void Clear()
    {
        if (shown == null) return;
        if (sr != null) sr.sprite = basis;
        Destroy(shown);
        shown = null;
    }

    void LateUpdate()
    {
        if (shown == null || sr == null) return;
        sr.sprite = shown;
        sr.flipX = false;
    }

    void OnDestroy()
    {
        if (shown != null) Destroy(shown);
    }
}
