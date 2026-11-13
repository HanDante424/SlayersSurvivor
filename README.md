# SlayersSurvivor

Unity 6 (6000.3.22f1) 2D 뱀서라이크 액션. 기획 기준: [Docs/Design/GDD_v0.1.md](Docs/Design/GDD_v0.1.md),
전투·스킬 성장은 [Docs/Design/Combat_Decisions_v1.0.md](Docs/Design/Combat_Decisions_v1.0.md) (확정 결정 · 최우선)
→ [Combat_Unified_v1.0.md](Docs/Design/Combat_Unified_v1.0.md) (통합 기획서 원문) → [Combat_SkillGrowth_v0.2.md](Docs/Design/Combat_SkillGrowth_v0.2.md) 순

## 실행
Unity Hub → Add → 이 폴더 → Play (타이틀부터 시작). 타이틀 "플레이" → 캐릭터 선택 (에른 스트레이 · 레니아(외형 테스트) · 스티움 카터렉/멜 발렌티나(준비 중)).
캐릭터 목록: `Assets/Scripts/Gameplay/CharacterRoster.cs`

설정 (타이틀 "설정"): 화면 모드 전체화면 / 창화면 · 해상도 1920 × 1080 / 1280 × 720, "적용" 시 저장 · 다음 실행 때 자동 적용 (`UI/SettingsScreen.cs`, 에디터에서는 저장만)

실행 파일 빌드: 메뉴 SlayersSurvivor → 빌드 → Windows 개발 빌드 (치트 포함) / 일반 빌드 (치트 없음) → `Builds/SlayersSurvivor_Win64/SlayersSurvivor.exe` (`Assets/Editor/BuildWindows.cs`, Builds 는 git 제외)

## 스테이지
- **생존** (타이틀 "플레이"): 무한 맵 · 일반 몹 · 3분마다 중간보스 · 10분 생존 시 승리
- **레이드 보스** (타이틀 "레이드 보스" → 레이드 보스 선택: 가니악 벤데스트라 / 카구츠치 (입장 연출까지, `Gameplay/Kagutsuchi.cs`)): 맵 36 × 24 로 제한 (경계 밖으로 플레이어 · 적 · 카메라가 못 나감), 레이드 보스 1마리만, 처치 시 "레이드 성공!".
  일반 몹이 없어 경험치가 없음 → 치트 패널(F2) 레벨 +10 으로 스킬을 갖추고 테스트. 중간보스 소환(F10 · F12)은 레이드에서 막힘, F11 보스 체력 절반은 레이드 보스에도 동작.
  스테이지 설정 `Core/Stage.cs` (맵 크기 `RaidHalfSize`), 레이드 진행 `Gameplay/RaidBoss.cs`.
  레이드 보스 **가니악 벤데스트라** (`Gameplay/Ganiac.cs`, 스탯 `Enemy.cs` → `Setup()` 의 `Ganiac` [임시] 체력 3000 · 속도 1.0): 맵 이탈 상태로 시작 → 특수 패턴 1 (무작위 직선 10개) → 특수 패턴 2 (중앙 레이저 · 중앙에 등장 · 좌우 순차 레이저) → 느리게 추격하며 이동 패턴 1 (멀면 옆으로 빠졌다가 플레이어 옆에 등장) · 공격 패턴 1 (횡베기 2 → 전방위) · 공격 패턴 2 (돌진 → 연속 베기 10회 → 전방위) · 공격 패턴 3 (횡베기 → 전방위, 2번) · 공격 패턴 4 (반경 9 횡베기 → 연속 베기) · 특수 패턴 3 (체력 90 · 60 · 30%: 카메라 맵 전체 · 가니악의 검 3개를 1분 안에 부수기, 실패 시 검마다 반경 12 베기) · 특수 패턴 4 (체력 75 · 50 · 25%: 좌하 → 우상 구석에서 레이저 난사, 보스 뒤 모서리만 안전) · 특수 패턴 3 이후 공격 패턴 5 (어둠의 검 4개 지그재그 돌진) · 특수 패턴 4 이후 공격 패턴 6 · 7 (돌진/횡베기 → 플레이어를 지나는 레이저 6개). 치트 패널 "보스 체력 -30%" 로 확인. 그림 `Enemy_Ganiac` 없으면 짙은 보라 원.
  설정 [Raid_Ganiac_Setting_v0.1.md](Docs/Design/Raid_Ganiac_Setting_v0.1.md) · 구현 계획 [Raid_Ganiac_Plan_v0.1.md](Docs/Design/Raid_Ganiac_Plan_v0.1.md)

## 조작 (임시 키 — `Assets/Resources/Config/SkillTuning.asset` 에서 변경)
- 이동: WASD / 방향키
- 수동 스킬: J / K / L (획득 순서대로 배정)
- 이동 스킬: Space
- 궁극기: Q (게이지가 가득 찼을 때)
- 레벨업 선택: 마우스 클릭 또는 1 / 2 / 3
- 일시정지: ESC, 재시작: R (게임 오버 후)
- 테스트: F1 판정 표시, F2 치트 패널 (무적, 경험치 미드랍, 레벨 +1/+10, 궁극기 게이지 채우기 등), F6 쿨타임 감소 100%, F7 자동 스킬 사용 금지, F8 적 생성 중지, F10 고블린 장로 소환 · F12 홉 고블린 기사 소환 (중간보스 + 포위, 적 생성 중지와 같이 사용 가능 · 치트 버튼 옆에 항상 표시), F11 보스 체력 절반 (지금 체력의 절반, 죽지 않음), 치트 패널 오른쪽 열에서 스킬별 사용 금지 · 스킬별 Lv+1 (기본 강화 → 특수 강화 화면 바로 열기)

