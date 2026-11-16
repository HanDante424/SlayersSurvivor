using System;
using UnityEngine;

/// <summary>
/// 공격 7: 잡힌 플레이어 한 명에게 연속 공격. Player의 소유자별 홀드와 카메라 예외를 재사용한다.
/// 보스 지면 이동·순간이동은 Tick 반환값으로만 적용해 Enemy.Update의 좌표 덮어쓰기를 피한다.
/// </summary>
public sealed class KagutsuchiGrabComboPattern : MonoBehaviour
{
    public const float TriggerRadius = 10f, Cooldown = 20f;
    const float WarningTime = 1f, WarningWidth = 3f, DashDistance = 12f, DashTime = 0.25f;
    const float HoldTime = 1f, PunchDistance = 8f, PunchTime = 1f;
    const float KickDistance = 6f, KickTime = 0.25f;
    const float UppercutDistance = 6f, AirHeight = 6f, UppercutTime = 0.25f;
    const float DiveDistance = 6f, DiveTime = 0.5f, GroundSmashTime = 1f;
    const float StompTime = 0.25f, StompReadyTime = 0.25f;
    const float LiftTime = 0.1f; // 사용자: 눕힌 후 중앙 전방으로 끌어올림 이동 시간.
    const float FinalKickDistance = 10f /* 사용자 2026-11-16: 5 → +5 */, FinalMoveTime = 0.2f, FinalKickPoseTime = 2f, FinalStun = 1f;
    const float DamagePerHit = 5f, CameraMagnification = 1.5f;
    const float ReadyFrontDistance = 0.5f;
    const float OppositeDistance = 0.5f; // [임시] 사용자 지정 반대편 순간이동 거리
    const float ShakeTime = 0.5f, ShakeAmplitude = 0.2f; // [임시] 기존 공격 흔들림 세기 재사용
    // 원화 실측 접촉점 (좌상단 px / 원화 크기). 수치·판정이 아닌 포즈 정렬 기준.
    static readonly Vector2 GrabFingertip = new Vector2(60f / 1691f, 455f / 930f);
    static readonly Vector2 DiveFingertip = new Vector2(25f / 1132f, 700f / 1205f);
    static readonly Vector2 SmashFistBottom = new Vector2(410f / 1218f, 1148f / 1153f);
    const float Epsilon = 0.0001f;
    const float SlamDustScale = 3f;   // 내려찍기 먼지 효과 배율 (피해 범위와 무관)

    // ---- 체력 50% 이하 강화 (사용자 2026-11-16) [해석 많음 — 보고서 참조] ----
    // 마지막 발차기 (밀어냄) 이후, 플레이어가 도착하기 0.1초 전에 보스가 2 거리를 파고들며 공격 → 밀어냄 → 반복.
    const float ChaseLunge = 0.1f, ChaseDistance = 2f, ChaseKnockTime = 0.25f, ChaseHoldTime = 1f;
    const float ChaseFinalKnock = 8f, ChaseFinalStun = 3f;
    const float FirstHoldShift = 1f;   // 첫 홀딩 때 플레이어 전진 거리
    const float ChaseHoldShift = 2f,ChaseFollowGap = 2.5f /* 사용자: 1 → +1 → +1 → -0.5 */, ChaseFollowInterval = 0.15f /* 사용자: 후속 3연타 간격 0.1 → 0.25 → 0.15 */;  // 홀딩 때 플레이어 전진 거리 / 후속 3연타 보스 · 플레이어 간격
    enum ChasePose { Punch, Kick, StompReady, GrabDash }
    readonly struct ChaseStage
    {
        public readonly ChasePose Pose; public readonly float Knock; public readonly bool Hold;
        public ChaseStage(ChasePose pose, float knock, bool hold = false) { Pose = pose; Knock = knock; Hold = hold; }
    }
    // 0~2: 밀어냄 (펀치 7 · 킥 5 · StompReady 4), 3: GrabDash 로 1초 홀딩, 4~6: 펀치 · StompReady = 넉백 없음, 마지막 킥 = 8 넉백 + 3초 기절. 보스 위치는 매 단계 좌우 교대.
    static readonly ChaseStage[] ChaseTable =
    {
        new ChaseStage(ChasePose.Punch, 7f), new ChaseStage(ChasePose.Kick, 5f), new ChaseStage(ChasePose.StompReady, 4f),
        new ChaseStage(ChasePose.GrabDash, 0f, true),
        new ChaseStage(ChasePose.Punch, 0f), new ChaseStage(ChasePose.StompReady, 0f), new ChaseStage(ChasePose.Kick, ChaseFinalKnock),
    };
    public Func<bool> IsEnhanced;
    /// <summary>지금 표시 중인 포즈 pivot 의 보스 지면 위 높이 (월드). GrabDash 홀딩 정렬 결과를 읽어 후속 공격 높이로 씀</summary>
    public Func<float> PoseWorldHeight;
    float chasePoseHeight;
    bool enhancedRun;
    int chaseIndex;
    float knockClock, knockDuration = 1f, lungeClock, finalStun = FinalStun;
    Vector2 lungeStart, lungeEnd;

