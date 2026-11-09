# SlayersSurvivor

Unity 6 (6000.3.22f1) 2D 뱀서라이크 액션. 기획 기준: [Docs/Design/GDD_v0.1.md](Docs/Design/GDD_v0.1.md)

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
- 스킬 레벨 1~15, Lv.5·10·15 특수 강화 자리만 있음 (효과는 기획 미설계)
- 패시브, 두 번째 궁극기: 기획 미설계라 없음

## 임시값 위치
기획서의 정성 등급(짧음/중간/김, 낮음/높음 …)은 코드에 등급 그대로 들어가 있고,
등급 → 숫자 변환은 한 곳에서 관리합니다. 기획 수치가 확정되면 여기만 고치면 됩니다.

- 등급 환산표·슬롯 수·궁극기 조건·조작키: `Assets/Resources/Config/SkillTuning.asset`
- 기획서에 등급조차 없는 값(참격 횟수, 슬로우 비율 등): `Assets/Scripts/Skills/ErnStray/*.cs` 각 클래스 맨 위 `[임시]` 상수
- 캐릭터 감각 값: `Assets/Resources/Config/ErnStrayConfig.asset`
- 적 판정: `Assets/Resources/Config/EnemyConfig.asset`, 적 스탯: `Enemy.cs` → `Setup()`
- 스폰 속도/난이도 곡선: `GameManager.cs` → `Spawning()`

## 그래픽 교체
`Assets/Resources/Sprites/` 에 정해진 이름으로 넣으면 자동 적용, 없으면 도형으로 대체.
- 캐릭터: `ErnStray_Run`, `ErnStray_Idle`, `ErnStray_SlashH`, `ErnStray_SlashV`, `ErnStray_Thrust` (4행 x 8열)
- 이펙트: `ErnStray_FireSlash`, `ErnStray_FireSlashCrit`, `FireBall`
- 스킬 이펙트 (FireForge 제작, `ErnSkillFx` 에서 로드 · 크기/속도는 각 스킬 `[임시] Fx*` 상수):
  `ErnStray_SkillFireSlash` 1행 x 4열 · `ErnStray_SkillTripleSlash` 2행 x 5열 · `ErnStray_SkillSolarSlash` 4행 x 3열
- 기타: `Enemy_Bat`, `Enemy_Runner`, `Enemy_Tank`, `Enemy_Boss`, `Gem`, `Ground`
