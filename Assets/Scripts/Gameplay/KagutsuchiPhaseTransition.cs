using UnityEngine;

/// <summary>
/// 카구츠치 1→2페이즈 전환·입장 전용. RaidStage가 진행 권한을 넘기고,
/// 마지막에 EndTransition으로 돌려받음. 일반 2페이즈 패턴은 여기서 실행하지 않음.
/// </summary>
public class KagutsuchiPhaseTransition : MonoBehaviour
{
    const float ChargeTime = 2f;
    const float IntroPoseAt = 0.5f;          // [임시] 고개를 완전히 숙이는 시점
    const float ZoomInTime = 0.5f;          // [임시] 초기 확대 시간
    const float ZoomStart = 4.5f, ZoomEnd = 4f; // [임시] 문서의 시야
    const float WarnTime = 5f, ImpactRadius = 16f;
    const float HealTime = 1f, KnockTime = 0.3f; // 넉백 시간 [임시]
    const float ImpactDamage = 40f;         // [임시] 입장 강타 피해
    const float HitTime = 0.05f, PoseHold = 2f; // 입장 강타 판정 종료 후 자세 유지
    const float ChargeFlashHold = 0.5f, ImpactFlashHold = 0.2f;
    const float FlashFade = 0.5f;           // [임시] 흰색 점멸이 사라지는 시간
    const float BodyShake = 0.12f, CameraShake = 0.05f, Aftershock = 0.35f; // [임시] 진동 세기(유닛)
    const float ShakeHz = 30f;             // [임시] 진동 빈도
    const float BodyHeight = 4f;           // [임시] 1페이즈와 같은 화면 키
    const string Intro1 = "Kagutsuchi_P2_Intro1", Intro2 = "Kagutsuchi_P2_Intro2";
    const string Smash = "Kagutsuchi_P2_Atk_GroundSmashHeavy", Stand = "Kagutsuchi_P2_Stand";

    enum Step { Charge, Warning, Impact, Finished }
    Step step;
    Enemy body;
    SpriteRenderer source, visual;
    GameObject visualRoot;
    BurnFx flames;
    BossWarning warning;
    Sprite originalSprite;
    bool originalFlip, initialized, completed, hit, bowed;
    Vector3 originalPosition;
    float elapsed, flashLeft, visualHeight, footOffset;
    Vector3 visualRest;
    Camera cam;
    float initialZoom;

    public void Begin(Enemy boss)
    {
        body = boss;
        source = boss.GetComponent<SpriteRenderer>();
        originalSprite = source.sprite;
        originalFlip = source.flipX;
        originalPosition = transform.position;
        visualHeight = source.bounds.size.y;
        footOffset = source.bounds.min.y - transform.position.y;
        cam = Camera.main;
        initialZoom = cam != null ? cam.orthographicSize : ZoomStart;
        body.SetScripted(true);
        body.ControlImmune = true;
        FlameSword.RemoveBurn(body);
        body.ClearControlEffects();
        body.SetHidden(true);
        visualRoot = new GameObject("KagutsuchiTransitionVisual");
        visualRoot.transform.SetParent(transform, false);
        visual = visualRoot.AddComponent<SpriteRenderer>();
        visual.sortingOrder = source.sortingOrder;
        ShowPose(Intro1, false);
        flames = BurnFx.Show(visualRoot.transform, () => body != null ? body.radius : 0f, ChargeTime);
        CameraFollow.Focus(transform, ZoomStart);
        CameraFollow.SetTransitionView(initialZoom, Vector2.zero);
        initialized = true;
    }

    void ShowPose(string name, bool side)
    {
        visual.sprite = SpriteFactory.TryGet(name) ?? SpriteFactory.Get("Enemy_Kagutsuchi", SpriteFactory.Shape.Circle);
        visual.flipX = side && Player.I != null && Player.I.HurtCenter.x > transform.position.x;
        float height = side ? BodyHeight : visualHeight;
        var bounds = visual.sprite.bounds;
        float scale = height / Mathf.Max(0.001f, bounds.size.y);
        Vector3 parentScale = transform.lossyScale;
        visualRoot.transform.localScale = new Vector3(scale / Mathf.Abs(parentScale.x), scale / Mathf.Abs(parentScale.y), 1f);
        visualRoot.transform.position = transform.position + new Vector3(0f, footOffset - bounds.min.y * scale, 0f);
        visualRest = visualRoot.transform.localPosition;
    }

