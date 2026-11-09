using System;
using System.Collections.Generic;

/// <summary>스킬 한 종류의 정적 정보</summary>
public class SkillEntry
{
    public string id;          // 기획서 코드 (A01, M01 ...)
    public string name;
    public string nameEn;
    public SkillKind kind;
    public Type type;          // 구현 컴포넌트
    public string summary;     // 레벨업 선택지에 보일 설명 (기획서 내용 그대로)
}

/// <summary>
/// 캐릭터별 스킬 목록. 기획서(Docs/Design/GDD_v0.1.md) 4~7장 기준.
/// 패시브는 기획 미설계라 비어 있고, 두 번째 궁극기(U02)도 미설계라 없음.
/// </summary>
public static class SkillCatalog
{
    public static readonly List<SkillEntry> ErnStray = new List<SkillEntry>
    {
        // 자동 6종
        E("A01", "파이어 슬래시", "Fire Slash", SkillKind.Auto, typeof(FireSlash), "화염 검격. 범위 좁음 · 쿨타임 짧음 · 피해 낮음"),
        E("A02", "솔라 슬래시", "Solar Slash", SkillKind.Auto, typeof(SolarSlash), "전방위 베기. 범위 중간 · 쿨타임 중간 · 피해 중간"),
        E("A03", "파이어 볼", "Fire Ball", SkillKind.Auto, typeof(FireBall), "화염 투사체. 사거리 김 · 범위 좁음 · 쿨타임 짧음 · 피해 낮음"),
        E("A04", "파이어 버스터", "Fire Buster", SkillKind.Auto, typeof(FireBuster), "주변 적 밀치기. 범위 중간 · 쿨타임 중간 · 피해 중간"),
        E("A05", "파이어 블레이드", "Fire Blade", SkillKind.Auto, typeof(FireBlade), "범위 내 대상 다수 참격 후 폭발. 범위 중간 · 쿨타임 김 · 총 피해 높음"),
        E("A06", "파이어 스톰", "Fire Storm", SkillKind.Auto, typeof(FireStorm), "지속 피해 + 슬로우 후 폭발하며 밀치기. 쿨타임 중간 · 피해 중간"),

        // 수동 5종
        E("M01", "트리플 슬래시", "Triple Slash", SkillKind.Manual, typeof(TripleSlash), "3연속 베기. 범위 중간 · 피해 높음 · 쿨타임 중간"),
        E("M02", "파이어 차지", "Fire Charge", SkillKind.Manual, typeof(FireCharge), "돌진 이동기. 무적 · 주변 적 밀치기 · 피해 매우 낮음"),
        E("M03", "오버 플레임 슬래시", "Over Flame Slash", SkillKind.Manual, typeof(OverFlameSlash), "긴 사거리 일직선 베기 + 기절. 피해 매우 높음 · 쿨타임 김"),
        E("M04", "파이어 브레이커", "Fire Breaker", SkillKind.Manual, typeof(FireBreaker), "범위 피해 + 1초 기절, 2초 후 같은 위치 재발동"),
        E("M05", "파이어 엑스큐션", "Fire Execution", SkillKind.Manual, typeof(FireExecution), "전방위 공격. 피해 매우 높음 · 쿨타임 김 · 발동 중 이동 불가"),

        // 이동 1종
        E("D01", "파이어 스트림", "Fire Stream", SkillKind.Movement, typeof(FireStream), "3스택 고속 이동. 무적 · 피해 낮음"),

        // 궁극기 1종 (U02 미설계)
        E("U01", "블레이징 스톰", "Blazing Storm", SkillKind.Ultimate, typeof(BlazingStorm), "화면 전체 매우 높은 피해 후 모든 적 기절. 사용 중 무적"),
    };

    static SkillEntry E(string id, string name, string en, SkillKind kind, Type type, string summary)
        => new SkillEntry { id = id, name = name, nameEn = en, kind = kind, type = type, summary = summary };

    public static SkillEntry Find(List<SkillEntry> list, string id) => list.Find(e => e.id == id);

    public static string KindLabel(SkillKind k) => k switch
    {
        SkillKind.Auto => "자동",
        SkillKind.Manual => "수동",
        SkillKind.Movement => "이동",
        SkillKind.Passive => "패시브",
        SkillKind.Ultimate => "궁극기",
        _ => "",
    };
}
