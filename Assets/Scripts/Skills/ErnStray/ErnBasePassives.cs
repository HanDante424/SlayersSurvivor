using UnityEngine;

// 에른 스트레이 새 패시브 3종 — 방어 · 피해 대비 · 자원 획득 (2026-11-24 사용자)
// 기본 성능 + 고유 기본 강화 3종. 특수 강화: 세 패시브 모두 구현됨 (화염 보호막은 특수 조합 미정) (선택 화면에 "미설계" 확인 버튼만).

/// <summary>
/// P04 잿불 갑주: 최대 체력 +25% · 받는 피해량 -20% · 모든 회복량 +10%
/// 고유 기본 강화: 최대 체력 +5% / 받는 피해 -1% / 모든 회복량 +2%
/// 특수 강화 (2026-11-24 사용자):
///   Lv.5  다시 타오르는 불씨 (초당 체력 1 회복) · 두꺼운 장벽 (최대 체력 +25%) · 흘리기 (받는 피해 -5%)
///   Lv.10 연소 흡수 (화상 적 처치 시 체력 1 회복, 쿨타임 1초) · 불굴 (체력 30% 이하 받는 피해 -10%) · 타오르는 장갑 (수동 스킬 시전 중 받는 피해 -15%)
///   Lv.15 잿불 비축 · 불사의 화톳불 · 화염 진지 (체력 90% 이상 받는 피해 -15%)
///   조합 영원의 화로 = 불굴 + 불사의 화톳불
/// [해석] 설명의 "모든 회복량 +10%" 는 기존 기본 성능(체력 +25% · 받는 피해 -20%)에 추가한 것으로 처리.
/// [해석] 최대 체력 · 회복량 증가는 자기 안에서 합연산, 받는 피해 감소는 모두 서로 곱연산.
/// [해석] "수동 스킬" = 수동 슬롯 스킬(트리플 슬래시 · 파이어 차지 · 오버 드라이브 플레임 슬래시 · 파이어 브레이커 · 파이어 엑스큐션). 이동 스킬(파이어 스트림) · 궁극기는 제외.
/// [해석] 잿불 비축의 "받는 피해" = 받는 피해 배율 적용 후, 보호막 흡수 전 양. 쿨타임 중에는 누적하지 않음. 누적이 차면 0 으로 되돌림.
/// [임시] 잿불 비축 넉백 반경 5 · 거리 4, 시각 효과 없음.
/// </summary>
public class EmberArmor : Skill
{
    const float BaseMaxHp = 0.25f, BaseTakenCut = 0.20f, BaseHeal = 0.10f;
    const float UpMaxHp = 0.05f, UpTakenCut = 0.01f, UpHeal = 0.02f;

    // 특수 강화 id
    public const string Rekindle = "AR5A", Thick = "AR5B", Deflect = "AR5C";
    public const string Absorb = "AR10A", Resolute = "AR10B", Plating = "AR10C";
    public const string Hoard = "AR15A", Phoenix = "AR15B", Fortress = "AR15C";
    public const string ComboHearth = "ARC1";   // 영원의 화로

    // 특수 강화 수치
    const float RekindleHeal = 1f;                  // 초당 체력 1
    const float ThickHp = 0.25f;                    // 최대 체력 +25%
    const float DeflectCut = 0.05f;                 // 받는 피해 -5%
    const float AbsorbHeal = 1f, AbsorbCooldown = 1f;
    const float ResoluteHpRatio = 0.30f, ResoluteCut = 0.10f;
    const float PlatingCut = 0.15f;
    const float HoardRatio = 0.50f, HoardHeal = 0.25f, HoardCooldown = 20f;
    const float HoardRadius = 5f, HoardKnock = 4f;  // [임시] 넉백 반경 · 거리
    const float PhoenixHpRatio = 0.30f, PhoenixInvuln = 3f, PhoenixCooldown = 90f;
    const float FortressHpRatio = 0.90f, FortressCut = 0.15f;
    // 영원의 화로
    const float HearthRegen = 2f, HearthStillRegen = 3f, HearthStillCut = 0.10f, HearthStillTime = 3f;

    const float StillEpsilon = 0.001f;   // 한 프레임에 이만큼 넘게 움직이면 이동 중

    int hpUps, takenUps, healUps;
    float appliedHpMul = 1f, appliedTakenMul = 1f, appliedHealMul = 1f;
    float absorbReadyAt, hoardReadyAt, reviveReadyAt;
    float hoardStored;          // 잿불 비축: 누적 피해
    float stillTime;            // 가만히 있은 시간 (영원의 화로)
    Vector2 lastPos;

    bool Hearth => HasCombo(ComboHearth);

    void OnEnable()
    {
        Apply();
        var p = P;
        if (p != null)
        {
            p.TakenMods.Add(DynamicTaken);
            p.DamageReceived = OnDamageReceived;
            p.ReviveHook = TryRevive;
            lastPos = p.transform.position;
        }
        Enemy.Killed += OnKilled;
    }

    void OnDisable()
    {
        Enemy.Killed -= OnKilled;
        var p = P;
        if (p != null)
        {
            p.TakenMods.Remove(DynamicTaken); p.DamageReceived = null; p.ReviveHook = null;
        }
        Remove();
    }

