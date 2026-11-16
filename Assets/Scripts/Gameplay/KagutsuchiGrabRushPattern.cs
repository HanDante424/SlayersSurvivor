using System;
using UnityEngine;

/// <summary>
/// 공격 11 · 잡기 → 연속 추격 → 공중 넉백 (사용자 2026-11-16, 스펙 [ACTIVE UPDATE] 공격 11).
/// 1초 준비 (거리 15 × 폭 3 워닝) → 달리기 0.25초 돌진 → 적중 시 1초 홀딩 → 발차기로 2초 동안 8 넉백 (남은 공간이 큰 쪽)
/// → 0.15초 간격 6타 연속 추격 (0.9초) → 0.2초 간격 교차 4타 (0.8초: 반대편 StompReady · 원래 방향 Kick · 반대편 Uppercut · 원래 방향 Punch) → 남은 0.3초 동안 진행 반대편 (플레이어 앞쪽) 에 GrabDash 로 이동 없이 나타나 1초 홀딩
/// → 펀치 (0.25초 3 넉백) → 추격해 GrabReady 0.25초 홀딩 → 어퍼컷 1.5초 (5 상승, 보스도 StompReady 로 도착 0.75초 전부터 상승, 0.25초 전 최고 높이)
/// → 발차기로 맵 끝까지 넉백 (플레이어 속도 10배) → 1.5초 원래 높이로 낙하 → 기절 1초.
/// 보스 지면 이동은 Tick 반환값 (속도) 으로만 적용 (Enemy.Update 좌표 덮어쓰기 회피, 공격 7 과 같은 방식).
/// [임시] 모든 피해 5 (사용자) · 쿨타임 20초 (사용자) · 돌진이 빗나가면 1초 경직 뒤 종료 · 보스 상승 높이 5 (플레이어와 같게) ·
/// 보스도 낙하 때 1.5초에 지면으로 내려옴 · 추격 6타 간격 위치 (플레이어 뒤 2) · 강화 없음 (원본 구현 뒤 추가).
/// [해석] 발차기는 넉백을 시작하는 순간 (0초), 6타는 0.15 · 0.30 · … · 0.90초, 교차 4타는 1.1 · 1.3 · 1.5 · 1.7초의 타격 순간. 마지막 타격 자세는 접근 (1.7초) 시작과 함께 GrabDash 로 바뀜.
/// </summary>
public sealed class KagutsuchiGrabRushPattern : MonoBehaviour
{
    public const float Cooldown = 20f;                                           // 사용자
    const float WarningTime = 1f, WarningWidth = 3f, DashDistance = 15f, DashTime = 0.25f, MissRecover = 1f;
    const float HoldTime = 1f, LaunchGap = 2f, LaunchDistance = 8f, KnockMoveTime = 2f /* 사용자: 넉백 1.2 → 2초 */, KnockTime = 2.2f /* + 마지막 Punch 0.2초 표시 (사용자 2026-11-16) */;
    const float ChaseGap = 2f, CrossGap = 3f /* 사용자 2026-11-16: 방향을 바꾸는 교차 4타는 간격 +1 */, CrossStart = 6, ChaseTotal = 1.9f, HoldShift = 2.5f /* 사용자 2026-11-16: 기본 사항 3 → 0.5 감소 */, HoldShiftTime = 0.1f, EnhShiftTime = 0.05f, EnhDown = 0.5f, EnhExtraShift = 0.5f;   // 마지막 Punch 를 1.7~1.9초 표시한 뒤 GrabDash (사용자), GrabDash 홀딩 때 플레이어를 보스가 보는 방향으로 3                              // 추격 = 6타 0.9초 + 교차 4타 0.8초 (사용자), 이후 0.3초 접근
    const float ApproachDistance = 0f /* 사용자 2026-11-16: GrabDash 는 이동 없이 (4 → 0), 플레이어 앞쪽에 그대로 나타남 */, ApproachTime = 0.3f, RegrabHold = 1f;    // 반대편에서 4 들어와 1초 홀딩
    const float PunchDistance = 3f, PunchTime = 0.25f, RegrabTime = 0.25f;
    const float LiftTime = 1.5f, LiftHeight = 5f, BossRiseStart = 0.75f, BossPeakBefore = 0.25f;
    const float FinalSpeedMul = 10f, FallTime = 1.5f, FinalStun = 1f;
    const float DamagePerHit = 5f, CameraMagnification = 1.5f, OppositeDistance = 0.5f, Epsilon = 0.0001f;
    const float KickWindScale = 0.4f;   // 사용자 2026-11-16: 마지막 발차기 풍압 크기 60% 감소
    const float ShakeTime = 0.25f, ShakeAmplitude = 0.2f;

