using UnityEngine;

/// <summary>
/// 감각 조정용 설정 파일 모음. Resources/Config/ 에서 불러오며, 파일이 없으면 기본값으로 동작합니다.
/// </summary>
public static class Tuning
{
    static PlayerConfig ernStray;
    static EnemyConfig enemy;
    static SkillTuning skills;

    /// <summary>스킬 등급 → 숫자 임시 환산표</summary>
    public static SkillTuning Skills => skills != null ? skills : (skills = Load<SkillTuning>("Config/SkillTuning"));

    public static PlayerConfig ErnStray => ernStray != null ? ernStray : (ernStray = Load<PlayerConfig>("Config/ErnStrayConfig"));
    public static EnemyConfig Enemy => enemy != null ? enemy : (enemy = Load<EnemyConfig>("Config/EnemyConfig"));

    static T Load<T>(string path) where T : ScriptableObject
    {
        var a = Resources.Load<T>(path);
        if (a != null) return a;
        Debug.LogWarning($"[Tuning] Resources/{path}.asset 이 없어 기본값을 사용합니다.");
        return ScriptableObject.CreateInstance<T>();
    }
}