    float HpMul => 1f + BaseMaxHp + UpMaxHp * hpUps + (HasSpecial(Thick) ? ThickHp : 0f);
    float TakenMul => (1f - BaseTakenCut) * Mathf.Pow(1f - UpTakenCut, takenUps) * (HasSpecial(Deflect) ? 1f - DeflectCut : 1f);
    float HealMul => 1f + BaseHeal + UpHeal * healUps;

    /// <summary>현재 강화 상태로 플레이어 수치를 갱신 (이전 몫을 나누고 새 몫을 곱함)</summary>
    void Apply()
    {
        var p = P;
        if (p == null) return;
        float before = p.maxHp;
        p.maxHp = p.maxHp / appliedHpMul * HpMul;
        p.hp = Mathf.Min(p.maxHp, p.hp + Mathf.Max(0f, p.maxHp - before));   // 늘어난 만큼 현재 체력도 함께
        p.damageTakenMul = p.damageTakenMul / appliedTakenMul * TakenMul;
        p.healMul = p.healMul / appliedHealMul * HealMul;
        appliedHpMul = HpMul; appliedTakenMul = TakenMul; appliedHealMul = HealMul;
    }

    void Remove()
    {
        var p = P;
        if (p == null) return;
        p.maxHp /= appliedHpMul;
        p.hp = Mathf.Min(p.hp, p.maxHp);
        p.damageTakenMul /= appliedTakenMul;
        p.healMul /= appliedHealMul;
        appliedHpMul = appliedTakenMul = appliedHealMul = 1f;
    }

    protected override void OnSpecialUpgrade(int lv, string id) => Apply();

    /// <summary>수동 스킬을 시전 중인지 (타오르는 장갑)</summary>
    bool ManualCasting()
    {
        var p = P;
        if (p == null) return false;
        foreach (var s in p.skills)
            if (s.Entry.kind == SkillKind.Manual && s is ActiveSkill a && a.IsCastingNow) return true;
        return false;
    }

    /// <summary>상황별 받는 피해 배율 (피격 순간 계산)</summary>
    float DynamicTaken()
    {
        var p = P;
        if (p == null) return 1f;
        float m = 1f;
        float ratio = p.maxHp > 0f ? p.hp / p.maxHp : 1f;
        if (HasSpecial(Resolute) && ratio <= ResoluteHpRatio) m *= 1f - ResoluteCut;
        if (HasSpecial(Plating) && ManualCasting()) m *= 1f - PlatingCut;
        if (HasSpecial(Fortress) && ratio >= FortressHpRatio) m *= 1f - FortressCut;
        if (Hearth && stillTime >= HearthStillTime) m *= 1f - HearthStillCut;
        return m;
    }

    /// <summary>연소 흡수: 화상 상태 적 처치 시 체력 회복 (쿨타임 1초)</summary>
    void OnKilled(Enemy e)
    {
        if (!HasSpecial(Absorb) || P == null || Time.time < absorbReadyAt) return;
        if (!FlameSword.DiedBurning(e)) return;
        absorbReadyAt = Time.time + AbsorbCooldown;
        P.Heal(AbsorbHeal);
    }

    /// <summary>잿불 비축: 받는 피해 누적 → 최대 체력의 50% 에 닿으면 주변 넉백 + 체력 25% 회복 (쿨타임 20초)</summary>
    void OnDamageReceived(float dmg)
    {
        var p = P;
        if (p == null || !HasSpecial(Hoard) || Time.time < hoardReadyAt) return;
        hoardStored += dmg;
        if (hoardStored < p.maxHp * HoardRatio) return;
        hoardStored = 0f;
        hoardReadyAt = Time.time + HoardCooldown;
        p.Heal(p.maxHp * HoardHeal);
        Vector2 c = p.transform.position;
        foreach (var e in Enemy.All)
        {
            if (e == null || e.hp <= 0f || e.IsWall) continue;
            Vector2 to = (Vector2)e.transform.position - c;
            if (to.magnitude > HoardRadius + e.radius) continue;
            e.Push(to.sqrMagnitude > 0.0001f ? to : Random.insideUnitCircle, Enemy.KnockSpeed(HoardKnock));
        }
    }

    /// <summary>불사의 화톳불: 체력 1 이하 → 최대 체력의 30% 로 부활 + 3초 무적 (쿨타임 90초)</summary>
    bool TryRevive()
    {
        var p = P;
        if (p == null || !HasSpecial(Phoenix) || Time.time < reviveReadyAt) return false;
        reviveReadyAt = Time.time + PhoenixCooldown;
        p.hp = p.maxHp * PhoenixHpRatio;
        p.SetInvulnerable(PhoenixInvuln);
        return true;
    }

    public override void Tick(float dt)
    {
        var p = P;
        if (p == null) return;
        // 가만히 있은 시간 (영원의 화로)
        Vector2 pos = p.transform.position;
        if ((pos - lastPos).sqrMagnitude > StillEpsilon * StillEpsilon) stillTime = 0f; else stillTime += dt;
        lastPos = pos;

        float regen = 0f;
        if (HasSpecial(Rekindle)) regen += RekindleHeal;
        if (Hearth)
        {
            regen += HearthRegen;
            if (stillTime >= HearthStillTime) regen += HearthStillRegen;   // [해석] 기본 2 에 더해 총 5
        }
        if (regen > 0f) p.Heal(regen * dt);
    }

