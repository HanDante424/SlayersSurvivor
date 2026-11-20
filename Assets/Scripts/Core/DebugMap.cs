using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 디버그 맵에서 패턴을 하나씩 실행할 수 있는 보스 AI (2026-11-16 사용자). 가니악 · 카구츠치 (1 · 2페이즈) · 홉 고블린 기사 · 고블린 장로가 구현.
/// 자동 패턴 선택을 끄고 (DebugAuto = false) DebugRun 으로 지정한 패턴만 실행. 패턴 안의 단계 선택은 아직 없음 (패턴 단위).
/// </summary>
public interface IDebugBoss
{
    /// <summary>지금 상태 (페이즈 포함) 에서 고를 수 있는 패턴 이름 (DebugRun 의 index 순서)</summary>
    string[] DebugPatternNames { get; }
    /// <summary>true = 평소처럼 자동으로 패턴 선택, false = DebugRun 으로 지정한 패턴만 (보스는 제자리에 대기)</summary>
    bool DebugAuto { get; set; }
    /// <summary>한 줄 상태 (실행 중인 패턴 이름 등)</summary>
    string DebugStatus { get; }
    /// <summary>소환 직후 호출: 입장 연출을 건너뛰고 phase 페이즈의 전투 대기 상태로 (카구츠치 1 · 2, 나머지는 무시)</summary>
    void DebugBegin(int phase, bool playIntro);
    /// <summary>index 번 패턴을 지금 바로 실행 (진행 중인 패턴은 취소). 쿨타임 · 거리 조건을 무시</summary>
    void DebugRun(int index);
    /// <summary>진행 중인 패턴을 취소하고 경고 · 소환물을 정리 (대기 상태로)</summary>
    void DebugCancel();
}

/// <summary>디버그 맵 "스프라이트" 메뉴: 패턴을 실행하지 않고 보스의 포즈 스프라이트만 바꿔 보기. 되돌리기는 IDebugBoss.DebugCancel</summary>
public interface IDebugSprites
{
    string[] DebugSpriteNames { get; }
    /// <summary>index 번 스프라이트를 그 자리에 계속 표시 (진행 중인 패턴은 취소)</summary>
    void DebugShowSprite(int index);
}

/// <summary>디버그 맵에서 소환할 수 있는 대상 1종</summary>
public class DebugEntry
{
    public string label;
    public EnemyKind kind;
    public int phase = 1;
    public bool boss;
    /// <summary>소환 위치 — 기본 맵 기준 좌표 (맵 크기를 바꿔도 이 값을 쓰고 맵 밖이면 안쪽으로 보정)</summary>
    public Vector2 basePos;
}

/// <summary>디버그 맵 상태: 소환 목록 · 소환 · 정리</summary>
public static class DebugMap
{
    // 위치는 기본 맵 (18 × 12 절반) 기준. 카구츠치 1페이즈 = 입장 때 멈추는 자리 (맵 위쪽에서 3 안), 2페이즈 = 전환 뒤 맵 중앙 (사용자)
    public static readonly List<DebugEntry> Bosses = new List<DebugEntry>
    {
        new DebugEntry { label = "가니악 벤데스트라", kind = EnemyKind.Ganiac, boss = true, basePos = new Vector2(0f, 2f) },
        new DebugEntry { label = "카구츠치 1페이즈", kind = EnemyKind.Kagutsuchi, phase = 1, boss = true, basePos = new Vector2(0f, 9f) },
        new DebugEntry { label = "카구츠치 2페이즈", kind = EnemyKind.Kagutsuchi, phase = 2, boss = true, basePos = new Vector2(0f, 0f) },
        new DebugEntry { label = "고블린 장로", kind = EnemyKind.GoblinElder, boss = true, basePos = new Vector2(0f, 4f) },
        new DebugEntry { label = "홉 고블린 기사", kind = EnemyKind.HobgoblinKnight, boss = true, basePos = new Vector2(0f, 4f) },
    };

    public static readonly List<DebugEntry> Monsters = new List<DebugEntry>
    {
        new DebugEntry { label = "고블린", kind = EnemyKind.Goblin, basePos = new Vector2(0f, 5f) },
        new DebugEntry { label = "고블린 궁수", kind = EnemyKind.GoblinArcher, basePos = new Vector2(0f, 5f) },
        new DebugEntry { label = "홉 고블린", kind = EnemyKind.Hobgoblin, basePos = new Vector2(0f, 5f) },
        new DebugEntry { label = "고블린 라이더", kind = EnemyKind.GoblinRider, basePos = new Vector2(0f, 5f) },
    };

