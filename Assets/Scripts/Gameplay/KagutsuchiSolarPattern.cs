using System;
using UnityEngine;

/// <summary>2페이즈 특수 1. Phase2가 발동과 Tick을 소유하고, 실행기는 경고·기둥·표시·무적·카메라를 회수한다.</summary>
public sealed class KagutsuchiSolarPattern : MonoBehaviour
{
    public static readonly float[] Thresholds = { 0.9f, 0.6f, 0.3f };
    const float EdgeToCenterTime = 1f, PrepareTime = 2.5f, ExitTime = 0.5f;
    const int RandomCount = 20, TargetCount = 10;
    const float RandomInterval = 0.1f, RandomWarning = 1f, RandomRadius = 1.5f;
    const float TargetInterval = 0.25f, TargetWarning = 0.5f, TargetRadius = 1f, BetweenRains = 2f;
    const float PillarWarning = 0.5f, PillarFall = 0.25f, PillarShake = 0.25f;
    const float SunRadius = 12f, SunFall = 6f, SunHold = 2f;
    const float SunWarningScale = 1.25f; // 사용자 지정: 경고와 착지 피해 타원 25% 확대
    const float SunVisualScale = 0.9225f; // 사용자 지정: 직전 본체 그림 크기에서 2.5% 증가
    const float GroundEffectLead = 1f, GroundEffectFullSizeLead = 1f;
    const int ContactSearchIterations = 24; // 낙하 곡선 역산 정밀도
    const float BossDrop = 0.25f, BossBelowSunTop = 6f, StompPose = 2f;
    const float MapWarning = 4f, LandPose = 0.5f, MapStun = 10f;
    const float FlashHold = 1f, FlashFade = 1f, LaughTime = 15f, LaughCameraHold = 2f;
    const float Damage = 5f; // [임시] 사용자 지정 모든 타격 피해
    const float Magnification = 1.5f; // [임시] 기존 잡기 연출의 확대 배율 재사용
    const float WeakShake = 0.05f, StrongShake = 0.35f, PillarShakeAmp = 0.2f; // [임시] 기존 연출 세기 재사용
    const float Epsilon = 0.0001f;
    enum Step { Idle, CenterRun, Prepare, Exit, RandomRain, Between, TargetRain, PillarWarning, PillarFall, SunFall, SunHold, BossDrop, Stomp, MapWarning, Land, Flash, Laugh }
    Step step;
    Enemy body;
    SpriteRenderer bodyVisual;
    Sprite stomp, jump, stompReady, stompAttack, landAttack, laugh;
    Action<Sprite, float> showPose;
    Action<float> showRun;
    Action stopPose;
    Func<Transform> visualTarget;
    float elapsed, startHeight, sunGroundStartTime;
    int emitted;
    bool beforeScripted, beforeHidden, disposed;
    GameObject cameraAnchor, sun;
    Material sunMaterial;
    KagutsuchiSolarGroundEffect sunGroundEffect;
    BossWarning sunWarning;
    KagutsuchiSolarArena arena;
    KagutsuchiSolarRain rain;
    Vulnerability modifier;
    public bool Active => step != Step.Idle;
    public bool Vulnerable => step == Step.Laugh;
    static Vector2 SunHalfAxes => new Vector2(SunRadius, SunRadius * KagutsuchiSolarGroundEffect.WarningAspectRatio) * SunWarningScale;

    sealed class Vulnerability : CombatModifier
    {
        readonly KagutsuchiSolarPattern owner;
        public Vulnerability(KagutsuchiSolarPattern owner) { this.owner = owner; }
        public override float ReceivedMul(Enemy target, in DamageInfo damage)
            => owner.Vulnerable && target == owner.body ? 3f : 1f;
    }