    enum Step { Finished, Warning, Dash, Hold, Punch, PunchArrived, Kick, KickArrived, Uppercut, UppercutArrived, Dive, GroundSmash, Stomp, StompReady, FinalKick,
                ChaseWait, ChaseLunge, ChaseHold }
    Step step;
    Enemy body;
    SpriteRenderer bodyVisual;
    Player player;
    Sprite grabReady, grabDash, punch, kick, uppercut, diveReady, groundSmash, stomp, stompReady;
    Action<Sprite, float> showPose;
    Action stopPose;
    Action<Sprite, Sprite, float, Vector2> showContactPose;
    Sprite currentPose;
    float poseHeight;
    BossWarning warning;
    Vector2 dashOrigin, dashDirection, dashEnd, bossGround, moveStart, moveEnd;
    float elapsed, attackSign;
    public bool Finished => step == Step.Finished;

    public void Initialize(Enemy owner, Sprite grabReady, Sprite grabDash, Sprite punch, Sprite kick,
                           Sprite uppercut, Sprite diveReady, Sprite groundSmash, Sprite stomp, Sprite stompReady,
                           Action<Sprite, float> showPose, Action stopPose, Action<Sprite, Sprite, float, Vector2> showContactPose)
    {
        body = owner;
        bodyVisual = owner.GetComponent<SpriteRenderer>();
        this.grabReady = grabReady; this.grabDash = grabDash;
        this.punch = punch; this.kick = kick; this.uppercut = uppercut; this.diveReady = diveReady;
        this.groundSmash = groundSmash; this.stomp = stomp; this.stompReady = stompReady;
        this.showPose = showPose; this.stopPose = stopPose;
        this.showContactPose = showContactPose;
    }

    public void Begin()
    {
        Cancel();
        player = Player.I;
        if (player == null) return;
        enhancedRun = IsEnhanced != null && IsEnhanced();
        finalStun = FinalStun; chaseIndex = 0; chasePoseHeight = 0f;
        bossGround = dashOrigin = body.transform.position;
        Vector2 aim = player.HurtCenter - dashOrigin;
        dashDirection = aim.sqrMagnitude > Epsilon ? aim.normalized : Vector2.down;
        float travel = KagutsuchiPhase2.BoundedTravel(dashOrigin, dashDirection, DashDistance, body.radius);
        dashEnd = dashOrigin + dashDirection * travel;
        attackSign = dashDirection.x < 0f ? -1f : 1f;
        bodyVisual.flipX = dashDirection.x > 0f;
        showPose(grabReady, 0f);
        warning = BossWarning.Box(dashOrigin, dashDirection, DashDistance, WarningWidth, 0f);
        step = Step.Warning; elapsed = 0f;
    }

    void Update()
    {
        if (Finished || GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) Cancel();
    }