    /// <summary>플레이어 시작 위치 (기본 맵 기준, 레이드와 같음)</summary>
    public static readonly Vector2 PlayerBasePos = new Vector2(0f, -6f);

    /// <summary>true = 보스를 소환할 때 등장 연출을 처음부터 재생 (끝난 뒤 자동 패턴은 시작하지 않음). false = 바로 전투 대기 상태</summary>
    public static bool PlayIntro;

    public static readonly List<Enemy> Spawned = new List<Enemy>();
    /// <summary>패널이 다루는 대상 (마지막으로 소환한 것 또는 고른 것)</summary>
    public static Enemy Target { get; set; }
    public static Enemy TargetAlive => Target != null && Target.hp > 0f ? Target : null;

    public static void Clean() => Spawned.RemoveAll(e => e == null);

    static bool IsRaidKind(EnemyKind k) => k == EnemyKind.Ganiac || k == EnemyKind.Kagutsuchi;

    /// <summary>기본 맵 기준 위치를 지금 맵 안으로 보정 (몸 반경만큼 안쪽)</summary>
    public static Vector2 PlaceInMap(Vector2 basePos, float radius) => Stage.ClampInside(basePos, radius + 0.1f);

    /// <summary>대상 소환. count 는 몬스터만 의미 있음 (보스는 1마리). 같은 계열 레이드 보스는 하나만 유지</summary>
    public static Enemy Spawn(DebugEntry en, int count)
    {
        Clean();
        if (IsRaidKind(en.kind)) RemoveRaidBosses();
        if (en.boss && !IsRaidKind(en.kind)) RemoveKind(en.kind);
        Enemy last = null;
        int n = en.boss ? 1 : Mathf.Clamp(count, 1, 12);
        bool intro = PlayIntro && en.boss;
        // 카구츠치 2페이즈의 등장 연출 = 1페이즈 자리에서 시작하는 실제 페이즈 전환 연출 (진입 → 입장 → 체력 회복)
        bool p2intro = intro && en.kind == EnemyKind.Kagutsuchi && en.phase == 2;
        bool ownEntrance = intro && IsRaidKind(en.kind) && !p2intro;   // 가니악 · 카구츠치 1페이즈: 보스가 스스로 등장 위치를 정함
        for (int i = 0; i < n; i++)
        {
            Vector2 at = p2intro ? new Vector2(0f, 9f) : en.basePos;
            if (n > 1) at += new Vector2((i - (n - 1) * 0.5f) * 1.2f, 0f);   // 여러 마리면 가로로 벌림
            if (IsRaidKind(en.kind)) RaidStage.DebugSetPhase(p2intro ? 1 : en.phase);
            if (en.kind == EnemyKind.Kagutsuchi) ArenaView.SetBackdropPhase(p2intro ? 1 : en.phase);   // 전환 연출이면 1페이즈 배경에서 시작해 점멸 때 교체
            var e = Enemy.Spawn(en.kind, at, 1f);
            if (IsRaidKind(en.kind)) RaidStage.DebugAdopt(e);
            if (e.TryGetComponent<IDebugBoss>(out var d))
            {
                d.DebugAuto = false;       // 디버그: 지정한 패턴만 (등장 연출 뒤에도 자동 패턴은 시작하지 않음)
                d.DebugBegin(p2intro ? 1 : en.phase, ownEntrance);
            }
            if (!ownEntrance) e.transform.position = PlaceInMap(at, e.radius);
            if (p2intro) RaidStage.DebugBeginTransition(e);
            Spawned.Add(e);
            last = e;
        }
        Target = last;
        return last;
    }

    static void RemoveRaidBosses()
    {
        foreach (var e in Spawned) if (e != null && IsRaidKind(e.kind)) Destroy(e);
    }

    static void RemoveKind(EnemyKind k)
    {
        foreach (var e in Spawned) if (e != null && e.kind == k) Destroy(e);
    }

    /// <summary>적 하나 제거 (진행 중 패턴 · 경고 · 소환물은 각 AI 의 OnDisable 이 정리)</summary>
    public static void Destroy(Enemy e)
    {
        if (e == null) return;
        if (e.TryGetComponent<IDebugBoss>(out var d)) d.DebugCancel();
        e.gameObject.SetActive(false);
        Object.Destroy(e.gameObject);
        if (Target == e) Target = null;
    }

