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
    const float FinalKickDistance = 5f, FinalMoveTime = 0.2f, FinalKickPoseTime = 2f, FinalStun = 1f;
    const float DamagePerHit = 5f, CameraMagnification = 1.5f;
    const float ReadyFrontDistance = 0.5f;
    const float OppositeDistance = 0.5f; // [임시] 사용자 지정 반대편 순간이동 거리
    const float ShakeTime = 0.5f, ShakeAmplitude = 0.2f; // [임시] 기존 공격 흔들림 세기 재사용
    // 원화 실측 접촉점 (좌상단 px / 원화 크기). 수치·판정이 아닌 포즈 정렬 기준.
    static readonly Vector2 GrabFingertip = new Vector2(60f / 1691f, 455f / 930f);
    static readonly Vector2 DiveFingertip = new Vector2(25f / 1132f, 700f / 1205f);
    static readonly Vector2 SmashFistBottom = new Vector2(410f / 1218f, 1148f / 1153f);
    const float Epsilon = 0.0001f;

    enum Step { Finished, Warning, Dash, Hold, Punch, PunchArrived, Kick, KickArrived, Uppercut, UppercutArrived, Dive, GroundSmash, Stomp, StompReady, FinalKick }
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
        // Enemy.Update의 지면 이동이 적용된 뒤 정렬해야 돌진·순간이동 프레임에도 일치한다.
        if (step == Step.Hold) showContactPose(currentPose, grabDash, poseHeight, GrabFingertip);
        else if (step == Step.Dive) showContactPose(currentPose, diveReady, poseHeight, DiveFingertip);
        else if (step == Step.GroundSmash || step == Step.Stomp)
            showContactPose(currentPose, groundSmash, poseHeight, SmashFistBottom);
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
                    moveStart = player.transform.position;
                    Vector2 hurtOffset = player.HurtCenter - moveStart;
                    moveEnd = Stage.ClampInside(bossGround + new Vector2(attackSign * ReadyFrontDistance, 0f), player.radius) - hurtOffset;
                    Show(stompReady, 0f);
                }
                break;
            case Step.StompReady:
                // 기존 준비 시간 안에 주먹 아래에서 중앙 전방으로 끌어올린다.
                float liftProgress = Mathf.Clamp01(elapsed / StompReadyTime);
                MoveHeld(liftProgress, 0f);
                player.SetBossHoldOffsetWeight(this, 1f - liftProgress);
                if (elapsed >= StompReadyTime && AtMoveEnd())
                {
                    step = Step.FinalKick; elapsed = 0f;
                    SetMove(FinalKickDistance);
                    Show(kick, 0f);
                    Hit();
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
                    player.StunByBoss(FinalStun);
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
        Hit();
    }

    void BeginKick()
    {
        step = Step.Kick; elapsed = 0f;
        attackSign = -attackSign;
        bossGround = BesidePlayer();
        SetMove(KickDistance);
        Show(kick, 0f);
        Hit();
    }

    void BeginUppercut()
    {
        step = Step.Uppercut; elapsed = 0f;
        attackSign = -attackSign;
        bossGround = BesidePlayer();
        SetMove(UppercutDistance);
        Show(uppercut, 0f);
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