    public override string Badge
    {
        get
        {
            var p = P;
            if (p == null) return null;
            if (HasSpecial(Phoenix) && Time.time < reviveReadyAt) return $"부활 {Mathf.CeilToInt(reviveReadyAt - Time.time)}";
            if (HasSpecial(Hoard)) return Time.time < hoardReadyAt ? $"비축 {Mathf.CeilToInt(hoardReadyAt - Time.time)}" : $"{Mathf.RoundToInt(hoardStored / Mathf.Max(1f, p.maxHp * HoardRatio) * 100f)}%";
            return null;
        }
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"체력 강화  <size=16>최대 체력 +{UpMaxHp * 100:0}%</size>",
            desc = $"최대 체력 +{(BaseMaxHp + UpMaxHp * hpUps) * 100:0}% → +{(BaseMaxHp + UpMaxHp * (hpUps + 1)) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"방어 강화  <size=16>받는 피해 -{UpTakenCut * 100:0}%</size>",
            desc = $"받는 피해 -{(1f - TakenMul) * 100:0.#}% → -{(1f - TakenMul * (1f - UpTakenCut)) * 100:0.#}%",
        },
        new PassiveUpgradeOption
        {
            title = $"회복 강화  <size=16>모든 회복량 +{UpHeal * 100:0}%</size>",
            desc = $"모든 회복량 +{(BaseHeal + UpHeal * healUps) * 100:0}% → +{(BaseHeal + UpHeal * (healUps + 1)) * 100:0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: hpUps++; break;
            case 1: takenUps++; break;
            case 2: healUps++; break;
        }
        Apply();
    }

    public override string BasicSummary =>
        $"체력 +{(HpMul - 1f) * 100:0}% · 받는 피해 -{(1f - TakenMul) * 100:0.#}% · 회복 +{(HealMul - 1f) * 100:0}%";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Rekindle, title = "다시 타오르는 불씨", desc = "초당 체력 1 회복" },
                new SpecialUpgradeOption { id = Thick, title = "두꺼운 장벽", desc = "최대 체력 +25%" },
                new SpecialUpgradeOption { id = Deflect, title = "흘리기", desc = "받는 피해 -5%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Absorb, title = "연소 흡수", desc = "화상 상태의 적을 처치하면 체력 1 회복 (쿨타임 1초)" },
                new SpecialUpgradeOption { id = Resolute, title = "불굴", desc = "체력이 30% 이하일 때 받는 피해 -10%" },
                new SpecialUpgradeOption { id = Plating, title = "타오르는 장갑", desc = "수동 스킬 시전 중 받는 피해 -15%" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Hoard, title = "잿불 비축", desc = "받는 피해가 누적됩니다. 최대 체력의 50%만큼 누적되면 주변 적을 넉백하고 최대 체력의 25% 회복 (쿨타임 20초)" },
                new SpecialUpgradeOption { id = Phoenix, title = "불사의 화톳불", desc = "체력이 1 이하가 되면 최대 체력의 30%로 부활하고 3초간 무적 (쿨타임 90초)" },
                new SpecialUpgradeOption { id = Fortress, title = "화염 진지", desc = "체력이 90% 이상일 때 받는 피해 -15%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboHearth, title = "영원의 화로", recipe = "불굴 + 불사의 화톳불", requires = new[] { Resolute, Phoenix },
            desc = "꺼지지 않는 화로처럼 계속 체력을 회복합니다.\n가만히 있으면 더 크게 회복하고 더 단단해집니다.",
            specs = new[] { "매 초 체력 2 회복", "3초 이상 이동하지 않을 때 매 초 체력 3 회복", "3초 이상 이동하지 않을 때 받는 피해 -10%" },
        },
    };
}

