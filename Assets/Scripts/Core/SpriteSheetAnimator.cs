using UnityEngine;

public enum Facing4 { Down = 0, Left = 1, Right = 2, Up = 3 }

/// <summary>
/// 4방향 스프라이트 시트 애니메이터.
/// 시트 규칙: 행(위→아래) = 아래, 왼쪽, 오른쪽, 위 / 열 = 프레임
/// Resources/Sprites/{sheetName}.png 를 런타임에 잘라 사용합니다.
/// </summary>
public class SpriteSheetAnimator : MonoBehaviour
{
    public float fps = 12f;

    Sprite[,] frames;   // [direction, frame]
    int cols;
    SpriteRenderer sr;
    Facing4 facing = Facing4.Down;
    float time;
    bool moving;

    /// <summary>시트를 불러와 설정. 없으면 false.</summary>
    /// <param name="cellWorldSize">한 칸이 월드에서 차지할 크기(유닛)</param>
    /// <param name="pivotY">칸 안에서 transform 위치가 될 세로 지점 (0=아래, 1=위)</param>
    public bool Load(string sheetName, int rows, int columns, float cellWorldSize, float pivotY)
    {
        var tex = Resources.Load<Texture2D>("Sprites/" + sheetName);
        if (tex == null) return false;

        cols = columns;
        float cw = tex.width / (float)columns, ch = tex.height / (float)rows;
        float ppu = cw / cellWorldSize;
        frames = new Sprite[rows, columns];
        for (int r = 0; r < rows; r++)
        for (int c = 0; c < columns; c++)
        {
            // 텍스처 좌표는 아래에서 위로 → 시트의 r행은 (rows-1-r)
            var rect = new Rect(c * cw, (rows - 1 - r) * ch, cw, ch);
            frames[r, c] = Sprite.Create(tex, rect, new Vector2(0.5f, pivotY), ppu, 0, SpriteMeshType.FullRect);
        }

        sr = GetComponent<SpriteRenderer>();
        if (sr == null) sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0, 0];
        return true;
    }

    /// <summary>매 프레임 이동 방향을 넘겨주세요. 0 벡터면 정지.</summary>
    public void SetMove(Vector2 dir)
    {
        moving = dir.sqrMagnitude > 0.01f;
        if (!moving) return;
        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y) + 0.01f) facing = dir.x < 0 ? Facing4.Left : Facing4.Right;
        else facing = dir.y < 0 ? Facing4.Down : Facing4.Up;
    }

    void Update()
    {
        if (frames == null) return;
        int f;
        if (moving)
        {
            time += Time.deltaTime;
            f = (int)(time * fps) % cols;
        }
        else
        {
            // 정지 상태 전용 프레임이 없으므로 첫 프레임 고정
            time = 0;
            f = 0;
        }
        sr.sprite = frames[(int)facing, f];
    }
}