    public void Initialize(Enemy body, Sprite stomp, Sprite jump, Sprite stompReady, Sprite stompAttack,
                           Sprite landAttack, Sprite laugh, Action<Sprite, float> showPose, Action<float> showRun,
                           Action stopPose, Func<Transform> visualTarget)
    {
        this.body = body; bodyVisual = body.GetComponent<SpriteRenderer>();
        this.stomp = stomp; this.jump = jump; this.stompReady = stompReady;
        this.stompAttack = stompAttack; this.landAttack = landAttack; this.laugh = laugh;
        this.showPose = showPose; this.showRun = showRun; this.stopPose = stopPose; this.visualTarget = visualTarget;
        modifier = new Vulnerability(this); CombatMods.Add(modifier);
    }

    public void Begin()
    {
        Cancel();
        if (body == null || disposed) return;
        beforeScripted = body.Scripted; beforeHidden = body.Hidden;
        body.SetScripted(true);
        startHeight = Stage.HalfSize.y * 2f + SunRadius; // 맵 위 화면 밖에서 시작하는 표시 높이
        rain = new KagutsuchiSolarRain(startHeight);
        cameraAnchor = new GameObject("SolarFixedCameraAnchor");
        cameraAnchor.transform.position = Stage.Center;
        Enter(Step.CenterRun);
        showRun(0f);
        CameraFollow.BeginOutsideMapFollow(this, visualTarget(), Magnification);
    }

    void Update()
    {
        if (!Active || GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) Cancel();
    }

    public Vector2 Tick(float dt)
    {
        if (!Active || dt <= 0f) return Vector2.zero;
        if (body == null || body.hp <= 0f || Player.I == null || Player.I.hp <= 0f) { Cancel(); return Vector2.zero; }
        elapsed += dt;
        rain?.Tick(dt);
        sunGroundEffect?.Tick(dt);
        switch (step)
        {
            case Step.CenterRun:
                Vector2 current = body.transform.position;
                Vector2 next = Vector2.MoveTowards(current, Stage.Center, Stage.HalfSize.magnitude / EdgeToCenterTime * dt);
                bodyVisual.flipX = Stage.Center.x > current.x;
                showRun(elapsed);
                if ((next - Stage.Center).sqrMagnitude <= Epsilon * Epsilon)
                {
                    Enter(Step.Prepare); showPose(stomp, 0f);
                    CameraFollow.Shake(PrepareTime, WeakShake);
                }
                return (next - current) / dt;
            case Step.Prepare:
                if (elapsed >= PrepareTime)
                {
                    Enter(Step.Exit); showPose(jump, 0f);
                    CameraFollow.BeginOutsideMapFollow(this, cameraAnchor.transform, Magnification);
                    CameraFollow.Shake(ExitTime, StrongShake);
                }
                break;
            case Step.Exit:
                showPose(jump, startHeight * Mathf.Clamp01(elapsed / ExitTime));
                if (elapsed >= ExitTime)
                {
                    body.SetHidden(true); stopPose();
                    CameraFollow.SetOutsideMapFullView(this);
                    emitted = 0; Enter(Step.RandomRain);
                }
                break;
            case Step.RandomRain:
                SpawnRain(false);
                if (emitted == RandomCount && !rain.HasActive) Enter(Step.Between);
                break;
            case Step.Between:
                if (elapsed >= BetweenRains) { emitted = 0; Enter(Step.TargetRain); }
                break;
            case Step.TargetRain:
                SpawnRain(true);
                if (emitted == TargetCount && !rain.HasActive)
                {
                    arena = new KagutsuchiSolarArena(); arena.Warn(); Enter(Step.PillarWarning);
                }
                break;
            case Step.PillarWarning:
                if (elapsed >= PillarWarning) { Enter(Step.PillarFall); arena.DrawFalling(startHeight); }
                break;
            case Step.PillarFall:
                arena.DrawFalling(startHeight * (1f - Mathf.Clamp01(elapsed / PillarFall)));
                if (elapsed >= PillarFall)
                {
                    arena.Land();
                    foreach (var position in arena.Positions) HitCircle(position, KagutsuchiSolarArena.Radius);
                    CameraFollow.Shake(PillarShake, PillarShakeAmp);
                    BeginSun();
                }
                break;
            case Step.SunFall:
                if (elapsed >= sunGroundStartTime)
                    sunGroundEffect?.Show((elapsed - sunGroundStartTime) / (SunFall - GroundEffectFullSizeLead - sunGroundStartTime));
                sun.transform.position = Stage.Center + Vector2.up * (startHeight * (1f - KagutsuchiSolarGeometry.SunFallProgress(elapsed / SunFall)));
                if (elapsed >= SunFall)
                {
                    sunGroundEffect?.Show();
                    ClearSunWarning(); HitSunLanding(); Enter(Step.SunHold);
                }
                break;
            case Step.SunHold:
                if (elapsed >= SunHold)
                {
                    body.SetHidden(false); Enter(Step.BossDrop);
                    ShowFeet(stompReady, SunRadius + startHeight);
                }
                break;
            case Step.BossDrop:
                float footY = Mathf.Lerp(SunRadius + startHeight, SunRadius - BossBelowSunTop, Mathf.Clamp01(elapsed / BossDrop));
                ShowFeet(stompReady, footY);
                if (elapsed >= BossDrop)
                {
                    Enter(Step.Stomp); ShowFeet(stompAttack, SunRadius - BossBelowSunTop);
                }
                break;
            case Step.Stomp:
                if (elapsed >= StompPose) { arena.WarnWholeMap(); Enter(Step.MapWarning); }
                break;
            case Step.MapWarning:
                if (elapsed >= MapWarning)
                {
                    ShowFeet(landAttack, SunRadius - BossBelowSunTop); Enter(Step.Land);
                    CameraFollow.BeginOutsideMapFollow(this, visualTarget(), Magnification);
                }
                break;
            case Step.Land:
                if (elapsed >= LandPose) Blast();
                break;
            case Step.Flash:
                if (elapsed >= FlashHold + FlashFade)
                {
                    Enter(Step.Laugh); body.SetScripted(false); showPose(laugh, 0f);
                }
                break;
            case Step.Laugh:
                if (elapsed >= LaughCameraHold) CameraFollow.EndOutsideMapFollow(this);
                if (elapsed >= LaughTime) Cancel();
                break;
        }
        return Vector2.zero;
    }

