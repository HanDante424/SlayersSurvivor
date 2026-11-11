using System.Collections.Generic;

/// <summary>캐릭터 선택 가능 상태</summary>
public enum CharacterAvailability
{
    /// <summary>외형 · 스킬 모두 구현</summary>
    Playable,
    /// <summary>외형만 있음 (스킬은 다른 캐릭터 것으로 임시 플레이)</summary>
    VisualOnly,
    /// <summary>기획 · 리소스 준비 중 (선택 불가)</summary>
    Locked,
}

/// <summary>캐릭터 선택 화면에 보일 캐릭터 1명</summary>
public class CharacterInfo
{
    public string id;               // 스프라이트 시트 접두어 (Resources/Sprites/{id}_Idle · _Run)
    public string name, nameEn;
    public string element, style;   // 속성 · 전투 방식
    public string range, speed;     // 공격 범위 · 공격 속도
    public string feature;          // 특징
    public string note;             // 상태 안내 (선택 화면 하단)
    public CharacterAvailability availability;
    public Player.CharacterVisual visual;
    /// <summary>이 캐릭터의 스킬 목록 · 시작 시 지급 스킬 id</summary>
    public List<SkillEntry> catalog;
    public string startSkill;
}

/// <summary>
/// 플레이어블 캐릭터 목록 (GDD v0.1 1.2 · 통합 기획서 v1.0 1.2 기준).
/// 캐릭터를 추가하려면 이 목록에 한 줄 + Player.CharacterVisual + 스프라이트 시트.
/// </summary>
public static class CharacterRoster
{
    public static readonly List<CharacterInfo> All = new List<CharacterInfo>
    {
        new CharacterInfo
        {
            id = "ErnStray", name = "에른 스트레이", nameEn = "Ern Stray",
            element = "화염", style = "검술", range = "중간", speed = "빠름", feature = "고속 검술 중심 · 연속 검격",
            note = "스킬 · 패시브 구현 완료",
            availability = CharacterAvailability.Playable, visual = Player.ErnStrayVisual,
            catalog = SkillCatalog.ErnStray, startSkill = "A01",
        },
        new CharacterInfo
        {
            id = "Lenia", name = "레니아", nameEn = "Lenia",
            element = "기획 중", style = "터렛 · 공구 투척", range = "-", speed = "-", feature = "터렛 설치 · 스패너 / 망치 투척 · 포격",
            note = "스킬 9종 구현 (터렛 강화 · 과부화는 연출만, 공격 모션 없음)",
            availability = CharacterAvailability.Playable, visual = Player.LeniaVisual,
            catalog = SkillCatalog.Lenia, startSkill = "LA01",
        },
        new CharacterInfo
        {
            id = "StiumCaterrek", name = "스티움 카터렉", nameEn = "Stium Caterrek",
            element = "어둠", style = "특대검", range = "중간", speed = "느림", feature = "높은 위력 · 높은 내구성",
            note = "준비 중 — 스킬셋 미설계",
            availability = CharacterAvailability.Locked,
        },
        new CharacterInfo
        {
            id = "MelValentina", name = "멜 발렌티나", nameEn = "Mel Valentina",
            element = "전 속성", style = "마법", range = "넓음", speed = "빠름", feature = "높은 위력 · 낮은 내구성",
            note = "준비 중 — 스킬셋 미설계",
            availability = CharacterAvailability.Locked,
        },
    };

    /// <summary>이번 판에 고른 캐릭터 (타이틀 → 게임으로 넘어갈 때 유지)</summary>
    public static CharacterInfo Selected = All[0];
}