    public static void ClearAll()
    {
        foreach (var e in Spawned.ToArray()) Destroy(e);
        Spawned.Clear();
        Target = null;
    }
}

/// <summary>
/// 디버그 맵 패널 (화면 오른쪽) · 시간 · 플레이어 조작. GameManager.Awake 가 디버그 맵에서만 붙임 (개발 빌드 전용).
/// 우클릭 = 플레이어를 그 위치로 이동.
/// </summary>
public class DebugMapController : MonoBehaviour
{
    const float PanelWidth = 300f, PanelMargin = 8f;
    static readonly float[] TimeScales = { 0f, 0.25f, 0.5f, 1f };

    Rect panelRect;
    Vector2 scroll;
    int monsterCount = 1;
    bool showSpriteInfo = true;
    bool panelOpen = true;                                   // 디버그 창 전체 켜기 · 끄기 (사용자 2026-11-16, 버튼 또는 ` 키)
    readonly System.Collections.Generic.Dictionary<string, bool> sectionOpen = new System.Collections.Generic.Dictionary<string, bool>();   // 태그 (구역) 별 접기 · 펴기
    Rect toggleRect;
    float timeScale = 1f;
    int stepFrames;               // > 0 이면 한 프레임만 진행 후 다시 정지
    GUIStyle btn, btnOn, label, head, info;

    void Start()
    {
        // 디버그 맵 기본: 플레이어 무적 · 적 체력 고정 (패턴을 계속 보려고) — 패널에서 끌 수 있음
        DebugCheats.God = true;
        DebugCheats.ImmortalEnemies = true;
        DebugCheats.OneHitKill = false;
        PlacePlayer();
    }

    void PlacePlayer()
    {
        var p = Player.I;
        if (p == null) return;
        Vector2 off = p.HurtCenter - (Vector2)p.transform.position;
        p.transform.position = Stage.ClampInside(DebugMap.PlayerBasePos, p.radius + 0.1f) - off;
    }

    bool autoHeal;

