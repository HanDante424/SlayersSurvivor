using System;
using UnityEngine;

/// <summary>
/// 카구츠치 공격 6 실행기. 지면 이동은 소유자 Tick으로 반환하고 공중 높이는 표시만 이동한다.
/// 홀드·카메라 예외·경고는 정상 종료와 강제 중단이 같은 정리 경로를 사용한다.
/// </summary>
public sealed class KagutsuchiGrabPattern : MonoBehaviour
{
    public const float TriggerRadius = 10f, Cooldown = 15f;
    const float WarningTime = 0.5f, HoldTime = 1f;
    const float DashDistance = 14f, DashMaxSpeed = 16f, DashTime = 2f * DashDistance / DashMaxSpeed;
    const float GuideInterval = 0.25f, TurnDegrees = 5f; // 이동 1과 동일한 유도 규칙
    const float PlayerRiseHeight = 10f, PlayerRiseTime = 2f;
    const float UppercutPoseTime = 1f, BossRiseTime = 0.5f, BossHeightAbovePlayer = 2.5f;
    const float FallTime = 0.1f, SlamPoseTime = 1f, KickPrepareTime = 1f, KickPoseTime = 1f;
    const float ThrowTime = 0.1f; // [임시] 사용자 지정 맵 끝 이동 시간
    const float DamagePerHit = 10f; // [임시] 사용자 지정 각 단계 피해
    const float ThrowStun = 2f, UppercutShakeTime = 0.2f, SlamShakeTime = 0.5f;
    const float ShakeAmplitude = 0.2f; // [임시] 기존 2페이즈 공격 흔들림 세기 재사용
    const float GrabCameraMagnification = 1.5f;
    const float BossSlowdownDistance = 0.9f;
    // Stomp 원화의 전방 발바닥 아래 실측점 (좌상단 px / 원화 크기).
    static readonly Vector2 StompFootBottom = new Vector2(80f / 1152f, 1095f / 1109f);
    const float Epsilon = 0.0001f;

    enum Step { Finished, Warning, Dash, Hold, Uppercut, Fall, Slam, KickPrepare, Kick }
    Step step;
    Enemy body;
    SpriteRenderer bodyVisual;
    Player player;
    Sprite ready, dash, uppercut, stompReady, stomp, kick;
    Action<Sprite, float> showPose;
    Action stopPose;
    Action<float> showRunPose;
    Action<Vector2> alignStompFoot;
    Func<Transform> cameraTarget;
    BossWarning warning;
    Vector2 direction, throwStart, throwEnd;
    float elapsed, runElapsed, runDistance, nextGuide;
    bool released;
    public bool Finished => step == Step.Finished;

    public void Initialize(Enemy owner, Sprite ready, Sprite dash, Sprite uppercut,
                           Sprite stompReady, Sprite stomp, Sprite kick,
                           Action<Sprite, float> showPose, Action stopPose, Func<Transform> cameraTarget, Action<float> showRunPose, Action<Vector2> alignStompFoot)
    {
        body = owner;
        bodyVisual = owner.GetComponent<SpriteRenderer>();
        this.ready = ready; this.dash = dash; this.uppercut = uppercut;
        this.stompReady = stompReady; this.stomp = stomp; this.kick = kick;
        this.showPose = showPose; this.stopPose = stopPose;
        this.cameraTarget = cameraTarget;
        this.showRunPose = showRunPose;
        this.alignStompFoot = alignStompFoot;
    }

    public void Begin()
    {
        Cancel();
        player = Player.I;
        if (player == null) return;
        direction = Aim(body.transform.position);
        elapsed = runElapsed = runDistance = 0f;
        nextGuide = GuideInterval;
        released = false;
        step = Step.Warning;
        showPose(ready, 0f);
        warning = BossWarning.Box(body.transform.position, direction, DashDistance, body.radius * 2f, 0f);
        UpdateWarning(body.transform.position);
    }

    // Enemy.Update가 멈추는 승패 화면에서도 임시 상태를 회수. 일시정지는 그대로 보존.
    void Update()
    {
        if (Finished || GameManager.I == null) return;
        if (GameManager.I.State == GameState.GameOver || GameManager.I.State == GameState.Victory) Cancel();
    }

