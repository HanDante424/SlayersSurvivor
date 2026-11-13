using System;
using UnityEngine;

/// <summary>
/// 화면 설정 (2026-11-13 사용자): 화면 모드 전체화면 / 창화면 · 해상도 1920 × 1080 / 1280 × 720.
/// PlayerPrefs 에 저장, 게임을 켤 때(타이틀) 저장된 값을 적용. 에디터에서는 저장만 되고 화면은 바뀌지 않음.
/// </summary>
public static class GameSettings
{
    public struct Res
    {
        public int w, h;
        public string Label => $"{w} × {h}";
    }

    public static readonly Res[] Resolutions = { new Res { w = 1920, h = 1080 }, new Res { w = 1280, h = 720 } };
    public static readonly string[] ModeLabels = { "전체화면", "창화면" };

    const string KeyMode = "SlayersSurvivor.ScreenMode", KeyRes = "SlayersSurvivor.Resolution";

    /// <summary>0 = 전체화면, 1 = 창화면</summary>
    public static int Mode = 0;
    /// <summary>Resolutions 번호</summary>
    public static int ResIndex = 0;

    static bool appliedOnce;

    public static void Load()
    {
        Mode = Mathf.Clamp(PlayerPrefs.GetInt(KeyMode, 0), 0, ModeLabels.Length - 1);
        ResIndex = Mathf.Clamp(PlayerPrefs.GetInt(KeyRes, 0), 0, Resolutions.Length - 1);
    }

    public static void Save()
    {
        PlayerPrefs.SetInt(KeyMode, Mode);
        PlayerPrefs.SetInt(KeyRes, ResIndex);
        PlayerPrefs.Save();
    }

    /// <summary>현재 값을 화면에 적용</summary>
    public static void Apply()
    {
        var r = Resolutions[ResIndex];
        // 전체화면 = 테두리 없는 전체화면 창 (해상도는 화면 크기에 맞춰 늘어남), 창화면 = 지정 해상도 창
        Screen.SetResolution(r.w, r.h, Mode == 0 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
    }

    /// <summary>게임을 켠 뒤 처음 한 번: 저장된 설정 적용 (타이틀 화면 Awake)</summary>
    public static void ApplySavedOnce()
    {
        if (appliedOnce) return;
        appliedOnce = true;
        Load();
#if !UNITY_EDITOR
        Apply();
#endif
    }
}

/// <summary>
/// 설정 화면 (타이틀 화면 위에 열림). 키보드: ↑/↓ (W/S) 항목, ←/→ (A/D) 값 변경, Enter 적용, Esc/Backspace 뒤로. 마우스 클릭도 가능.
/// 값은 "적용"을 눌러야 화면에 반영 · 저장됨.
/// </summary>
public class SettingsScreen : MonoBehaviour
{
    Action onBack;
    int row;                 // 0 = 화면 모드, 1 = 해상도, 2 = 적용, 3 = 뒤로
    int mode, res;           // 고르는 중인 값 (적용 전)
    string toast;
    float toastTime;
    Vector2 lastMouse;

