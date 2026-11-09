# SlayersSurvivor

Unity 6 (6000.3.22f1) 2D 뱀서라이크 액션. 기획 기준: [Docs/Design/GDD_v0.1.md](Docs/Design/GDD_v0.1.md),
전투·스킬 성장은 [Docs/Design/Combat_SkillGrowth_v0.2.md](Docs/Design/Combat_SkillGrowth_v0.2.md) 우선

## 실행
Unity Hub → Add → 이 폴더 → Play (타이틀부터 시작).

## 조작 (임시 키 — `Assets/Resources/Config/SkillTuning.asset` 에서 변경)
- 이동: WASD / 방향키
- 수동 스킬: J / K / L (획득 순서대로 배정)
- 이동 스킬: Space
- 궁극기: Q (게이지가 가득 찼을 때)
- 레벨업 선택: 마우스 클릭 또는 1 / 2 / 3
- 일시정지: ESC, 재시작: R (게임 오버 후)
- 테스트: F1 판정 표시, F2 치트 패널 (무적, 레벨 +1/+10, 궁극기 게이지 채우기 등)

## 현재 구현 (에른 스트레이)
- 자동 6종: 파이어 슬래시, 솔라 슬래시, 파이어 볼, 파이어 버스터, 파이어 블레이드, 파이어 스톰
- 수동 5종: 트리플 슬래시, 파이어 차지, 오버 플레임 슬래시, 파이어 브레이커, 파이어 엑스큐션
- 이동 1종: 파이어 스트림 (3스택)
- 궁극기 1종: 블레이징 스톰 (캐릭터 Lv.45 획득 / Lv.60 강화, 적 처치로 게이지 충전)
- 기본 수치 확정 (v0.2): 파이어 슬래시(기본 무장, 3초 · 10), 트리플 슬래시(8초 · 8×3), 솔라 슬래시(5초 · 베기 5 → 폭발 12).
  나머지 스킬은 아직 등급 환산표 임시값
- 스킬 성장 (v0.2): 보유 스킬 선택 → 기본 강화(공격력 +10% / 범위 +10% / 쿨타임 -5%, 합연산) → 스킬 Lv +1.
  스킬 Lv.5·10·15 는 이어서 특수 강화 화면 (효과·선택지는 기획 미설계 → "확인"만, 효과 없음).
  피해량 내림, 쿨타임 감소 최대 90%. 스킬 Lv.15 는 후보에서 제외
- 패시브, 두 번째 궁극기: 기획 미설계라 없음
- [임시] 레벨업 후보는 이미지 리소스가 있는 파이어 슬래시 · 솔라 슬래시 · 트리플 슬래시만 (`SkillCatalog.LevelUpPool`). 나머지 스킬·궁극기는 후보에 안 나옴

## 임시값 위치
기획서의 정성 등급(짧음/중간/김, 낮음/높음 …)은 코드에 등급 그대로 들어가 있고,
등급 → 숫자 변환은 한 곳에서 관리합니다. 기획 수치가 확정되면 여기만 고치면 됩니다.

- 등급 환산표·슬롯 수·궁극기 조건·조작키·기본 강화율: `Assets/Resources/Config/SkillTuning.asset`
- 파이어/트리플/솔라 슬래시 타격 판정(모양·크기·넉백 거리·타 간격·폭발 지연): 같은 `SkillTuning.asset` 의 `[임시] 타격 판정`
  — Play 중 Inspector 에서 바꾸면 즉시 반영, F1 을 켜면 공격 순간 실제 판정 도형이 보임 (노랑 = 베기: 파이어·트리플은 기본 캡슐, shape 를 Sector 로 바꾸면 부채꼴 + 근접 원 · 주황 = 솔라 폭발). 적의 빨간 원이 도형에 닿으면 맞음
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