    // ---- 체력 50% 이하 강화 (사용자 2026-11-16) ----
    const float EnhReadyHold = 0.2f;                                              // [임시] 단계 6: GrabReady 홀딩 (플레이어 이동 보류) 시간 → 이후 GrabDash + 이동
    const float LaserTime = 1f, LaserInterval = 0.05f, LaserFan = 30f, LaserStartBack = 0.5f /* 사용자 2026-11-16: 0.3 → 0.5 */, LaserWidth = 1f;
    const int LaserPerVolley = 3, LaserHits = 5;                                  // 1회 3개 · 피해 5 는 총 5회 (레이저는 연출만, 발사 동안 일정 간격 = 1초 / 5)
    const float LaserLife = 0.3f, LaserShake = 0.12f;                             // [임시] 레이저 1개 지속 · 발사 동안 화면 흔들림 세기
    const float FireGrowTime = 1f, FireStartRadius = 0.5f, FireRadius = 5f, FireSpeedMul = 1f /* 사용자 2026-11-16: 플레이어 속도의 0.5배 → 2배 증가 = 1배 */;   // 반지름 5 (사용자), 시작 반지름 [임시]
    const float JumpDelay = 0.5f, JumpHeight = 5f, JumpTime = 0.4f;               // 화염구 이동 0.5초 뒤 상승, 상승 시간 [임시]
    const float DiveReadyTime = 0.5f, DiveTime = 0.25f, FlashHold = 1f, FlashFade = 2f, LaughTime = 3f;
    const float StompAttackTime = 0.3f, GrabReadyTime = 0.3f, StompReadyTime = 0.3f /* 사용자 2026-11-16: 0.2 → 0.3초 */, FootGap = 1.75f /* 사용자 2026-11-16: 눕는 위치를 보스가 보는 방향으로 0.25 + 0.5 이동 (1 → 1.75) */, LieHeadHeight = 0.3f, PullShift = 2f;   // 발아래 누운 위치 간격 [임시]
    const float EndKnock = 10f, EndKnockTime = 0.25f, EndStun = 1f;               // 마지막 Kick 넉백 거리 10 · 0.25초 · 뒤 기절 [임시]
    const float LandShake = 0.4f, LandShakeAmp = 0.3f;
    const float StompAttackScale = 0.9f, DiveScale = 0.9f /* 사용자 2026-11-16: Dive 도 10% 감소 */;   // 사용자 2026-11-16: Laugh 뒤 StompAttack 스프라이트 크기 10% 감소

    // 6타 자세 순서 (사용자): punch, uppercut, stompready, punch, kick, punch
    enum ChasePose { Punch, Uppercut, StompReady, Kick }
    // 이어서 0.8초 교차 추격 (사용자): 0.2초마다 반대편 StompReady → 원래 방향 Kick → 반대편 Uppercut → 원래 방향 Punch
    static readonly ChasePose[] ChaseOrder =
    {
        ChasePose.Punch, ChasePose.Uppercut, ChasePose.StompReady, ChasePose.Punch, ChasePose.Kick, ChasePose.Punch,
        ChasePose.StompReady, ChasePose.Kick, ChasePose.Uppercut, ChasePose.Punch,
    };
    static readonly float[] ChaseTimes = { 0.15f, 0.3f, 0.45f, 0.6f, 0.75f, 0.9f, 1.1f, 1.3f, 1.5f, 1.7f };   // 타격 순간
    static readonly bool[] ChaseOpposite = { false, false, false, false, false, false, true, false, true, false };   // true = 반대편 (플레이어 진행 방향 앞쪽)

