using System;
using UnityEngine;

/// <summary>
/// 카구츠치 2페이즈 패턴 실행. KagutsuchiAI가 수명과 Tick을 소유하며,
/// 이동은 속도로 반환해 Enemy.Update의 이동·맵 경계 처리 한 곳에서 적용한다.
/// </summary>
public sealed class KagutsuchiPhase2 : IDisposable
{
    const float PunchWarnTime = 1.25f;
    const float PunchDistance = 7.5f, PunchWidth = 3f;
    const float PunchDashTime = 0.1f, PunchHitTime = 0.05f, PunchRecovery = 0.25f;
    const float PunchCooldown = 5f, PunchStun = 1f;
    const float PunchDamage = 30f;          // [임시] 기획서의 공격 1 피해
    const float RunTrigger = 6f, RunCooldown = 5f;
    const float RunPrepare = 0.5f, RunDistance = 14f, RunMaxSpeed = 16f, RunStun = 1f;
    const float RunTime = 2f * RunDistance / RunMaxSpeed; // 등가속: 평균 속도는 최대 속도의 절반 → 1.75초
    const float RunGuideStep = 0.25f, RunTurnDegrees = 30f; // [임시] 기획서의 유도 주기·회전 제한
    const float RunDamage = 25f, RunRecovery = 0.3f;       // [임시] 기획서의 이동 피해·종료 경직
    const float RunFps = 10f;                            // [임시] 달리기 시트 재생 속도
    const int RunCell = 400, RunFrameCount = 8;
    const float RunContentHeight = 372f, RunBottomMargin = 11f; // 원화 알파 경계 실측
    const string RunSheet = "Kagutsuchi_P2_Run";
    const float DiveWarnTime = 1.5f, DivePrepareTime = 1f, DiveFlightTime = 0.1f;
    const float DiveRadius = 4f, DiveKickDelay = 1f;
    const float DiveHeight = 3f;                       // [임시] 사용자 지정 점프 높이
    const float LandingShakeTime = 0.25f;
    const float LandingShakeAmplitude = 0.2f;          // [임시] 착지 흔들림 세기
    const float DiveDamage = 25f, DiveCooldown = 12f; // [임시] 기획서의 착지 피해·공격 2 쿨타임
    const float KickWarnTime = 1f, KickRadius = 6f, KickAngle = 120f;
    const float KickDamage = 30f;                    // [임시] 기획서의 발차기 피해
    const float KickStun = 1f, KickKnockDistance = 2f, KickKnockTime = 0.1f;
    const string DiveReadySheet = "Kagutsuchi_P2_Atk_DiveReady", DiveSheet = "Kagutsuchi_P2_Atk_Dive";
    const string KickSheet = "Kagutsuchi_P2_Atk_Kick";
    const string GroundSmashSheet = "Kagutsuchi_P2_Atk_GroundSmash", StompSheet = "Kagutsuchi_P2_Atk_Stomp";
    const float PatternGap = 0.5f;          // [임시] 기획서의 패턴 간 공통 간격
    const float DirectionEpsilon = 0.0001f;
    const string PunchSheet = "Kagutsuchi_P2_Atk_Punch";

    enum Phase { Waiting, Warning, Dash, Hit, Recovery, RunWarning, Running, RunRecovery,
                 DiveWarning, Flying, Landing, KickWarning, KickHit, KickRecovery }
    enum Pattern { Punch, Run, DiveKick }
    readonly System.Collections.Generic.List<Pattern> ready = new System.Collections.Generic.List<Pattern>(3);
    Pattern pattern;
    Phase phase;
    readonly Enemy body;
    readonly SpriteRenderer visual;
    readonly Sprite stand;
    readonly Sprite punch, diveReady, dive, kick, groundSmash, stomp;
    GameObject airRoot;
    SpriteRenderer airVisual;
    bool airActive, sourceWasEnabled;
    readonly Sprite[] runFrames;
    BossWarning warning;
    Vector2 origin, direction, dashEnd, landingPoint;
    float elapsed, cooldownLeft, runCooldownLeft, diveCooldownLeft, gapLeft = PatternGap;
    float runElapsed, runDistance, nextGuide;
    float travel, contactDamage;
    bool hit, ownsContactDamage, disposed;

