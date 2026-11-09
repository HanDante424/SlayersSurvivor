using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 테스트용 치트 패널 (에디터 / 개발 빌드 전용, GameManager 가 붙여줌).
/// 화면 왼쪽 아래 버튼 또는 단축키로 켜고 끕니다.
///  F1 판정 표시 / F2 패널 열기 / F3 무적 / F4 원히트킬 / F5 적 체력 무제한
///  F6 쿨타임 감소 100% / F7 자동 스킬 사용 금지 / F8 적 생성 중지 / F9 경험치 미드랍
///  레벨 +1 / +10 (궁극기 획득 Lv.45 확인용), 궁극기 게이지 채우기
///  오른쪽 열: 스킬별 사용 금지 (레벨업 후보 스킬 + 보유 스킬) / Lv+1 = 그 스킬의 기본 강화 화면 바로 열기
///            (Lv.5·10·15 특수 강화 확인용, 캐릭터 레벨은 그대로)
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
    /// <summary>모든 스킬 쿨타임 0 (자동 스킬은 매 프레임 발동)</summary>
    public static bool NoCooldown;
    /// <summary>자동 스킬이 발동하지 않음 — 수동 스킬만 테스트할 때</summary>
    public static bool NoAutoSkills;
    /// <summary>새 적(보스 포함)이 생성되지 않음. 이미 있는 적은 그대로</summary>
    public static bool NoSpawn;
    /// <summary>적이 죽어도 경험치 보석을 떨어뜨리지 않음 — 레벨업 화면 없이 스킬만 볼 때</summary>
    public static bool NoXpDrop;

    /// <summary>스킬별 사용 금지 (SkillEntry.id). 자동 = 발동 안 함, 수동 = 키 입력 무시</summary>
    static readonly HashSet<string> disabledSkills = new HashSet<string>();
    public static bool IsSkillDisabled(string id) => disabledSkills.Contains(id);
    static void ToggleSkill(string id) { if (!disabledSkills.Remove(id)) disabledSkills.Add(id); }

    static bool open;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2)) open = !open;
        if (Input.GetKeyDown(KeyCode.F3)) God = !God;
        if (Input.GetKeyDown(KeyCode.F4)) SetOneHitKill(!OneHitKill);
        if (Input.GetKeyDown(KeyCode.F5)) SetImmortalEnemies(!ImmortalEnemies);
        if (Input.GetKeyDown(KeyCode.F6)) NoCooldown = !NoCooldown;
        if (Input.GetKeyDown(KeyCode.F7)) NoAutoSkills = !NoAutoSkills;
        if (Input.GetKeyDown(KeyCode.F8)) NoSpawn = !NoSpawn;
        if (Input.GetKeyDown(KeyCode.F9)) NoXpDrop = !NoXpDrop;
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
            if (NoCooldown) on.Add("쿨타임 0");
            if (NoAutoSkills) on.Add("자동 스킬 금지");
            if (NoSpawn) on.Add("적 생성 중지");
            if (NoXpDrop) on.Add("경험치 미드랍");
            if (HitboxView.Show) on.Add("판정 표시");
            foreach (var e in SkillList()) if (disabledSkills.Contains(e.id)) on.Add($"{e.name} 금지");
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
            $"쿨타임 감소 100% (F6)  {OnOff(NoCooldown)}",
            $"자동 스킬 금지 (F7)  {OnOff(NoAutoSkills)}",
            $"적 생성 중지 (F8)  {OnOff(NoSpawn)}",
            $"경험치 미드랍 (F9)  {OnOff(NoXpDrop)}",
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
                case 4: NoCooldown = !NoCooldown; break;
                case 5: NoAutoSkills = !NoAutoSkills; break;
                case 6: NoSpawn = !NoSpawn; break;
                case 7: NoXpDrop = !NoXpDrop; break;
                case 8: GameManager.I?.DebugAddLevels(1); break;
                case 9: GameManager.I?.DebugAddLevels(10); break;
                case 10: GameManager.I?.DebugFillUltimate(); break;
                case 11: open = false; break;
            }
        }

        // ---- 오른쪽 열: 스킬별 사용 금지 ----
        var skillsToShow = SkillList();
        if (skillsToShow.Count == 0) return;
        float sx = x + w + 18, sw = 250, lw = 74;
        float stop = H - 12 - (skillsToShow.Count + 1) * (bh + gap);
        GUI.color = UIStyles.Dim;
        GUI.DrawTexture(new Rect(sx - 6, stop - 6, sw + lw + 18, (skillsToShow.Count + 1) * (bh + gap) + 6), UIStyles.White);
        GUI.color = Color.white;
        GUI.Label(new Rect(sx, stop, sw, bh), "스킬별 사용 금지", UIStyles.Small);
        for (int k = 0; k < skillsToShow.Count; k++)
        {
            var e = skillsToShow[k];
            var r = new Rect(sx, stop + (k + 1) * (bh + gap), sw, bh);
            string kind = SkillCatalog.KindLabel(e.kind);
            if (GUI.Button(r, $"{e.name} <size=13>[{kind}]</size>  {OnOff(disabledSkills.Contains(e.id))}", Small))
                ToggleSkill(e.id);
            var owned = Player.I != null ? Player.I.GetSkill(e.id) : null;
            GUI.enabled = owned != null && owned.IsGeneral && owned.level < owned.LevelCap;
            if (GUI.Button(new Rect(sx + sw + 6, r.y, lw, bh), owned != null ? $"Lv.{owned.level}+1" : "Lv+1", Small))
                GameManager.I?.DebugUpgradeSkill(owned);
            GUI.enabled = true;
        }
    }

    /// <summary>패널에 보일 스킬: 레벨업 후보 스킬 + 이미 보유한 스킬 (카탈로그 순서)</summary>
    static List<SkillEntry> SkillList()
    {
        var list = new List<SkillEntry>();
        var p = Player.I;
        if (p == null) return list;
        foreach (var e in p.Catalog)
            if (SkillCatalog.LevelUpPool.Contains(e.id) || p.GetSkill(e.id) != null) list.Add(e);
        return list;
    }

    static string OnOff(bool b) => b ? "<color=#7CFF7C>ON</color>" : "<color=#888888>OFF</color>";

    static GUIStyle small;
    static GUIStyle Small => small ??= new GUIStyle(UIStyles.MenuButton) { fontSize = 17, richText = true };
}