    enum Step { Finished, Warning, Dash, Miss, Hold, Knock, Hold2, Punch, Regrab, Lift, FinalKick, Fall,
                EnhLaser, EnhFireGrow, EnhFireMove, EnhJump, EnhDiveReady, EnhDive, EnhLaugh, EnhStompAttack, EnhGrabReady, EnhStompReady, EnhKick }
    Step step;
    Enemy body;
    SpriteRenderer bodyVisual;
    Player player;
    Sprite grabReady, grabDash, punch, kick, uppercut, stompReady, jump, diveReady, dive, laugh, stompAttack;
    Func<Transform> cameraTarget;
    public Func<bool> IsEnhanced;
    bool enhancedRun, poseScaled;
    int volleys, laserHits;
    GameObject fireGo; SpriteRenderer fireSr; KagutsuchiSunFireAnim fireAnim;
    Vector2 firePos, fireVel, fireHurtOffset, diveStart, layPos;
    float fireRadius, fireCellScale = 1f;
    Action<Sprite, float> showPose;
    Action stopPose;
    Action<float> showRunPose;
    BossWarning warning;
    KagutsuchiKickWindFx kickWind;   // 마지막 발차기 부채꼴 풍압 (사용자 2026-11-16)
    Vector2 dashOrigin, dashDirection, dashEnd, bossGround, moveStart, moveEnd, approachStart, approachEnd, regrabStart;
    float elapsed, attackSign, finalDuration, bossHeight;
    int chaseSlot;
    bool approaching, enhReadyShown, enhDashShown;
    public bool Finished => step == Step.Finished;

    public void Initialize(Enemy owner, Sprite grabReady, Sprite grabDash, Sprite punch, Sprite kick, Sprite uppercut, Sprite stompReady,
                           Sprite jump, Sprite diveReady, Sprite dive, Sprite laugh, Sprite stompAttack, Func<Transform> cameraTarget,
                           Action<Sprite, float> showPose, Action stopPose, Action<float> showRunPose)
    {
        this.jump = jump; this.diveReady = diveReady; this.dive = dive; this.laugh = laugh; this.stompAttack = stompAttack; this.cameraTarget = cameraTarget;
        body = owner; bodyVisual = owner.GetComponent<SpriteRenderer>();
        this.grabReady = grabReady; this.grabDash = grabDash; this.punch = punch; this.kick = kick;
        this.uppercut = uppercut; this.stompReady = stompReady;
        this.showPose = showPose; this.stopPose = stopPose; this.showRunPose = showRunPose;
        kickWind = new KagutsuchiKickWindFx { Scale = KickWindScale };
    }

    public void Begin()
    {
        Cancel();
        player = Player.I;
        if (player == null) return;
        enhancedRun = IsEnhanced != null && IsEnhanced();
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
    }

