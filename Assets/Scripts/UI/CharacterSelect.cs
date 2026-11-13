using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 캐릭터 선택 화면 (타이틀 화면 위에 열림). 목록은 CharacterRoster.All.
/// 키보드: ←/→ (A/D) 이동, Enter/Space 시작, Esc/Backspace 뒤로. 마우스: 카드에 올리면 선택, 클릭 또는 "시작" = 시작.
/// 카드 그림: Resources/Sprites/{id}_Idle · {id}_Run 의 정면(아래) 행. 선택된 카드는 달리기 애니메이션.
/// </summary>
public class CharacterSelect : MonoBehaviour
{
    Action onBack;
    int selected;
    Vector2 lastMouse;
    string toast;
    float toastTime;

    /// <summary>시트 1장 + 행·열 수 (정면 = 0행)</summary>
    class Sheet
    {
        public Texture2D tex;
        public int rows, cols;
    }

    readonly Dictionary<string, Sheet> idle = new Dictionary<string, Sheet>();
    readonly Dictionary<string, Sheet> run = new Dictionary<string, Sheet>();

    public static CharacterSelect Open(GameObject host, Action onBack)
    {
        var cs = host.AddComponent<CharacterSelect>();
        cs.onBack = onBack;
        cs.selected = Mathf.Max(0, CharacterRoster.All.IndexOf(CharacterRoster.Selected));
        return cs;
    }

    void Awake()
    {
        foreach (var c in CharacterRoster.All)
        {
            if (c.availability == CharacterAvailability.Locked) continue;
            idle[c.id] = LoadSheet(c.id + "_Idle", 4, c.visual.idleCols);
            run[c.id] = LoadSheet(c.id + "_Run", 4, c.visual.runCols);
        }
    }

    static Sheet LoadSheet(string name, int rows, int cols)
    {
        var tex = Resources.Load<Texture2D>("Sprites/" + name);
        return tex == null || cols <= 0 ? null : new Sheet { tex = tex, rows = rows, cols = cols };
    }