    public KagutsuchiPhase2(Enemy body)
    {
        this.body = body;
        visual = body.GetComponent<SpriteRenderer>();
        stand = visual.sprite;
        punch = CreatePose(PunchSheet);
        diveReady = CreatePose(DiveReadySheet);
        dive = CreatePose(DiveSheet);
        kick = CreatePose(KickSheet);
        groundSmash = CreatePose(GroundSmashSheet);
        stomp = CreatePose(StompSheet);
        var sheet = Resources.Load<Texture2D>("Sprites/" + RunSheet);
        if (sheet != null && stand != null && sheet.width == RunCell * RunFrameCount && sheet.height == RunCell)
        {
            // 같은 배율·같은 발 기준으로 잘라 3·7프레임의 공중 높이를 보존.
            float ppu = RunContentHeight / Mathf.Max(DirectionEpsilon, stand.bounds.size.y);
            float pivotY = (RunBottomMargin + RunContentHeight * stand.pivot.y / stand.rect.height) / RunCell;
            runFrames = new Sprite[RunFrameCount];
            for (int i = 0; i < runFrames.Length; i++)
            {
                runFrames[i] = Sprite.Create(sheet, new Rect(i * RunCell, 0f, RunCell, RunCell),
                                            new Vector2(0.5f, pivotY), ppu, 0, SpriteMeshType.FullRect);
                runFrames[i].name = RunSheet + "_" + i;
            }
        }
    }

    Sprite CreatePose(string sheetName)
    {
        var art = SpriteFactory.TryGet(sheetName);
        if (art == null || stand == null) return null;
        // 공용 캐시·원본을 변경하지 않고 키·발 기준을 맞춘 실행기 소유 스프라이트 생성.
        var pose = Sprite.Create(art.texture, art.rect, new Vector2(0.5f, stand.pivot.y / stand.rect.height),
                                 art.rect.height / Mathf.Max(DirectionEpsilon, stand.bounds.size.y), 0, SpriteMeshType.FullRect);
        pose.name = sheetName;
        return pose;
    }