    void LateUpdate()
    {
        if (step == Step.Slam && player != null && player.IsHeldByBoss(this))
            alignStompFoot(StompFootBottom);
    }

    public Vector2 Tick(float dt)
    {
        if (Finished || dt <= 0f) return Vector2.zero;
        if (player == null || player.hp <= 0f || body == null || body.hp <= 0f)
        { Cancel(); return Vector2.zero; }
        if (step != Step.Warning && step != Step.Dash && !released
            && (!player.IsHeldByBoss(this) || player.IsInvulnerable || DebugCheats.God))
        { Cancel(); return Vector2.zero; }
        elapsed += dt;
        switch (step)
        {
            case Step.Warning:
                // 준비 경고는 플레이어를 따라가고, 돌진은 이동 1과 같은 제한 유도.
                direction = Aim(body.transform.position);
                showPose(ready, 0f);
                UpdateWarning(body.transform.position);
                if (elapsed >= WarningTime) { step = Step.Dash; elapsed = 0f; showRunPose(0f); }
                break;
            case Step.Dash:
                return TickDash(dt);
            case Step.Hold:
                if (elapsed >= HoldTime)
                {
                    step = Step.Uppercut; elapsed = 0f;
                    showPose(uppercut, 0f);
                    CameraFollow.Shake(UppercutShakeTime, ShakeAmplitude);
                    player.TakeDamage(DamagePerHit);
                }
                break;
            case Step.Uppercut:
                player.SetBossHoldHeight(this, PlayerRiseHeight * EaseOutRise(elapsed / PlayerRiseTime));
                if (elapsed < UppercutPoseTime) showPose(uppercut, 0f);
                else showPose(stompReady, (PlayerRiseHeight + BossHeightAbovePlayer)
                              * BossRiseProgress((elapsed - UppercutPoseTime) / BossRiseTime));
                if (elapsed >= PlayerRiseTime) { step = Step.Fall; elapsed = 0f; }
                break;
            case Step.Fall:
                float fraction = 1f - Mathf.Clamp01(elapsed / FallTime);
                player.SetBossHoldHeight(this, PlayerRiseHeight * fraction);
                showPose(stompReady, (PlayerRiseHeight + BossHeightAbovePlayer) * fraction);
                if (elapsed >= FallTime)
                {
                    step = Step.Slam; elapsed = 0f;
                    showPose(stomp, 0f);
                    CameraFollow.Shake(SlamShakeTime, ShakeAmplitude);
                    player.TakeDamage(DamagePerHit);
                }
                break;
            case Step.Slam:
                if (elapsed >= SlamPoseTime)
                { step = Step.KickPrepare; elapsed = 0f; showPose(stompReady, 0f); }
                break;
            case Step.KickPrepare:
                // 발 아래 표시 보정은 기존 발차기 준비 시간 동안 복구한다.
                player.SetBossHoldOffsetWeight(this, 1f - Mathf.Clamp01(elapsed / KickPrepareTime));
                if (elapsed >= KickPrepareTime) BeginKick();
                break;
            case Step.Kick:
                if (!released)
                {
                    player.SetBossHoldPosition(this, Vector2.Lerp(throwStart, throwEnd, Mathf.Clamp01(elapsed / ThrowTime)));
                    if (elapsed >= ThrowTime)
                    {
                        player.EndBossHold(this);
                        released = true;
                        player.StunByBoss(ThrowStun);
                    }
                }
                if (elapsed >= KickPoseTime) Cancel();
                break;
        }
        if (player.hp <= 0f) Cancel();
        return Vector2.zero;
    }

    static float EaseOutRise(float progress)
    {
        float remaining = 1f - Mathf.Clamp01(progress);
        return 1f - remaining * remaining;
    }

    static float BossRiseProgress(float progress)
    {
        float t = Mathf.Clamp01(progress);
        // 거리 90%까지 등속, 마지막 10%에서 급감속. 두 구간의 접속 속도를 같게 맞춘다.
        // 뒤 구간이 등감속이므로 시작 속도는 평균의 두 배: d/t = 2(1-d)/(1-t).
        float splitTime = BossSlowdownDistance / (2f - BossSlowdownDistance);
        if (t <= splitTime) return BossSlowdownDistance * t / splitTime;
        return BossSlowdownDistance + (1f - BossSlowdownDistance)
            * EaseOutRise((t - splitTime) / (1f - splitTime));
    }