    void Update()
    {
        if (!initialized || completed || body == null) return;
        if (GameManager.I == null) { enabled = false; return; }
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory)
        { enabled = false; return; }
        if (GameManager.I.State != GameState.Playing) return;
        float dt = Time.deltaTime;
        flashLeft = Mathf.Max(0f, flashLeft - dt);
        elapsed += dt;
        if (step == Step.Charge)
        {
            if (elapsed >= IntroPoseAt && !bowed) { ShowPose(Intro2, false); bowed = true; }
            float zoom = elapsed <= ZoomInTime
                ? Mathf.Lerp(initialZoom, ZoomStart, Mathf.Clamp01(elapsed / ZoomInTime))
                : Mathf.Lerp(ZoomStart, ZoomEnd, Mathf.Clamp01((elapsed - ZoomInTime) / (ChargeTime - ZoomInTime)));
            Vector2 noise = new Vector2(Mathf.Sin(elapsed * ShakeHz), Mathf.Cos(elapsed * ShakeHz * 1.3f));
            Vector3 parentScale = transform.lossyScale;
            visualRoot.transform.localPosition = visualRest + new Vector3(noise.x * BodyShake / parentScale.x, noise.y * BodyShake / parentScale.y, 0f);
            CameraFollow.SetTransitionView(zoom, noise * CameraShake);
            if (elapsed >= ChargeTime) BeginWarning();
        }
        else if (step == Step.Warning)
        {
            if (warning != null) warning.Set(Stage.Center, ImpactRadius * Mathf.Clamp01(elapsed / WarnTime));
            if (elapsed >= WarnTime) BeginImpact();
        }
        else if (step == Step.Impact)
        {
            if (!hit && elapsed <= HitTime) TryImpactHit();
            body.hp = Mathf.Max(0.01f, body.maxHp * Mathf.SmoothStep(0f, 1f, elapsed / HealTime));
            float amp = Aftershock * (1f - Mathf.Clamp01(elapsed / HealTime));
            CameraFollow.SetTransitionView(-1f, Random.insideUnitCircle * amp);
            if (elapsed >= Mathf.Max(HealTime, HitTime + PoseHold)) Finish();
        }
    }

    void BeginWarning()
    {
        step = Step.Warning;
        elapsed = 0f;
        Flash(ChargeFlashHold);
        StopFlames();
        visual.enabled = false;
        CameraFollow.SetTransitionView(-1f, Vector2.zero);
        CameraFollow.ClearFocus();
        CameraFollow.FollowMap(KagutsuchiAI.BattleViewScale, KagutsuchiAI.FullMapCameraMargin);
        warning = BossWarning.Circle(Stage.Center, 0f, 0f);
    }

    void BeginImpact()
    {
        step = Step.Impact;
        elapsed = 0f;
        ClearWarning();
        transform.position = Stage.Center;
        ShowPose(Smash, true);
        visual.enabled = true;
        Flash(ImpactFlashHold);
        CameraFollow.Focus(transform, ZoomStart);
        TryImpactHit();
        var p = Player.I;
        if (p != null && !DebugCheats.God && !p.IsInvulnerable)
        {
            Vector2 dir = p.HurtCenter - Stage.Center;
            if (dir.sqrMagnitude < 0.0001f) dir = Random.insideUnitCircle;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector2.down;
            dir.Normalize();
            Vector2 edge = p.HurtCenter + dir * Stage.ExitDistance(p.HurtCenter, dir);
            Vector2 target = Stage.ClampInside(edge, p.radius) - (p.HurtCenter - (Vector2)p.transform.position);
            p.MoveByBoss(target, KnockTime);
        }
    }

    void TryImpactHit()
    {
        var p = Player.I;
        if (p == null) return;
        float r = ImpactRadius + p.radius;
        if ((p.HurtCenter - Stage.Center).sqrMagnitude <= r * r)
        {
            hit = true;
            p.TakeDamage(ImpactDamage);
        }
    }

    void Flash(float hold) { flashLeft = hold + FlashFade; }

    void OnGUI()
    {
        if (!initialized || flashLeft <= 0f) return;
        int depth = GUI.depth;
        Color color = GUI.color;
        GUI.depth = int.MinValue;
        GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(flashLeft / FlashFade));
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = color;
        GUI.depth = depth;
    }

    void Finish()
    {
        body.hp = body.maxHp;
        FlameSword.RemoveBurn(body);
        body.ClearControlEffects();
        source.sprite = SpriteFactory.TryGet(Stand) ?? originalSprite;
        source.flipX = Player.I != null && Player.I.HurtCenter.x > transform.position.x;
        body.SetHidden(false);
        completed = true;
        step = Step.Finished;
        Cleanup();
        RaidStage.EndTransition(body);
        Destroy(this);
    }

    void StopFlames()
    {
        if (flames != null) { flames.gameObject.SetActive(false); Destroy(flames.gameObject); flames = null; }
    }

    void ClearWarning()
    {
        if (warning != null) { warning.gameObject.SetActive(false); Destroy(warning.gameObject); warning = null; }
    }

    void Cleanup()
    {
        flashLeft = 0f;
        StopFlames();
        ClearWarning();
        if (visualRoot != null) { visualRoot.SetActive(false); Destroy(visualRoot); visualRoot = null; }
        CameraFollow.ClearTransitionView();
        CameraFollow.ClearFocus();
        CameraFollow.FollowMap(KagutsuchiAI.BattleViewScale, KagutsuchiAI.FullMapCameraMargin);
    }

    void OnDisable()
    {
        if (!initialized || completed) return;
        Cleanup();
        if (!completed && body != null)
        {
            if (source != null) { source.sprite = originalSprite; source.flipX = originalFlip; }
            transform.position = originalPosition;
            body.SetHidden(false);
            body.ControlImmune = false;
            // 중단된 연출이 무적·전환 상태를 남기지 않게 공통 종료로 반환.
            RaidStage.EndTransition(body);
        }
    }
}
