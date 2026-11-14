using System;
using UnityEngine;

/// <summary>
/// 레이드 난이도 선택 화면 (레이드 보스 선택 → 이 화면 → 캐릭터 선택). 목록은 RaidDifficultyInfo.All.
/// 잠긴 난이도 (하드 · 익스트림, 준비 중) 는 보이지만 고를 수 없음.
/// 키보드: ↑/↓ (W/S) 이동, Enter/Space 선택, Esc/Backspace 뒤로. 마우스: 올리면 선택, 클릭 = 결정.
/// </summary>
public class RaidDifficultyScreen : MonoBehaviour
{
    Action<RaidDifficultyInfo> onPick;
    Action onBack;
    RaidBossInfo boss;
    int selected;
    Vector2 lastMouse;

    static int Count => RaidDifficultyInfo.All.Count;

    public static RaidDifficultyScreen Open(GameObject host, RaidBossInfo boss, Action<RaidDifficultyInfo> onPick, Action onBack)
    {
        var s = host.AddComponent<RaidDifficultyScreen>();
        s.boss = boss;
        s.onPick = onPick;
        s.onBack = onBack;
        s.selected = Mathf.Max(0, RaidDifficultyInfo.All.FindIndex(d => d.difficulty == RaidStage.Difficulty));
        return s;
    }

    void Update()
    {
        int n = Count + 1;   // 마지막 = 뒤로
        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) selected = (selected - 1 + n) % n;
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) selected = (selected + 1) % n;
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)) Choose(selected);
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Backspace)) Close();
    }

    void Choose(int i)
    {
        if (i >= Count) { Close(); return; }
        var d = RaidDifficultyInfo.All[i];
        if (d.locked) return;   // 준비 중
        Destroy(this);
        onPick?.Invoke(d);
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
        GUI.Label(new Rect(0, H * 0.1f, W, 60), "난이도 선택", UIStyles.Big);
        if (boss != null)
            GUI.Label(new Rect(0, H * 0.1f + 58, W, 30), boss.name, new GUIStyle(UIStyles.Label) { alignment = TextAnchor.MiddleCenter, normal = { textColor = UIStyles.Accent } });

        bool mouseMoved = false;
        if (Event.current.type == EventType.Repaint)
        {
            mouseMoved = (Event.current.mousePosition - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = Event.current.mousePosition;
        }

        float cw = 560, ch = 80, gap = 14;
        float x = (W - cw) / 2, y = H * 0.26f;
        var nameStyle = new GUIStyle(UIStyles.Label) { fontSize = 26, fontStyle = FontStyle.Bold, richText = true };
        var small = new GUIStyle(UIStyles.Small) { fontSize = 16, richText = true };
        for (int i = 0; i < Count; i++)
        {
            var d = RaidDifficultyInfo.All[i];
            var r = new Rect(x, y + i * (ch + gap), cw, ch);
            if (mouseMoved && r.Contains(Event.current.mousePosition)) selected = i;
            bool sel = selected == i;

            GUI.color = sel ? (d.locked ? new Color(1, 1, 1, 0.35f) : UIStyles.Accent) : new Color(1, 1, 1, 0.18f);
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), UIStyles.White);
            GUI.color = sel && !d.locked ? new Color(0.13f, 0.11f, 0.08f, 0.96f) : new Color(0.08f, 0.08f, 0.09f, 0.92f);
            GUI.DrawTexture(r, UIStyles.White);
            GUI.color = Color.white;

            string nameCol = d.locked ? "#777777" : sel ? "#F2BF4C" : "#FFFFFF";
            string phases = d.locked ? "준비 중" : d.PhaseText;
            GUI.Label(new Rect(r.x + 24, r.y + 8, cw - 48, 34),
                      $"<color={nameCol}>{d.name}</color>  <size=16><color=#999999>{d.nameEn}</color></size>", nameStyle);
            GUI.Label(new Rect(r.x + 24, r.y + 46, cw - 48, 24), $"<color=#F2BF4C>{phases}</color>   <color=#AAAAAA>{d.note}</color>", small);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && r.Contains(Event.current.mousePosition))
            {
                Event.current.Use();
                Choose(i);
                return;
            }
        }

        float bw = 220, bh = 56;
        var back = new Rect((W - bw) / 2, y + Count * (ch + gap) + 20, bw, bh);
        if (mouseMoved && back.Contains(Event.current.mousePosition)) selected = Count;
        string label = selected == Count ? "▶ 뒤로 ◀" : "뒤로";
        if (GUI.Button(back, label, UIStyles.MenuButton)) Close();

        GUI.Label(new Rect(W - 460, H - 32, 450, 24), "↑↓ 이동   Enter 선택   Esc 뒤로",
                  new GUIStyle(UIStyles.Small) { alignment = TextAnchor.UpperRight });
    }
}
