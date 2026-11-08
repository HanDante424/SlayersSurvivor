using UnityEngine;

/// <summary>
/// 모든 화면이 함께 쓰는 IMGUI 스타일. OnGUI 안에서만 호출하세요.
/// 나중에 uGUI/UI Toolkit으로 옮길 때 이 파일 기준으로 색과 크기를 맞추면 됩니다.
/// </summary>
public static class UIStyles
{
    public static readonly Color Accent = new Color(0.95f, 0.75f, 0.3f);
    public static readonly Color Dim = new Color(0, 0, 0, 0.65f);

    public static GUIStyle Label, Small, Center, Big, Title, Button, MenuButton;
    public static Texture2D White => Texture2D.whiteTexture;
    static Font font;

    public static void Init()
    {
        if (Label != null) return;
        font = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "맑은 고딕", "Arial" }, 20);

        Label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 20, normal = { textColor = Color.white } };
        Small = new GUIStyle(Label) { fontSize = 16, wordWrap = true, normal = { textColor = new Color(0.85f, 0.85f, 0.85f) } };
        Center = new GUIStyle(Label) { alignment = TextAnchor.MiddleCenter };
        Big = new GUIStyle(Label) { fontSize = 44, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        Title = new GUIStyle(Big) { fontSize = 72, normal = { textColor = Accent } };
        Button = new GUIStyle(GUI.skin.button)
        {
            font = font, fontSize = 20, alignment = TextAnchor.UpperLeft,
            padding = new RectOffset(16, 16, 14, 14), wordWrap = true, richText = true
        };
        MenuButton = new GUIStyle(GUI.skin.button)
        {
            font = font, fontSize = 26, alignment = TextAnchor.MiddleCenter, richText = true
        };
    }

    public static void Overlay(Color c)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), White);
        GUI.color = Color.white;
    }

    public static void Bar(Rect r, float t, Color fill)
    {
        GUI.color = new Color(0, 0, 0, 0.6f); GUI.DrawTexture(r, White);
        GUI.color = fill; GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(t), r.height), White);
        GUI.color = Color.white;
    }
}
