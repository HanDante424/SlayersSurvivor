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
        player.AddWeapon<MagicBolt>();

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
    public void OnEnemyKilled(Enemy e) => Kills++;

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
        pendingLevelUps--;
        BuildChoices();
        SetState(GameState.LevelUp);
    }

    void Pick(int i)
    {
        if (i < 0 || i >= choices.Count) return;
        choices[i].apply();
        SetState(GameState.Playing);
        if (pendingLevelUps > 0) OpenLevelUp();
    }

    void BuildChoices()
    {
        var p = Player.I;
        var pool = new List<Upgrade>();

        AddWeaponOption<MagicBolt>(pool, p);
        AddWeaponOption<OrbitBlades>(pool, p);
        AddWeaponOption<HolyAura>(pool, p);
        AddWeaponOption<Lightning>(pool, p);

        pool.Add(new Upgrade { title = "체력 강화", desc = "최대 체력 +20, 체력 회복", apply = () => { p.maxHp += 20; p.hp = Mathf.Min(p.maxHp, p.hp + 20); } });
        pool.Add(new Upgrade { title = "신속", desc = "이동 속도 +8%", apply = () => p.moveSpeedMul *= 1.08f });
        pool.Add(new Upgrade { title = "재생", desc = "초당 체력 회복 +0.5", apply = () => p.regen += 0.5f });
        pool.Add(new Upgrade { title = "자석", desc = "획득 범위 +30%", apply = () => p.pickupMul *= 1.3f });
        pool.Add(new Upgrade { title = "힘", desc = "모든 피해량 +10%", apply = () => p.damageMul += 0.1f });
        if (p.cooldownMul > 0.5f)
            pool.Add(new Upgrade { title = "가속", desc = "모든 쿨다운 -8%", apply = () => p.cooldownMul *= 0.92f });
        pool.Add(new Upgrade { title = "확장", desc = "공격 범위 +10%", apply = () => p.areaMul += 0.1f });

        choices.Clear();
        // 무기 옵션이 나올 확률을 조금 높이기 위해 무기 먼저 섞어서 뽑기
        for (int n = 0; n < 3 && pool.Count > 0; n++)
        {
            int idx = Random.Range(0, pool.Count);
            choices.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
    }

    void AddWeaponOption<T>(List<Upgrade> pool, Player p) where T : Weapon
    {
        var w = p.GetWeapon<T>();
        if (w != null && w.level >= Weapon.MaxLevel) return;
        if (w == null && p.weapons.Count >= 6) return;

        // 설명 문자열을 얻기 위해 임시 인스턴스 대신 이름/설명을 가진 컴포넌트를 사용
        string name, desc;
        if (w != null) { name = $"{w.DisplayName} Lv.{w.level + 1}"; desc = w.NextDescription(w.level + 1); }
        else
        {
            var tmp = new GameObject("tmp").AddComponent<T>();
            name = $"{tmp.DisplayName} (신규)"; desc = tmp.NextDescription(1);
            DestroyImmediate(tmp.gameObject);
        }
        var entry = new Upgrade { title = name, desc = desc, apply = () => p.AddWeapon<T>() };
        pool.Add(entry);
        pool.Add(entry); // 무기는 가중치 2배
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

        // 보유 무기
        float y = 52;
        foreach (var w in p.weapons)
        {
            GUI.Label(new Rect(12, y, 300, 26), $"{w.DisplayName}  Lv.{w.level}", UIStyles.Small);
            y += 22;
        }

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