    void Enter(Step next) { step = next; elapsed = 0f; }

    void SpawnRain(bool targeted)
    {
        int total = targeted ? TargetCount : RandomCount;
        float radius = targeted ? TargetRadius : RandomRadius;
        float interval = targeted ? TargetInterval : RandomInterval;
        while (emitted < total && elapsed >= emitted * interval)
        {
            Vector2 target = targeted ? Stage.ClampInside(Player.I.HurtCenter, radius)
                : Stage.Center + new Vector2(UnityEngine.Random.Range(-Stage.HalfSize.x + radius, Stage.HalfSize.x - radius),
                                             UnityEngine.Random.Range(-Stage.HalfSize.y + radius, Stage.HalfSize.y - radius));
            rain.Spawn(target, radius, targeted ? TargetWarning : RandomWarning);
            emitted++;
        }
    }

    void BeginSun()
    {
        sunWarning = BossWarning.Ellipse(Stage.Center, SunHalfAxes, 0f);
        sun = new GameObject("KagutsuchiSunFire");
        var sr = sun.AddComponent<SpriteRenderer>();
        sr.sprite = SpriteFactory.TryGet("Kagutsuchi_P2_SunFire") ?? SpriteFactory.Get("Fx_Circle", SpriteFactory.Shape.Circle);
        sun.transform.localScale = Vector3.one * (SunRadius * 2f * SunVisualScale / sr.sprite.bounds.size.x);
        sun.transform.position = Stage.Center + Vector2.up * startHeight;
        // 태양 꼭대기에 착지한 보스와 테두리를 화염구가 덮지 않도록 그 뒤에 표시.
        sr.sortingOrder = bodyVisual.sortingOrder - 2;
        var shader = Resources.Load<Shader>("Shaders/SolarWorldClip");
        if (shader != null)
        {
            sunMaterial = new Material(shader); sunMaterial.SetFloat("_ClipY", Stage.Center.y); sr.sharedMaterial = sunMaterial;
        }
        else Debug.LogError("SolarWorldClip 셰이더를 찾을 수 없습니다.");
        sunGroundEffect = new KagutsuchiSolarGroundEffect(sr, sunMaterial, Stage.Center, SunRadius);
        sunGroundStartTime = FindGroundEffectStartTime();
        Enter(Step.SunFall);
    }