    Vector2 Aim(Vector2 from)
    {
        Vector2 aim = player.HurtCenter - from;
        return aim.sqrMagnitude > Epsilon ? aim.normalized : Vector2.down;
    }

    Vector2 TickDash(float dt)
    {
        Vector2 start = body.transform.position, pos = start;
        float remaining = dt;
        while (remaining > 0f && runElapsed < DashTime)
        {
            if (runElapsed >= nextGuide - Epsilon && nextGuide < DashTime)
            {
                float angle = Mathf.Clamp(Vector2.SignedAngle(direction, Aim(pos)), -TurnDegrees, TurnDegrees) * Mathf.Deg2Rad;
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
                direction = new Vector2(direction.x * c - direction.y * s, direction.x * s + direction.y * c).normalized;
                nextGuide += GuideInterval;
            }
            float slice = Mathf.Min(remaining, Mathf.Min(DashTime, nextGuide) - runElapsed);
            if (slice <= 0f) break;
            float nextTime = Mathf.Min(DashTime, runElapsed + slice);
            float requested = DashDistance * (nextTime * nextTime - runElapsed * runElapsed) / (DashTime * DashTime);
            float allowed = KagutsuchiPhase2.BoundedTravel(pos, direction, requested, body.radius);
            Vector2 next = pos + direction * allowed;
            if (TryCatch(pos, next)) return (next - start) / dt;
            pos = next;
            runElapsed = nextTime; runDistance += allowed; remaining -= slice;
            if (allowed + Epsilon < requested) { FinishMiss(); break; }
        }
        if (step == Step.Dash)
        {
            UpdateWarning(pos);
            showRunPose(runElapsed);
            if (runElapsed >= DashTime) FinishMiss();
        }
        return (pos - start) / dt;
    }

    bool TryCatch(Vector2 from, Vector2 to)
    {
        Vector2 segment = to - from;
        float t = segment.sqrMagnitude > Epsilon
            ? Mathf.Clamp01(Vector2.Dot(player.HurtCenter - from, segment) / segment.sqrMagnitude) : 0f;
        float radius = body.radius + player.radius;
        if ((player.HurtCenter - (from + segment * t)).sqrMagnitude > radius * radius
            || !player.TryBeginBossHold(this)) return false;
        ClearWarning();
        step = Step.Hold; elapsed = 0f;
        showPose(ready, 0f);
        CameraFollow.BeginOutsideMapFollow(this, cameraTarget(), GrabCameraMagnification);
        player.TakeDamage(DamagePerHit);
        if (player.hp <= 0f) Cancel();
        return true;
    }

    void BeginKick()
    {
        step = Step.Kick; elapsed = 0f;
        showPose(kick, 0f);
        player.SetBossHoldHeight(this, 0f);
        throwStart = player.transform.position;
        Vector2 away = player.HurtCenter - (Vector2)body.transform.position;
        away = away.sqrMagnitude > Epsilon ? away.normalized : direction;
        float distance = Stage.ExitDistance(player.HurtCenter, away);
        Vector2 offset = player.HurtCenter - throwStart;
        throwEnd = Stage.ClampInside(player.HurtCenter + away * distance, player.radius) - offset;
        player.TakeDamage(DamagePerHit);
    }

    void UpdateWarning(Vector2 from)
    {
        if (warning == null) return;
        float length = KagutsuchiPhase2.BoundedTravel(from, direction, Mathf.Max(0f, DashDistance - runDistance), body.radius);
        warning.transform.position = from + direction * (length * 0.5f);
        warning.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        warning.transform.localScale = new Vector3(length, body.radius * 2f, 1f);
        bodyVisual.flipX = direction.x > 0f;
    }

    // 빗나가면 후속 홀드 없이 종료하고 기존 공통 패턴 간격을 따른다.
    void FinishMiss()
    {
        ClearWarning();
        released = true;
        Cancel();
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