## 현재 구현 (에른 스트레이)
- 자동 6종: 파이어 슬래시, 솔라 슬래시, 파이어 볼, 파이어 버스터, 파이어 블레이드, 파이어 스톰
- 수동 5종: 트리플 슬래시, 파이어 차지, 오버 드라이브 플레임 슬래시, 파이어 브레이커, 파이어 엑스큐션
- 이동 1종: 파이어 스트림 (3스택)
- 궁극기 1종: 블레이징 스톰 (캐릭터 Lv.45 획득 / Lv.60 강화, 적 처치로 게이지 충전)
- 기본 수치 확정 (v0.2): 파이어 슬래시(기본 무장, 3초 · 10), 트리플 슬래시(8초 · 8×3), 솔라 슬래시(5초 · 베기 5 → 폭발 12).
  나머지 스킬은 아직 등급 환산표 임시값
- 스킬 성장 (v0.2): 보유 스킬 선택 → 기본 강화(공격력 +10% / 범위 +10% / 쿨타임 -5%, 합연산) → 스킬 Lv +1.
  스킬 Lv.5·10·15 는 이어서 특수 강화 화면 (3개 중 1개, 이전 단계 강화 유지·중첩).
  특수 강화 구현: 파이어 슬래시 · 솔라 슬래시 각 9종 (`Docs/Design/SpecialUpgrades_FireSolar_v1.0.md`).
  파이어 · 솔라 · 트리플 슬래시와 패시브 3종 외의 스킬은 기획 미설계 → "확인"만, 효과 없음. 획득한 특수 강화는 화면 왼쪽 위 스킬 목록에 ★ 로 표시
  피해 계산: 초기 피해량 → 피해량 → 추가 피해량 → 최종 피해량 → (대상) 받는 최종 피해량 → 내림 (`Core/Damage.cs`).
  쿨타임: 기본 강화 배율 × 독립 배율 곱연산, 최종 감소 최대 90%. 스킬 Lv.15 는 후보에서 제외
- 패시브: 화염의 검 (`ErnPassives.cs`) · 옥염 (`Hellfire.cs`) · 흡수 가열 (`HeatAbsorption.cs`) — 각각 기본 + 고유 강화 3종 + 특수 강화 9종
- 트리플 슬래시 특수 강화 9종 (`TripleSlash.cs`, 일섬 전용 이펙트 포함)
- 두 번째 궁극기: 기획 미설계라 없음
- 치트 패널 오른쪽 열: 미보유 스킬은 "획득" 버튼으로 바로 획득. 화염의 검 보유 시 "화상 1스택 부여 (화면 안 적)"
- [임시] 레벨업 후보는 이미지 리소스가 있는 파이어 슬래시 · 솔라 슬래시 · 트리플 슬래시 + 패시브 3종만 (`SkillCatalog.LevelUpPool`). 나머지 스킬·궁극기는 후보에 안 나옴

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
- 적 판정: `Assets/Resources/Config/EnemyConfig.asset`, 적 스탯: `Enemy.cs` → `Setup()`, 궁수 · 라이더 행동 수치: `EnemyBehaviors.cs` 맨 위
- 스폰 속도/난이도 곡선: `GameManager.cs` → `Spawning()`

## 그래픽 교체
`Assets/Resources/Sprites/` 에 정해진 이름으로 넣으면 자동 적용, 없으면 도형으로 대체.
- 캐릭터: `{id}_Idle` (4행 x 1열), `{id}_Run` (4행 x 8열), `{id}_Slash` (공격 4행 x 8열). id = `ErnStray`, `Lenia`
- 이펙트: `ErnStray_FireSlash` (기본 공격, 치명타도 동일), `FireBall`
- 스킬 이펙트 (FireForge 제작, `ErnSkillFx` 에서 로드 · 크기/속도는 각 스킬 `[임시] Fx*` 상수):
  `ErnStray_SkillFireSlash` 2행 x 4열(7프레임) · `ErnStray_SkillTripleSlash` 2행 x 5열 · `ErnStray_SkillSolarSlash` 4행 x 3열
- 적: `Enemy_Goblin`, `Enemy_GoblinArcher`, `Enemy_Hobgoblin`, `Enemy_GoblinRider`, `Enemy_Boss`([임시]), 궁수 화살 `Enemy_Arrow` — 오른쪽을 보는 그림 1장, 여백 없이 자름 (긴 변 = 1유닛 × 적 크기). 몬스터 기획: [Docs/Design/Monsters_v1.md](Docs/Design/Monsters_v1.md)
- 경험치 = 영혼 조각: `Soul_Small` (청백, 1~4) · `Soul_Mid` (청록, 5~19) · `Soul_Large` (붉은 주황, 20~), 256px · 떠서 흔들리는 연출은 `Gem.cs`
- 바닥: `Ground` — 이음매 없는 반복 타일 1장 = `SpriteFactory.GroundTileSize` (6유닛). 현재 Dark_Moss_Wasteland_Tile_v1 (경계 번진 띠 보정)
- 한 장짜리 그림(적 · 영혼 조각 · `FireBall`)은 긴 변이 1유닛이 되도록 맞춘 뒤 각 오브젝트 크기를 곱함