    void LateUpdate()
    {
        if (Finished || player == null || !player.IsHeldByBoss(this)) return;
        player.SetBossHoldFacing(this, !bodyVisual.flipX);
        // Enemy.Update의 지면 이동이 적용된 뒤 정렬해야 돌진·순간이동 프레임에도 일치한다.
        // 첫 홀딩 (Hold) 도 접촉점 정렬을 하지 않음 (사용자 2026-11-16): 정렬하면 첫 적중 때 보스가 위로 올라가 후속 펀치와 높이가 어긋남.
        // GrabDash 도 pivot (발바닥) 높이 그대로 → 후속 펀치와 같은 높이.
        if (step == Step.Dive) showContactPose(currentPose, diveReady, poseHeight, DiveFingertip);
        else if (step == Step.GroundSmash || step == Step.Stomp)
            showContactPose(currentPose, groundSmash, poseHeight, SmashFistBottom);
        // 강화 추격의 홀딩 (ChaseHold) 은 접촉점 정렬을 하지 않음: 정렬하면 보스가 플레이어 머리를 따라 위로 올라가 버림 (사용자 2026-11-16).
        // 보스는 pivot (발바닥) 높이 그대로 두고 후속 공격도 같은 높이를 씀.
    }

    public Vector2 Tick(float dt)
    {
        if (Finished || dt <= 0f) return Vector2.zero;
        if (body == null || body.hp <= 0f || player == null || player.hp <= 0f)
        { Cancel(); return Vector2.zero; }
        if (step != Step.Warning && step != Step.Dash
            && (!player.IsHeldByBoss(this) || player.IsInvulnerable || DebugCheats.God))
        { Cancel(); return Vector2.zero; }
        Vector2 before = body.transform.position;
        elapsed += dt;
        switch (step)
        {
            case Step.Warning:
                if (elapsed >= WarningTime)
                { ClearWarning(); step = Step.Dash; elapsed = 0f; showPose(grabDash, 0f); }
                break;
            case Step.Dash:
                Vector2 next = Vector2.Lerp(dashOrigin, dashEnd, Mathf.Clamp01(elapsed / DashTime));
                if (TryCatch(before, next, out Vector2 contact)) bossGround = contact;
                else
                {
                    bossGround = next;
                    if (elapsed >= DashTime) Cancel();
                }
                break;
            case Step.Hold:
                MoveHeld(Mathf.Clamp01(elapsed / ChaseLunge), 0f);   // 두 번째 GrabDash 홀딩과 같은 0.1초 이동
                if (elapsed >= HoldTime) BeginPunch();
                break;
            case Step.Punch:
                float t = Mathf.Clamp01(elapsed / PunchTime);
                MoveHeld(1f - (1f - t) * (1f - t), 0f);
                if (elapsed >= PunchTime && AtMoveEnd()) step = Step.PunchArrived;
                break;
            case Step.Kick:
                MoveHeld(Mathf.Clamp01(elapsed / KickTime), 0f);
                if (elapsed >= KickTime && AtMoveEnd()) step = Step.KickArrived;
                break;
            case Step.Uppercut:
                float rise = Mathf.Clamp01(elapsed / UppercutTime);
                MoveHeld(rise, AirHeight * rise);
                if (elapsed >= UppercutTime && AtMoveEnd()) step = Step.UppercutArrived;
                break;
            // 도착 프레임은 앞 공격의 끝 자세로 표시하고, 다음 Tick에서 후속 공격을 시작한다.
            case Step.PunchArrived:
                MoveHeld(1f, 0f);
                if (AtMoveEnd()) BeginKick();
                break;
            case Step.KickArrived:
                MoveHeld(1f, 0f);
                if (AtMoveEnd()) BeginUppercut();
                break;
            case Step.UppercutArrived:
                MoveHeld(1f, AirHeight);
                if (AtMoveEnd()) BeginDive();
                break;
            case Step.Dive:
                float progress = Mathf.Clamp01(elapsed / DiveTime);
                MoveHeld(progress, AirHeight * (1f - progress));
                bossGround = BesidePlayer();
                Show(diveReady, AirHeight * (1f - progress));
                if (elapsed >= DiveTime)
                {
                    step = Step.GroundSmash; elapsed = 0f;
                    Show(groundSmash, 0f);
                    body.GetComponent<KagutsuchiPhysicalStompFx>()?.Play(bossGround, body.radius * SlamDustScale);   // 사용자 (2026-11-16): 내려찍기 먼지 효과 3배 (표시 크기만)
                    CameraFollow.Shake(ShakeTime, ShakeAmplitude);
                    Hit();
                }
                break;
            case Step.GroundSmash:
                if (elapsed >= GroundSmashTime)
                { step = Step.Stomp; elapsed = 0f; Show(stomp, 0f); }
                break;
            case Step.Stomp:
                if (elapsed >= StompTime)
                {
                    step = Step.StompReady; elapsed = 0f;
                    player.SetBossHoldProne(this, false);
                    moveStart = player.transform.position;
                    Vector2 hurtOffset = player.HurtCenter - moveStart;
                    moveEnd = Stage.ClampInside(bossGround + new Vector2(attackSign * ReadyFrontDistance, 0f), player.radius) - hurtOffset;
                    Show(stompReady, 0f);
                }
                break;
            case Step.StompReady:
                // 지면 이동과 주먹 아래 표시 보정을 함께0.1초에 완료. 준비 자세 시간은 유지.
                float liftProgress = Mathf.Clamp01(elapsed / LiftTime);
                MoveHeld(liftProgress, 0f);
                player.SetBossHoldOffsetWeight(this, 1f - liftProgress);
                if (elapsed >= StompReadyTime && AtMoveEnd())
                {
                    step = enhancedRun ? Step.ChaseWait : Step.FinalKick; elapsed = 0f;
                    SetMove(FinalKickDistance);
                    Show(kick, 0f);
                    body.GetComponent<KagutsuchiSonicBoomFx>()?.Play(bossGround, bodyVisual.flipX);
                    Hit();
                    if (enhancedRun) { chaseIndex = 0; knockClock = 0f; knockDuration = FinalMoveTime; }   // 강화: 이 밀어냄이 끝나기 0.1초 전부터 추격
                }
                break;
            case Step.ChaseWait:
                knockClock += dt;
                MoveHeld(Mathf.Clamp01(knockClock / knockDuration), 0f);
                if (chaseIndex >= ChaseTable.Length) { Cancel(); break; }
                if (chaseIndex >= 4 || knockClock >= knockDuration - ChaseLunge) BeginChaseLunge();   // 반대 방향 단계는 바로
                break;
            case Step.ChaseLunge:
                knockClock += dt; lungeClock += dt;
                MoveHeld(Mathf.Clamp01(knockClock / knockDuration), 0f);
                float lungeTime = chaseIndex >= 4 ? ChaseFollowInterval : ChaseLunge;
                bossGround = Vector2.Lerp(lungeStart, lungeEnd, Mathf.Clamp01(lungeClock / lungeTime));
                if (lungeClock >= lungeTime) ResolveChase();
                break;
            case Step.ChaseHold:
                MoveHeld(Mathf.Clamp01(elapsed / ChaseLunge), 0f);
                if (elapsed >= ChaseHoldTime)
                {
                    // 사용자: GrabDash 이후 공격은 GrabDash (홀딩 정렬된) pivot 높이에 맞춤
                    chasePoseHeight = PoseWorldHeight != null ? PoseWorldHeight() : 0f;
                    chaseIndex++; step = Step.ChaseWait; elapsed = 0f; knockClock = knockDuration;
                }
                break;
            case Step.FinalKick:
                float launchProgress = Mathf.Clamp01(elapsed / FinalMoveTime);
                MoveHeld(launchProgress, 0f);
                // 준비 단계에서 높이 보정을 끝냈으므로 이후 넉백은 수평으로만 이동.
                if (elapsed >= FinalKickPoseTime)
                {
                    // 최종 자세가 끝날 때까지 홀드 기절, 종료 뒤 일반 기절 적용.
                    player.EndBossHold(this);
                    player.StunByBoss(finalStun);
                    Cancel();
                }
                break;
        }
        if (player.hp <= 0f) Cancel();
        return (bossGround - before) / dt;
    }

