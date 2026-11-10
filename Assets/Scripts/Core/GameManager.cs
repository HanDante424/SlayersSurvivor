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

    /// <summary>[임시] 게임 카메라 시야 (화면 세로 절반, 유닛). 기존 7 → 1.5배</summary>
    public const float ViewSize = 10.5f;
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
        /// <summary>null = 선택 불가 (회색 표시, 눌러도 반응 없음)</summary>
        public Action apply;
    }

    void Awake()
    {
        I = this;
        Time.timeScale = 1f;
        CombatMods.Clear();   // 재시작 시 이전 판의 패시브 수정자가 남지 않게
        Application.targetFrameRate = 120;

        var cam = Camera.main;
        if (cam == null)
        {
            var cgo = new GameObject("Main Camera");
            cgo.tag = "MainCamera";
            cam = cgo.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = ViewSize;
        cam.backgroundColor = new Color(0.08f, 0.1f, 0.09f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        if (cam.GetComponent<CameraFollow>() == null) cam.gameObject.AddComponent<CameraFollow>();

        var pgo = new GameObject("Player");
        var player = pgo.AddComponent<Player>();
        // 파이어 슬래시 = 에른 스트레이 기본 무장 (기획 v0.2: 게임 시작 시 보유)
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
                if (Input.GetKeyDown(KeyCode.Alpha1 + i) || Input.GetKeyDown(KeyCode.Keypad1 + i)) { Pick(i); break; }
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

        if (!DebugCheats.NoSpawn) Spawning();   // 치트 F8: 적 생성 중지
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

    /// <summary>화면 밖 거리: 플레이어(화면 중앙)에서 화면 모서리 + 1.5 유닛. 적 생성·재배치 기준</summary>
    public static float OffscreenRadius
    {
        get
        {
            var cam = Camera.main;
            if (cam == null) return 16f;
            float h = cam.orthographicSize, w = h * cam.aspect;
            return Mathf.Sqrt(w * w + h * h) + 1.5f;
        }
    }

    Vector2 SpawnPos() => (Vector2)Player.I.transform.position + Random.insideUnitCircle.normalized * OffscreenRadius;

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

    // ---------- 선택 화면 (레벨업 → 기본 강화 → 특수 강화, 순차) ----------
    // 한 번의 캐릭터 레벨업 안에서 화면이 이어짐. 선택지의 apply 가 다음 화면을 열면(ShowMenu) 게임으로 돌아가지 않음.
    string menuTitle = "레벨 업!", menuSub = "";
    int menuSerial;

    void ShowMenu(string title, string sub, List<Upgrade> list)
    {
        menuTitle = title; menuSub = sub;
        choices.Clear();
        choices.AddRange(list);
        menuSerial++;
        SetState(GameState.LevelUp);
    }

    void OpenLevelUp()
    {
        while (pendingLevelUps > 0)
        {
            pendingLevelUps--;
            var list = BuildChoices();
            if (list.Count > 0) { ShowMenu("레벨 업!", "", list); return; }
            // 고를 수 있는 스킬이 하나도 없으면 이번 레벨업은 건너뜀
        }
    }

    void Pick(int i)
    {
        if (i < 0 || i >= choices.Count) return;
        if (choices[i].apply == null) return;   // 선택 불가 후보
        int serial = menuSerial;
        choices[i].apply();
        if (menuSerial != serial) return;      // 이어지는 선택 화면이 열림 (기본 강화 → 특수 강화)
        SetState(GameState.Playing);
        if (pendingLevelUps > 0) OpenLevelUp();
    }

    /// <summary>보유 일반 스킬 선택 시: 기본 강화 3종 중 하나 (기획 v0.2 4장)</summary>
    void ShowBasicUpgrade(Skill s)
    {
        var T = Tuning.Skills;
        int from = s.level, to = s.level + 1;
        string special = Skill.IsMilestone(to) ? $"\n<color=#F2BF4C>선택 후 Lv.{to} 특수 강화가 이어집니다</color>" : "";

        // 패시브: 공통 기본 강화 대신 고유 강화 3종
        var passive = s.PassiveBasicOptions();
        if (passive != null)
        {
            var plist = new List<Upgrade>();
            for (int k = 0; k < passive.Length; k++)
            {
                int idx = k;
                plist.Add(new Upgrade { title = passive[k].title, desc = passive[k].desc, apply = () => FinishPassiveBasic(s, idx) });
            }
            ShowMenu($"{s.Entry.name} 고유 강화", $"Lv.{from} → {to}{special}", plist);
            return;
        }

        float cdNow = s.CooldownReduction, cdNext = Mathf.Min(T.upgradeCooldown * (s.CooldownUps + 1), T.cooldownReductionCap);
        string cdCap = cdNext <= cdNow ? "  <color=#FF8080>(최대 90% 도달 — 더 줄지 않음)</color>" : "";
        var list = new List<Upgrade>
        {
            new Upgrade
            {
                title = $"공격력 강화  <size=16>피해량 +{Pct(T.upgradeDamage)}</size>",
                desc = $"누적 +{Pct(s.DamageBonus)} → +{Pct(s.DamageBonus + T.upgradeDamage)}",
                apply = () => FinishBasic(s, BasicUpgrade.Damage),
            },
            new Upgrade
            {
                title = $"범위 강화  <size=16>공격 범위 +{Pct(T.upgradeRange)}</size>",
                desc = $"누적 +{Pct(s.RangeBonus)} → +{Pct(s.RangeBonus + T.upgradeRange)}",
                apply = () => FinishBasic(s, BasicUpgrade.Range),
            },
            new Upgrade
            {
                title = $"쿨타임 강화  <size=16>쿨타임 -{Pct(T.upgradeCooldown)}</size>",
                desc = $"누적 -{Pct(cdNow)} → -{Pct(cdNext)}{cdCap}",
                apply = () => FinishBasic(s, BasicUpgrade.Cooldown),
            },
        };
        ShowMenu($"{s.Entry.name} 기본 강화", $"Lv.{from} → {to}{special}", list);
    }

    /// <summary>기본 강화 확정 → 스킬 레벨 +1 → 특수 강화 레벨이면 특수 강화 화면</summary>
    void FinishBasic(Skill s, BasicUpgrade u)
    {
        s.ApplyBasicUpgrade(u);
        if (s.SpecialPending) ShowSpecialUpgrade(s);
    }

    /// <summary>패시브 고유 강화 확정 → 스킬 레벨 +1 → 특수 강화 레벨이면 특수 강화 화면</summary>
    void FinishPassiveBasic(Skill s, int index)
    {
        s.ApplyPassiveBasic(index);
        if (s.SpecialPending) ShowSpecialUpgrade(s);
    }

    /// <summary>
    /// 스킬 Lv.5·10·15 특수 강화 선택 (기본 강화 직후, 3개 중 1개).
    /// - 선택 불가 후보: 회색 + 이유 표시 (예: 타오름 보유 시 연속 폭발)
    /// - 경고 후보: 확인 창 → 그래도 선택 / 취소 (예: 뇌진탕 보유 시 타오름)
    /// 스킬이 선택지를 주지 않으면(기획 미설계) 확인 버튼 하나만 (효과 없음).
    /// </summary>
    void ShowSpecialUpgrade(Skill s)
    {
        int lv = s.level;
        var opts = s.SpecialOptions(lv);
        var list = new List<Upgrade>();
        for (int k = 0; k < opts.Length; k++)
        {
            int idx = k;
            var o = opts[k];
            if (o.locked)
                list.Add(new Upgrade
                {
                    title = $"<color=#777777>{o.title}</color>",
                    desc = $"<color=#FF8080>{o.lockReason}</color>",
                    apply = null,
                });
            else if (!string.IsNullOrEmpty(o.warning))
                list.Add(new Upgrade
                {
                    title = $"{o.title}  <size=14><color=#FF9A50>[경고]</color></size>",
                    desc = o.desc,
                    apply = () => ShowSpecialWarning(s, lv, idx, o),
                });
            else
                list.Add(new Upgrade { title = o.title, desc = o.desc, apply = () => s.ApplySpecial(lv, idx) });
        }
        if (list.Count == 0)
            list.Add(new Upgrade
            {
                title = "확인",
                desc = "<color=#AAAAAA>특수 강화 효과는 아직 기획되지 않았습니다 (적용되는 효과 없음)</color>",
                apply = () => s.ApplySpecial(lv, -1),
            });
        string order = lv == 5 ? "1차" : lv == 10 ? "2차" : "3차";
        ShowMenu($"{s.Entry.name} {order} 특수 강화", $"스킬 Lv.{lv} 도달", list);
    }

    /// <summary>특수 강화 경고 확인: 그래도 선택 → 획득 / 취소 → 특수 강화 선택 화면으로</summary>
    void ShowSpecialWarning(Skill s, int lv, int idx, SpecialUpgradeOption o)
    {
        var list = new List<Upgrade>
        {
            new Upgrade { title = $"그래도 {o.title} 선택", desc = o.desc, apply = () => s.ApplySpecial(lv, idx) },
            new Upgrade { title = "취소", desc = "다른 특수 강화를 고릅니다", apply = () => ShowSpecialUpgrade(s) },
        };
        ShowMenu("경고", $"<color=#FF9A50>{o.warning}</color>", list);
    }

    static string Pct(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

    /// <summary>테스트: 보유 스킬 강화 화면 바로 열기 (치트 패널 → 스킬별 Lv +1)</summary>
    public void DebugUpgradeSkill(Skill s)
    {
        if (State != GameState.Playing || s == null || !s.IsGeneral || s.level >= s.LevelCap) return;
        ShowBasicUpgrade(s);
    }

    /// <summary>
    /// 레벨업 선택지 3개 (기획서 2.3 / v0.2 3장). 자동·수동·이동·패시브·궁극기가 함께 후보로 등장.
    /// - 미보유 스킬 → 신규 획득 (Lv.1)
    /// - 보유 일반 스킬 → 기본 강화 선택 화면으로 이어짐 (선택 후 스킬 레벨 +1)
    /// - Lv.15(최대) 스킬은 후보에서 제외
    /// [임시] 후보별 출현 확률 미정 → 모두 같은 확률. 슬롯이 가득 찬 종류의 신규 스킬은 제외
    /// </summary>
    List<Upgrade> BuildChoices()
    {
        var p = Player.I;
        var T = Tuning.Skills;
        var pool = new List<Upgrade>();

        foreach (var entry in p.Catalog)
        {
            if (!SkillCatalog.LevelUpPool.Contains(entry.id)) continue;   // [임시] 리소스 있는 스킬만
            var owned = p.GetSkill(entry.id);
            string kind = SkillCatalog.KindLabel(entry.kind);

            if (owned != null)
            {
                if (owned.level >= owned.LevelCap) continue;
                // 궁극기 강화는 캐릭터 Lv.60 부터 (잠정)
                if (entry.kind == SkillKind.Ultimate && Level < T.ultimateUpgradeLevel) continue;
                int next = owned.level + 1;
                if (owned.IsGeneral)
                {
                    string extra = Skill.IsMilestone(next) ? $"  <color=#F2BF4C>★ Lv.{next} 특수 강화</color>" : "";
                    pool.Add(new Upgrade
                    {
                        title = $"{entry.name}  Lv.{owned.level} → {next}  <size=14>[{kind}]</size>",
                        desc = (owned.PassiveBasicOptions() is { } po
                                   ? "고유 강화 선택: " + string.Join(" / ", System.Array.ConvertAll(po, o => o.title))
                                   : "기본 강화 선택: 공격력 / 범위 / 쿨타임") + extra,
                        apply = () => ShowBasicUpgrade(owned),
                    });
                }
                else
                {
                    pool.Add(new Upgrade
                    {
                        title = $"{entry.name}  Lv.{owned.level} → {next}  <size=14>[{kind}]</size>",
                        desc = "궁극기 강화",
                        apply = () => owned.LevelUp(),
                    });
                }
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

        var picked = new List<Upgrade>();
        for (int n = 0; n < 3 && pool.Count > 0; n++)
        {
            int idx = Random.Range(0, pool.Count);
            picked.Add(pool[idx]);
            pool.RemoveAt(idx);
        }
        return picked;
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
    static GUIStyle subtitleStyle;
    static GUIStyle SubtitleStyle => subtitleStyle ??= new GUIStyle(UIStyles.Center) { richText = true };
    static GUIStyle cardText;
    static GUIStyle CardText => cardText ??= new GUIStyle(UIStyles.Label)
        { fontSize = 20, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };

    // ---------- 선택 카드 (세로 카드 가로 배치, 내용이 길면 드래그/휠로 스크롤) ----------
    const float CardDragThreshold = 8f;   // 이만큼 움직이면 클릭이 아니라 드래그
    float[] cardScroll = new float[4];
    int cardSerial = -1, dragCard = -1;
    float dragStartY, dragStartScroll;
    bool dragMoved;

    void DrawChoiceCards(float W, float H)
    {
        int n = choices.Count;
        if (n == 0) return;
        if (cardScroll.Length < n) cardScroll = new float[n];
        if (cardSerial != menuSerial)   // 새 화면이 열리면 스크롤 초기화
        {
            cardSerial = menuSerial; dragCard = -1;
            for (int i = 0; i < cardScroll.Length; i++) cardScroll[i] = 0f;
        }

        const float gap = 20f, pad = 16f;
        float cw = Mathf.Min(280f, (W - 40f - gap * (n - 1)) / n);
        float top = H * 0.27f;
        float ch = Mathf.Clamp(H * 0.82f - 70f - top, 160f, 420f);   // 아래 스킬 바 위까지
        float x0 = (W - (cw * n + gap * (n - 1))) / 2f;

        var ev = Event.current;
        Vector2 mouse = ev.mousePosition;

        for (int i = 0; i < n; i++)
        {
            var c = choices[i];
            var card = new Rect(x0 + i * (cw + gap), top, cw, ch);
            var view = new Rect(card.x + pad, card.y + pad, cw - pad * 2 - 6, ch - pad * 2);
            string text = $"<b>[{i + 1}] {c.title}</b>\n\n<size=17>{c.desc}</size>";
            float contentH = CardText.CalcHeight(new GUIContent(text), view.width);
            float maxScroll = Mathf.Max(0f, contentH - view.height);
            bool selectable = c.apply != null;
            bool over = card.Contains(mouse);

            // 입력: 누르고 움직이면 스크롤, 안 움직이고 떼면 선택
            switch (ev.type)
            {
                case EventType.MouseDown when ev.button == 0 && over:
                    dragCard = i; dragMoved = false;
                    dragStartY = mouse.y; dragStartScroll = cardScroll[i];
                    ev.Use();
                    break;
                case EventType.MouseDrag when dragCard == i:
                    float dy = mouse.y - dragStartY;
                    if (Mathf.Abs(dy) > CardDragThreshold) dragMoved = true;
                    if (dragMoved) cardScroll[i] = dragStartScroll - dy;
                    ev.Use();
                    break;
                case EventType.MouseUp when dragCard == i:
                    bool click = !dragMoved && over && selectable;
                    dragCard = -1;
                    ev.Use();
                    if (click) { Pick(i); return; }
                    break;
                case EventType.ScrollWheel when over:
                    cardScroll[i] += ev.delta.y * 20f;
                    ev.Use();
                    break;
            }
            cardScroll[i] = Mathf.Clamp(cardScroll[i], 0f, maxScroll);

            if (ev.type != EventType.Repaint) continue;

            // 카드 배경 (선택 불가 = 반투명 회색)
            GUI.color = selectable ? Color.white : new Color(1f, 1f, 1f, 0.45f);
            bool pressed = dragCard == i && !dragMoved;
            UIStyles.Button.Draw(card, GUIContent.none, over && selectable, pressed, false, false);

            // 내용 (카드 안으로 잘라서 그림)
            GUI.BeginGroup(view);
            CardText.Draw(new Rect(0, -cardScroll[i], view.width, contentH), new GUIContent(text), false, false, false, false);
            GUI.EndGroup();

            // 스크롤 표시: 오른쪽 막대 + 아래에 더 있으면 ▼
            if (maxScroll > 0f)
            {
                var track = new Rect(card.xMax - 8, view.y, 3, view.height);
                GUI.color = new Color(1, 1, 1, 0.12f * GUI.color.a); GUI.DrawTexture(track, UIStyles.White);
                float th = Mathf.Max(20f, track.height * view.height / contentH);
                float ty = track.y + (track.height - th) * (cardScroll[i] / maxScroll);
                GUI.color = new Color(1, 1, 1, 0.5f); GUI.DrawTexture(new Rect(track.x, ty, track.width, th), UIStyles.White);
                if (cardScroll[i] < maxScroll - 1f)
                {
                    GUI.color = UIStyles.Accent;
                    GUI.Label(new Rect(card.x, card.yMax - 22, card.width, 20), "▼", UIStyles.Center);
                }
            }
            GUI.color = Color.white;
        }
    }

    // 스킬 슬롯 색: 사용 가능 = 밝은 회색 / 사용 불가 = 어두운 회색, 쿨타임 동안 아래에서 회색이 차오름
    static readonly Color SlotReady = new Color(0.78f, 0.78f, 0.78f, 0.95f);
    static readonly Color SlotReadyText = new Color(0.08f, 0.08f, 0.08f, 1f);
    static readonly Color SlotLocked = new Color(0.16f, 0.16f, 0.16f, 0.92f);
    static readonly Color SlotFill = new Color(0.45f, 0.45f, 0.45f, 0.95f);
    static GUIStyle slotTime;
    static GUIStyle SlotTime => slotTime ??= new GUIStyle(UIStyles.Label) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

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
            Color text = Color.white;

            if (s == null)
            {
                // 빈 칸
                GUI.color = new Color(0, 0, 0, 0.35f);
                GUI.DrawTexture(r, UIStyles.White);
                text = new Color(1, 1, 1, 0.4f);
            }
            else
            {
                float cd = s.CooldownFraction;               // 1 = 방금 사용, 0 = 사용 가능
                bool ready = cd <= 0f && !(s.Entry.kind != SkillKind.Ultimate && p.IsBusy) && !s.CheatDisabled;
                if (ready)
                {
                    // 사용 가능: 밝은 회색
                    GUI.color = SlotReady;
                    GUI.DrawTexture(r, UIStyles.White);
                    text = SlotReadyText;
                }
                else
                {
                    // 사용 불가: 어두운 회색 + 아래에서부터 회색이 차오름 (다 차면 사용 가능)
                    GUI.color = SlotLocked;
                    GUI.DrawTexture(r, UIStyles.White);
                    float filled = r.height * Mathf.Clamp01(1f - cd);
                    GUI.color = SlotFill;
                    GUI.DrawTexture(new Rect(r.x, r.yMax - filled, r.width, filled), UIStyles.White);
                    // 남은 시간 (초)
                    float left = s.CooldownRemaining;
                    if (left > 0f)
                    {
                        GUI.color = Color.white;
                        GUI.Label(r, left >= 10f ? $"{left:0}" : $"{left:0.0}", SlotTime);
                    }
                }
                GUI.color = text;
                GUI.Label(new Rect(r.x + 2, r.y, r.width - 4, r.height - 2), s.Entry.name, slotName);
                if (s.Badge != null) GUI.Label(new Rect(r.x, r.y + 2, r.width - 4, 20), s.Badge, slotBadge);
            }
            GUI.color = text;
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
                // 기본 강화 누적치도 표시 (합연산 확인용)
                string ups = !s.IsGeneral || s.level <= 1 ? ""
                    : s.BasicSummary != null ? $"  <size=12><color=#AAAAAA>{s.BasicSummary}</color></size>"
                    : $"  <size=12><color=#AAAAAA>공 +{Pct(s.DamageBonus)} · 범 +{Pct(s.RangeBonus)} · 쿨 -{Pct(s.CooldownReduction)}</color></size>";
                GUI.Label(new Rect(12, y, 480, 26),
                    $"<size=13><color=#AAAAAA>{SkillCatalog.KindLabel(k)}</color></size>  {s.Entry.name}  Lv.{s.level}{ups}",
                    SkillListStyle);
                y += 22;
                string spSummary = s.IsGeneral ? s.SpecialSummary : "";
                if (spSummary.Length > 0)
                {
                    GUI.Label(new Rect(28, y - 2, 560, 24), $"<size=12><color=#F2BF4C>★ {spSummary}</color></size>", SkillListStyle);
                    y += 18;
                }
            }
        }

        DrawSkillBar(p, W, H);

        float mbw = 260, mbh = 56;
        float mbx = (W - mbw) / 2;

        if (State == GameState.LevelUp)
        {
            UIStyles.Overlay(UIStyles.Dim);
            GUI.Label(new Rect(0, H * 0.15f - 20, W, 60), menuTitle, UIStyles.Big);
            if (!string.IsNullOrEmpty(menuSub))
                GUI.Label(new Rect(0, H * 0.15f + 36, W, 56), menuSub, SubtitleStyle);
            DrawChoiceCards(W, H);
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