/// <summary>
/// P05 불씨 수확: 경험치 획득 +20% · 획득 범위 +30%
/// 고유 기본 강화: 경험치 획득 +5% / 획득 범위 +8% / 유물 획득 확률 +10%
/// 특수 강화 (2026-11-24 사용자):
///   Lv.5  불씨 모으기 (획득 범위 +50%) · 커지는 불씨 (경험치 획득 +25%) · 잿빛 행운 (유물 획득 확률 +50%)
///   Lv.10 공급되는 연료 (경험치 획득 시 이동속도 +5%, 최대 4스택, 5초) · 들이치는 폭풍 (30초마다 화면의 모든 경험치 획득) · 탐욕의 불씨 (처치 시 50% 확률로 경험치 2배)
///   Lv.15 풍요의 불씨 (경험치 획득 시 1 획득, 200 도달 시 유물 생성) · 더욱 타오르는 불씨 (경험치 획득량 +50%) · 타오르는 보물 (매초 경험치 1% 획득)
///   조합 풍요로운 탐욕 [임시 이름] = 탐욕의 불씨 + 풍요의 불씨: 유물 획득 시 50% 확률로 추가 획득
/// [해석] 경험치 · 획득 범위 · 유물 확률 증가는 자기 안에서 합연산 (기본 + 기본 강화 + 특수 강화).
/// [해석] 유물 획득 확률 = 일반 몹 상자 드랍 확률(0.4%)의 상대 증가. 중간보스 상자는 확정이라 영향 없음.
/// [해석] 공급되는 연료: 영혼 조각 1개를 얻을 때마다 스택 +1, 그때마다 5초로 갱신 (모든 스택 같이 유지).
/// [해석] 들이치는 폭풍: 화면 안의 영혼 조각을 즉시 끌어당김 (그 영혼은 정상적으로 경험치 · 연료 · 풍요의 불씨 계산).
/// [해석] 탐욕의 불씨: 모든 적(보스 포함)의 경험치 드랍에 적용. 영혼 조각 1개의 가치가 2배.
/// [해석] 풍요의 불씨: 영혼 조각을 1개 얻을 때마다 1 (경험치 양과 무관). 200 에 닿으면 플레이어 위치에 유물 상자 생성.
/// [해석] 타오르는 보물: 매초 현재 레벨 필요 경험치의 1% (경험치 획득 배율 미적용, 영혼 조각이 아니라 연료 · 풍요의 불씨에 안 셈).
/// [해석] 조합의 추가 획득: 유물을 얻을 때 50% 확률로 유물 상자 1개를 더 생성. 추가로 얻은 유물은 다시 굴리지 않음 (무한 연쇄 방지).
/// </summary>
public class EmberHarvest : Skill
{
    const float BaseXp = 0.20f, BasePickup = 0.30f;
    const float UpXp = 0.05f, UpPickup = 0.08f, UpRelic = 0.10f;

    // 특수 강화 id
    public const string Gather = "EH5A", Bloom = "EH5B", Luck = "EH5C";
    public const string Fuel = "EH10A", Storm = "EH10B", Greed = "EH10C";
    public const string Abundance = "EH15A", Blaze = "EH15B", Treasure = "EH15C";
    public const string ComboFeast = "EHC1";   // 풍요로운 탐욕 [임시 이름]

    // 특수 강화 수치
    const float GatherPickup = 0.50f, BloomXp = 0.25f, LuckRelic = 0.50f;
    const float FuelMove = 0.05f, FuelSeconds = 5f; const int FuelMaxStacks = 4;
    const float StormInterval = 30f;
    const float GreedChance = 0.5f, GreedMul = 2f;
    const int AbundanceNeed = 200;
    const float BlazeXp = 0.50f;
    const float TreasurePerSecond = 0.01f;   // 매초 필요 경험치의 1%
    const float FeastChance = 0.5f;

    int xpUps, pickupUps, relicUps;
    float appliedXp = 1f, appliedPickup = 1f, appliedRelic = 1f;

    int fuelStacks;
    float fuelLeft, appliedMove = 1f;
    float stormTimer;
    int abundance;
    int skipFeastRolls;   // 조합이 만든 추가 상자에서 나온 유물은 다시 굴리지 않음

    bool Feast => HasCombo(ComboFeast);

    float XpMul => 1f + BaseXp + UpXp * xpUps + (HasSpecial(Bloom) ? BloomXp : 0f) + (HasSpecial(Blaze) ? BlazeXp : 0f);
    float PickupMul => 1f + BasePickup + UpPickup * pickupUps + (HasSpecial(Gather) ? GatherPickup : 0f);
    float RelicMul => 1f + UpRelic * relicUps + (HasSpecial(Luck) ? LuckRelic : 0f);

    void OnEnable()
    {
        Apply();
        Gem.Collected += OnGemCollected;
        Relics.Obtained += OnRelicObtained;
        if (P != null) P.XpDropMul = GreedRoll;
    }

    void OnDisable()
    {
        Gem.Collected -= OnGemCollected;
        Relics.Obtained -= OnRelicObtained;
        var p = P;
        if (p == null) return;
        p.XpDropMul = null;
        p.xpGainMul /= appliedXp;
        p.pickupMul /= appliedPickup;
        p.relicChanceMul /= appliedRelic;
        p.moveSpeedMul /= appliedMove;
        appliedXp = appliedPickup = appliedRelic = appliedMove = 1f;
    }

    void Apply()
    {
        var p = P;
        if (p == null) return;
        p.xpGainMul = p.xpGainMul / appliedXp * XpMul;
        p.pickupMul = p.pickupMul / appliedPickup * PickupMul;
        p.relicChanceMul = p.relicChanceMul / appliedRelic * RelicMul;
        appliedXp = XpMul; appliedPickup = PickupMul; appliedRelic = RelicMul;
    }

    protected override void OnSpecialUpgrade(int lv, string id) => Apply();

    /// <summary>탐욕의 불씨: 적 처치 시 경험치 드랍 배율</summary>
    float GreedRoll() => HasSpecial(Greed) && Random.value < GreedChance ? GreedMul : 1f;