    bool TryCatch(Vector2 from, Vector2 to, out Vector2 contact)
    {
        Vector2 segment = to - from;
        float fraction = segment.sqrMagnitude > Epsilon
            ? Mathf.Clamp01(Vector2.Dot(player.HurtCenter - from, segment) / segment.sqrMagnitude) : 0f;
        contact = from + segment * fraction;
        float radius = body.radius + player.radius;
        if ((player.HurtCenter - contact).sqrMagnitude > radius * radius || !player.TryBeginBossHold(this)) return false;
        step = Step.Hold; elapsed = 0f;
        float horizontal = player.HurtCenter.x - contact.x;
        if (Mathf.Abs(horizontal) > Epsilon) attackSign = horizontal < 0f ? -1f : 1f;
        Show(grabDash, 0f);
        SetMove(FirstHoldShift);   // 사용자 (2026-11-16): 첫 홀딩은 플레이어를 보스 진행 방향으로 1 이동 (두 번째 GrabDash 는 ChaseHoldShift = 2)
        player.SetBossHoldFacing(this, !bodyVisual.flipX);
        CameraFollow.BeginOutsideMapFollow(this, player.BossHoldVisual, CameraMagnification);
        return true;
    }

    void BeginPunch()
    {
        // 홀드가 끝나고 첫 펀치를 시작할 때 플레이어 기준 좌우 여유를 비교한다.
        float leftRoom = Stage.ExitDistance(player.HurtCenter, new Vector2(-1f, 0f));
        float rightRoom = Stage.ExitDistance(player.HurtCenter, new Vector2(1f, 0f));
        float beforeSign = attackSign;
        if (leftRoom > rightRoom) attackSign = -1f;
        else if (rightRoom > leftRoom) attackSign = 1f;
        // 동률이면 기존 조준 방향 유지. 방향이 바뀌면 밀어낼 수 있는 반대편에 선다.
        if (attackSign != beforeSign) bossGround = BesidePlayer();
        step = Step.Punch; elapsed = 0f;
        SetMove(PunchDistance);
        Show(punch, 0f);
        body.GetComponent<KagutsuchiSonicBoomFx>()?.Play(bossGround, bodyVisual.flipX);
        Hit();
    }