    public static SettingsScreen Open(GameObject host, Action onBack)
    {
        var s = host.AddComponent<SettingsScreen>();
        s.onBack = onBack;
        GameSettings.Load();
        s.mode = GameSettings.Mode;
        s.res = GameSettings.ResIndex;
        return s;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) row = (row + 3) % 4;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) row = (row + 1) % 4;
        if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) Change(-1);
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) Change(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
        {
            if (row == 3) Close();
            else if (row == 2) ApplyNow();
            else Change(1);
        }
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Close();
        if (toastTime > 0) toastTime -= Time.unscaledDeltaTime;
    }

    void Change(int d)
    {
        if (row == 0) mode = (mode + d + GameSettings.ModeLabels.Length) % GameSettings.ModeLabels.Length;
        else if (row == 1) res = (res + d + GameSettings.Resolutions.Length) % GameSettings.Resolutions.Length;
    }

    void ApplyNow()
    {
        GameSettings.Mode = mode;
        GameSettings.ResIndex = res;
        GameSettings.Save();
        GameSettings.Apply();
#if UNITY_EDITOR
        toast = "저장됨 (에디터에서는 화면이 바뀌지 않음 — 빌드에서 적용)";
#else
        toast = "적용됨";
#endif
        toastTime = 2f;
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
        UIStyles.Overlay(new Color(0, 0, 0, 0.6f));
        GUI.Label(new Rect(0, H * 0.14f, W, 60), "설정", UIStyles.Big);

        bool mouseMoved = false;
        if (Event.current.type == EventType.Repaint)
        {
            mouseMoved = (Event.current.mousePosition - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = Event.current.mousePosition;
        }

        float rw = 560, rh = 64, gap = 16;
        float x = (W - rw) / 2, y = H * 0.32f;
        OptionRow(0, new Rect(x, y, rw, rh), "화면 모드", GameSettings.ModeLabels[mode], mouseMoved);
        OptionRow(1, new Rect(x, y + (rh + gap), rw, rh), "해상도", GameSettings.Resolutions[res].Label, mouseMoved);

        // 지금 적용된 화면
        var cur = $"현재  {Screen.width} × {Screen.height}  ·  {(Screen.fullScreenMode == FullScreenMode.Windowed ? "창화면" : "전체화면")}";
        GUI.Label(new Rect(x, y + (rh + gap) * 2 - 6, rw, 28), cur, new GUIStyle(UIStyles.Small) { alignment = TextAnchor.MiddleCenter });

        float bw = 220, by = y + (rh + gap) * 2 + 34;
        ButtonRow(2, new Rect(W / 2f + 12f, by, bw, rh), "<color=#F2BF4C>적용</color>", ApplyNow, mouseMoved);
        ButtonRow(3, new Rect(W / 2f - 12f - bw, by, bw, rh), "뒤로", Close, mouseMoved);

        if (toastTime > 0 && !string.IsNullOrEmpty(toast))
        {
            var c = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastTime / 0.4f));
            GUI.Label(new Rect(0, by + rh + 20, W, 30), toast, UIStyles.Center);
            GUI.color = c;
        }

        GUI.Label(new Rect(W - 560, H - 32, 550, 24), "↑↓ 항목   ←→ 변경   Enter 적용   Esc 뒤로",
                  new GUIStyle(UIStyles.Small) { alignment = TextAnchor.UpperRight });
    }

    /// <summary>"이름   ◀ 값 ▶" 한 줄. 화살표 클릭 = 값 변경</summary>
    void OptionRow(int index, Rect r, string name, string value, bool mouseMoved)
    {
        if (mouseMoved && r.Contains(Event.current.mousePosition)) row = index;
        bool sel = row == index;
        GUI.color = sel ? UIStyles.Accent : new Color(1, 1, 1, 0.18f);
        GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UIStyles.White);
        GUI.color = sel ? new Color(0.13f, 0.11f, 0.08f, 0.96f) : new Color(0.08f, 0.08f, 0.09f, 0.92f);
        GUI.DrawTexture(r, UIStyles.White);
        GUI.color = Color.white;

        var nameStyle = new GUIStyle(UIStyles.Label) { fontSize = 24, alignment = TextAnchor.MiddleLeft, richText = true };
        GUI.Label(new Rect(r.x + 24, r.y, 200, r.height), sel ? $"<color=#F2BF4C>{name}</color>" : name, nameStyle);

        float aw = 48;
        var left = new Rect(r.xMax - 300, r.y + 8, aw, r.height - 16);
        var right = new Rect(r.xMax - aw - 16, r.y + 8, aw, r.height - 16);
        if (GUI.Button(left, "◀", UIStyles.MenuButton)) { row = index; Change(-1); }
        if (GUI.Button(right, "▶", UIStyles.MenuButton)) { row = index; Change(1); }
        GUI.Label(new Rect(left.xMax, r.y, right.x - left.xMax, r.height), value,
                  new GUIStyle(UIStyles.Center) { fontSize = 24 });
    }

    void ButtonRow(int index, Rect r, string label, Action action, bool mouseMoved)
    {
        if (mouseMoved && r.Contains(Event.current.mousePosition)) row = index;
        string text = row == index ? $"▶ {label} ◀" : label;
        if (GUI.Button(r, text, UIStyles.MenuButton)) { row = index; action(); }
    }
}
