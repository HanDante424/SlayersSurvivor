# SlayersSurvivor

Unity 6 (6000.3.22f1) 2D 뱀서라이크 액션. 기획 기준: [Docs/Design/GDD_v0.1.md](Docs/Design/GDD_v0.1.md),
전투·스킬 성장은 [Docs/Design/Combat_Decisions_v1.0.md](Docs/Design/Combat_Decisions_v1.0.md) (확정 결정 · 최우선)
→ [Combat_Unified_v1.0.md](Docs/Design/Combat_Unified_v1.0.md) (통합 기획서 원문) → [Combat_SkillGrowth_v0.2.md](Docs/Design/Combat_SkillGrowth_v0.2.md) 순

## 실행
Unity Hub → Add → 이 폴더 → Play (타이틀부터 시작).

## 조작 (임시 키 — `Assets/Resources/Config/SkillTuning.asset` 에서 변경)
- 이동: WASD / 방향키
- 수동 스킬: J / K / L (획득 순서대로 배정)
- 이동 스킬: Space
- 궁극기: Q (게이지가 가득 찼을 때)
- 레벨업 선택: 마우스 클릭 또는 1 / 2 / 3
- 일시정지: ESC, 재시작: R (게임 오버 후)
- 테스트: F1 판정 표시, F2 치트 패널 (무적, 경험치 미드랍, 레벨 +1/+10, 궁극기 게이지 채우기 등), F6 쿨타임 감소 100%, F7 자동 스킬 사용 금지, F8 적 생성 중지, 치트 패널 오른쪽 열에서 스킬별 사용 금지 · 스킬별 Lv+1 (기본 강화 → 특수 강화 화면 바로 열기)

## 현재 구현 (에른 스트레이)
- 자동 6종: 파이어 슬래시, 솔라 슬래시, 파이어 볼, 파이어 버스터, 파이어 블레이드, 파이어 스톰
- 수동 5종: 트리플 슬래시, 파이어 차지, 오버 플레임 슬래시, 파이어 브레이커, 파이어 엑스큐션
- 이동 1종: 파이어 스트림 (3스택)
- 궁극기 1종: 블레이징 스톰 (캐릭터 Lv.45 획득 / Lv.60 강화, 적 처치로 게이지 충전)
- 기본 수치 확정 (v0.2): 파이어 슬래시(기본 무장, 3초 · 10), 트리플 슬래시(8초 · 8×3), 솔라 슬래시(5초 · 베기 5 → 폭발 12).
  나머지 스킬은 아직 등급 환산표 임시값
- 스킬 성장 (v0.2): 보유 스킬 선택 → 기본 강화(공격력 +10% / 범위 +10% / 쿨타임 -5%, 합연산) → 스킬 Lv +1.
  스킬 Lv.5·10·15 는 이어서 특수 강화 화면 (3개 중 1개, 이전 단계 강화 유지·중첩).
  특수 강화 구현: 파이어 슬래시 · 솔라 슬래시 각 9종 (`Docs/Design/SpecialUpgrades_FireSolar_v1.0.md`).
  트리플 슬래시 등 나머지는 기획 미설계 → "확인"만, 효과 없음. 획득한 특수 강화는 화면 왼쪽 위 스킬 목록에 ★ 로 표시
  피해 계산: 초기 피해량 → 피해량 → 추가 피해량 → 최종 피해량 → (대상) 받는 최종 피해량 → 내림 (`Core/Damage.cs`).
  쿨타임: 기본 강화 배율 × 독립 배율 곱연산, 최종 감소 최대 90%. 스킬 Lv.15 는 후보에서 제외
- 패시브: 화염의 검 (기본 + 고유 강화 3종 + 특수 강화 9종, `ErnPassives.cs`). 옥염 · 흡수 가열은 예정
- 두 번째 궁극기: 기획 미설계라 없음
- 치트 패널 오른쪽 열: 미보유 스킬은 "획득" 버튼으로 바로 획득. 화염의 검 보유 시 "화상 1스택 부여 (화면 안 적)"
- [임시] 레벨업 후보는 이미지 리소스가 있는 파이어 슬래시 · 솔라 슬래시 · 트리플 슬래시 + 화염의 검만 (`SkillCatalog.LevelUpPool`). 나머지 스킬·궁극기는 후보에 안 나옴

## 임시값 위치
기획서의 정성 등급(짧음/중간/김, 낮음/높음 …)은 코드에 등급 그대로 들어가 있고,
등급 → 숫자 변환은 한 곳에서 관리합니다. 기획 수치가 확정되면 여기만 고치면 됩니다.

- 등급 환산표·슬롯 수·궁극기 조건·조작키·기본 강화율: `Assets/Resources/Config/SkillTuning.asset`
- 파이어/트리플/솔라 슬래시 타격 판정(모양·크기·넉백 거리·타 간격·폭발 지연): 같은 `SkillTuning.asset` 의 `[임시] 타격 판정`
  — Play 중 Inspector 에서 바꾸면 즉시 반영, F1 을 켜면 공격 순간 실제 판정 도형이 보임 (노랑 = 베기: 파이어·트리플은 기본 캡슐, shape 를 Sector 로 바꾸면 부채꼴 + 근접 원 · 주황 = 솔라 폭발). 적의 빨간 원이 도형에 닿으면 맞음
- 특수 강화 중 기획서에 수치가 없는 값(검기 속도·사거리, 대상 추적 거리, 추가 참격 위치·지연, 연속 폭발 간격, 메아리 간격·프레임당 상한·난사 흩뿌림 각도, 타오름 판정·넉백 시점, 뇌진탕 충돌 시간·대상): 같은 `SkillTuning.asset` 의 `[임시] 파이어/솔라 슬래시 특수 강화`.
  F1 판정 표시에서 하늘색 원 = 솔라 슬래시 넉백 범위 (피해 없이 밀기만)
- 기획서에 등급조차 없는 값(참격 횟수, 슬로우 비율 등): `Assets/Scripts/Skills/ErnStray/*.cs` 각 클래스 맨 위 `[임시]` 상수
- 캐릭터 감각 값: `Assets/Resources/Config/ErnStrayConfig.asset`
- 적 판정: `Assets/Resources/Config/EnemyConfig.asset`, 적 스탯: `Enemy.cs` → `Setup()`
- 스폰 속도/난이도 곡선: `GameManager.cs` → `Spawning()`

## 그래픽 교체
`Assets/Resources/Sprites/` 에 정해진 이름으로 넣으면 자동 적용, 없으면 도형으로 대체.
- 캐릭터: `ErnStray_Run`, `ErnStray_Idle`, `ErnStray_SlashH`, `ErnStray_SlashV`, `ErnStray_Thrust` (4행 x 8열)
- 이펙트: `ErnStray_FireSlash` (기본 공격, 치명타도 동일), `FireBall`
- 스킬 이펙트 (FireForge 제작, `ErnSkillFx` 에서 로드 · 크기/속도는 각 스킬 `[임시] Fx*` 상수):
  `ErnStray_SkillFireSlash` 2행 x 4열(7프레임) · `ErnStray_SkillTripleSlash` 2행 x 5열 · `ErnStray_SkillSolarSlash` 4행 x 3열
- 기타: `Enemy_Bat`, `Enemy_Runner`, `Enemy_Tank`, `Enemy_Boss`, `Gem`, `Ground`
