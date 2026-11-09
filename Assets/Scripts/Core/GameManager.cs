using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = UnityEngine.Random;

public enum GameState { Playing, LevelUp, Paused, GameOver, Victory }

public class GameManager : MonoBehaviour
{
    public static GameManager I;

    [Header("진행")]
    public float surviveTime = 600f;      // 10분 생존 시 승리
    public GameState State { get; private set; } = GameState.Playing;
    public float Elapsed { get; private set; }
    public int Kills { get; private set; }
    public int Level { get; private set; } = 1;
    public int Xp { get; private set; }
    public int XpToNext => 5 + (Level - 1) * 6 + (Level / 10) * 20;

    float spawnTimer;
    float nextBossTime = 180f;
    readonly List<Upgrade> choices = new List<Upgrade>();
    int pendingLevelUps;

    // ---------- 업그레이드 정의 ----------
    class Upgrade
    {
        public string title, desc;
        public Action apply;
    }

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        Application.targetFrameRate = 120;

        var cam = Camera.main;
        if (cam == null)
        {
            var cgo = new GameObject("Main Camera");
            cgo.tag = "MainCamera";
            cam = cgo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.09f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        if (cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>();

        var pgo = new GameObject("Player");
        var player = pgo.AddComponent<Player>();
        // [임시] 시작 스킬은 기획 미정 → 파이어 슬래시로 시작
        player.AddOrLevelSkill(SkillCatalog.Find(player.Catalog, "A01"));

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        gameObject.AddComponent<DebugCheats>();   // 테스트용 치트 패널 (F2)
#endif
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (State == GameState.Playing) SetState(GameState.Paused);
            else if (State == GameState.Paused) SetState(GameState.Playing);
        }

        if (State == GameState.LevelUp)
        {
            for (int i = 0; i < choices.Count; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) Pick(i);
            return;
        }
        if (State == GameState.GameOver || State == GameState.Victory)
        {
            if (Input.GetKeyDown(KeyCode.R)) Restart();
            return;
        }
        if (State != GameState.Playing) return;

        Elapsed += Time.deltaTime;
        if (Elapsed >= surviveTime) { Victory(); return; }

        Spawning();
        Enemy.Separate();
    }

    // ---------- 스폰 ----------
    void Spawning()
    {
        float minutes = Elapsed / 60f;
        float interval = Mathf.Max(0.08f, 0.9f - minutes * 0.12f);
        int maxEnemies = 60 + (int)(minutes * 40);
        float hpMul = 1f + minutes * 0.35f;

        spawnTimer -= Time.deltaTime;
        if (spawnTimer <= 0 && Enemy.All.Count < maxEnemies)
        {
            spawnTimer = interval;
            int batch = 1 + (int)(minutes * 0.8f);
            for (int i = 0; i < batch; i++) Enemy.Spawn(PickKind(minutes), SpawnPos(), hpMul);
        }

        if (Elapsed >= nextBossTime)
        {
            nextBossTime += 180f;
            Enemy.Spawn(EnemyKind.Boss, SpawnPos(), hpMul);
        }
    }

    EnemyKind PickKind(float minutes)
    {
        float r = Random.value;
        if (minutes > 2f && r < Mathf.Min(0.2f, 0.05f + minutes * 0.02f)) return EnemyKind.Tank;
        if (minutes > 1f && r < 0.45f) return EnemyKind.Runner;
        return EnemyKind.Bat;
    }

    Vector2 SpawnPos()
    {
        var cam = Camera.main;
        float h = cam.orthographicSize, w = h * cam.aspect;
        float dist = Mathf.Sqrt(w * w + h * h) + 1.5f;
        return (Vector2)Player.I.transform.position + Random.insideUnitCircle.normalized * dist;
    }

    // ---------- 경험치 / 레벨업 ----------
    public void OnEnemyKilled(Enemy e)
    {
        Kills++;
        // 궁극기 게이지: 적 처치 수 기반 (기획서 확정). [임시] 궁극기 보유 중에만 충전
        var p = Player.I;
        if (p != null && p.FirstOfKind(SkillKind.Ultimate) != null)
            ultimateGauge = Mathf.Min(Tuning.Skills.gaugeMax, ultimateGauge + Tuning.Skills.gaugePerKill);
    }