    void OnGemCollected(int value)
    {
        if (HasSpecial(Fuel))
        {
            fuelStacks = Mathf.Min(FuelMaxStacks, fuelStacks + 1);
            fuelLeft = FuelSeconds;
            ApplyFuel();
        }
        if (HasSpecial(Abundance) && P != null)
        {
            abundance++;
            if (abundance >= AbundanceNeed)
            {
                abundance -= AbundanceNeed;
                Chest.Spawn(P.transform.position);   // 유물 생성: 상자를 플레이어 위치에 (바로 획득)
            }
        }
    }

    /// <summary>특수 조합 풍요로운 탐욕: 유물을 얻을 때 50% 확률로 한 번 더</summary>
    void OnRelicObtained()
    {
        if (!Feast || P == null) return;
        if (skipFeastRolls > 0) { skipFeastRolls--; return; }
        if (Random.value < FeastChance)
        {
            skipFeastRolls++;
            Chest.Spawn(P.transform.position);
        }
    }

    void ApplyFuel()
    {
        var p = P;
        if (p == null) return;
        float move = 1f + FuelMove * fuelStacks;
        p.moveSpeedMul = p.moveSpeedMul / appliedMove * move;
        appliedMove = move;
    }

    public override void Tick(float dt)
    {
        var p = P;
        if (p == null) return;

        // 공급되는 연료: 5초 지나면 모든 스택 해제
        if (fuelStacks > 0)
        {
            fuelLeft -= dt;
            if (fuelLeft <= 0f) { fuelStacks = 0; ApplyFuel(); }
        }

        // 들이치는 폭풍: 30초마다 화면 안의 영혼 조각을 모두 끌어당김
        if (HasSpecial(Storm))
        {
            stormTimer += dt;
            if (stormTimer >= StormInterval)
            {
                stormTimer = 0f;
                PullVisibleGems();
            }
        }

        // 타오르는 보물: 매초 필요 경험치의 1%
        if (HasSpecial(Treasure) && GameManager.I != null)
            GameManager.I.AddXpRaw(GameManager.I.XpToNext * TreasurePerSecond * dt);
    }

    static void PullVisibleGems()
    {
        var cam = Camera.main;
        if (cam == null) return;
        foreach (var g in Gem.All)
        {
            if (g == null) continue;
            Vector3 v = cam.WorldToViewportPoint(g.transform.position);
            if (v.x >= 0f && v.x <= 1f && v.y >= 0f && v.y <= 1f) g.Attract();
        }
    }

    public override string Badge
    {
        get
        {
            if (HasSpecial(Abundance)) return $"{abundance}/{AbundanceNeed}";
            if (fuelStacks > 0) return $"{fuelStacks}";
            return null;
        }
    }

    // ---------- 강화 목록 ----------
    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"경험치 강화  <size=16>경험치 획득 +{UpXp * 100:0}%</size>",
            desc = $"경험치 획득 +{(XpMul - 1f) * 100:0}% → +{(XpMul - 1f + UpXp) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"범위 강화  <size=16>획득 범위 +{UpPickup * 100:0}%</size>",
            desc = $"획득 범위 +{(PickupMul - 1f) * 100:0}% → +{(PickupMul - 1f + UpPickup) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"유물 강화  <size=16>유물 획득 확률 +{UpRelic * 100:0}%</size>",
            desc = $"유물 획득 확률 +{(RelicMul - 1f) * 100:0}% → +{(RelicMul - 1f + UpRelic) * 100:0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: xpUps++; break;
            case 1: pickupUps++; break;
            case 2: relicUps++; break;
        }
        Apply();
    }

    public override string BasicSummary =>
        $"경험치 +{(XpMul - 1f) * 100:0}% · 범위 +{(PickupMul - 1f) * 100:0}% · 유물 +{(RelicMul - 1f) * 100:0}%";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Gather, title = "불씨 모으기", desc = "획득 범위 +50%" },
                new SpecialUpgradeOption { id = Bloom, title = "커지는 불씨", desc = "경험치 획득 +25%" },
                new SpecialUpgradeOption { id = Luck, title = "잿빛 행운", desc = "유물 획득 확률 +50%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Fuel, title = "공급되는 연료", desc = "경험치를 획득할 때마다 이동 속도 +5% (최대 4스택, 지속 5초)" },
                new SpecialUpgradeOption { id = Storm, title = "들이치는 폭풍", desc = "30초마다 화면의 모든 경험치를 획득합니다" },
                new SpecialUpgradeOption { id = Greed, title = "탐욕의 불씨", desc = "적을 처치할 때 50% 확률로 떨어뜨리는 경험치가 2배가 됩니다" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Abundance, title = "풍요의 불씨", desc = "경험치를 획득할 때마다 풍요의 불씨를 1 얻습니다. 풍요의 불씨가 200에 도달하면 유물이 생성됩니다" },
                new SpecialUpgradeOption { id = Blaze, title = "더욱 타오르는 불씨", desc = "경험치 획득량 +50%" },
                new SpecialUpgradeOption { id = Treasure, title = "타오르는 보물", desc = "매초 경험치 1%를 획득합니다 (현재 레벨의 필요 경험치 기준)" },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboFeast, title = "풍요로운 탐욕", recipe = "탐욕의 불씨 + 풍요의 불씨", requires = new[] { Greed, Abundance },
            desc = "탐욕이 풍요를 불러, 유물이 겹쳐서 찾아옵니다.",
            specs = new[] { "유물 획득 시 50% 확률로 추가 획득" },
        },
    };
}

