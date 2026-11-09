using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트용 치트 패널 (에디터 / 개발 빌드 전용, GameManager 가 붙여줌).
/// 화면 왼쪽 아래 버튼 또는 단축키로 켜고 끕니다.
///  F1 판정 표시 / F2 패널 열기 / F3 무적 / F4 원히트킬 / F5 적 체력 무제한
///  레벨 +1 / +10 (궁극기 획득 Lv.45 확인용), 궁극기 게이지 채우기
/// 상태는 static 이라 재시작(R)해도 유지됩니다.
/// </summary>
public class DebugCheats : MonoBehaviour
{
    /// <summary>플레이어가 피해를 받지 않음</summary>
    public static bool God;
    /// <summary>한 번 맞으면 적이 죽음 (적 체력 무제한과 동시에 켤 수 없음)</summary>
    public static bool OneHitKill;
    /// <summary>적이 피해 숫자만 띄우고 체력은 줄지 않음 (원히트킬과 동시에 켤 수 없음)</summary>
    public static bool ImmortalEnemies;

    static bool open;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) open = !open;
        if (Input.GetKeyDown(KeyCode.F3)) God = !God;
        if (Input.GetKeyDown(KeyCode.F4)) SetOneHitKill(!OneHitKill);
        if (Input.GetKeyDown(KeyCode.F5)) SetImmortalEnemies(!ImmortalEnemies);
    }

    static void SetOneHitKill(bool on)
    {
        OneHitKill = on;
        if (on) ImmortalEnemies = false;
    }

    static void SetImmortalEnemies(bool on)
    {
        ImmortalEnemies = on;
        if (on) OneHitKill = false;
    }

    void OnGUI()
    {
        UIStyles.Init();
        const float x = 12, w = 230, bh = 34, gap = 6;
        float H = Screen.height;

        if (!open)
        {
            // 닫혀 있어도 켜진 치트는 보이게 (끄는 걸 잊지 않도록)
            var on = new List<string>();
            if (God) on.Add("무적");
            if (OneHitKill) on.Add("원히트킬");
            if (ImmortalEnemies) on.Add("적 체력 무제한");
            if (HitboxView.Show) on.Add("판정 표시");
            if (on.Count > 0)
            {
                GUI.color = UIStyles.Accent;
                GUI.Label(new Rect(x, H - bh - 46, 600, 30), "치트: " + string.Join(" · ", on), UIStyles.Small);
                GUI.color = Color.white;
            }
            if (GUI.Button(new Rect(x, H - bh - 12, 140, bh), "치트 (F2)", UIStyles.MenuButton)) open = true;
            return;
        }

        string[] labels =
        {
            $"무적 (F3)  {OnOff(God)}",
            $"원히트킬 (F4)  {OnOff(OneHitKill)}",
            $"적 체력 무제한 (F5)  {OnOff(ImmortalEnemies)}",
            $"판정 표시 (F1)  {OnOff(HitboxView.Show)}",
            "레벨 +1",
            "레벨 +10",
            "궁극기 게이지 채우기",
            "닫기 (F2)",
        };
        float top = H - 12 - labels.Length * (bh + gap);

        GUI.color = UIStyles.Dim;
        GUI.DrawTexture(new Rect(x - 6, top - 6, w + 12, labels.Length * (bh + gap) + 6), UIStyles.White);
        GUI.color = Color.white;

        for (int i = 0; i < labels.Length; i++)
        {
            var r = new Rect(x, top + i * (bh + gap), w, bh);
            if (!GUI.Button(r, labels[i], Small)) continue;
            switch (i)
            {
                case 0: God = !God; break;
                case 1: SetOneHitKill(!OneHitKill); break;
                case 2: SetImmortalEnemies(!ImmortalEnemies); break;
                case 3: HitboxView.Show = !HitboxView.Show; break;
                case 4: GameManager.I?.DebugAddLevels(1); break;
                case 5: GameManager.I?.DebugAddLevels(10); break;
                case 6: GameManager.I?.DebugFillUltimate(); break;
                case 7: open = false; break;
            }
        }
    }

    static string OnOff(bool b) => b ? "<color=#7CFF7C>ON</color>" : "<color=#888888>OFF</color>";

    static GUIStyle small;
    static GUIStyle Small => small ??= new GUIStyle(UIStyles.MenuButton) { fontSize = 17 };
}