    public Vector2 Tick(float dt, Vector2 toPlayer)
    {
        if (disposed || body == null || body.hp <= 0f || dt <= 0f || body.Scripted || body.Hidden
            || GameManager.I == null || GameManager.I.State != GameState.Playing) return Vector2.zero;

        if (phase == Phase.Waiting)
        {
            visual.flipX = toPlayer.x > 0f;
            // 1페이즈와 동일하게 다른 패턴이 진행 중일 때는 쿨타임이 흐르지 않음.
            cooldownLeft = Mathf.Max(0f, cooldownLeft - dt);
            runCooldownLeft = Mathf.Max(0f, runCooldownLeft - dt);
            diveCooldownLeft = Mathf.Max(0f, diveCooldownLeft - dt);
            gapLeft = Mathf.Max(0f, gapLeft - dt);
            if (gapLeft <= 0f) ChoosePattern(toPlayer);
            return Vector2.zero;
        }

        elapsed += dt;
        switch (phase)
        {
            case Phase.Warning:
                if (elapsed >= PunchWarnTime) BeginDash();
                break;
            case Phase.Dash:
                Vector2 next = Vector2.Lerp(origin, dashEnd, Mathf.Clamp01(elapsed / PunchDashTime));
                Vector2 velocity = (next - (Vector2)body.transform.position) / dt;
                if (elapsed >= PunchDashTime)
                {
                    phase = Phase.Hit;
                    elapsed = 0f;
                    TryHit();
                }
                return velocity;
            case Phase.Hit:
                if (elapsed <= PunchHitTime) TryHit();
                if (elapsed >= PunchHitTime) { phase = Phase.Recovery; elapsed = 0f; }
                break;
            case Phase.Recovery:
                if (elapsed >= PunchRecovery) CompletePattern();
                break;
            case Phase.RunWarning:
                if (elapsed >= RunPrepare) BeginRun();
                break;
            case Phase.Running:
                return TickRun(dt);
            case Phase.RunRecovery:
                if (elapsed >= RunRecovery) CompletePattern();
                break;
            case Phase.DiveWarning:
                if (elapsed >= DiveWarnTime - DivePrepareTime)
                {
                    float rise = Mathf.Clamp01((elapsed - (DiveWarnTime - DivePrepareTime)) / DivePrepareTime);
                    ShowAirPose(diveReady, DiveHeight * rise);
                }
                if (elapsed >= DiveWarnTime) BeginFlight();
                break;
            case Phase.Flying:
                Vector2 flightPos = Vector2.Lerp(origin, landingPoint, Mathf.Clamp01(elapsed / DiveFlightTime));
                Vector2 flightVelocity = (flightPos - (Vector2)body.transform.position) / dt;
                ShowAirPose(dive, DiveHeight * (1f - Mathf.Clamp01(elapsed / DiveFlightTime)));
                if (elapsed >= DiveFlightTime)
                {
                    StopAirPose();
                    visual.sprite = groundSmash != null ? groundSmash : stand;
                    CameraFollow.Shake(LandingShakeTime, LandingShakeAmplitude);
                    phase = Phase.Landing;
                    elapsed = 0f;
                    hit = false;
                    TryLandingHit();
                }
                return flightVelocity;
            case Phase.Landing:
                if (elapsed <= PunchHitTime) TryLandingHit();
                if (elapsed >= DiveKickDelay) BeginKickWarning();
                break;
            case Phase.KickWarning:
                if (elapsed >= KickWarnTime) BeginKick();
                break;
            case Phase.KickHit:
                if (elapsed <= PunchHitTime) TryKickHit();
                if (elapsed >= PunchHitTime) { phase = Phase.KickRecovery; elapsed = 0f; }
                break;
            case Phase.KickRecovery:
                if (elapsed >= PunchRecovery) CompletePattern();
                break;
        }
        return Vector2.zero;
    }

    void ChoosePattern(Vector2 toPlayer)
    {
        bool punchReady = cooldownLeft <= 0f;
        var player = Player.I;
        bool runReady = runCooldownLeft <= 0f && player != null
                     && (player.HurtCenter - (Vector2)body.transform.position).sqrMagnitude > RunTrigger * RunTrigger;
        // 조건을 만족한 준비 패턴만 같은 확률로 선택. 진행 중인 패턴은 겹치지 않음.
        ready.Clear();
        if (punchReady) ready.Add(Pattern.Punch);
        if (runReady) ready.Add(Pattern.Run);
        if (diveCooldownLeft <= 0f && player != null) ready.Add(Pattern.DiveKick);
        if (ready.Count == 0) return;
        pattern = ready[UnityEngine.Random.Range(0, ready.Count)];
        switch (pattern)
        {
            case Pattern.Run: BeginRunWarning(toPlayer); break;
            case Pattern.DiveKick: BeginDiveWarning(); break;
            default: BeginWarning(toPlayer); break;
        }
    }

    void BeginWarning(Vector2 toPlayer)
    {
        origin = body.transform.position;
        direction = toPlayer.sqrMagnitude > DirectionEpsilon ? toPlayer.normalized : Vector2.down;
        travel = BoundedTravel(origin, direction, PunchDistance, body.radius);
        dashEnd = origin + direction * travel;
        visual.flipX = direction.x > 0f;
        warning = BossWarning.Box(origin, direction, PunchDistance, PunchWidth, 0f);
        phase = Phase.Warning;
        elapsed = 0f;
        hit = false;
    }

    void BeginDash()
    {
        ClearWarning();
        if (punch != null) visual.sprite = punch;
        // 돌진 중 접촉 피해가 먼저 피격 무적을 만들어 펀치 피해를 막지 않게 공격이 피해를 소유.
        SuppressContactDamage();
        phase = Phase.Dash;
        elapsed = 0f;
    }