    float FindGroundEffectStartTime()
    {
        float contactProgress = Mathf.Clamp01(1f - KagutsuchiSolarGroundEffect.VisibleSunContactHeight / startHeight);
        float low = 0f, high = SunFall;
        // 실제 낙하 곡선을 역산하여 초별 진행률을 바꾸어도 접촉보다 1초 앞서 시작.
        for (int i = 0; i < ContactSearchIterations; i++)
        {
            float mid = (low + high) * 0.5f;
            if (KagutsuchiSolarGeometry.SunFallProgress(mid / SunFall) < contactProgress) low = mid;
            else high = mid;
        }
        return Mathf.Max(0f, high - GroundEffectLead);
    }

    void ShowFeet(Sprite pose, float heightAboveCenter)
    {
        float bottom = pose != null ? pose.bounds.min.y * Mathf.Abs(body.transform.lossyScale.y) : 0f;
        showPose(pose, Stage.Center.y + heightAboveCenter - body.transform.position.y - bottom);
    }

    void HitSunLanding()
    {
        var player = Player.I;
        if (player != null && KagutsuchiSolarGeometry.EllipseOverlapsCircle(Stage.Center, SunHalfAxes, player.HurtCenter, player.radius))
            player.TakeDamage(Damage);
    }

    void HitCircle(Vector2 center, float radius)
    {
        var player = Player.I;
        if (player != null && (player.HurtCenter - center).sqrMagnitude <= (radius + player.radius) * (radius + player.radius))
            player.TakeDamage(Damage);
    }

    void Blast()
    {
        var player = Player.I;
        if (player != null && !arena.IsSafe(player.HurtCenter)) player.HitByBoss(Damage, MapStun, Vector2.zero, 0f);
        arena.Dispose(); arena = null;
        sunGroundEffect?.Dispose(); sunGroundEffect = null;
        if (sun != null) { sun.SetActive(false); Destroy(sun); sun = null; }
        if (sunMaterial != null) { Destroy(sunMaterial); sunMaterial = null; }
        // 흰 화면 시작부터 지면 중앙에서 Laugh 표시. 취약 시간은 화면 복귀 후 시작.
        showPose(laugh, 0f);
        Enter(Step.Flash);
    }

    void OnGUI()
    {
        if (step != Step.Flash) return;
        int depth = GUI.depth; Color color = GUI.color;
        GUI.depth = -1000;
        float alpha = elapsed <= FlashHold ? 1f : 1f - Mathf.Clamp01((elapsed - FlashHold) / FlashFade);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.depth = depth; GUI.color = color;
    }

    void ClearSunWarning()
    {
        if (sunWarning == null) return;
        sunWarning.gameObject.SetActive(false); Destroy(sunWarning.gameObject); sunWarning = null;
    }

    public void Cancel()
    {
        bool wasActive = Active;
        step = Step.Idle;
        rain?.Dispose(); rain = null;
        arena?.Dispose(); arena = null;
        ClearSunWarning();
        sunGroundEffect?.Dispose(); sunGroundEffect = null;
        if (sun != null) { sun.SetActive(false); Destroy(sun); sun = null; }
        if (sunMaterial != null) { Destroy(sunMaterial); sunMaterial = null; }
        CameraFollow.EndOutsideMapFollow(this);
        if (cameraAnchor != null) { Destroy(cameraAnchor); cameraAnchor = null; }
        if (wasActive && body != null)
        {
            body.SetHidden(beforeHidden);
            if (!RaidStage.InTransition && body.hp > 0f) body.SetScripted(beforeScripted);
        }
        stopPose?.Invoke();
    }

    void OnDisable()
    {
        Cancel();
        if (modifier != null) CombatMods.Remove(modifier);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true; Cancel();
        if (modifier != null) CombatMods.Remove(modifier);
    }
}