    void BeginKick()
    {
        step = Step.Kick; elapsed = 0f;
        attackSign = -attackSign;
        bossGround = BesidePlayer();
        SetMove(KickDistance);
        Show(kick, 0f);
        body.GetComponent<KagutsuchiSonicBoomFx>()?.Play(bossGround, bodyVisual.flipX);
        Hit();
    }

    void BeginUppercut()
    {
        step = Step.Uppercut; elapsed = 0f;
        attackSign = -attackSign;
        bossGround = BesidePlayer();
        SetMove(UppercutDistance);
        Show(uppercut, 0f);
        body.GetComponent<KagutsuchiUppercutFx>()?.Play(bossGround, bodyVisual.flipX);
        Hit();
    }

    void BeginDive()
    {
        step = Step.Dive; elapsed = 0f;
        attackSign = -attackSign;
        bossGround = BesidePlayer();
        SetMove(DiveDistance);
        Show(diveReady, AirHeight);
    }

    Sprite ChaseSprite(ChasePose pose)
    {
        switch (pose)
        {
            case ChasePose.Punch: return punch;
            case ChasePose.Kick: return kick;
            case ChasePose.StompReady: return stompReady;
            default: return grabDash;
        }
    }

    /// <summary>플레이어가 도착할 지점 (밀려나는 중이면 그 끝) 앞에 2 뒤에서 파고들며 공격 자세. 4번째 단계부터는 반대 방향.</summary>
    void BeginChaseLunge()
    {
        var stage = ChaseTable[chaseIndex];
        // 사용자: 단계마다 보스가 서는 쪽이 번갈아 바뀜 (발차기 왼쪽 → 펀치 오른쪽 → 킥 왼쪽 → StompReady 오른쪽 → 잡기 왼쪽 → 펀치 오른쪽 → StompReady 왼쪽 → 킥 오른쪽).
        // attackSign = 밀어내는 방향이므로 단계마다 뒤집으면 보스는 직전 밀려난 쪽 (플레이어가 향한 쪽) 반대편에 선다.
        attackSign = -attackSign;
        Vector2 arrival = moveEnd;
        // 사용자: 홀딩 이후 후속 3연타 (4~6번째) 는 이동하면서 공격하지 않고 플레이어에게서 거리 1 떨어진 자리에 생성만
        float gap = chaseIndex >= 4 ? ChaseFollowGap : OppositeDistance;
        float runIn = chaseIndex >= 4 ? 0f : ChaseDistance;
        lungeEnd = Stage.ClampInside(arrival - new Vector2(attackSign * gap, 0f), body.radius);
        lungeStart = Stage.ClampInside(arrival - new Vector2(attackSign * (gap + runIn), 0f), body.radius);
        bossGround = lungeStart;
        lungeClock = 0f;
        step = Step.ChaseLunge;
        Show(ChaseSprite(stage.Pose), chaseIndex >= 4 ? chasePoseHeight : 0f);
    }

