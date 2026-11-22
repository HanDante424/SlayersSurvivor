using UnityEngine;

/// <summary>
/// 참격 시트 (ErnStray_TripleSlashFlame, 4x3 칸) 를 직접 곡선으로 휘어 그리는 이펙트 (2026-11-21 사용자 요청).
/// 한 칸을 가로로 잘게 나눈 띠 메시로 그리고, 띠마다 세로로 옮겨서 그림의 원래 호 (왼쪽 아래 → 오른쪽 위) 를 먼저 곧게 편 뒤
/// 원하는 곡선 (위로 볼록한 포물선, sag 만큼) 으로 다시 휨. RibbonTornadoFx 와 같은 "그림을 메시로 구부리기" 방식.
/// 휨 정도 sag (칸 가로 단위, 양수 = 위로 볼록 / 음수 = 아래로 볼록) · 길이 · 회전은 Spawn 인자.
/// </summary>
public class BentSlashFx : MonoBehaviour
{
    // 시트 사양 (가장 큰 프레임에서 측정): 칸 500x500, 호가 차지하는 가로 범위 u = 0.072 ~ 0.812 (칸 가로 대비)
    const float ArtU0 = 0.072f, ArtU1 = 0.812f;
    // 그림 호의 중심선 높이 v (칸 세로 대비, 위가 0) = 3차 다항식 (u): c3 u^3 + c2 u^2 + c1 u + c0 (측정값)
    const float C3 = -0.350988f, C2 = -0.086952f, C1 = -0.268867f, C0 = 0.698480f;
    const int Columns = 28;     // 가로로 나누는 띠 수

    Mesh mesh;
    Material mat;
    Vector2[] uvs;
    int cols, rows, frameCount;
    float fps, t;
    int shown = -1;

    static float Center(float u) { u = Mathf.Clamp(u, ArtU0, ArtU1); return ((C3 * u + C2) * u + C1) * u + C0; }

    /// <param name="tex">시트 (칸 cols x rows, 프레임 순서 = 행 (위→아래) → 열 (왼→오))</param>
    /// <param name="frames">실제 프레임 수 (마지막 빈 칸 제외)</param>
    /// <param name="width">한 칸 가로 (월드)</param>
    /// <param name="sag">호의 볼록한 정도 (칸 가로 단위). 0 = 곧게 펴짐</param>
    public static BentSlashFx Spawn(Texture2D tex, int cols, int rows, int frames, float fps, Vector2 pos, float angleDeg,
                                    float width, float sag, int sortingOrder, bool flipX = false)
    {
        if (tex == null) return null;
        var go = new GameObject("FX_BentSlash");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        go.transform.localScale = new Vector3(flipX ? -1f : 1f, 1f, 1f);
        var fx = go.AddComponent<BentSlashFx>();
        fx.cols = cols; fx.rows = rows; fx.frameCount = frames; fx.fps = fps;

        // 띠 메시: 열마다 위 · 아래 꼭짓점 2개
        int n = Columns + 1;
        var v = new Vector3[n * 2];
        fx.uvs = new Vector2[n * 2];
        var tri = new int[Columns * 6];
        float um = (ArtU0 + ArtU1) * 0.5f, hw = (ArtU1 - ArtU0) * 0.5f;
        float cm = Center(um);
        for (int i = 0; i < n; i++)
        {
            float u = (float)i / Columns;
            float uu = (u - um) / hw;
            float g = sag * Mathf.Clamp01(1f - uu * uu);            // 원하는 곡선 (가운데가 가장 볼록, 양 끝은 0)
            float shift = (Center(u) - cm) + g;                      // 원래 호를 펴고 (중심선 - 가운데 높이) 새 곡선을 더함 (위가 + 인 칸 세로 단위)
            // 회전 · 위치의 중심 = 호 양 끝을 잇는 선의 가운데 (칸 가운데가 아님). 칸 가운데는 호 가운데에서 떨어져 있어서
            // 방향 (특히 반대쪽) 에 따라 참격들이 한곳에 모이지 않았음 (사용자 2026-11-22: 왼쪽을 보고 쓰면 안 모임)
            float x = (u - um) * width;
            v[i * 2] = new Vector3(x, (cm + shift) * width, 0f);            // 칸 위쪽
            v[i * 2 + 1] = new Vector3(x, (cm - 1f + shift) * width, 0f);   // 칸 아래쪽
        }
        for (int i = 0; i < Columns; i++)
        {
            int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
            int k = i * 6;
            tri[k] = a; tri[k + 1] = c; tri[k + 2] = b;
            tri[k + 3] = b; tri[k + 4] = c; tri[k + 5] = d;
        }
        fx.mesh = new Mesh { name = "BentSlash" };
        fx.mesh.vertices = v;
        fx.mesh.triangles = tri;
        go.AddComponent<MeshFilter>().sharedMesh = fx.mesh;
        fx.mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex };
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = fx.mat;
        mr.sortingOrder = sortingOrder;
        fx.ShowFrame(0);
        return fx;
    }

    void ShowFrame(int f)
    {
        if (f == shown) return;
        shown = f;
        int r = f / cols, c = f % cols;
        float u0 = (float)c / cols, u1 = (float)(c + 1) / cols;
        float vTop = 1f - (float)r / rows, vBot = 1f - (float)(r + 1) / rows;   // 텍스처 v 는 아래가 0
        int n = Columns + 1;
        for (int i = 0; i < n; i++)
        {
            float u = Mathf.Lerp(u0, u1, (float)i / Columns);
            uvs[i * 2] = new Vector2(u, vTop);
            uvs[i * 2 + 1] = new Vector2(u, vBot);
        }
        mesh.uv = uvs;
    }

    void Update()
    {
        t += Time.deltaTime;
        int f = (int)(t * fps);
        if (f >= frameCount) { Destroy(gameObject); return; }
        ShowFrame(f);
    }

    void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (mat != null) Destroy(mat);
    }
}