    void TryHit()
    {
        var player = Player.I;
        if (hit || player == null) return;
        // 사용자 확정: 돌진 끝의 점이 아니라 실제로 이동한 경로 전체가 하나의 타격.
        if (!GaniacAI.InBox(player.HurtCenter, origin, direction, travel, PunchWidth, player.radius)) return;
        hit = true;
        player.HitByBoss(PunchDamage, PunchStun, Vector2.zero, 0f);
    }

    void CompletePattern()
    {
        ClearWarning();
        RestoreContactDamage();
        visual.sprite = stand;
        phase = Phase.Waiting;
        elapsed = 0f;
        switch (pattern)
        {
            case Pattern.Run: runCooldownLeft = RunCooldown; break;
            case Pattern.DiveKick: diveCooldownLeft = DiveCooldown; break;
            default: cooldownLeft = PunchCooldown; break;
        }
        gapLeft = PatternGap;
    }

    void BeginDiveWarning()
    {
        origin = body.transform.position;
        landingPoint = Stage.ClampInside(Player.I.HurtCenter, body.radius);
        Vector2 aim = landingPoint - origin;
        direction = aim.sqrMagnitude > DirectionEpsilon ? aim.normalized : Vector2.down;
        visual.flipX = direction.x > 0f;
        warning = BossWarning.Circle(landingPoint, DiveRadius, 0f);
        phase = Phase.DiveWarning;
        elapsed = 0f;
        hit = false;
    }

    void BeginFlight()
    {
        ClearWarning();
        SuppressContactDamage();
        ShowAirPose(dive, DiveHeight);
        phase = Phase.Flying;
        elapsed = 0f;
    }

    // 높이는 표시 자식에만 적용. 조준 위치·지면 이동·타격 판정은 Enemy가 계속 소유.
    void ShowAirPose(Sprite pose, float height)
    {
        if (airRoot == null)
        {
            airRoot = new GameObject("KagutsuchiAirVisual");
            airRoot.transform.SetParent(body.transform, false);
            airVisual = airRoot.AddComponent<SpriteRenderer>();
            airVisual.sortingLayerID = visual.sortingLayerID;
            airVisual.sortingOrder = visual.sortingOrder;
            airVisual.sharedMaterial = visual.sharedMaterial;
            BossOutline.Attach(airRoot, airVisual);
        }
        if (!airActive)
        {
            sourceWasEnabled = visual.enabled;
            airActive = true;
            visual.enabled = false;
            airRoot.SetActive(true);
        }
        airVisual.sprite = pose != null ? pose : stand;
        airVisual.flipX = visual.flipX;
        airVisual.color = visual.color;
        float scaleY = Mathf.Max(DirectionEpsilon, Mathf.Abs(body.transform.lossyScale.y));
        airRoot.transform.localPosition = Vector3.up * (height / scaleY);
    }

    void StopAirPose()
    {
        if (!airActive) return;
        airActive = false;
        if (airRoot != null) airRoot.SetActive(false);
        if (visual != null) visual.enabled = sourceWasEnabled && body != null && !body.Hidden;
    }

    void TryLandingHit()
    {
        var player = Player.I;
        if (hit || player == null || player.IsInvulnerable || DebugCheats.God) return;
        float radius = DiveRadius + player.radius;
        if ((player.HurtCenter - landingPoint).sqrMagnitude > radius * radius) return;
        hit = true;
        player.TakeDamage(DiveDamage); // 착지 공격은 기절 없음
    }

    void BeginKickWarning()
    {
        origin = body.transform.position;
        Vector2 aim = Player.I != null ? Player.I.HurtCenter - origin : direction;
        direction = aim.sqrMagnitude > DirectionEpsilon ? aim.normalized : direction;
        visual.sprite = stomp != null ? stomp : stand;
        visual.flipX = direction.x > 0f;
        warning = BossWarning.Sector(origin, direction, KickRadius, KickAngle, 0f);
        phase = Phase.KickWarning;
        elapsed = 0f;
    }

