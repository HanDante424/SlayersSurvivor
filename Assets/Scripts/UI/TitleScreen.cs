using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 타이틀 화면. 메뉴 항목을 추가하려면 Awake의 items 목록에 한 줄 추가하면 됩니다.
/// 키보드: ↑/↓(W/S) 이동, Enter/Space 선택. 마우스 클릭도 가능.
/// </summary>
public class TitleScreen : MonoBehaviour
{
    class MenuItem
    {
        public string label;
        public bool enabled;
        public Action action;
    }

    readonly List<MenuItem> items = new List<MenuItem>();
    int selected;
    Vector2 lastMouse;
    string toast;
    float toastTime;
    readonly List<Transform> deco = new List<Transform>();
    readonly List<Vector2> decoVel = new List<Vector2>();

    void Awake()
    {
        Time.timeScale = 1f;
        GameSettings.ApplySavedOnce();   // 저장된 화면 설정 (게임을 켤 때 한 번)
        items.Add(new MenuItem { label = "플레이", enabled = true, action = () => OpenCharacterSelect(StageKind.Survival) });
        items.Add(new MenuItem { label = "레이드 보스", enabled = true, action = OpenRaidSelect });
        items.Add(new MenuItem { label = "설정", enabled = true, action = OpenSettings });
        items.Add(new MenuItem { label = "게임 종료", enabled = true, action = SceneLoader.Quit });

        SetupCamera();
        SpawnDecoration();
    }

    /// <summary>열려 있는 캐릭터 선택 화면 (없으면 null). 열려 있는 동안 타이틀 메뉴는 입력 · 표시 안 함</summary>
    CharacterSelect select;

    /// <summary>열려 있는 설정 화면 (없으면 null). 열려 있는 동안 타이틀 메뉴는 입력 · 표시 안 함</summary>
    SettingsScreen settings;

    void OpenSettings()
    {
        if (settings != null || select != null) return;
        settings = SettingsScreen.Open(gameObject, onBack: () => settings = null);
    }

    /// <summary>열려 있는 레이드 보스 선택 화면 (없으면 null)</summary>
    RaidSelectScreen raidSelect;

    /// <summary>레이드 보스 선택 → 고르면 그 보스로 캐릭터 선택</summary>
    void OpenRaidSelect()
    {
        if (OverlayOpen) return;
        raidSelect = RaidSelectScreen.Open(gameObject,
            onPick: b => { raidSelect = null; RaidStage.Selected = b.kind; OpenCharacterSelect(StageKind.Raid); },
            onBack: () => raidSelect = null);
    }

    /// <summary>캐릭터 선택 · 설정 · 레이드 선택 화면이 위에 열려 있음</summary>
    bool OverlayOpen => select != null || settings != null || raidSelect != null;

    /// <summary>스테이지를 정하고 캐릭터 선택 화면 열기 (시작하면 Main 씬이 그 스테이지로)</summary>
    void OpenCharacterSelect(StageKind stage)
    {
        if (select != null) return;
        Stage.Current = stage;
        select = CharacterSelect.Open(gameObject, onBack: () => select = null);
    }

