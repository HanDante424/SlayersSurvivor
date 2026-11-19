using System.Collections;
using UnityEngine;

/// <summary>
/// 임팩트 프레임 (2026-11-20 사용자 요청): 적중한 순간 시간이 duration 초 멈추고 화면이 흑백이 되었다가 돌아옴.
///
/// 방식: 렌더 파이프라인이 기본 (후처리 없음) 이라 셰이더를 쓰지 않고,
///   ① 시간을 멈춘 프레임 끝에서 화면을 한 장 캡처 → ② CPU 로 흑백 변환 → ③ 멈춰 있는 동안 전체 화면에 덮어 그림.
///   시간이 멈춘 동안은 화면이 움직이지 않으므로 흑백 후처리와 같게 보임. 끝나면 덮개를 치우고 시간 배속을 되돌림.
/// 멈춘 동안에는 Time.timeScale = 0 이라 모든 이펙트 · 코루틴 (WaitForSeconds) 이 함께 멈춤.
/// </summary>
public class ImpactFrame : MonoBehaviour
{
    /// <summary>시간이 멈춰 있는 중 (디버그 맵이 시간 배속을 덮어쓰지 않게 확인용)</summary>
    public static bool Frozen { get; private set; }
    static bool queued;

    const float Contrast = 1.2f;   // [임시] 흑백 대비 (1 = 그대로 회색조)

    // 적중 파티클 (임팩트 프레임 전용, 2026-11-20): 3열 x 2행 6프레임 시트. 임팩트 프레임 (시간 정지 · 흑백) 이 끝난 뒤 월드에 한 번 재생.
    const string ParticleSheet = "ErnStray_SlashHitParticle";
    const float ParticleFps = 20f;   // 제작 사양 (6프레임 → 0.3초)
    const int ParticleSortingOrder = 12;
    // 파티클과 함께 화면 흔들림 (2026-11-20 사용자 지정 0.15초). 세기는 보스 패턴의 가장 강한 흔들림 (0.3초 · 0.25) 보다 약간 크게
    const float ShakeTime = 0.15f;
    const float ShakeAmp = 0.6f;   // [임시] 월드 거리 (0.3 → 0.6, 사용자 요청 "더 강하게")

    float delay, duration, prevScale;
    Texture2D shot;
    Transform follow;           // 파티클 위치 = 맞은 적 (끝나는 순간의 위치). 사라졌으면 fallbackPos
    Vector2 fallbackPos;
    float particleWorldWidth;   // 파티클 한 칸의 월드 가로 길이 (0 이하 = 파티클 없음)

    /// <summary>delay 초 뒤 (게임 시간) 에 duration 초 동안 시간 정지 + 흑백. 이미 진행 중이면 무시</summary>
    public static void Play(float delay, float duration) => Play(delay, duration, null, Vector2.zero, 0f);

    /// <param name="follow">파티클을 띄울 기준 (맞은 적). 없어지면 fallbackPos 사용</param>
    /// <param name="particleWorldWidth">파티클 한 칸의 월드 가로 길이. 0 이하면 파티클 없이 흑백만</param>
    public static void Play(float delay, float duration, Transform follow, Vector2 fallbackPos, float particleWorldWidth)
    {
        if (queued || Frozen) return;
        queued = true;
        var go = new GameObject("ImpactFrame");
        var f = go.AddComponent<ImpactFrame>();
        f.delay = Mathf.Max(0f, delay);
        f.duration = Mathf.Max(0.01f, duration);
        f.follow = follow; f.fallbackPos = fallbackPos; f.particleWorldWidth = particleWorldWidth;
    }

    IEnumerator Start()
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);   // 참격이 가장 커지는 프레임까지 기다림

        prevScale = Time.timeScale;
        Time.timeScale = 0f;
        Frozen = true;
        yield return new WaitForEndOfFrame();   // 이 프레임이 다 그려진 뒤 캡처 (UI 포함)

        var tex = ScreenCapture.CaptureScreenshotAsTexture();
        if (tex != null)
        {
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                var c = px[i];
                float g = (c.r * 0.299f + c.g * 0.587f + c.b * 0.114f) / 255f;
                g = Mathf.Clamp01((g - 0.5f) * Contrast + 0.5f);
                byte b = (byte)(g * 255f + 0.5f);
                px[i] = new Color32(b, b, b, 255);
            }
            tex.SetPixels32(px);
            tex.Apply();
            shot = tex;
        }

        yield return new WaitForSecondsRealtime(duration);   // 변환에 걸린 시간은 빼고 화면이 보이는 시간만 셈
        Finish(true);
    }

    void OnGUI()
    {
        if (shot == null) return;
        GUI.depth = -1000;   // 가장 위
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), shot, ScaleMode.StretchToFill, false);
    }

    void OnDestroy() => Finish(false);

    /// <param name="spawnParticle">정상 종료일 때만 (씬이 바뀌는 등 중간에 끊겼으면 파티클을 만들지 않음)</param>
    void Finish(bool spawnParticle)
    {
        if (shot != null) { Destroy(shot); shot = null; }
        bool wasFrozen = Frozen;
        if (Frozen)
        {
            Frozen = false;
            // 멈춘 동안 일시정지 등으로 상태가 바뀌었으면 그 상태를 존중
            bool playing = GameManager.I == null || GameManager.I.State == GameState.Playing;
            if (playing && Mathf.Approximately(Time.timeScale, 0f)) Time.timeScale = prevScale;
        }
        queued = false;

        // 임팩트 프레임이 끝난 뒤 (시간이 다시 흐른 뒤) 월드에 파티클 딱 하나 + 화면 흔들림.
        // 임팩트 1번 = 파티클 1개 (맞은 적이 여럿이어도 처음 맞은 적 위치 하나). particleWorldWidth 를 0 으로 비워 다시 만들어지지 않게 함
        if (spawnParticle && wasFrozen && particleWorldWidth > 0f)
        {
            float width = particleWorldWidth;
            particleWorldWidth = 0f;
            var frames = SpriteEffect.LoadFrames(ParticleSheet, 2, 3, 1f);
            if (frames != null)
            {
                Vector2 at = follow != null ? (Vector2)follow.position : fallbackPos;
                SpriteEffect.Spawn(frames, ParticleFps, null, at, 0f, false, ParticleSortingOrder, width);
            }
            CameraFollow.Shake(ShakeTime, ShakeAmp);
        }

        if (this != null) Destroy(gameObject);
    }
}