    void Update()
    {
        int n = CharacterRoster.All.Count;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) selected = (selected - 1 + n) % n;
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) selected = (selected + 1) % n;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) StartSelected();
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Close();
        if (toastTime > 0) toastTime -= Time.deltaTime;
    }

    void StartSelected()
    {
        var c = CharacterRoster.All[selected];
        if (c.availability == CharacterAvailability.Locked)
        {
            toast = $"{c.name} 은(는) 준비 중입니다.";
            toastTime = 1.8f;
            return;
        }
        CharacterRoster.Selected = c;
        Player.Visual = c.visual;
        SceneLoader.Load(SceneLoader.Game);
    }

    void Close()
    {
        onBack?.Invoke();
        Destroy(this);
    }

    void OnGUI()
    {
        UIStyles.Init();
        float W = Screen.width, H = Screen.height;
        UIStyles.Overlay(new Color(0, 0, 0, 0.55f));
        GUI.Label(new Rect(0, H * 0.06f, W, 60), Stage.IsRaid ? "캐릭터 선택 — 레이드 보스" : "캐릭터 선택", UIStyles.Big);

        var list = CharacterRoster.All;
        int n = list.Count;
        // 카드 크기: 화면에 맞춰 (최대 260 x 380)
        float gap = 24f;
        float cw = Mathf.Min(260f, (W - 80f - gap * (n - 1)) / n);
        float ch = Mathf.Min(380f, H * 0.46f);
        float total = cw * n + gap * (n - 1);
        float x0 = (W - total) / 2f, y0 = H * 0.17f;

        bool mouseMoved = false;
        if (Event.current.type == EventType.Repaint)
        {
            mouseMoved = (Event.current.mousePosition - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = Event.current.mousePosition;
        }

        for (int i = 0; i < n; i++)
        {
            var c = list[i];
            var r = new Rect(x0 + i * (cw + gap), y0, cw, ch);
            if (mouseMoved && r.Contains(Event.current.mousePosition)) selected = i;
            DrawCard(c, r, i == selected);

            // 클릭: 다른 카드면 선택, 이미 선택된 카드면 시작
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
            {
                if (selected == i) StartSelected();
                selected = i;
                Event.current.Use();
            }
        }

        DrawDetail(list[selected], new Rect(x0, y0 + ch + 20f, total, H * 0.2f));

        // 하단 버튼
        float bw = 220f, bh = 56f, by = H - bh - 54f;
        var cur = list[selected];
        bool locked = cur.availability == CharacterAvailability.Locked;
        GUI.color = locked ? new Color(1, 1, 1, 0.45f) : Color.white;
        if (GUI.Button(new Rect(W / 2f + 12f, by, bw, bh), locked ? "준비 중" : "<color=#F2BF4C>시작</color>", UIStyles.MenuButton)) StartSelected();
        GUI.color = Color.white;
        if (GUI.Button(new Rect(W / 2f - 12f - bw, by, bw, bh), "뒤로", UIStyles.MenuButton)) Close();

        if (toastTime > 0 && !string.IsNullOrEmpty(toast))
        {
            var col = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastTime / 0.4f));
            GUI.Label(new Rect(0, by - 40f, W, 30), toast, UIStyles.Center);
            GUI.color = col;
        }

        GUI.Label(new Rect(W - 460, H - 32, 450, 24), "←→ 이동   Enter 시작   Esc 뒤로",
                  new GUIStyle(UIStyles.Small) { alignment = TextAnchor.UpperRight });
    }

    void DrawCard(CharacterInfo c, Rect r, bool sel)
    {
        bool locked = c.availability == CharacterAvailability.Locked;

        // 배경 · 테두리
        GUI.color = sel ? UIStyles.Accent : new Color(1, 1, 1, 0.18f);
        GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UIStyles.White);
        GUI.color = sel ? new Color(0.13f, 0.11f, 0.08f, 0.96f) : new Color(0.08f, 0.08f, 0.09f, 0.92f);
        GUI.DrawTexture(r, UIStyles.White);
        GUI.color = Color.white;

        // 그림 영역 (카드 위쪽), 발이 영역 아래쪽에 오도록
        var art = new Rect(r.x + 10, r.y + 10, r.width - 20, r.height - 92);
        if (locked)
        {
            GUI.Label(art, "<size=96><color=#555555>?</color></size>", new GUIStyle(UIStyles.Center) { richText = true });
        }
        else
        {
            // 선택 = 달리기 애니메이션, 아니면 정지 자세 (정면)
            Sheet s = null;
            int frame = 0;
            if (sel && run.TryGetValue(c.id, out var rs) && rs != null)
            {
                s = rs;
                float fps = c.visual.runFps > 0 ? c.visual.runFps : 10f;
                frame = (int)(Time.unscaledTime * fps) % rs.cols;
            }
            else if (idle.TryGetValue(c.id, out var ids) && ids != null)
            {
                s = ids;
                float fps = c.visual.idleFps > 0 ? c.visual.idleFps : 1f;
                frame = (int)(Time.unscaledTime * fps) % ids.cols;
            }
            if (s != null) DrawFrame(s, 0, frame, art);
        }

        // 이름 · 상태
        var nameStyle = new GUIStyle(UIStyles.Center) { fontSize = 22, fontStyle = FontStyle.Bold, richText = true };
        string nameColor = locked ? "#777777" : sel ? "#F2BF4C" : "#FFFFFF";
        GUI.Label(new Rect(r.x, r.yMax - 78, r.width, 30), $"<color={nameColor}>{c.name}</color>", nameStyle);
        GUI.Label(new Rect(r.x, r.yMax - 50, r.width, 22), $"<color=#999999>{c.nameEn}</color>",
                  new GUIStyle(UIStyles.Center) { fontSize = 14, richText = true });
        string badge = c.availability switch
        {
            CharacterAvailability.Playable => "<color=#7CFF7C>플레이 가능</color>",
            CharacterAvailability.VisualOnly => "<color=#9AD0FF>외형 테스트</color>",
            _ => "<color=#888888>준비 중</color>",
        };
        GUI.Label(new Rect(r.x, r.yMax - 28, r.width, 22), badge, new GUIStyle(UIStyles.Center) { fontSize = 14, richText = true });
    }

    /// <summary>시트의 (row, col) 칸을 영역 안에 비율 유지, 아래 정렬로 그림</summary>
    static void DrawFrame(Sheet s, int row, int col, Rect area)
    {
        float cellW = s.tex.width / (float)s.cols, cellH = s.tex.height / (float)s.rows;
        float scale = Mathf.Min(area.width / cellW, area.height / cellH);
        float w = cellW * scale, h = cellH * scale;
        var dst = new Rect(area.x + (area.width - w) / 2f, area.yMax - h, w, h);
        // 텍스처 좌표는 아래에서 위로 → 시트의 row 행은 (rows - 1 - row)
        var uv = new Rect(col / (float)s.cols, (s.rows - 1 - row) / (float)s.rows, 1f / s.cols, 1f / s.rows);
        GUI.DrawTextureWithTexCoords(dst, s.tex, uv);
    }

    static void DrawDetail(CharacterInfo c, Rect r)
    {
        GUI.color = new Color(0, 0, 0, 0.45f);
        GUI.DrawTexture(r, UIStyles.White);
        GUI.color = Color.white;
        var label = new GUIStyle(UIStyles.Label) { fontSize = 20, richText = true };
        var small = new GUIStyle(UIStyles.Small) { fontSize = 16, richText = true };
        float x = r.x + 20, y = r.y + 14, half = (r.width - 40) / 2f;
        GUI.Label(new Rect(x, y, r.width - 40, 30), $"<b>{c.name}</b>  <color=#AAAAAA><size=15>{c.nameEn}</size></color>", label);
        y += 36;
        GUI.Label(new Rect(x, y, half, 24), $"속성  <color=#F2BF4C>{c.element}</color>     전투 방식  <color=#F2BF4C>{c.style}</color>", small);
        GUI.Label(new Rect(x + half, y, half, 24), $"공격 범위  <color=#F2BF4C>{c.range}</color>     공격 속도  <color=#F2BF4C>{c.speed}</color>", small);
        y += 28;
        GUI.Label(new Rect(x, y, r.width - 40, 24), $"특징  {c.feature}", small);
        y += 28;
        string noteColor = c.availability == CharacterAvailability.Playable ? "#7CFF7C"
                         : c.availability == CharacterAvailability.VisualOnly ? "#9AD0FF" : "#888888";
        GUI.Label(new Rect(x, y, r.width - 40, 24), $"<color={noteColor}>{c.note}</color>", small);
    }
}
