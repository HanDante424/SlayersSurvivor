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
    /// <summary>스킬 선택창 표기: 키워드 → 설명 → 세부 스펙 (제목 = name)</summary>
    public SkillText text;
    /// <summary>한 줄 요약 (좁은 곳용)</summary>
    public string summary => text?.Short ?? "";
}

/// <summary>
/// 캐릭터별 스킬 목록. 설명 · 키워드 · 세부 스펙 = 2026-11-11 사용자 전달 중간본 (최신 기준).
/// 패시브는 화염의 검 · 옥염 · 흡수 가열, 두 번째 궁극기(U02)는 미설계라 없음.
/// </summary>
public static class SkillCatalog
{
    public static readonly List<SkillEntry> ErnStray = new List<SkillEntry>
    {
        // 자동 6종
        E("A01", "파이어 슬래시", "Fire Slash", SkillKind.Auto, typeof(FireSlash), new SkillText(
            new[] { "자동", "직접 사용", "직접 피해", "근거리" },
            "바라보는 방향으로 화염을 휘둘러 피해를 줍니다.",
            "'초기 피해량' 10", "'쿨타임' 2초")),
        E("A02", "솔라 슬래시", "Solar Slash", SkillKind.Auto, typeof(SolarSlash), new SkillText(
            new[] { "자동", "직접 사용", "직접 피해", "근거리", "넉백" },
            "플레이어 중심으로 전방위를 벱니다.\n이후 폭발하며 주변 적을 밀쳐냅니다.",
            "베기 '초기 피해량' 5", "폭발 '초기 피해량' 12", "'쿨타임' 5초")),
        E("A03", "파이어 볼", "Fire Ball", SkillKind.Auto, typeof(FireBall), new SkillText(
            new[] { "자동", "직접 사용", "직접 피해", "원거리", "투사체" },
            "가까운 적에게 파이어 볼을 발사합니다.",
            "'초기 피해량' 7", "'쿨타임' 3초")),
        E("A04", "파이어 버스터", "Fire Buster", SkillKind.Auto, typeof(FireBuster), new SkillText(
            new[] { "자동", "직접 사용", "직접 피해", "근거리", "넉백" },
            "거대한 폭발을 일으켜 주변 적을 밀쳐냅니다.",
            "'초기 피해량' 20", "'쿨타임' 10초")),
        E("A05", "파이어 블레이드", "Fire Blade", SkillKind.Auto, typeof(FireBlade), new SkillText(
            new[] { "자동", "직접 사용", "직접 피해", "근거리" },
            "적을 여러번 베어냅니다.\n이후 베어진 적에게서 폭발이 일어납니다.\n폭발은 주변 모든 적에게 피해를 입힙니다.",
            "베기 '초기 피해량' 3", "베기 '공격 횟수' 5", "폭발 '초기 피해량' 20", "'쿨타임' 15초")),
        E("A06", "파이어 스톰", "Fire Storm", SkillKind.Auto, typeof(FireStorm), new SkillText(
            new[] { "자동", "직접 사용", "직접 피해", "간접 사용", "지속 피해", "근거리", "넉백" },
            "플레이어 중심으로 화염이 소용돌이칩니다.\n화염 소용돌이에서는 적들이 느려집니다.\n이후 화염 소용돌이가 폭발하며 주변 적을 밀쳐냅니다.",
            "화염 소용돌이 '간접 피해' '지속 피해' '초기 피해량' 2", "화염 소용돌이 '지속 시간' 3초",
            "폭발 '직접 피해' '초기 피해량' 15", "'쿨타임' 14초")),

        // 수동 5종
        E("M01", "트리플 슬래시", "Triple Slash", SkillKind.Manual, typeof(TripleSlash), new SkillText(
            new[] { "수동", "직접 사용", "직접 피해", "근거리", "피해" },
            "빠르게 3번 베어냅니다.",
            "'초기 피해량' 8", "'공격 횟수' 3", "'쿨타임' 8초")),
        E("M02", "파이어 차지", "Fire Charge", SkillKind.Manual, typeof(FireCharge), new SkillText(
            new[] { "수동", "직접 사용", "직접 피해", "근거리", "이동", "넉백", "무적" },
            "주변 적을 밀치며 돌진합니다.\n사용 중 무적입니다.",
            "'초기 피해량' 4", "'쿨타임' 15초")),
        E("M03", "오버 드라이브 플레임 슬래시", "Overdrive Flame Slash", SkillKind.Manual, typeof(OverFlameSlash), new SkillText(
            new[] { "수동", "직접 사용", "직접 피해", "근거리", "기절", "군중제어" },
            "화염의 검을 내려쳐 적들을 기절시킵니다.\n이후 폭발하며 피해를 입힙니다.",
            "화염의 검 '초기 피해량' 20", "화염의 검 '기절' '지속 시간' 2초", "폭발 '딜레이' 1초", "폭발 '초기 피해량' 50", "'쿨타임' 20초")),
        E("M04", "파이어 브레이커", "Fire Breaker", SkillKind.Manual, typeof(FireBreaker), new SkillText(
            new[] { "수동", "직접 사용", "직접 피해", "근거리", "기절", "군중제어" },
            "화염의 검을 내리치며 폭발시켜 주변 적들을 기절시킵니다.\n잠시 후 폭발이 한번 더 발생합니다.",
            "'초기 피해량' 10", "'기절' '지속 시간' 1초", "폭발 '딜레이' 2초", "'쿨타임' 10초")),
        E("M05", "파이어 엑스큐션", "Fire Execution", SkillKind.Manual, typeof(FireExecution), new SkillText(
            new[] { "수동", "직접 사용", "직접 피해", "근거리", "고정", "피해" },
            "빠른 속도로 검을 휘둘러 주변 적들을 베어냅니다.",
            "'초기 피해량' 5", "'공격 횟수' 30", "'쿨타임' 30초")),

        // 이동 1종
        E("D01", "파이어 스트림", "Fire Stream", SkillKind.Movement, typeof(FireStream), new SkillText(
            new[] { "수동", "직접 사용", "직접 피해", "근거리", "이동", "스택", "무적" },
            "빠르게 돌진합니다.\n이동 경로의 적들에게 피해를 입힙니다.\n사용 중 무적입니다.",
            "'초기 피해량' 3", "'스택' 3", "'쿨타임' 15초")),

        // 패시브 (전투 통합 기획서 v1.0 8~10장)
        E("P01", "화염의 검", "Flame Sword", SkillKind.Passive, typeof(FlameSword), new SkillText(
            new[] { "패시브", "간접 사용", "지속 피해" },
            "'직접 피해'를 입힐때마다 화상을 적용합니다.",
            "'화상' '초기 피해량' 2", "'화상' '지속 시간' 3초", "'화상' '스택' 5")),
        E("P02", "옥염", "Hellfire", SkillKind.Passive, typeof(Hellfire), new SkillText(
            new[] { "패시브" },
            "피해량이 증가합니다",
            "'최종 피해량' +30%")),
        E("P03", "흡수 가열", "Heat Absorption", SkillKind.Passive, typeof(HeatAbsorption), new SkillText(
            new[] { "패시브" },
            "주변의 화상 상태의 적마다 이로운 효과를 얻습니다.",
            "'스택' 당 '최종 피해량' +5%", "'스택' 당 '쿨타임 감소' +1%")),

        // 궁극기 1종 (U02 미설계)
        E("U01", "블레이징 스톰", "Blazing Storm", SkillKind.Ultimate, typeof(BlazingStorm), new SkillText(
            new[] { "궁극기", "직접 사용", "직접 피해", "근거리", "무적", "기절" },
            "화염의 소용돌이를 내려쳐 모든 적에게 피해를 입힙니다.\n사용 중 무적입니다.",
            "'초기 피해량' 500")),
    };

    /// <summary>공통 기본 강화 3종 표기 (제목 · 세부 스펙)</summary>
    public static readonly (string title, string spec)[] BasicUpgrades =
    {
        ("공격력 증가", "'피해량' +10%"),
        ("범위 증가", "'범위' +10%"),
        ("쿨타임 감소", "'쿨타임 감소' +5%"),
    };

    /// <summary>
    /// [임시] 레벨업 후보에 나오는 스킬 (이미지 리소스가 있는 것만): 파이어 슬래시 · 솔라 슬래시 · 트리플 슬래시 + 화염의 검 · 옥염 · 흡수 가열(패시브).
    /// 나머지 스킬은 코드는 남아 있지만 후보에 나오지 않음. 리소스가 추가되면 여기에 id 를 더하면 됨.
    /// </summary>
    public static readonly HashSet<string> LevelUpPool = new HashSet<string> { "A01", "A02", "M01", "P01", "P02", "P03" };

    static SkillEntry E(string id, string name, string en, SkillKind kind, Type type, SkillText text)
        => new SkillEntry { id = id, name = name, nameEn = en, kind = kind, type = type, text = text };

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