/// <summary>
/// P06 화염 보호막 (2026-11-24 사용자): 화상 피해를 적용할 때 그 피해의 10% 만큼 보호막 획득.
/// 한 번에 얻는 양은 최대 체력의 5% 까지, 획득 쿨타임 0.5초, 보호막은 매초 5% 소모, 총량 기본 상한 = 최대 체력의 100%.
/// 고유 기본 강화: 최대 보호막 상한 +5% / 보호막 획득량 +10% / 보호막 감소량 -0.2%p
/// 특수 강화:
///   Lv.5  두꺼운 껍질 (얻는 보호막 상한 +100%) · 임계 돌파 (최대 보호막 상한 +100%) · 고속 흡수 (획득 쿨타임 -50%)
///   Lv.10 불길 흐름 (보호막이 있을 때 이동속도 +10%) · 재의 방패 (보호막이 있을 때 받는 피해 -10%) · 긴급 주입 (체력 50% 이하 → 주변 넉백 + 보호막 최대치 획득, 쿨타임 60초)
///   Lv.15 불타는 방벽 (보호막이 있을 때 매초 주변 적 화상 스택 +1) · 보호막 일격 (보호막 80% 이상에서 수동 스킬 사용 시 보호막 50% 감소, 그 스킬 최종 피해량 +50%) · 터질듯한 보호막 (최대 보호막 상한 +100%)
/// [해석] "피해" = 화상 피해 1회(틱)에 실제 적용된 피해량. "매초 5% 소모" = 현재 보호막의 5% (지수 감소).
/// [해석] 상한류 +100% 는 최대 체력 대비 비율에 +100%p 를 합연산 (기본 100% + 기본 강화 + 임계 돌파 + 터질듯한 보호막).
///        '얻는 보호막 상한' = 한 번에 얻는 양의 상한 (최대 체력의 5% → 10%), '최대 보호막 상한' = 총량 상한.
/// [해석] 보호막 일격: "보호막이 80% 이상" = 최대 보호막 상한 대비 80% 이상. "보호막 50% 감소" = 현재 보호막의 절반 소모.
///        피해 +50% 는 그 발동 (시전 중 + 끝난 뒤 {StrikeGrace}초) 의 그 스킬 피해에만. 수동 슬롯 스킬만 해당.
/// [임시] 긴급 주입 · 불타는 방벽 범위 5, 긴급 주입 넉백 거리 4.
/// 특수 조합은 사용자 입력이 중간에 끊겨 ("[긴급 주입] +") 아직 없음.
/// </summary>
public class FlameBarrier : Skill
{
    const float ShieldPerBurn = 0.10f;      // 화상 피해의 10%
    const float BaseMaxGainOfMaxHp = 0.05f; // 한 번에 얻는 보호막 상한 = 최대 체력의 5%
    const float BaseCapOfMaxHp = 1.0f;      // 기본 총량 상한 = 최대 체력의 100%
    const float BaseGainCooldown = 0.5f;    // 획득 쿨타임
    const float BaseDecay = 0.05f;          // 매초 5% 소모

    const float UpCap = 0.05f, UpGain = 0.10f, UpDecay = 0.002f;

    // 특수 강화 id
    public const string Thick = "FB5A", Breakthrough = "FB5B", Rapid = "FB5C";
    public const string Flow = "FB10A", AshShield = "FB10B", Emergency = "FB10C";
    public const string Wall = "FB15A", Strike = "FB15B", Bloated = "FB15C";
    public const string ComboOverflow = "FBC1";   // 넘치는 방벽 [임시 이름] = 긴급 주입 + 터질듯한 보호막

    // 특수 강화 수치
    const float ThickMul = 1.0f;            // 얻는 보호막 상한 +100%
    const float CapBonus = 1.0f;            // 최대 보호막 상한 +100% (임계 돌파 · 터질듯한 보호막 각각)
    const float RapidCooldownCut = 0.5f;    // 획득 쿨타임 -50%
    const float FlowMove = 0.10f;           // 보호막이 있을 때 이동속도 +10%
    const float AshShieldCut = 0.10f;       // 보호막이 있을 때 받는 피해 -10%
    const float EmergencyHpRatio = 0.50f, EmergencyCooldown = 60f;
    const float AreaRadius = 5f, EmergencyKnock = 4f;   // [임시]
    const int WallStacks = 1; const float WallInterval = 1f;
    const float StrikeShieldRatio = 0.80f, StrikeConsume = 0.50f, StrikeFinal = 0.50f, StrikeGrace = 1.5f;
    // 특수 조합 넘치는 방벽: 긴급 주입 발동 시 10초간 최대 보호막 상한 +100% · 보호막 감소 없음, 긴급 주입 쿨타임 -25%
    const float OverflowSeconds = 10f, OverflowCap = 1.0f, OverflowCooldownCut = 0.25f;

    int capUps, gainUps, decayUps;
    float readyAt;          // 다음 획득이 가능한 시각 (Time.time 기준)
    float appliedMove = 1f;
    float emergencyReadyAt;
    float overflowUntil;    // 넘치는 방벽 효과가 끝나는 시각
    float wallTimer;
    // 보호막 일격: 이번에 강화된 발동
    Skill strikeSkill;
    float strikeUntil;
    Mod mod;