    /// <summary>플레이어 도착 순간: 피해 · 밀어냄 · 다음 단계</summary>
    void ResolveChase()
    {
        var stage = ChaseTable[chaseIndex];
        bossGround = lungeEnd;
        Hit();
        // 펀치 · 킥과 함께, 후속 3연타의 2번째 (StompReady) 에도 풍압 이펙트 (사용자 2026-11-16)
        if (stage.Pose == ChasePose.Punch || stage.Pose == ChasePose.Kick || (stage.Pose == ChasePose.StompReady && chaseIndex == 5))
            body.GetComponent<KagutsuchiSonicBoomFx>()?.Play(bossGround, bodyVisual.flipX);
        if (chaseIndex == ChaseTable.Length - 1)
        {
            // 마지막: 8 넉백 · 2초 자세 유지 · 3초 기절 (기존 마지막 발차기 단계 재사용)
            finalStun = ChaseFinalStun;
            step = Step.FinalKick; elapsed = 0f;
            SetMove(ChaseFinalKnock);
            Show(kick, chasePoseHeight);
            return;
        }
        if (stage.Hold)
        {
            SetMove(ChaseHoldShift);   // 사용자: 홀딩 시 플레이어를 보스 진행 방향으로 2 이동
            step = Step.ChaseHold; elapsed = 0f;
            Show(grabDash, 0f);
            return;
        }
        if (stage.Knock > 0f) { SetMove(stage.Knock); knockClock = 0f; knockDuration = ChaseKnockTime; }
        chaseIndex++;
        step = Step.ChaseWait;
    }

    Vector2 BesidePlayer()
    {
        Vector2 ground = player.transform.position;
        return Stage.ClampInside(ground - new Vector2(attackSign * OppositeDistance, 0f), body.radius);
    }

    void SetMove(float distance)
    {
        moveStart = player.transform.position;
        Vector2 offset = player.HurtCenter - moveStart;
        moveEnd = Stage.ClampInside(player.HurtCenter + new Vector2(attackSign * distance, 0f), player.radius) - offset;
    }

    bool AtMoveEnd()
    {
        return ((Vector2)player.transform.position - moveEnd).sqrMagnitude <= Epsilon * Epsilon;
    }

    void MoveHeld(float progress, float height)
    {
        player.SetBossHoldPosition(this, Vector2.Lerp(moveStart, moveEnd, progress));
        player.SetBossHoldHeight(this, height);
    }

    void Show(Sprite pose, float height)
    {
        bodyVisual.flipX = attackSign > 0f;
        currentPose = pose;
        poseHeight = height;
        showPose(pose, height);
    }

    void Hit()
    {
        // 사용자 확정: 빠른 연타의 피격 직후 무적만 무시. 스킬·치트 무적은 Player가 차단.
        player.TakeDamage(DamagePerHit, ignoreHitInvuln: true);
    }

    void ClearWarning()
    {
        if (warning == null) return;
        warning.gameObject.SetActive(false);
        Destroy(warning.gameObject);
        warning = null;
    }

    public void Cancel()
    {
        if (player != null) player.EndBossHold(this);
        CameraFollow.EndOutsideMapFollow(this);
        ClearWarning();
        stopPose?.Invoke();
        step = Step.Finished;
        elapsed = 0f;
    }

    void OnDisable() => Cancel();
}