    void BeginKick()
    {
        ClearWarning();
        visual.sprite = kick != null ? kick : stand;
        phase = Phase.KickHit;
        elapsed = 0f;
        hit = false;
        TryKickHit();
    }

    void TryKickHit()
    {
        var player = Player.I;
        if (hit || player == null || player.IsInvulnerable || DebugCheats.God) return;
        if (!CircleTouchesKick(player.HurtCenter - origin, player.radius)) return;
        hit = true;
        Vector2 knock = player.HurtCenter - origin;
        knock = knock.sqrMagnitude > DirectionEpsilon ? knock.normalized : direction;
        Vector2 hurtOffset = player.HurtCenter - (Vector2)player.transform.position;
        Vector2 destination = Stage.ClampInside(player.HurtCenter + knock * KickKnockDistance, player.radius) - hurtOffset;
        player.HitByBoss(KickDamage, KickStun, Vector2.zero, 0f);
        player.MoveByBoss(destination, KickKnockTime);
    }

    bool CircleTouchesKick(Vector2 delta, float radius)
    {
        if (delta.sqrMagnitude > (KickRadius + radius) * (KickRadius + radius)) return false;
        if (delta.sqrMagnitude <= radius * radius) return true;
        float angle = KickAngle * 0.5f * Mathf.Deg2Rad;
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        if (Vector2.Dot(delta.normalized, direction) >= c) return true;
        Vector2 left = new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c);
        Vector2 right = new Vector2(direction.x * c + direction.y * s, -direction.x * s + direction.y * c);
        Vector2 a = left * Mathf.Clamp(Vector2.Dot(delta, left), 0f, KickRadius);
        Vector2 b = right * Mathf.Clamp(Vector2.Dot(delta, right), 0f, KickRadius);
        return (delta - a).sqrMagnitude <= radius * radius || (delta - b).sqrMagnitude <= radius * radius;
    }

    void BeginRunWarning(Vector2 toPlayer)
    {
        pattern = Pattern.Run;
        direction = toPlayer.sqrMagnitude > DirectionEpsilon ? toPlayer.normalized : Vector2.down;
        visual.flipX = direction.x > 0f;
        warning = BossWarning.Box(body.transform.position, direction, RunDistance, body.radius * 2f, 0f);
        runDistance = runElapsed = elapsed = 0f;
        hit = false;
        phase = Phase.RunWarning;
    }

    void BeginRun()
    {
        SuppressContactDamage();
        nextGuide = RunGuideStep;
        phase = Phase.Running;
        elapsed = 0f;
        ShowRunFrame();
    }

    Vector2 TickRun(float dt)
    {
        Vector2 start = body.transform.position, pos = start;
        float remaining = dt;
        while (remaining > 0f && runElapsed < RunTime)
        {
            if (runElapsed >= nextGuide - DirectionEpsilon && nextGuide < RunTime)
            {
                GuideRun(pos);
                nextGuide += RunGuideStep;
            }
            float until = Mathf.Min(RunTime, nextGuide);
            float slice = Mathf.Min(remaining, until - runElapsed);
            if (slice <= 0f) break;
            float nextTime = Mathf.Min(RunTime, runElapsed + slice);
            // 정지 상태에서 등가속: s(t)=거리×(t/시간)^2. 끝 속도 16, 총 거리 14.
            float nextDistance = RunDistance * (nextTime / RunTime) * (nextTime / RunTime);
            float requested = Mathf.Max(0f, nextDistance - runDistance);
            float allowed = BoundedTravel(pos, direction, requested, body.radius);
            Vector2 next = pos + direction * allowed;
            TryRunHit(pos, next);
            pos = next;
            runElapsed = nextTime;
            runDistance += allowed;
            remaining -= slice;
            if (allowed + DirectionEpsilon < requested)
            {
                EndRun();
                break;
            }
        }
        ShowRunFrame();
        if (phase == Phase.Running)
        {
            UpdateRunWarning(pos);
            if (runElapsed >= RunTime) EndRun();
        }
        return (pos - start) / dt;
    }

    void GuideRun(Vector2 pos)
    {
        var player = Player.I;
        if (player == null) return;
        Vector2 target = player.HurtCenter - pos;
        if (target.sqrMagnitude <= DirectionEpsilon) return;
        float angle = Mathf.Clamp(Vector2.SignedAngle(direction, target), -RunTurnDegrees, RunTurnDegrees) * Mathf.Deg2Rad;
        float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
        direction = new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c).normalized;
    }

    void UpdateRunWarning(Vector2 pos)
    {
        if (warning == null) return;
        float length = BoundedTravel(pos, direction, Mathf.Max(0f, RunDistance - runDistance), body.radius);
        warning.transform.position = pos + direction * (length * 0.5f);
        warning.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        warning.transform.localScale = new Vector3(length, body.radius * 2f, 1f);
    }

    void ShowRunFrame()
    {
        if (runFrames != null) visual.sprite = runFrames[(int)(runElapsed * RunFps) % runFrames.Length];
        visual.flipX = direction.x > 0f;
    }

    void TryRunHit(Vector2 from, Vector2 to)
    {
        var player = Player.I;
        if (hit || player == null || player.IsInvulnerable || DebugCheats.God) return;
        Vector2 segment = to - from;
        float lengthSq = segment.sqrMagnitude;
        float k = lengthSq > DirectionEpsilon ? Mathf.Clamp01(Vector2.Dot(player.HurtCenter - from, segment) / lengthSq) : 0f;
        float radius = body.radius + player.radius;
        if ((player.HurtCenter - (from + segment * k)).sqrMagnitude > radius * radius) return;
        hit = true;
        player.HitByBoss(RunDamage, RunStun, Vector2.zero, 0f);
    }

    void EndRun()
    {
        ClearWarning();
        phase = Phase.RunRecovery;
        elapsed = 0f;
    }

    void SuppressContactDamage()
    {
        if (ownsContactDamage) return;
        contactDamage = body.damage;
        body.damage = 0f;
        ownsContactDamage = true;
    }

    // 방향을 바꾸지 않고 피격 판정 원이 맵 안에 남는 최대 직선 이동 거리.
    static float BoundedTravel(Vector2 from, Vector2 dir, float distance, float radius)
    {
        if (!Stage.Bounded) return distance;
        Vector2 min = Stage.Center - Stage.HalfSize + Vector2.one * radius;
        Vector2 max = Stage.Center + Stage.HalfSize - Vector2.one * radius;
        if (dir.x > DirectionEpsilon) distance = Mathf.Min(distance, (max.x - from.x) / dir.x);
        else if (dir.x < -DirectionEpsilon) distance = Mathf.Min(distance, (min.x - from.x) / dir.x);
        if (dir.y > DirectionEpsilon) distance = Mathf.Min(distance, (max.y - from.y) / dir.y);
        else if (dir.y < -DirectionEpsilon) distance = Mathf.Min(distance, (min.y - from.y) / dir.y);
        return Mathf.Max(0f, distance);
    }

    void ClearWarning()
    {
        if (warning == null) return;
        warning.gameObject.SetActive(false);
        UnityEngine.Object.Destroy(warning.gameObject);
        warning = null;
    }

    void RestoreContactDamage()
    {
        if (!ownsContactDamage) return;
        if (body != null) body.damage = contactDamage;
        ownsContactDamage = false;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        StopAirPose();
        if (airRoot != null) UnityEngine.Object.Destroy(airRoot);
        ClearWarning();
        RestoreContactDamage();
        if (visual != null) visual.sprite = stand;
        if (punch != null) UnityEngine.Object.Destroy(punch);
        if (diveReady != null) UnityEngine.Object.Destroy(diveReady);
        if (dive != null) UnityEngine.Object.Destroy(dive);
        if (kick != null) UnityEngine.Object.Destroy(kick);
        if (groundSmash != null) UnityEngine.Object.Destroy(groundSmash);
        if (stomp != null) UnityEngine.Object.Destroy(stomp);
        if (runFrames != null)
            foreach (var frame in runFrames) if (frame != null) UnityEngine.Object.Destroy(frame);
    }
}