    void SetupCamera()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = go.AddComponent<Camera>();
        }
        cam.orthographic = true;
        cam.orthographicSize = 7f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.06f, 0.07f, 0.07f);
        cam.transform.position = new Vector3(0, 0, -10);

        var ground = new GameObject("Ground");
        var sr = ground.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.Get("Ground", SpriteFactory.Shape.Ground);
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(40, 24);
        sr.sortingOrder = -100;
        sr.color = new Color(1, 1, 1, 0.6f);
    }

    // 배경에 적들이 천천히 떠다니는 연출
    void SpawnDecoration()
    {
        var kinds = new[]
        {
            ("Enemy_Goblin", SpriteFactory.Shape.Circle, new Color(0.45f, 0.75f, 0.3f), 0.8f),
            ("Enemy_GoblinArcher", SpriteFactory.Shape.Diamond, new Color(0.85f, 0.8f, 0.3f), 0.8f),
            ("Enemy_Hobgoblin", SpriteFactory.Shape.Square, new Color(0.3f, 0.5f, 0.25f), 1.3f),
            ("Enemy_GoblinRider", SpriteFactory.Shape.Diamond, new Color(0.75f, 0.45f, 0.25f), 1.2f),
        };
        for (int i = 0; i < 28; i++)
        {
            var k = kinds[UnityEngine.Random.Range(0, kinds.Length)];
            var go = new GameObject("Deco");
            go.transform.position = new Vector3(UnityEngine.Random.Range(-14f, 14f), UnityEngine.Random.Range(-8f, 8f));
            go.transform.localScale = Vector3.one * k.Item4;
            var sr = SpriteFactory.AddRenderer(go, k.Item1, k.Item2, k.Item3, 0);
            var c = sr.color; c.a = 0.35f; sr.color = c;
            deco.Add(go.transform);
            decoVel.Add(UnityEngine.Random.insideUnitCircle * 0.6f);
        }
    }

    void Update()
    {
        for (int i = 0; i < deco.Count; i++)
        {
            var p = (Vector2)deco[i].position + decoVel[i] * Time.deltaTime;
            if (p.x < -15) p.x = 15; else if (p.x > 15) p.x = -15;
            if (p.y < -9) p.y = 9; else if (p.y > 9) p.y = -9;
            deco[i].position = p;
        }
        if (OverlayOpen) return;   // 캐릭터 선택 · 설정 화면이 입력 처리

        if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W)) Move(-1);
        if (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S)) Move(1);
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            items[selected].action();

        if (toastTime > 0) toastTime -= Time.deltaTime;
    }

    void Move(int dir)
    {
        selected = (selected + dir + items.Count) % items.Count;
    }

    void ShowToast(string msg)
    {
        toast = msg;
        toastTime = 1.8f;
    }

    void OnGUI()
    {
        if (OverlayOpen) return;   // 캐릭터 선택 · 설정 화면이 그림
        UIStyles.Init();
        float W = Screen.width, H = Screen.height;

        UIStyles.Overlay(new Color(0, 0, 0, 0.35f));

        // 타이틀
        float bob = Mathf.Sin(Time.time * 1.5f) * 6f;
        GUI.Label(new Rect(0, H * 0.18f + bob, W, 90), "SLAYERS SURVIVOR", UIStyles.Title);
        GUI.Label(new Rect(0, H * 0.18f + 90, W, 30), "끝없이 몰려오는 적을 버텨내라", UIStyles.Center);

        // 메뉴
        float bw = 320, bh = 64, gap = 16;
        float bx = (W - bw) / 2, by = H * 0.48f;
        // 마우스를 움직였을 때만 마우스 위치로 선택 이동 (키보드 선택과 충돌 방지)
        bool mouseMoved = false;
        if (Event.current.type == EventType.Repaint)
        {
            mouseMoved = (Event.current.mousePosition - lastMouse).sqrMagnitude > 0.5f;
            lastMouse = Event.current.mousePosition;
        }
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var r = new Rect(bx, by + i * (bh + gap), bw, bh);

            if (mouseMoved && r.Contains(Event.current.mousePosition)) selected = i;

            bool sel = i == selected;
            string text = it.enabled ? it.label : $"{it.label}  <size=16>(준비 중)</size>";
            if (sel) text = $"<color=#F2BF4C>▶ {text} ◀</color>";

            GUI.color = it.enabled ? Color.white : new Color(1, 1, 1, 0.5f);
            if (GUI.Button(r, text, UIStyles.MenuButton))
            {
                selected = i;
                it.action();
            }
            GUI.color = Color.white;
        }

        if (toastTime > 0 && !string.IsNullOrEmpty(toast))
        {
            var c = GUI.color; GUI.color = new Color(1, 1, 1, Mathf.Clamp01(toastTime / 0.4f));
            GUI.Label(new Rect(0, by + items.Count * (bh + gap) + 10, W, 30), toast, UIStyles.Center);
            GUI.color = c;
        }

        GUI.Label(new Rect(12, H - 32, 300, 24), "v0.1 prototype", UIStyles.Small);
        GUI.Label(new Rect(W - 360, H - 32, 350, 24), "↑↓ 이동   Enter 선택", new GUIStyle(UIStyles.Small) { alignment = TextAnchor.UpperRight });
    }
}