    bool Overflow => HasCombo(ComboOverflow);
    bool OverflowActive => Overflow && Time.time < overflowUntil;
    float CapOfMaxHp => BaseCapOfMaxHp + UpCap * capUps + (HasSpecial(Breakthrough) ? CapBonus : 0f) + (HasSpecial(Bloated) ? CapBonus : 0f)
                        + (OverflowActive ? OverflowCap : 0f);
    float CapAmount => P != null ? P.maxHp * CapOfMaxHp : 0f;
    float MaxGainOfMaxHp => BaseMaxGainOfMaxHp * (1f + (HasSpecial(Thick) ? ThickMul : 0f));
    float GainCooldown => BaseGainCooldown * (HasSpecial(Rapid) ? 1f - RapidCooldownCut : 1f);
    float GainMul => 1f + UpGain * gainUps;
    float Decay => OverflowActive ? 0f : Mathf.Max(0f, BaseDecay - UpDecay * decayUps);   // 넘치는 방벽: 10초간 감소량 -100%
    bool HasShield => P != null && P.shield > 0.01f;

    class Mod : CombatModifier
    {
        readonly FlameBarrier f;
        public Mod(FlameBarrier f) => this.f = f;
        /// <summary>보호막 일격: 강화된 발동의 스킬 피해 최종 +50%</summary>
        public override float FinalMul(in DamageInfo d) =>
            f.strikeSkill != null && d.source == f.strikeSkill && Time.time < f.strikeUntil ? 1f + StrikeFinal : 1f;
    }

    void OnEnable()
    {
        Enemy.Damaged += OnDamaged;
        Skill.Activated += OnSkillActivated;
        mod = new Mod(this);
        CombatMods.Add(mod);
        readyAt = 0f;
        if (P != null) P.TakenMods.Add(AshShieldTaken);
    }

    void OnDisable()
    {
        Enemy.Damaged -= OnDamaged;
        Skill.Activated -= OnSkillActivated;
        if (mod != null) { CombatMods.Remove(mod); mod = null; }
        var p = P;
        if (p == null) return;
        p.TakenMods.Remove(AshShieldTaken);
        p.moveSpeedMul /= appliedMove;
        appliedMove = 1f;
        p.shield = 0f;
    }

    /// <summary>재의 방패: 보호막이 있을 때 받는 피해 -10%</summary>
    float AshShieldTaken() => HasSpecial(AshShield) && HasShield ? 1f - AshShieldCut : 1f;

    void OnDamaged(Enemy e, DamageInfo d, int dealt)
    {
        if (!d.IsBurn || dealt <= 0 || P == null || Time.time < readyAt) return;
        readyAt = Time.time + GainCooldown;
        float gain = Mathf.Min(dealt * ShieldPerBurn, P.maxHp * MaxGainOfMaxHp) * GainMul;
        if (P.shield < CapAmount) P.shield = Mathf.Min(CapAmount, P.shield + gain);   // 상한이 줄어도 이미 있는 보호막은 깎지 않음
    }

    /// <summary>보호막 일격: 보호막 80% 이상에서 수동 스킬을 쓰면 보호막 절반 소모 + 그 스킬 최종 피해 +50%</summary>
    void OnSkillActivated(Skill s)
    {
        if (!HasSpecial(Strike) || P == null || s == null || s.Entry == null || s.Entry.kind != SkillKind.Manual) return;
        if (P.shield < CapAmount * StrikeShieldRatio) return;
        P.shield *= 1f - StrikeConsume;
        strikeSkill = s;
        strikeUntil = Time.time + StrikeGrace;
    }

    public override void Tick(float dt)
    {
        var p = P;
        if (p == null) return;

        // 보호막 감소
        if (p.shield > 0f)
        {
            p.shield *= Mathf.Pow(1f - Decay, dt);
            if (p.shield < 0.01f) p.shield = 0f;
        }

        // 불길 흐름: 보호막이 있을 때 이동속도 +10%
        float move = HasSpecial(Flow) && HasShield ? 1f + FlowMove : 1f;
        if (!Mathf.Approximately(move, appliedMove))
        {
            p.moveSpeedMul = p.moveSpeedMul / appliedMove * move;
            appliedMove = move;
        }

        // 보호막 일격: 강화된 스킬이 아직 시전 중이면 유지
        if (strikeSkill != null && strikeSkill is ActiveSkill a && a.IsCastingNow) strikeUntil = Mathf.Max(strikeUntil, Time.time + StrikeGrace);

        // 긴급 주입: 체력 50% 이하 → 주변 넉백 + 보호막 최대치 (쿨타임 60초)
        if (HasSpecial(Emergency) && Time.time >= emergencyReadyAt && p.hp <= p.maxHp * EmergencyHpRatio)
        {
            emergencyReadyAt = Time.time + EmergencyCooldown * (Overflow ? 1f - OverflowCooldownCut : 1f);
            if (Overflow) overflowUntil = Time.time + OverflowSeconds;   // 상한 +100% · 감소 없음 (10초) 을 먼저 켜고 최대치까지 채움
            p.shield = CapAmount;
            Vector2 c = p.transform.position;
            foreach (var e in Enemy.All)
            {
                if (e == null || e.hp <= 0f || e.IsWall) continue;
                Vector2 to = (Vector2)e.transform.position - c;
                if (to.magnitude > AreaRadius + e.radius) continue;
                e.Push(to.sqrMagnitude > 0.0001f ? to : Random.insideUnitCircle, Enemy.KnockSpeed(EmergencyKnock));
            }
        }

        // 불타는 방벽: 보호막이 있는 동안 매초 주변 적에게 화상 스택 +1
        if (HasSpecial(Wall) && HasShield && FlameSword.I != null)
        {
            wallTimer += dt;
            if (wallTimer >= WallInterval)
            {
                wallTimer -= WallInterval;
                Vector2 c = p.transform.position;
                foreach (var e in Enemy.All)
                {
                    if (e == null || e.hp <= 0f || e.IsWall || e.Hidden) continue;
                    float rr = AreaRadius + e.radius;
                    if (((Vector2)e.transform.position - c).sqrMagnitude <= rr * rr) FlameSword.I.AddStacks(e, WallStacks);
                }
            }
        }
        else wallTimer = 0f;
    }