    void Update()
    {
        // 체력 회복: H 키 = 즉시 최대, 자동 회복 켜면 매 프레임 최대 유지 (무적과 달리 피격 연출은 그대로 보임)
        if (Player.I != null && (autoHeal || Input.GetKeyDown(KeyCode.H))) Player.I.hp = Player.I.maxHp;

        if (Input.GetKeyDown(KeyCode.BackQuote)) panelOpen = !panelOpen;   // ` 키: 디버그 창 켜기 · 끄기

        // 우클릭: 플레이어 이동 (패널 위는 제외)
        if (Input.GetMouseButtonDown(1) && Player.I != null && Camera.main != null)
        {
            Vector2 gui = new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y);
            if (!panelRect.Contains(gui) && !toggleRect.Contains(gui))
            {
                Vector2 world = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                var p = Player.I;
                Vector2 off = p.HurtCenter - (Vector2)p.transform.position;
                p.transform.position = Stage.ClampInside(world, p.radius + 0.05f) - off;
            }
        }
    }

    void LateUpdate()
    {
        if (GameManager.I == null || GameManager.I.State != GameState.Playing) return;
        if (ImpactFrame.Frozen) return;   // 임팩트 프레임 (시간 정지) 중에는 시간 배속을 덮어쓰지 않음
        // 한 프레임 진행: 이번 LateUpdate 에서 1 로 올려 다음 프레임이 진행되고, 그 프레임의 LateUpdate 에서 다시 정지
        if (stepFrames > 0)
        {
            Time.timeScale = 1f;
            stepFrames--;
            if (stepFrames == 0) timeScale = 0f;
            return;
        }
        Time.timeScale = timeScale;
    }

    // ---------- 패널 ----------
    void Styles()
    {
        if (btn != null) return;
        btn = new GUIStyle(UIStyles.MenuButton) { fontSize = 15, richText = true };
        btnOn = new GUIStyle(btn);
        btnOn.normal.textColor = UIStyles.Accent;
        label = new GUIStyle(UIStyles.Small) { fontSize = 14, richText = true, wordWrap = true };
        head = new GUIStyle(UIStyles.Label) { fontSize = 16, fontStyle = FontStyle.Bold, richText = true,
                                              normal = { textColor = UIStyles.Accent } };
        info = new GUIStyle(UIStyles.Small) { fontSize = 12, richText = true, wordWrap = true };
    }

    /// <summary>구역 제목 (누르면 그 구역을 접거나 폄). 펴져 있으면 true</summary>
    bool Section(string title)
    {
        bool open = !sectionOpen.TryGetValue(title, out bool stored) || stored;   // 처음에는 모두 펴짐
        if (GUILayout.Button($"{(open ? "▼" : "▶")} {title}", head)) { open = !open; sectionOpen[title] = open; }
        return open;
    }

    bool Toggle(string text, bool on) => GUILayout.Button(on ? $"{text}  <color=#7CFF7C>ON</color>" : $"{text}  <color=#888888>OFF</color>", btn, GUILayout.Height(28));

    void OnGUI()
    {
        UIStyles.Init();
        Styles();
        float H = Screen.height;
        // 창 전체 켜기 · 끄기 버튼 (항상 보임)
        toggleRect = new Rect(Screen.width - 150f - PanelMargin, 34f, 150f, 22f);
        if (GUI.Button(toggleRect, panelOpen ? "디버그 창 ON  (`)" : "디버그 창 OFF  (`)", btn)) panelOpen = !panelOpen;
        if (!panelOpen) { panelRect = Rect.zero; if (showSpriteInfo) DrawSpriteInfo(); return; }
        panelRect = new Rect(Screen.width - PanelWidth - PanelMargin, 60f, PanelWidth, H - 60f - PanelMargin);
        GUI.color = new Color(0, 0, 0, 0.72f);
        GUI.DrawTexture(panelRect, UIStyles.White);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(panelRect.x + 8, panelRect.y + 6, panelRect.width - 16, panelRect.height - 12));
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("디버그 맵", head);

        // ----- 맵 -----
        if (Section("맵"))
        {
            GUILayout.Label($"맵 크기  <color=#AAAAAA>{Stage.PresetLabel(Stage.Preset)} · {Stage.HalfSize.x * 2f:0.#} × {Stage.HalfSize.y * 2f:0.#}</color>", label);
            GUILayout.BeginHorizontal();
            foreach (MapPreset p in System.Enum.GetValues(typeof(MapPreset)))
                if (GUILayout.Button(Stage.PresetLabel(p), p == Stage.Preset ? btnOn : btn, GUILayout.Height(28))) SetMap(p);
            GUILayout.EndHorizontal();
            GUILayout.Label("<size=12><color=#888888>패턴의 위치 · 크기는 기본 맵 기준 값 그대로, 맵 밖이면 안쪽으로 보정</color></size>", label);
        }

        // ----- 소환 -----
        GUILayout.Space(6);
        if (Section("소환 — 보스"))
        {
            if (Toggle("등장 연출 재생", DebugMap.PlayIntro)) DebugMap.PlayIntro = !DebugMap.PlayIntro;
            foreach (var en in DebugMap.Bosses)
                if (GUILayout.Button(en.label, btn, GUILayout.Height(26))) DebugMap.Spawn(en, 1);
        }
        if (Section("소환 — 몬스터"))
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"수량 {monsterCount}", label, GUILayout.Width(70));
            if (GUILayout.Button("−", btn, GUILayout.Width(34), GUILayout.Height(24))) monsterCount = Mathf.Max(1, monsterCount - 1);
            if (GUILayout.Button("+", btn, GUILayout.Width(34), GUILayout.Height(24))) monsterCount = Mathf.Min(12, monsterCount + 1);
            GUILayout.EndHorizontal();
            foreach (var en in DebugMap.Monsters)
                if (GUILayout.Button(en.label, btn, GUILayout.Height(26))) DebugMap.Spawn(en, monsterCount);
            if (GUILayout.Button("모두 제거", btn, GUILayout.Height(26))) DebugMap.ClearAll();
        }

        // ----- 대상 -----
        DebugMap.Clean();
        GUILayout.Space(6);
        var t = DebugMap.Target;
        if (Section("대상"))
        {
            if (DebugMap.Spawned.Count == 0) GUILayout.Label("<color=#888888>소환된 대상 없음</color>", label);
            foreach (var e in DebugMap.Spawned)
            {
                if (e == null) continue;
                bool sel = e == DebugMap.Target;
                if (GUILayout.Button($"{(sel ? "▶ " : "")}{e.DisplayName}  <size=12>{Mathf.CeilToInt(Mathf.Max(0f, e.hp))}/{Mathf.CeilToInt(e.maxHp)}</size>", sel ? btnOn : btn, GUILayout.Height(24)))
                    DebugMap.Target = e;
            }
            t = DebugMap.Target;
            if (t != null)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("체력 회복", btn, GUILayout.Height(24))) t.hp = t.maxHp;
                // 사용자 (2026-11-16): 현재 체력을 절반으로 (체력 문턱 패턴 · 50% 강화 확인용). 최소 1 남김
                if (GUILayout.Button("체력 50% 감소", btn, GUILayout.Height(24))) t.hp = Mathf.Max(1f, t.hp * 0.5f);
                if (GUILayout.Button("제거", btn, GUILayout.Height(24))) DebugMap.Destroy(t);
                GUILayout.EndHorizontal();
                if (t != null && showSpriteInfo) GUILayout.Label(SpriteInfoText(t), info);
            }
        }

        // ----- 패턴 -----
        t = DebugMap.Target;
        GUILayout.Space(6);
        if (Section("패턴"))
        {
        IDebugBoss boss = null;
        if (t != null) t.TryGetComponent(out boss);
        if (t == null) GUILayout.Label("<color=#888888>대상을 소환하세요</color>", label);
        else if (boss == null) GUILayout.Label("<color=#888888>이 대상은 패턴 선택을 지원하지 않음 (기본 행동만)</color>", label);
        else
        {
            if (Toggle("자동 패턴 선택", boss.DebugAuto)) boss.DebugAuto = !boss.DebugAuto;
            if (GUILayout.Button("진행 중인 패턴 취소", btn, GUILayout.Height(26))) { ClearSpriteView(t); boss.DebugCancel(); }
            GUILayout.Label($"<color=#AAAAAA>{boss.DebugStatus}</color>", label);
            var names = boss.DebugPatternNames;
            for (int i = 0; i < names.Length; i++)
                if (GUILayout.Button(names[i], btn, GUILayout.Height(26))) { ClearSpriteView(t); boss.DebugRun(i); }

            // ----- 스프라이트만 변경 -----
            if (t.TryGetComponent<IDebugSprites>(out var sprites) && sprites.DebugSpriteNames.Length > 0)
            {
                GUILayout.Space(6);
                if (Section("스프라이트 (패턴 없이 모양만)"))
                {
                    if (GUILayout.Button("원래대로 (대기 모양)", btn, GUILayout.Height(26))) { ClearSpriteView(t); boss.DebugCancel(); }
                    var spriteNames = sprites.DebugSpriteNames;
                    for (int i = 0; i < spriteNames.Length; i += 2)
                    {
                        GUILayout.BeginHorizontal();
                        for (int j = i; j < Mathf.Min(i + 2, spriteNames.Length); j++)
                            if (GUILayout.Button(spriteNames[j], btn, GUILayout.Height(24))) { ClearSpriteView(t); sprites.DebugShowSprite(j); }
                        GUILayout.EndHorizontal();
                    }
                }
            }
        }

        }   // 패턴 구역 끝

        DrawSpriteViewer(t);
        DrawPlayerSkills();
        DrawSpecialUpgrades();

        // ----- 보기 · 시간 · 플레이어 -----
        GUILayout.Space(6);
        if (Section("보기 · 시간 · 플레이어"))
        {
        if (Toggle("판정 표시 (F1)", HitboxView.Show)) HitboxView.Show = !HitboxView.Show;
        if (Toggle("스프라이트 경계 · 정보", showSpriteInfo)) showSpriteInfo = !showSpriteInfo;
        if (Toggle("맵 배경 이미지 (폐허 성, 임시)", ArenaView.DebugBackdrop)) { ArenaView.DebugBackdrop = !ArenaView.DebugBackdrop; ArenaView.Rebuild(); }
        if (Toggle("적 체력 고정", DebugCheats.ImmortalEnemies)) { DebugCheats.ImmortalEnemies = !DebugCheats.ImmortalEnemies; if (DebugCheats.ImmortalEnemies) DebugCheats.OneHitKill = false; }
        if (Toggle("플레이어 무적", DebugCheats.God)) DebugCheats.God = !DebugCheats.God;
        if (Toggle("플레이어 체력 자동 회복", autoHeal)) autoHeal = !autoHeal;
        if (GUILayout.Button("플레이어 체력 회복 (H)", btn, GUILayout.Height(26)) && Player.I != null) Player.I.hp = Player.I.maxHp;
        GUILayout.Label($"시간 배속  <color=#AAAAAA>{(stepFrames > 0 ? "한 프레임" : timeScale + "x")}</color>", label);
        GUILayout.BeginHorizontal();
        foreach (float s in TimeScales)
            if (GUILayout.Button(s == 0f ? "정지" : s + "x", Mathf.Approximately(timeScale, s) ? btnOn : btn, GUILayout.Height(26))) { timeScale = s; stepFrames = 0; }
        GUILayout.EndHorizontal();
        if (GUILayout.Button("한 프레임 진행 (정지 중)", btn, GUILayout.Height(26))) { timeScale = 0f; stepFrames = 1; }
        if (GUILayout.Button("플레이어 위치 초기화", btn, GUILayout.Height(24))) PlacePlayer();
        GUILayout.Label("<size=12><color=#888888>마우스 우클릭 = 플레이어를 그 위치로 이동</color></size>", label);
        }   // 보기 · 시간 · 플레이어 구역 끝

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (showSpriteInfo) DrawSpriteInfo();
    }

    // ----- 플레이어 스킬 (수동 · 이동 · 궁극기): 버튼을 누르면 그 스킬을 바로 사용 → 스킬 모션 · 이펙트 확인 (사용자 2026-11-18) -----
    string skillStatus = "";

    void DrawPlayerSkills()
    {
        var p = Player.I;
        if (p == null) return;
        GUILayout.Space(6);
        if (!Section("플레이어 스킬 (버튼 = 바로 사용)")) return;
        GUILayout.Label("<size=12><color=#888888>쿨타임 · 시전 중이어도 바로 사용, 안 가진 스킬은 획득 후 사용. 공격 모션은 매번 처음부터</color></size>", label);
        bool any = false;
        foreach (var entry in p.Catalog)
        {
            if (entry.kind != SkillKind.Manual && entry.kind != SkillKind.Movement && entry.kind != SkillKind.Ultimate) continue;
            any = true;
            var owned = p.GetSkill(entry.id);
            string tag = $"<size=12>[{SkillCatalog.KindLabel(entry.kind)}{(owned == null ? " · 미보유" : "")}]</size>";
            if (GUILayout.Button($"{entry.name}  {tag}", btn, GUILayout.Height(26))) skillStatus = RunSkill(p, entry);
        }
        if (!any) GUILayout.Label("<color=#888888>이 캐릭터에 수동 스킬이 없음</color>", label);
        if (skillStatus.Length > 0) GUILayout.Label($"<color=#AAAAAA>{skillStatus}</color>", label);
    }

    // ----- 스킬 특수 강화 바로 적용: 스킬을 고른 뒤 Lv.5 · 10 · 15 강화를 눌러 적용 · 교체 · 해제 (사용자 2026-11-20) -----
    string spSkillId = "";

    void DrawSpecialUpgrades()
    {
        var p = Player.I;
        if (p == null) return;
        GUILayout.Space(6);
        if (!Section("스킬 특수 강화 (바로 적용)")) return;
        GUILayout.Label("<size=12><color=#888888>스킬을 고르면 안 가진 스킬은 획득 (슬롯 제한 무시). 특수 강화는 누르면 적용 · 다른 것을 누르면 교체 · 해제 가능. 필요하면 스킬 레벨을 그 단계로 올림. 선택 불가 후보 · 조합 경고도 무시. 융해 · 영원의 불꽃은 교체해도 효과가 남을 수 있음 (R 재시작)</color></size>", label);

        var entries = new List<SkillEntry>();
        foreach (var e in p.Catalog) if (e.kind != SkillKind.Ultimate) entries.Add(e);
        for (int i = 0; i < entries.Count; i += 2)
        {
            GUILayout.BeginHorizontal();
            for (int j = i; j < Mathf.Min(i + 2, entries.Count); j++)
            {
                var e = entries[j];
                var have = p.GetSkill(e.id);
                string mark = have != null && have.SpecialSummary.Length > 0 ? " ★" : "";
                if (GUILayout.Button(e.name + mark, spSkillId == e.id ? btnOn : btn, GUILayout.Height(24))) spSkillId = e.id;
            }
            GUILayout.EndHorizontal();
        }

        SkillEntry sel = null;
        foreach (var e in entries) if (e.id == spSkillId) sel = e;
        if (sel == null) return;
        var s = p.GetSkill(sel.id) ?? p.AddOrLevelSkill(sel);
        if (s == null) { GUILayout.Label($"<color=#AAAAAA>{sel.name}: 획득 실패</color>", label); return; }

        string summary = s.SpecialSummary;
        GUILayout.Label($"<b>{sel.name}</b>  Lv.{s.level}  <color=#AAAAAA>{(summary.Length > 0 ? summary : "특수 강화 없음")}</color>", label);
        foreach (int lv in Skill.MilestoneLevels)
        {
            var opts = s.SpecialOptions(lv);
            GUILayout.Label($"Lv.{lv} 특수 강화", label);
            if (opts.Length == 0) { GUILayout.Label("<color=#888888>미설계 (선택지 없음)</color>", label); continue; }
            string picked = s.DebugPickId(lv);
            for (int k = 0; k < opts.Length; k++)
            {
                var o = opts[k];
                string t = o.locked ? $"{o.title}  <size=11>(선택 불가 후보)</size>" : o.title;
                if (GUILayout.Button(t, picked == o.id ? btnOn : btn, GUILayout.Height(24))) s.DebugApplySpecial(lv, k);
            }
            if (picked.Length > 0 && GUILayout.Button("해제", btn, GUILayout.Height(22))) s.DebugClearSpecial(lv);
        }
        // 특수 조합: 켜기 · 끄기 (조건 특수 강화를 모두 가져야 효과가 남 — 없으면 회색 안내)
        foreach (var c in s.SpecialCombos())
        {
            bool on = s.DebugComboOn(c.id), active = s.HasCombo(c.id);
            GUILayout.Label($"특수 조합  <color=#AAAAAA>({c.recipe})</color>", label);
            string t = on && !active ? $"{c.title}  <size=11>(조건 강화 없음 → 효과 없음)</size>" : c.title;
            if (GUILayout.Button(t, on ? btnOn : btn, GUILayout.Height(24))) s.DebugSetCombo(c.id, !on);
        }
    }

    static string RunSkill(Player p, SkillEntry entry)
    {
        var s = p.GetSkill(entry.id) ?? p.AddOrLevelSkill(entry);
        if (s == null) return $"{entry.name}: 획득 실패";
        p.DebugStopMotion();
        s.DebugResetForCast();
        if (entry.kind == SkillKind.Ultimate) GameManager.I?.DebugFillUltimate();
        return s.TryActivate() ? $"{entry.name}: 사용함" : $"{entry.name}: 사용 불가 (이동 · 기절 · 잡힘 중이거나 스킬 금지 치트)";
    }

    void SetMap(MapPreset p)
    {
        Stage.SetPreset(p);
        // 지금 있는 것들을 새 맵 안으로 (맵 밖에 남지 않게)
        var pl = Player.I;
        if (pl != null)
        {
            Vector2 off = pl.HurtCenter - (Vector2)pl.transform.position;
            pl.transform.position = Stage.ClampInside(pl.HurtCenter, pl.radius) - off;
        }
        DebugMap.Clean();
        foreach (var e in DebugMap.Spawned)
            if (e != null && !e.Scripted) e.transform.position = DebugMap.PlaceInMap(e.transform.position, e.radius);
    }

    // ---------- 스프라이트 경계 · 정보 ----------
    // ----- 스프라이트 보기 (모든 대상 공통: 시트 · 그림 하나를 고정 표시) -----
    int svDef, svRow, svCol;
    Enemy svTarget;
    static readonly string[] SvRowNames = { "아래", "왼쪽", "오른쪽", "위" };

    static void ClearSpriteView(Enemy t)
    {
        if (t != null && t.TryGetComponent<DebugSpriteView>(out var v)) v.Clear();
    }

    void ApplySpriteView(Enemy t, DebugSpriteView.Def d)
    {
        if (t.TryGetComponent<IDebugBoss>(out var b)) { b.DebugAuto = false; b.DebugCancel(); }   // 패턴 · 공중 포즈를 먼저 정리
        DebugSpriteView.Of(t).Show(d, svRow, svCol);
    }

    void DrawSpriteViewer(Enemy t)
    {
        if (t == null) return;
        var defs = DebugSpriteView.DefsFor(t);
        if (defs.Count == 0) return;
        if (svTarget != t) { svTarget = t; svDef = 0; svRow = 0; svCol = 0; }
        var view = DebugSpriteView.Of(t);
        GUILayout.Space(6);
        if (!Section("그림 보기 (시트 · 한 장 그림)")) return;
        GUILayout.BeginHorizontal();
        for (int i = 0; i < defs.Count; i++)
            if (GUILayout.Button(defs[i].Label, view.Active && svDef == i ? btnOn : btn, GUILayout.Height(24)))
            { svDef = i; svRow = 0; svCol = 0; ApplySpriteView(t, defs[i]); }
        GUILayout.EndHorizontal();
        if (view.Active && svDef < defs.Count)
        {
            var d = defs[svDef];
            bool changed = false;
            if (d.Rows > 1)
            {
                GUILayout.BeginHorizontal();
                for (int r = 0; r < d.Rows; r++)
                    if (GUILayout.Button(r < SvRowNames.Length ? SvRowNames[r] : "행 " + r, svRow == r ? btnOn : btn, GUILayout.Height(22))) { svRow = r; changed = true; }
                GUILayout.EndHorizontal();
            }
            if (d.Cols > 1)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("◀", btn, GUILayout.Width(40), GUILayout.Height(22))) { svCol = (svCol + d.Cols - 1) % d.Cols; changed = true; }
                GUILayout.Label($"<color=#AAAAAA>프레임 {svCol + 1} / {d.Cols}</color>", label);
                if (GUILayout.Button("▶", btn, GUILayout.Width(40), GUILayout.Height(22))) { svCol = (svCol + 1) % d.Cols; changed = true; }
                GUILayout.EndHorizontal();
            }
            if (changed) ApplySpriteView(t, d);
            if (GUILayout.Button("원래대로 (애니메이션 재개)", btn, GUILayout.Height(24))) view.Clear();
        }
    }

    void DrawSpriteInfo()
    {
        var t = DebugMap.TargetAlive;
        var cam = Camera.main;
        if (t == null || cam == null) return;
        var sr = t.GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null || !sr.enabled) return;

        Bounds b = sr.bounds;
        Vector2 a = W2G(cam, b.min), c = W2G(cam, b.max);
        var rect = Rect.MinMaxRect(Mathf.Min(a.x, c.x), Mathf.Min(a.y, c.y), Mathf.Max(a.x, c.x), Mathf.Max(a.y, c.y));
        GUI.color = new Color(0.4f, 1f, 0.5f, 0.9f);
        Line(rect.xMin, rect.yMin, rect.width, 1.5f); Line(rect.xMin, rect.yMax, rect.width, 1.5f);
        Line(rect.xMin, rect.yMin, 1.5f, rect.height); Line(rect.xMax, rect.yMin, 1.5f, rect.height);
        // 발끝 (경계 아래) 가로선 · 위치(피벗) 십자
        GUI.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Line(rect.xMin - 8, rect.yMax, rect.width + 16, 1.5f);
        Vector2 pv = W2G(cam, t.transform.position);
        GUI.color = new Color(1f, 0.3f, 0.3f, 0.95f);
        Line(pv.x - 8, pv.y, 16, 2f); Line(pv.x, pv.y - 8, 2f, 16);
        GUI.color = Color.white;

    }

    /// <summary>대상 스프라이트 정보 (패널 "대상" 아래에 표시): 이름 · 크기 · 원본 px · ppu · 위치 · 발끝 · 피벗 · scale · 판정 반경</summary>
    static string SpriteInfoText(Enemy t)
    {
        var sr = t != null ? t.GetComponent<SpriteRenderer>() : null;
        if (sr == null || sr.sprite == null) return "<color=#888888>스프라이트 없음</color>";
        var s = sr.sprite;
        Bounds b = sr.bounds;
        return $"스프라이트 <b>{s.name}</b>{(sr.enabled ? "" : " (숨김)")}\n" +
               $"경계 {b.size.x:0.00} × {b.size.y:0.00} 유닛\n" +
               $"원본 {s.rect.width:0} × {s.rect.height:0} px · ppu {s.pixelsPerUnit:0.#}\n" +
               $"위치 ({t.transform.position.x:0.00}, {t.transform.position.y:0.00}) · 발끝 y {b.min.y:0.00}\n" +
               $"피벗 ({s.pivot.x / s.rect.width:0.00}, {s.pivot.y / s.rect.height:0.00}) · scale {t.transform.localScale.x:0.00} × {t.transform.localScale.y:0.00}\n" +
               $"판정 반경 {t.radius:0.00}";
    }

    static Vector2 W2G(Camera cam, Vector3 world)
    {
        Vector3 sp = cam.WorldToScreenPoint(world);
        return new Vector2(sp.x, Screen.height - sp.y);
    }

    static void Line(float x, float y, float w, float h) => GUI.DrawTexture(new Rect(x, y, w, h), UIStyles.White);

    void OnDestroy()
    {
        Time.timeScale = 1f;
    }
}