    // ---------- 궁극기 게이지 ----------
    float ultimateGauge;
    public float UltimateGaugeFraction => Mathf.Clamp01(ultimateGauge / Mathf.Max(1f, Tuning.Skills.gaugeMax));
    public bool UltimateReady => ultimateGauge >= Tuning.Skills.gaugeMax;
    /// <summary>[임시] 사용 시 0으로 초기화 (기획 미정)</summary>
    public void ConsumeUltimateGauge() => ultimateGauge = 0f;

    // ---------- 테스트용 (DebugCheats 패널에서 호출) ----------
    /// <summary>테스트: 레벨 올리기 (선택지도 레벨 수만큼 열림)</summary>
    public void DebugAddLevels(int n)
    {
        Level += n;
        pendingLevelUps += n;
        if (State == GameState.Playing) OpenLevelUp();
    }
    public void DebugFillUltimate() => ultimateGauge = Tuning.Skills.gaugeMax;

    public void AddXp(int amount)
    {
        Xp += amount;
        while (Xp >= XpToNext)
        {
            Xp -= XpToNext;
            Level++;
            pendingLevelUps++;
        }
        if (pendingLevelUps > 0 && State == GameState.Playing) OpenLevelUp();
    }

    void OpenLevelUp()
    {
        while (pendingLevelUps > 0)
        {
            pendingLevelUps--;
            BuildChoices();
            if (choices.Count > 0) { SetState(GameState.LevelUp); return; }
            // 고를 수 있는 스킬이 하나도 없으면 이번 레벨업은 건너뜀
        }
    }

    void Pick(int i)
    {
        if (i < 0 || i >= choices.Count) return;
        choices[i].apply();
        SetState(GameState.Playing);
        if (pendingLevelUps > 0) OpenLevelUp();
    }

