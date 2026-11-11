using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// 스킬 · 특수 강화 설명 (스킬 선택창 표기 순서: 제목 → 키워드 → 설명 → 세부 스펙).
/// 문장 안의 '키워드' (작은따옴표) 는 강조 색으로 바뀜. 예: "'초기 피해량' 10" → 초기 피해량(강조) 10
/// 기준: 2026-11-11 사용자 전달 스킬 설명 · 키워드 중간본
/// </summary>
public class SkillText
{
    /// <summary>키워드 태그 (작은따옴표 없이). 예: { "자동", "직접 사용", "직접 피해", "근거리" }</summary>
    public string[] keywords;
    /// <summary>설명 (여러 줄 가능, '키워드' 강조)</summary>
    public string desc;
    /// <summary>세부 스펙 줄. 예: { "'초기 피해량' 10", "'쿨타임' 2초" }</summary>
    public string[] specs;

    public SkillText(string[] keywords, string desc, params string[] specs)
    {
        this.keywords = keywords;
        this.desc = desc;
        this.specs = specs;
    }

    // 표기 색
    public const string KeywordTagColor = "#9AD0FF";   // 키워드 태그
    public const string KeywordColor = "#F2BF4C";      // 문장 안 '키워드'
    public const string SpecColor = "#DDDDDD";

    static readonly Regex Quoted = new Regex("'([^']+)'");

    /// <summary>문장 안 '키워드' → 강조 색 (따옴표 제거)</summary>
    public static string Highlight(string s) =>
        string.IsNullOrEmpty(s) ? "" : Quoted.Replace(s, m => $"<color={KeywordColor}>{m.Groups[1].Value}</color>");

    /// <summary>키워드 태그 한 줄: [자동] [직접 사용] …</summary>
    public static string Tags(string[] keywords)
    {
        if (keywords == null || keywords.Length == 0) return "";
        var sb = new StringBuilder();
        foreach (var k in keywords)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append($"<color={KeywordTagColor}>[{k}]</color>");
        }
        return sb.ToString();
    }

    /// <summary>카드 본문 (제목 아래): 키워드 → 설명 → 세부 스펙. 빈 부분은 건너뜀</summary>
    public string Body()
    {
        var sb = new StringBuilder();
        string tags = Tags(keywords);
        if (tags.Length > 0) sb.Append("<size=14>").Append(tags).Append("</size>");
        if (!string.IsNullOrEmpty(desc))
        {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append(Highlight(desc));
        }
        if (specs != null && specs.Length > 0)
        {
            if (sb.Length > 0) sb.Append("\n\n");   // 설명과 스펙 사이 빈 줄 1개 (스펙만 있으면 빈 줄 없이 시작)
            for (int i = 0; i < specs.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append($"<size=15><color={SpecColor}>· </color>").Append(Highlight(specs[i])).Append("</size>");
            }
        }
        return sb.ToString();
    }

    /// <summary>한 줄 요약 (HUD · 치트 패널 등 좁은 곳): 설명 첫 줄</summary>
    public string Short => string.IsNullOrEmpty(desc) ? "" : desc.Split('\n')[0].Replace("'", "");
}
