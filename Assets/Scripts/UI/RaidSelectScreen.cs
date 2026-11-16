using System;
using UnityEngine;

/// <summary>
/// 레이드 보스 선택 화면 (타이틀 "레이드 보스" → 이 화면 → 캐릭터 선택). 목록은 RaidBossRoster.All.
/// 키보드: ↑/↓ (W/S) 이동, Enter/Space 선택, Esc/Backspace 뒤로. 마우스: 올리면 선택, 클릭 = 결정.
/// </summary>
public class RaidSelectScreen : MonoBehaviour
{
    Action<RaidBossInfo> onPick;
    Action onBack;
    int selected;
    Vector2 lastMouse;

    public static RaidSelectScreen Open(GameObject host, Action<RaidBossInfo> onPick, Action onBack)
    {
        var s = host.AddComponent<RaidSelectScreen>();
        s.onPick = onPick;
        s.onBack = onBack;
        s.selected = Mathf.Max(0, RaidBossRoster.All.IndexOf(RaidStage.SelectedInfo));
        return s;
    }

    void Update()
    {
        int n = RaidBossRoster.All.Count + 1;   // 마지막 = 뒤로
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected = (selected - 1 + n) % n;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected = (selected + 1) % n;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Choose(selected);
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Close();
    }

    void Choose(int i)
    {
        if (i >= RaidBossRoster.All.Count) { Close(); return; }
        var b = RaidBossRoster.All[i];
        if (b.locked) return;   // 개발 예정
        Destroy(this);
        onPick?.Invoke(b);
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
        GUI.Label(new Rect(0, H * 0.12f, W, 60), "레이드 보스 선택", UIStyles.Big);

        bool mouseMoved = false;
        if (Event.current.type == EventType.Repaint)
        {
            mouseMoved = (Event.current.mousePosition - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = Event.current.mousePosition;
        }

        float cw = 620, ch = 96, gap = 18;
        float x = (W - cw) / 2, y = H * 0.22f;   // 보스 4종 (2종 개발 예정) 이 1280×720 창에서도 들어가도록 위로
        var nameStyle = new GUIStyle(UIStyles.Label) { fontSize = 28, fontStyle = FontStyle.Bold, richText = true };
        var small = new GUIStyle(UIStyles.Small) { fontSize = 16, richText = true };
        for (int i = 0; i < RaidBossRoster.All.Count; i++)
        {
            var b = RaidBossRoster.All[i];
            var r = new Rect(x, y + i * (ch + gap), cw, ch);
            if (mouseMoved && r.Contains(Event.current.mousePosition)) selected = i;
            bool sel = selected == i;

            GUI.color = sel ? (b.locked ? new Color(1, 1, 1, 0.35f) : UIStyles.Accent) : new Color(1, 1, 1, 0.18f);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UIStyles.White);
            GUI.color = sel && !b.locked ? new Color(0.13f, 0.11f, 0.08f, 0.96f) : new Color(0.08f, 0.08f, 0.09f, 0.92f);
            GUI.DrawTexture(r, UIStyles.White);
            GUI.color = Color.white;

            string nameCol = b.locked ? "#777777" : sel ? "#F2BF4C" : "#FFFFFF";
            GUI.Label(new Rect(r.x + 24, r.y + 10, cw - 48, 36),
                      $"<color={nameCol}>{b.name}</color>  <size=16><color=#999999>{b.nameEn}</color></size>", nameStyle);
            string detail = b.locked ? $"<color=#777777>{b.note}</color>" : $"<color=#F2BF4C>{b.element}</color>   <color=#AAAAAA>{b.note}</color>";
            GUI.Label(new Rect(r.x + 24, r.y + 50, cw - 48, 24), detail, small);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                Choose(i);
                return;
            }
        }

        float bw = 220, bh = 56;
        var back = new Rect((W - bw) / 2, y + RaidBossRoster.All.Count * (ch + gap) + 20, bw, bh);
        if (mouseMoved && back.Contains(Event.current.mousePosition)) selected = RaidBossRoster.All.Count;
        string label = selected == RaidBossRoster.All.Count ? "▶ 뒤로 ◀" : "뒤로";
        if (GUI.Button(back, label, UIStyles.MenuButton)) Close();

        GUI.Label(new Rect(W - 460, H - 32, 450, 24), "↑↓ 이동   Enter 선택   Esc 뒤로",
                  new GUIStyle(UIStyles.Small) { alignment = TextAnchor.UpperRight });
    }
}