    /// <summary>
    /// 레벨업 선택지 3개 (기획서 2.3). 자동·수동·이동·패시브·궁극기가 함께 후보로 등장.
    /// [임시] 후보별 출현 확률 미정 → 모두 같은 확률
    /// [임시] 최대 레벨 스킬은 후보에서 제외, 슬롯이 가득 찬 종류의 신규 스킬도 제외 (기획 미정)
    /// </summary>
    void BuildChoices()
    {
        var p = Player.I;
        var T = Tuning.Skills;
        var pool = new List<Upgrade>();

        foreach (var entry in p.Catalog)
        {
            var owned = p.GetSkill(entry.id);
            string kind = SkillCatalog.KindLabel(entry.kind);

            if (owned != null)
            {
                if (owned.level >= owned.LevelCap) continue;
                // 궁극기 강화는 캐릭터 Lv.60 부터 (잠정)
                if (entry.kind == SkillKind.Ultimate && Level < T.ultimateUpgradeLevel) continue;
                int next = owned.level + 1;
                string extra = entry.kind != SkillKind.Ultimate && Skill.IsMilestone(next)
                    ? "  <color=#F2BF4C>★ 특수 강화 (기획 미설계)</color>" : "";
                pool.Add(new Upgrade
                {
                    title = $"{entry.name}  Lv.{owned.level} → {next}  <size=14>[{kind}]</size>",
                    desc = (entry.kind == SkillKind.Ultimate ? "궁극기 강화" : "피해량·쿨타임·범위 강화") + extra,
                    apply = () => p.AddOrLevelSkill(entry),
                });
            }
            else
            {
                if (!HasFreeSlot(p, entry.kind)) continue;
                // 궁극기 획득은 캐릭터 Lv.45 부터 (잠정)
                if (entry.kind == SkillKind.Ultimate && Level < T.ultimateUnlockLevel) continue;
                pool.Add(new Upgrade
                {
                    title = $"{entry.name}  <size=16>(신규)</size>  <size=14>[{kind}]</size>",
                    desc = entry.summary + KeyHint(p, entry.kind),
                    apply = () => p.AddOrLevelSkill(entry),
                });
            }
        }

        choices.Clear();
        for (int n = 0; n < 3 && pool.Count > 0; n++)
        {
            int idx = Random.Range(0, pool.Count);
            choices.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    static bool HasFreeSlot(Player p, SkillKind k)
    {
        var T = Tuning.Skills;
        int count = p.CountKind(k);
        return k switch
        {
            SkillKind.Auto => count < T.autoSlots,
            SkillKind.Passive => count < T.passiveSlots,
            SkillKind.Manual => count < Mathf.Min(T.manualSlots, T.manualKeys.Length),
            SkillKind.Movement => count < T.movementSlots,
            SkillKind.Ultimate => count < 1,   // [임시] 2종 동시 보유 여부 미정 → 1개
            _ => false,
        };
    }

    static string KeyHint(Player p, SkillKind k)
    {
        var T = Tuning.Skills;
        return k switch
        {
            SkillKind.Manual => $"  <color=#9AD0FF>[{T.manualKeys[p.CountKind(SkillKind.Manual)]}]</color>",
            SkillKind.Movement => $"  <color=#9AD0FF>[{T.movementKey}]</color>",
            SkillKind.Ultimate => $"  <color=#9AD0FF>[{T.ultimateKey}]</color>",
            _ => "",
        };
    }

    // ---------- 상태 ----------
    void SetState(GameState s)
    {
        State = s;
        Time.timeScale = s == GameState.Playing ? 1f : 0f;
    }

    public void GameOver() => SetState(GameState.GameOver);
    void Victory() => SetState(GameState.Victory);

    void Restart() => SceneLoader.Load(SceneManager.GetActiveScene().name);
    void ToTitle() => SceneLoader.Load(SceneLoader.Title);

    // ---------- 스킬 슬롯 바 (화면 아래 가운데) ----------
    static GUIStyle skillListStyle, slotKey, slotName, slotBadge;
    static GUIStyle SkillListStyle => skillListStyle ??= new GUIStyle(UIStyles.Small) { richText = true, wordWrap = false };

    void DrawSkillBar(Player p, float W, float H)
    {
        var T = Tuning.Skills;
        slotKey ??= new GUIStyle(UIStyles.Label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
        slotName ??= new GUIStyle(UIStyles.Small) { fontSize = 12, alignment = TextAnchor.LowerCenter, wordWrap = true };
        slotBadge ??= new GUIStyle(UIStyles.Label) { fontSize = 14, alignment = TextAnchor.UpperRight };

        int manualCount = Mathf.Min(T.manualSlots, T.manualKeys.Length);
        int n = manualCount + 2;
        float size = 64, gap = 8;
        float x0 = (W - (n * size + (n - 1) * gap)) / 2, yy = H - size - 16;

        for (int i = 0; i < n; i++)
        {
            Skill s; string key;
            if (i < manualCount) { s = p.ManualSlot(i); key = T.manualKeys[i].ToString(); }
            else if (i == manualCount) { s = p.FirstOfKind(SkillKind.Movement); key = KeyLabel(T.movementKey); }
            else { s = p.FirstOfKind(SkillKind.Ultimate); key = T.ultimateKey.ToString(); }

            var r = new Rect(x0 + i * (size + gap), yy, size, size);
            GUI.color = new Color(0, 0, 0, s != null ? 0.7f : 0.35f);
            GUI.DrawTexture(r, UIStyles.White);

            if (s != null)
            {
                // 쿨타임: 위에서부터 어둡게 덮음
                float cd = s.CooldownFraction;
                if (cd > 0)
                {
                    GUI.color = new Color(0, 0, 0, 0.6f);
                    GUI.DrawTexture(new Rect(r.x, r.y, r.width, r.height * cd), UIStyles.White);
                }
                else
                {
                    GUI.color = new Color(1f, 0.55f, 0.15f, 0.9f);
                    GUI.DrawTexture(new Rect(r.x, r.yMax - 3, r.width, 3), UIStyles.White);
                }
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 2, r.y, r.width - 4, r.height - 2), s.Entry.name, slotName);
                if (s.Badge != null) GUI.Label(new Rect(r.x, r.y + 2, r.width - 4, 20), s.Badge, slotBadge);
            }
            GUI.color = s != null ? Color.white : new Color(1, 1, 1, 0.4f);
            GUI.Label(new Rect(r.x + 5, r.y + 2, 40, 20), key, slotKey);
            GUI.color = Color.white;
        }
    }

    static string KeyLabel(KeyCode k) => k == KeyCode.Space ? "SPC" : k.ToString();

    // ---------- UI (IMGUI 임시 UI, 스타일은 UIStyles 공용) ----------
    void OnGUI()
    {
        UIStyles.Init();
        float W = Screen.width, H = Screen.height;
        var p = Player.I;
        if (p == null) return;

        // 상단 경험치 바
        UIStyles.Bar(new Rect(0, 0, W, 14), (float)Xp / XpToNext, new Color(0.3f, 0.7f, 1f));
        GUI.Label(new Rect(12, 18, 300, 30), $"Lv.{Level}", UIStyles.Label);
        var ts = TimeSpan.FromSeconds(Elapsed);
        GUI.Label(new Rect(W / 2 - 40, 18, 120, 30), $"{ts.Minutes:00}:{ts.Seconds:00}", UIStyles.Label);
        GUI.Label(new Rect(W - 160, 18, 150, 30), $"처치 {Kills}", UIStyles.Label);

        // 체력 바 (플레이어 아래)
        var sp = Camera.main.WorldToScreenPoint(p.transform.position + Vector3.down * p.HealthBarOffset);
        UIStyles.Bar(new Rect(sp.x - 30, H - sp.y, 60, 7), p.hp / p.maxHp, new Color(0.9f, 0.25f, 0.25f));

        // 보유 스킬 (종류별)
        float y = 52;
        foreach (SkillKind k in Enum.GetValues(typeof(SkillKind)))
        {
            foreach (var s in p.skills)
            {
                if (s.Entry.kind != k) continue;
                GUI.Label(new Rect(12, y, 360, 26),
                    $"<size=13><color=#AAAAAA>{SkillCatalog.KindLabel(k)}</color></size>  {s.Entry.name}  Lv.{s.level}",
                    SkillListStyle);
                y += 22;
            }
        }

        DrawSkillBar(p, W, H);

        float mbw = 260, mbh = 56;
        float mbx = (W - mbw) / 2;

        if (State == GameState.LevelUp)
        {
            UIStyles.Overlay(UIStyles.Dim);
            GUI.Label(new Rect(0, H * 0.15f, W, 60), "레벨 업!", UIStyles.Big);
            float bw = Mathf.Min(520, W - 40), bh = 86;
            float bx = (W - bw) / 2, by = H * 0.3f;
            for (int i = 0; i < choices.Count; i++)
            {
                var c = choices[i];
                if (GUI.Button(new Rect(bx, by + i * (bh + 14), bw, bh), $"[{i + 1}] {c.title}\n<size=16>{c.desc}</size>", UIStyles.Button)) Pick(i);
            }
        }
        else if (State == GameState.Paused)
        {
            UIStyles.Overlay(new Color(0, 0, 0, 0.5f));
            GUI.Label(new Rect(0, H * 0.3f, W, 60), "일시정지", UIStyles.Big);
            if (GUI.Button(new Rect(mbx, H * 0.3f + 90, mbw, mbh), "계속하기 (ESC)", UIStyles.MenuButton)) SetState(GameState.Playing);
            if (GUI.Button(new Rect(mbx, H * 0.3f + 90 + mbh + 14, mbw, mbh), "타이틀로", UIStyles.MenuButton)) ToTitle();
        }
        else if (State == GameState.GameOver || State == GameState.Victory)
        {
            UIStyles.Overlay(new Color(0, 0, 0, 0.7f));
            GUI.Label(new Rect(0, H * 0.3f, W, 60), State == GameState.Victory ? "생존 성공!" : "게임 오버", UIStyles.Big);
            GUI.Label(new Rect(0, H * 0.3f + 80, W, 30), $"생존 시간 {ts.Minutes:00}:{ts.Seconds:00}   레벨 {Level}   처치 {Kills}", UIStyles.Center);
            if (GUI.Button(new Rect(mbx, H * 0.3f + 140, mbw, mbh), "다시 하기 (R)", UIStyles.MenuButton)) Restart();
            if (GUI.Button(new Rect(mbx, H * 0.3f + 140 + mbh + 14, mbw, mbh), "타이틀로", UIStyles.MenuButton)) ToTitle();
        }
    }
}