    public override PassiveUpgradeOption[] PassiveBasicOptions() => new[]
    {
        new PassiveUpgradeOption
        {
            title = $"상한 강화  <size=16>최대 보호막 상한 +{UpCap * 100:0}%</size>",
            desc = $"상한 최대 체력의 {CapOfMaxHp * 100:0}% → {(CapOfMaxHp + UpCap) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"획득 강화  <size=16>보호막 획득량 +{UpGain * 100:0}%</size>",
            desc = $"획득량 +{UpGain * gainUps * 100:0}% → +{UpGain * (gainUps + 1) * 100:0}%",
        },
        new PassiveUpgradeOption
        {
            title = $"유지 강화  <size=16>보호막 감소량 -{UpDecay * 100:0.0}%p</size>",
            desc = $"매초 감소 {Decay * 100:0.0}% → {Mathf.Max(0f, BaseDecay - UpDecay * (decayUps + 1)) * 100:0.0}%",
        },
    };

    protected override void OnPassiveBasic(int index)
    {
        switch (index)
        {
            case 0: capUps++; break;
            case 1: gainUps++; break;
            case 2: decayUps++; break;
        }
    }

    public override string BasicSummary =>
        $"상한 {CapOfMaxHp * 100:0}% · 획득 +{UpGain * gainUps * 100:0}% · 감소 {Decay * 100:0.0}%/초";

    public override SpecialUpgradeOption[] SpecialOptions(int lv)
    {
        switch (lv)
        {
            case 5: return new[]
            {
                new SpecialUpgradeOption { id = Thick, title = "두꺼운 껍질", desc = "한 번에 얻는 보호막 상한 +100%" },
                new SpecialUpgradeOption { id = Breakthrough, title = "임계 돌파", desc = "최대 보호막 상한 +100%" },
                new SpecialUpgradeOption { id = Rapid, title = "고속 흡수", desc = "보호막 획득 쿨타임 -50%" },
            };
            case 10: return new[]
            {
                new SpecialUpgradeOption { id = Flow, title = "불길 흐름", desc = "보호막이 있을 때 이동 속도 +10%" },
                new SpecialUpgradeOption { id = AshShield, title = "재의 방패", desc = "보호막이 있을 때 받는 피해 -10%" },
                new SpecialUpgradeOption { id = Emergency, title = "긴급 주입", desc = "체력이 50% 이하일 때 주변 적을 넉백하고 보호막을 최대치까지 얻습니다 (쿨타임 60초)" },
            };
            case 15: return new[]
            {
                new SpecialUpgradeOption { id = Wall, title = "불타는 방벽", desc = "보호막이 있을 때 매초 주변 적의 화상 스택 +1" },
                new SpecialUpgradeOption { id = Strike, title = "보호막 일격", desc = "보호막이 80% 이상일 때 수동 스킬을 사용하면 보호막이 50% 감소하고, 그 수동 스킬의 최종 피해량 +50%" },
                new SpecialUpgradeOption { id = Bloated, title = "터질듯한 보호막", desc = "최대 보호막 상한 +100%" },
            };
        }
        return base.SpecialOptions(lv);
    }

    public override SpecialComboOption[] SpecialCombos() => new[]
    {
        new SpecialComboOption
        {
            id = ComboOverflow, title = "넘치는 방벽", recipe = "긴급 주입 + 터질듯한 보호막", requires = new[] { Emergency, Bloated },
            desc = "긴급 주입이 보호막을 한계 너머까지 채우고, 한동안 사라지지 않게 붙들어 둡니다.",
            specs = new[] { "긴급 주입 동안(10초) '최대 보호막 상한' +100%", "긴급 주입 동안(10초) '보호막 감소량' -100%", "긴급 주입 '쿨타임' -25%" },
        },
    };

    public override string Badge => P != null && P.shield > 0.5f ? Mathf.CeilToInt(P.shield).ToString() : null;
}