    public Vector2 Tick(float dt)
    {
        if (Finished || dt <= 0f) return Vector2.zero;
        if (body == null || body.hp <= 0f || player == null || player.hp <= 0f) { Cancel(); return Vector2.zero; }
        if (step != Step.Warning && step != Step.Dash && step != Step.Miss
            && (!player.IsHeldByBoss(this) || player.IsInvulnerable || DebugCheats.God))
        { Cancel(); return Vector2.zero; }
        Vector2 before = body.transform.position;
        elapsed += dt;
        kickWind?.Tick(dt);
        TickFire(dt);
        switch (step)
        {
            case Step.Warning:
                if (elapsed >= WarningTime) { ClearWarning(); step = Step.Dash; elapsed = 0f; }
                break;
            case Step.Dash:
                showRunPose(elapsed);
                Vector2 next = Vector2.Lerp(dashOrigin, dashEnd, Mathf.Clamp01(elapsed / DashTime));
                if (TryCatch(before, next, out Vector2 contact)) bossGround = contact;
                else
                {
                    bossGround = next;
                    if (elapsed >= DashTime) { step = Step.Miss; elapsed = 0f; showPose(grabReady, 0f); }   // [임시] 빗나가면 1초 경직
                }
                break;
            case Step.Miss:
                if (elapsed >= MissRecover) Cancel();
                break;
            case Step.Hold:
                if (elapsed >= HoldTime) BeginKnock();
                break;
            case Step.Knock:
                TickKnock();
                break;
            case Step.Hold2:
                if (elapsed >= RegrabHold) BeginPunch();
                break;
            case Step.Punch:
                MoveHeld(EaseOut(Mathf.Clamp01(elapsed / PunchTime)), 0f);
                if (elapsed >= PunchTime && AtMoveEnd()) BeginRegrab();
                break;
            case Step.Regrab:
                bossGround = Vector2.Lerp(regrabStart, BesidePlayer(), Mathf.Clamp01(elapsed / RegrabTime));
                if (elapsed >= RegrabTime) BeginLift();
                break;
            case Step.Lift:
                TickLift();
                break;
            case Step.FinalKick:
                MoveHeld(Mathf.Clamp01(elapsed / finalDuration), LiftHeight);
                if (elapsed >= finalDuration && AtMoveEnd()) { step = Step.Fall; elapsed = 0f; }
                break;
            case Step.EnhLaser: TickLaser(); break;
            case Step.EnhFireGrow:
                fireRadius = Mathf.Lerp(FireStartRadius, FireRadius, Mathf.Clamp01(elapsed / FireGrowTime));
                if (elapsed >= FireGrowTime)
                {
                    fireRadius = FireRadius;
                    fireVel = new Vector2(attackSign, 0f) * Mathf.Max(player.moveSpeed, 1f) * FireSpeedMul;   // 보스가 보던 방향, 플레이어 속도의 0.5배
                    step = Step.EnhFireMove; elapsed = 0f;
                }
                break;
            case Step.EnhFireMove:
                if (elapsed >= JumpDelay) BeginJump();
                break;
            case Step.EnhJump:
                bossHeight = JumpHeight * EaseOut(Mathf.Clamp01(elapsed / JumpTime));
                Show(jump, bossHeight, attackSign);
                if (elapsed >= JumpTime) { step = Step.EnhDiveReady; elapsed = 0f; bossHeight = JumpHeight; Show(diveReady, bossHeight, attackSign); }
                break;
            case Step.EnhDiveReady:
                if (elapsed >= DiveReadyTime) { step = Step.EnhDive; elapsed = 0f; diveStart = bossGround; SetPoseScale(DiveScale); }
                break;
            case Step.EnhDive:
                float dv = Mathf.Clamp01(elapsed / DiveTime);
                Vector2 target = firePos - fireHurtOffset;   // 초대형 화염구 중앙 (이동 중이므로 매 프레임 따라감)
                bossGround = Vector2.Lerp(diveStart, target, dv);
                bossHeight = JumpHeight * (1f - dv);
                Show(dive, bossHeight, target.x >= diveStart.x ? 1f : -1f);
                if (elapsed >= DiveTime) BeginLaugh(target);
                break;
            case Step.EnhLaugh:
                if (elapsed >= LaughTime) { step = Step.EnhStompAttack; elapsed = 0f; Show(stompAttack, 0f, attackSign); SetPoseScale(StompAttackScale); }
                break;
            case Step.EnhStompAttack:
                if (elapsed >= StompAttackTime)
                {
                    step = Step.EnhGrabReady; elapsed = 0f;
                    SetPoseScale(1f);
                    player.SetBossHoldProne(this, false);   // 눕힌 플레이어를 끌어올림
                    moveStart = player.transform.position;
                    moveEnd = Stage.ClampInside(bossGround + new Vector2(attackSign * (OppositeDistance + PullShift), 0f), player.radius);   // 끌어올린 최종 위치는 보스가 보는 방향으로 2 더 (사용자 2026-11-16)
                    Show(grabReady, 0f, attackSign);
                }
                break;
            case Step.EnhGrabReady:
                float lift = Mathf.Clamp01(elapsed / (GrabReadyTime * 0.5f));
                MoveHeld(lift, 0f);
                player.SetBossHoldOffsetWeight(this, 1f - lift);   // 눕힌 그림 정렬 보정을 끌어올리는 동안 풀어 줌
                if (elapsed >= GrabReadyTime) { step = Step.EnhStompReady; elapsed = 0f; Show(stompReady, 0f, attackSign); }
                break;
            case Step.EnhStompReady:
                if (elapsed >= StompReadyTime) BeginEnhKick();
                break;
            case Step.EnhKick:
                MoveHeld(EaseOut(Mathf.Clamp01(elapsed / EndKnockTime)), 0f);
                if (elapsed >= EndKnockTime && AtMoveEnd())
                {
                    player.EndBossHold(this);
                    player.StunByBoss(EndStun);   // [임시] 기본 패턴과 같이 끝난 뒤 기절
                    Cancel();
                }
                break;
            case Step.Fall:
                float f = Mathf.Clamp01(elapsed / FallTime);
                float fall = 1f - f * f;   // 점점 빨라지는 낙하
                player.SetBossHoldHeight(this, LiftHeight * fall);
                bossHeight = LiftHeight * fall;
                Show(kick, bossHeight, attackSign);
                if (elapsed >= FallTime)
                {
                    player.EndBossHold(this);
                    player.StunByBoss(FinalStun);   // 사용자: 낙하가 끝나면 기절 1초
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
        Show(grabReady, 0f, attackSign);
        moveStart = moveEnd = player.transform.position;
        player.SetBossHoldFacing(this, !bodyVisual.flipX);
        CameraFollow.BeginOutsideMapFollow(this, player.BossHoldVisual, CameraMagnification);
        return true;
    }

    /// <summary>발차기로 넉백 시작: 플레이어 좌우 중 남은 공간이 큰 쪽으로, 보스는 반대편 간격 2</summary>
    void BeginKnock()
    {
        float leftRoom = Stage.ExitDistance(player.HurtCenter, new Vector2(-1f, 0f));
        float rightRoom = Stage.ExitDistance(player.HurtCenter, new Vector2(1f, 0f));
        if (leftRoom > rightRoom) attackSign = -1f;
        else if (rightRoom > leftRoom) attackSign = 1f;
        bossGround = Stage.ClampInside((Vector2)player.transform.position - new Vector2(attackSign * LaunchGap, 0f), body.radius);
        SetMove(LaunchDistance);
        step = Step.Knock; elapsed = 0f; chaseSlot = -1; approaching = false; enhReadyShown = enhDashShown = false;
        Show(kick, 0f, attackSign);
        PlayBoom();
        Hit();
    }

    void TickKnock()
    {
        // 넉백 이동 (2초). GrabDash 를 시작하는 순간 (ChaseTotal) 부터 0.1초 동안 플레이어를 보스가 보는 방향으로 3 더 이동 (사용자 2026-11-16)
        Vector2 heldPos = Vector2.Lerp(moveStart, moveEnd, EaseOut(Mathf.Clamp01(elapsed / KnockMoveTime)));
        float shiftStart = enhancedRun ? ChaseTotal + EnhReadyHold : ChaseTotal;   // 강화: GrabReady 홀딩 (이동 보류) 뒤 GrabDash 와 함께 이동
        if (enhancedRun && elapsed > ChaseTotal && elapsed <= shiftStart) { if (!enhReadyShown) { enhReadyShown = true; Show(grabReady, 0f, -attackSign); } }
        else if (enhancedRun && elapsed > shiftStart && !enhDashShown) { enhDashShown = true; Show(grabDash, 0f, -attackSign); }
        if (elapsed > shiftStart)
        {
            float shiftTime = enhancedRun ? EnhShiftTime : HoldShiftTime;   // 강화: 레이저 직전 끌고 오는 시간 50% 감소 (사용자 2026-11-16)
            float progress = Mathf.Clamp01((elapsed - shiftStart) / shiftTime);
            float shift = (HoldShift + (enhancedRun ? EnhExtraShift : 0f)) * progress;   // 강화: 레이저 때 플레이어를 보스가 보는 방향으로 0.5 더 (사용자 2026-11-16)
            float down = enhancedRun ? EnhDown * progress : 0f;   // 강화: 플레이어 (와 레이저) 를 0.5 아래로 (사용자 2026-11-16)
            heldPos = Stage.ClampInside(heldPos - new Vector2(attackSign * shift, down), player.radius);   // 보스는 앞쪽에서 -attackSign 방향을 봄
        }
        player.SetBossHoldPosition(this, heldPos);
        player.SetBossHoldHeight(this, 0f);
        if (elapsed < ChaseTotal + Epsilon)
        {
            int slot = -1;
            for (int i = 0; i < ChaseTimes.Length; i++) if (elapsed + Epsilon >= ChaseTimes[i]) slot = i;
            if (slot >= 0 && slot != chaseSlot) { chaseSlot = slot; ChaseHit(slot); }
            bool opposite = chaseSlot >= 0 && ChaseOpposite[chaseSlot];
            float side = opposite ? 1f : -1f;   // 원래 방향 = 플레이어 뒤, 반대편 = 앞
            float gap = chaseSlot >= CrossStart ? CrossGap : ChaseGap;   // 교차 4타 (슬롯 6~9) 는 간격 3
            bossGround = Stage.ClampInside((Vector2)player.transform.position + new Vector2(side * attackSign * gap, 0f), body.radius);
        }
        else
        {
            if (!approaching)
            {
                approaching = true;
                // 진행 반대편 (앞쪽) 에서 GrabDash 로 4 들어옴 (플레이어는 넉백 끝 지점으로 가고 있음)
                approachStart = Stage.ClampInside(moveEnd + new Vector2(attackSign * (OppositeDistance + ApproachDistance), 0f), body.radius);
                approachEnd = Stage.ClampInside(moveEnd + new Vector2(attackSign * OppositeDistance, 0f), body.radius);
                if (!enhancedRun) Show(grabDash, 0f, -attackSign);   // 강화는 GrabReady 로 먼저 홀딩
            }
            bossGround = Vector2.Lerp(approachStart, approachEnd, Mathf.Clamp01((elapsed - ChaseTotal) / ApproachTime));
        }
        if (elapsed >= KnockTime)
        {
            moveStart = moveEnd = player.transform.position;   // 이동은 이미 끝남 (GrabDash 시작 0.1초)
            bossGround = approachEnd;
            attackSign = -attackSign;   // 보스는 플레이어 앞쪽에서 마주 봄 → 이후 공격은 지금까지의 반대 방향 (보스에서 멀어지는 쪽) 으로 밀어냄
            if (enhancedRun) { BeginLaser(); return; }
            step = Step.Hold2; elapsed = 0f;
            Show(grabDash, 0f, attackSign);
        }
    }

    void ChaseHit(int slot)
    {
        ChasePose pose = ChaseOrder[slot];
        float face = ChaseOpposite[slot] ? -attackSign : attackSign;   // 반대편에서는 마주 봄
        Sprite sprite = pose == ChasePose.Punch ? punch : pose == ChasePose.Kick ? kick : pose == ChasePose.Uppercut ? uppercut : stompReady;
        Show(sprite, 0f, face);
        PlayBoom(slot >= CrossStart ? -2f : 0f);   // 교차 4타는 이펙트를 보스 반대 방향으로 2 (사용자 2026-11-16: 1 → +1) · 추격 이펙트는 자세와 상관없이 전부 측면 소닉붐
        Hit();
    }

    // ---------------- 체력 50% 이하 강화 ----------------
    /// <summary>현재 표시 중인 보스 포즈 (airRoot) 크기 배율. 1 = 원래</summary>
    void SetPoseScale(float scale)
    {
        var t = cameraTarget != null ? cameraTarget() : null;
        if (t == null) return;
        t.localScale = Vector3.one * scale;
        poseScaled = !Mathf.Approximately(scale, 1f);
    }

    void BeginLaser()
    {
        step = Step.EnhLaser; elapsed = 0f; volleys = 0; laserHits = 0;
        moveStart = moveEnd = player.transform.position;
        CameraFollow.Shake(LaserTime, LaserShake);   // 레이저 발사 동안 화면 흔들림 (사용자)
        Show(grabDash, 0f, attackSign);
    }

    void TickLaser()
    {
        // 1회 3개씩 0.05초 간격 (총 20회 = 60개). 피해 5 는 총 5회, 발사 시간 / 5 간격 (레이저 자체는 연출만, 사용자)
        int total = Mathf.RoundToInt(LaserTime / LaserInterval);
        while (volleys < total && elapsed >= volleys * LaserInterval) { SpawnVolley(); volleys++; }
        while (laserHits < LaserHits && elapsed >= LaserTime / LaserHits * (laserHits + 1)) { Hit(); laserHits++; }
        if (elapsed >= LaserTime && volleys >= total && laserHits >= LaserHits) BeginFireGrow();
    }

    void SpawnVolley()
    {
        // 중심 방향 = 보스에서 플레이어 방향, 시작점만 플레이어에서 보스 쪽으로 0.3 (사용자)
        Vector2 center = new Vector2(attackSign, 0f);
        Vector2 origin = player.HurtCenter - center * LaserStartBack;
        Rect safe = new Rect(Stage.Center - Stage.HalfSize * 2f, Stage.HalfSize * 4f);   // 맵 전체가 안전 = 피해 없음 (연출만)
        for (int i = 0; i < LaserPerVolley; i++)
        {
            float angle = UnityEngine.Random.Range(-LaserFan * 0.5f, LaserFan * 0.5f);
            Vector2 dir = Quaternion.Euler(0f, 0f, angle) * center;
            float length = Mathf.Max(1f, Stage.ExitDistance(origin, dir) + 1f);
            var laser = RaidLaser.Line(origin, dir, length, LaserWidth, LaserLife, 0f, 1f, safe);
            KagutsuchiFlameLaserFx.Attach(laser);
        }
    }

    void BeginFireGrow()
    {
        step = Step.EnhFireGrow; elapsed = 0f;
        Show(grabReady, 0f, attackSign);
        fireHurtOffset = player.HurtCenter - (Vector2)player.transform.position;
        firePos = player.HurtCenter; fireVel = Vector2.zero; fireRadius = FireStartRadius;
        fireGo = new GameObject("KagutsuchiRushFireball");
        fireSr = fireGo.AddComponent<SpriteRenderer>();
        fireAnim = new KagutsuchiSunFireAnim();
        fireSr.sprite = fireAnim.Valid ? fireAnim.First : SpriteFactory.Get("Fx_Circle", SpriteFactory.Shape.Circle);
        fireSr.sortingOrder = Fx.DefaultOrder;   // 플레이어 스프라이트를 가림
        fireCellScale = fireAnim.Valid ? KagutsuchiSunFireAnim.CellScale : 1f;
        ApplyFire();
    }

    void TickFire(float dt)
    {
        if (fireGo == null) return;
        firePos += fireVel * dt;
        // 화염구가 움직이는 동안 잡힌 플레이어도 화염구 중앙에 붙어 같이 이동 (사용자 2026-11-16: 플레이어가 화염구에서 벗어남)
        if (fireVel != Vector2.zero && player != null && player.IsHeldByBoss(this))
            player.SetBossHoldPosition(this, firePos - fireHurtOffset);
        fireAnim?.Tick(dt, fireSr);
        ApplyFire();
    }

    void ApplyFire()
    {
        if (fireGo == null || fireSr.sprite == null) return;
        fireGo.transform.position = firePos;
        float size = fireAnim != null && fireAnim.Valid ? fireSr.sprite.bounds.size.x : fireSr.sprite.bounds.size.y;
        fireGo.transform.localScale = Vector3.one * (fireRadius * 2f * fireCellScale / Mathf.Max(0.0001f, size));
    }

    void ClearFire()
    {
        if (fireGo != null) { fireGo.SetActive(false); Destroy(fireGo); fireGo = null; }
        fireAnim?.Dispose(); fireAnim = null;
        fireVel = Vector2.zero;
    }

    void BeginJump()
    {
        step = Step.EnhJump; elapsed = 0f;
        Show(jump, 0f, attackSign);
        CameraFollow.EndOutsideMapFollow(this);
        if (cameraTarget != null) CameraFollow.BeginOutsideMapFollow(this, cameraTarget(), CameraMagnification);   // 이때부터 카메라는 보스에 귀속 (사용자)
    }

    void BeginLaugh(Vector2 landing)
    {
        bossGround = landing; bossHeight = 0f;
        SetPoseScale(1f);   // Dive 자세 크기 감소 해제
        ClearFire();
        Fx.ScreenFlash(Color.white, FlashHold, FlashFade);   // 하얀 화면 1초 유지 뒤 2초간 돌아옴 (사용자 2026-11-16)
        CameraFollow.Shake(LandShake, LandShakeAmp);
        step = Step.EnhLaugh; elapsed = 0f;
        float face = landing.x >= diveStart.x ? 1f : -1f;
        Show(laugh, 0f, face);
        // 플레이어는 공격 6 · 7 의 마지막 내려찍기처럼 보스 발아래 누움
        player.SetBossHoldProne(this, true);
        // 높이: 보스 발 (현재 포즈 그림의 아래 끝) 에 맞춤 (사용자 2026-11-16). 눕힌 그림의 머리를 발 앞 지면에 정렬 (공격 6 · 7 의 내려찍기와 같은 방식)
        var poseRenderer = cameraTarget != null ? cameraTarget().GetComponent<SpriteRenderer>() : null;
        float footY = poseRenderer != null && poseRenderer.sprite != null ? poseRenderer.bounds.min.y : bossGround.y;
        layPos = new Vector2(bossGround.x + face * FootGap, footY);
        player.SetBossHoldPosition(this, layPos);   // 판정 위치 (체력 바 기준) 도 보스 발 높이로 (사용자 2026-11-16: 체력 바가 그림과 어긋남)
        player.SetBossHoldHeight(this, 0f);
        player.AlignBossHoldHead(this, layPos + Vector2.up * LieHeadHeight);
        attackSign = face;
        moveStart = moveEnd = player.transform.position;
    }

    void BeginEnhKick()
    {
        step = Step.EnhKick; elapsed = 0f;
        SetMove(EndKnock);
        Show(kick, 0f, attackSign);
        PlayBoom();
        CameraFollow.Shake(ShakeTime, ShakeAmplitude);
        Hit();
    }

    void BeginPunch()
    {
        bossGround = BesidePlayer();
        SetMove(PunchDistance);
        step = Step.Punch; elapsed = 0f;
        Show(punch, 0f, attackSign);
        PlayBoom();
        Hit();
    }

    void BeginRegrab()
    {
        regrabStart = bossGround;
        moveStart = moveEnd = player.transform.position;
        step = Step.Regrab; elapsed = 0f;
        Show(grabReady, 0f, attackSign);
    }

    void BeginLift()
    {
        bossGround = BesidePlayer();
        moveStart = moveEnd = player.transform.position;
        step = Step.Lift; elapsed = 0f; bossHeight = 0f;
        Show(uppercut, 0f, attackSign);
        body.GetComponent<KagutsuchiUppercutFx>()?.Play(bossGround, bodyVisual.flipX);
        Hit();
    }

    void TickLift()
    {
        float t = Mathf.Clamp01(elapsed / LiftTime);
        player.SetBossHoldHeight(this, LiftHeight * EaseOut(t));   // 5 상승, 마지막에 최고 높이 도착
        float riseStart = BossRiseStart;                           // 플레이어 도착 0.75초 전
        float peakAt = LiftTime - BossPeakBefore;                  // 도착 0.25초 전 최고 높이
        if (elapsed >= riseStart)
        {
            bossHeight = LiftHeight * EaseOut(Mathf.Clamp01((elapsed - riseStart) / (peakAt - riseStart)));   // 빠르게 오르다 천천히
            Show(stompReady, bossHeight, attackSign);
        }
        if (elapsed >= LiftTime) BeginFinalKick();
    }

    void BeginFinalKick()
    {
        bossHeight = LiftHeight;
        SetMove(1000f);   // 맵 끝까지 (ClampInside 가 경계에서 멈춤)
        float distance = Vector2.Distance(moveStart, moveEnd);
        finalDuration = Mathf.Clamp(distance / (Mathf.Max(player.moveSpeed, 1f) * FinalSpeedMul), 0.05f, 2f);   // 플레이어 속도의 10배
        step = Step.FinalKick; elapsed = 0f;
        Show(kick, bossHeight, attackSign);
        PlayBoom(0f, LiftHeight);   // 마지막 발차기: 측면 소닉붐 + 부채꼴 풍압 (사용자 2026-11-16), 공중 높이에 맞춤
        kickWind?.Play(bossGround + Vector2.up * LiftHeight, bodyVisual.flipX, bodyVisual.sortingOrder + 1);
        CameraFollow.Shake(ShakeTime, ShakeAmplitude);
        Hit();
    }

    /// <summary>타격 이펙트 위치 (사용자 2026-11-16): 보스 간격과 상관없이 공격 7 처럼 플레이어 바로 옆 (보스가 보는 방향 반대로 0.5) 에서 시작한 것으로 재생</summary>
    /// <param name="towardBoss">보스 방향으로 옮길 거리, 음수 = 보스 반대 방향 (교차 4타 = -2, 사용자 2026-11-16)</param>
    /// <param name="height">공중 타격의 높이 (마지막 발차기)</param>
    void PlayBoom(float towardBoss = 0f, float height = 0f)
    {
        float face = bodyVisual.flipX ? 1f : -1f;
        Vector2 origin = (Vector2)player.transform.position - new Vector2(face * OppositeDistance, 0f);
        if (towardBoss != 0f)
        {
            float toBoss = bossGround.x >= player.transform.position.x ? 1f : -1f;
            origin += new Vector2(toBoss * towardBoss, 0f);
        }
        origin += Vector2.up * height;
        body.GetComponent<KagutsuchiSonicBoomFx>()?.Play(origin, bodyVisual.flipX);
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

    bool AtMoveEnd() => ((Vector2)player.transform.position - moveEnd).sqrMagnitude <= Epsilon * Epsilon;

    void MoveHeld(float progress, float height)
    {
        player.SetBossHoldPosition(this, Vector2.Lerp(moveStart, moveEnd, progress));
        player.SetBossHoldHeight(this, height);
    }

    static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

    /// <summary>faceSign 방향으로 때리는 자세 (flipX = 오른쪽을 봄). 앞쪽에서 마주 보는 자세는 -attackSign 을 넘김</summary>
    void Show(Sprite pose, float height, float faceSign)
    {
        bodyVisual.flipX = faceSign > 0f;
        showPose(pose, height);
    }

    void Hit() => player.TakeDamage(DamagePerHit, ignoreHitInvuln: true);   // 사용자: 모든 피해 5 [임시]

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
        kickWind?.Stop();
        if (poseScaled) SetPoseScale(1f);
        ClearFire();
        if (step != Step.Finished) stopPose?.Invoke();
        step = Step.Finished;
        elapsed = 0f;
    }

    void OnDisable() => Cancel();
    void OnDestroy() => kickWind?.Dispose();
}
