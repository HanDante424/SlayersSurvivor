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
            Vector2 hitAt = follow != null ? (Vector2)follow.position : fallbackPos;
            ImpactSlashOverlay.Spawn(hitAt);   // 화면을 가르는 일직선 참격 (맞은 위치를 지남)
            ImpactScreenFlow.Spawn();          // 화면 전체 불꽃 (파티클과 같은 순간)
        }

        if (this != null) Destroy(gameObject);
    }
}

/// <summary>
/// 임팩트 프레임이 끝날 때 화면 전체를 가르는 일직선 참격 (2026-11-20 사용자 요청).
/// 기존 일섬 시트 (ErnStray_SkillTripleSlashIlseom, 2행 x 3열, 6프레임, 칸 512x512, 참격이 약 45° 대각선) 를 그대로 재사용.
/// 월드 이펙트가 아니라 화면 위에 직접 그림 → 카메라 확대와 상관없이 항상 화면 전체를 가로지름.
/// 칸 한 변 = 화면 대각선 x SizeOfDiagonal, 칸 중심 = 맞은 위치 (참격이 맞은 곳을 지나감).
/// 같은 순간에 ImpactScreenFlow (화면 전체 불꽃) 도 함께 나옴.
/// </summary>
public class ImpactSlashOverlay : MonoBehaviour
{
    const string Sheet = "ErnStray_SkillTripleSlashIlseom";
    const int Cols = 3, Rows = 2;
    const float Fps = 20f;                  // 6프레임 → 0.3초
    const float SizeOfDiagonal = 0.9f;      // [임시] 칸 한 변 / 화면 대각선 (참격 길이가 칸 가로의 약 1.27배라 대각선보다 길어짐)

    Texture2D tex;
    Rect rect;
    float born;

    public static void Spawn(Vector2 worldPos)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var tex = SpriteLib.Load(Sheet);
        if (tex == null) return;
        var go = new GameObject("ImpactSlashOverlay");
        var o = go.AddComponent<ImpactSlashOverlay>();
        o.tex = tex;
        o.born = Time.unscaledTime;
        Vector3 c = cam.WorldToScreenPoint(worldPos);
        float side = Mathf.Sqrt((float)Screen.width * Screen.width + (float)Screen.height * Screen.height) * SizeOfDiagonal;
        o.rect = new Rect(c.x - side * 0.5f, Screen.height - c.y - side * 0.5f, side, side);   // GUI 는 위가 0
    }

    void Update()
    {
        if (Time.unscaledTime - born >= Cols * Rows / Fps) Destroy(gameObject);
    }

    void OnGUI()
    {
        if (tex == null) return;
        int f = Mathf.FloorToInt((Time.unscaledTime - born) * Fps);
        if (f < 0 || f >= Cols * Rows) return;
        int col = f % Cols, row = f / Cols;   // 시트 위쪽이 0행
        var uv = new Rect(col / (float)Cols, 1f - (row + 1) / (float)Rows, 1f / Cols, 1f / Rows);
        GUI.depth = -500;
        // 좌우 반전 (2026-11-20 사용자 요청): 시트의 왼쪽 아래 → 오른쪽 위 대각선이 오른쪽 아래 → 왼쪽 위가 됨. 칸 중심 기준으로 뒤집어 위치는 그대로
        var prev = GUI.matrix;
        GUIUtility.ScaleAroundPivot(new Vector2(-1f, 1f), rect.center);
        GUI.DrawTextureWithTexCoords(rect, tex, uv, true);
        GUI.matrix = prev;
    }
}

/// <summary>
/// 임팩트 프레임이 끝날 때 화면 전체에 깔리는 불꽃 (2026-11-20 사용자 요청: 파티클과 함께 나올 화면 전체 표기용 이펙트).
/// 시트 ErnStray_ScreenImpact: 4열 x 3행, 칸 960x540 (16:9), 마지막 칸은 비어 있음 → 12칸 재생.
/// 칸 하나가 화면 한 장이라 화면 전체에 늘려 그림 (모서리에 붙어 흘러가는 모양). 월드 위치와 무관, 카메라 확대와도 무관.
/// </summary>
public class ImpactScreenFlow : MonoBehaviour
{
    const string Sheet = "ErnStray_ScreenImpact";
    const int Cols = 4, Rows = 3;
    const float Fps = 30f;   // 미리보기 GIF 기준 (칸당 약 0.033초 → 전체 약 0.4초)

    Texture2D tex;
    float born;

    public static void Spawn()
    {
        var tex = SpriteLib.Load(Sheet);
        if (tex == null) return;
        var go = new GameObject("ImpactScreenFlow");
        var o = go.AddComponent<ImpactScreenFlow>();
        o.tex = tex;
        o.born = Time.unscaledTime;
    }

    void Update()
    {
        if (Time.unscaledTime - born >= Cols * Rows / Fps) Destroy(gameObject);
    }

    void OnGUI()
    {
        if (tex == null) return;
        int f = Mathf.FloorToInt((Time.unscaledTime - born) * Fps);
        if (f < 0 || f >= Cols * Rows) return;
        int col = f % Cols, row = f / Cols;   // 시트 위쪽이 0행
        var uv = new Rect(col / (float)Cols, 1f - (row + 1) / (float)Rows, 1f / Cols, 1f / Rows);
        GUI.depth = -501;   // 일직선 참격 (-500) 보다 뒤
        GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), tex, uv, true);
    }
}
