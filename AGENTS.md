# SlayersSurvivor 작업 규칙 (Claude · Codex 등 코드 에이전트 공용)

> Claude Code 환경 메모 (git 경로 · 컴파일 확인 · 한글 커밋 메시지 등) 는 [CLAUDE.md](CLAUDE.md). 규칙 · 진행 상태는 이 파일이 기준.
> GPT · Claude 가 번갈아 작업하므로 **작업 전 `git log` 로 최신 커밋을 확인**하고, 작업을 마치면 아래 "현재 진행 상태" 를 갱신한다.

Unity 6 (6000.3.22f1) 2D 뱀서라이크. 외부 패키지 없음, UI 는 IMGUI (`OnGUI`). 한국어로 소통하고 주석 · 커밋 메시지도 한국어.

## 먼저 읽을 것
1. `README.md` (실행 · 조작 · 현재 구현)
2. `Docs/Handoff_ForGPT_2026-11-13.md` (구조 · 가니악 패턴 · 주요 파일)
3. 작업 대상 기획서: `Docs/Design/` (카구츠치 1페이즈 `Raid_Kagutsuchi_Plan_v0.1.md`, 2페이즈 `Raid_Kagutsuchi_Phase2_Spec_v0.1.md`)
기획 우선순위: `Combat_Decisions_v1.0` → `Combat_Unified_v1.0` → `Combat_SkillGrowth_v0.2`.

## 절대 규칙
- **기획서에 없는 값은 만들어 확정하지 않는다.** 필요하면 코드에 `[임시]` 상수로 두고 문서 · 보고에 `[임시]` 표시. 기획과 다르게 해석한 곳은 `[해석]` 으로 보고.
- **요청 범위 밖은 건드리지 않는다.** 특히 가니악(`Ganiac.cs`) 동작 · 수치, 1페이즈 카구츠치 동작은 요청 없이 바꾸지 않는다. 리팩터링은 요청이 있을 때만.
- `main` 에 직접 커밋하지 않는다. 기능별 브랜치 → 사용자 확인 후 `--no-ff` 머지. 푸시는 사용자가 요청할 때만. 커밋 메시지는 한국어, 무엇을 바꿨는지 목록으로.
- `.meta` 파일은 지우거나 다시 만들지 않는다 (GUID 유지). 새 이미지를 넣으면 Unity 가 만든 `.meta` 를 함께 커밋. `Library` · `Temp` · `Builds` 는 커밋 제외.
- 씬(`*.unity`) · `.asset` 직접 편집 금지 (필요하면 사용자에게 에디터에서 하도록 안내). 씬 구성은 `Assets/Editor/SceneSetup.cs` 로 처리.
- 컴파일 검증이 안 되는 환경이면 "컴파일 확인 못 함" 이라고 보고. 확인 안 한 것을 됐다고 말하지 않는다.

## 코드 규칙
- 패턴 수치(반경 · 시간 · 피해 · 쿨타임)는 **파일 맨 위 상수**로 둔다 (`Ganiac.cs` · `Kagutsuchi.cs` 방식). 매직 넘버 금지.
- 보스 패턴 공통 규칙: 한 번에 하나 · 쿨타임은 패턴이 끝난 뒤부터 · 조건 맞는 준비된 패턴 중 무작위 · 기절하면 멈췄다가 이어서 · 진행 중 패턴 종료 시 경고 · 소환물 정리.
- 레이드 보스는 `IRaidBossAI` (`HideBossBar` · `DrawHud` · `OnPhaseEnd` · `OnPhaseBegin`) 를 구현. 레이드 진행 · 난이도 · 페이즈 전환은 `Gameplay/RaidBoss.cs`.
- 보스가 플레이어에게 주는 피해는 **숫자 하나** (피해 종류 없음), 피격 무적 0.35초. `Player.TakeDamage` · `Player.HitByBoss(피해, 기절, 밀림 방향, 거리)` 사용.
- 연출용 상태는 `Enemy.Scripted` (보이지만 맞지 않음 · 접촉 피해 없음). 카메라는 `CameraFollow.Focus / ClearFocus / ZoomToFullMap / ResetZoom / Shake`.
- 경고 표시는 `BossWarning` (원 · 반원 · 직사각형), 레이저는 `RaidLaser` 재사용.
- 피해 계산은 `Core/Damage.cs` 한 곳. 새 계산 경로를 만들지 않는다.
- 플레이어 → 보스 피해는 기존 `DamageInfo` 구조 그대로.

## 그림 리소스
- `Assets/Resources/Sprites/{이름}.png`, 이름이 맞으면 자동 적용, 없으면 도형 대체. 이름 규칙은 `README.md` "그래픽 교체" 참고.
- 새 그림은 규격 · 격자 · 투명 여백 · 발 위치를 **수치로 먼저 검사** (`Tools/*.py` 참고). 이미지를 임의로 리사이즈 · 덮어쓰기 하지 않는다. 원본은 보존.

## 작업 보고 형식
1. 바꾼 파일과 이유 (파일:줄)
2. 새로 넣은 `[임시]` 값 목록
3. 기획과 다르게 해석한 부분 `[해석]`
4. 사용자가 Unity 에서 확인할 테스트 방법 (치트 F2 · 보스 체력 -30% 등)
5. 확인하지 못한 것

## 현재 진행 상태 (2026-11-16, origin/main 0ca9a53 기준)
- **레이드 난이도 · 페이즈 전환** 구현됨: 레이드 보스 선택 → 난이도 선택 (이지 = 1페 · 노말 = 1 ~ 2페 · 하드 3페 · 익스트림 = **잠금**). 페이즈 체력을 다 깎으면 회복 연출 후 다음 페이즈 (`Gameplay/RaidBoss.cs`, 보스 전용 전환은 `IRaidPhaseTransition`).
- **가니악**: 1페이즈만 (등장 · 이동 1 · 공격 1 ~ 7 · 특수 1 ~ 4 · 50% 강화). 2페이즈 전용 패턴 없음 — 노말에서는 2페이즈도 1페이즈 패턴 그대로.
- **카구츠치 1페이즈**: 입장 · 공격 패턴 1 ~ 7 · 특수 패턴 1 (체력 70 · 40 · 10%) · 2 (체력 90 · 60 · 30%) · 야타의 거울 · 아메노무라쿠모 (`Kagutsuchi.cs`).
- **카구츠치 2페이즈: 대부분 구현됨** (`KagutsuchiPhase2.cs` · `KagutsuchiPhaseTransition.cs` · `KagutsuchiGrab*Pattern.cs` · `KagutsuchiSolar*.cs` · `Kagutsuchi*Fx.cs`). 사양 · 결정 기록은 `Raid_Kagutsuchi_Phase2_Spec_v0.1.md` 의 `[ACTIVE UPDATE]` 들 (날짜순, 아래로 갈수록 최신).
  - 구현: 1→2페이즈 진입 연출 · 입장 (워닝 → 강한 지면강타 · 넉백 · 체력 회복), 이동 1 (달리기), 공격 1 돌진 펀치 · 2 점프 돌진 → 발차기 · 3 지면 찍기 → 어퍼컷 · 4 지면 연속 찍기 · 5 연속 타격 · 6 잡기 → 어퍼컷 → 내려찍기 → 발차기 · 7 잡기 → 연속 공격, **특수 패턴 1** (태양 · 돌기둥 · 화염구 낙하, 체력 90 · 60 · 30%) · **특수 패턴 2** (`KagutsuchiSpecial2Pattern.cs`, 체력 75 · 50 · 25%) · **해금 패턴 공격 8 · 9** (특수 1 이후) · **공격 10** (특수 2 이후) (`KagutsuchiFirePatterns.cs`, 스펙 `[ACTIVE UPDATE 2026-11-16] 해금 패턴`). 공격 10 강화 = 총 3번 (`EnhancedExtraUses`, 컴파일만 확인)
  - 이펙트: 소닉붐 · 어퍼컷 풍압 · 물리 스톰프 · 연속 베기 · 화염 레이저 · 등 뒤 불꽃 등 (`Art/Generated/` = 생성 원본 · 프레임, 게임에서 쓰는 그림은 `Assets/Resources/Sprites/`)
  - 접촉 피해 (2026-11-14 사용자, `Enemy.Update`): 레이드 보스는 기본적으로 **접촉 피해 차단**, 예외로 **카구츠치 1페이즈만 허용** (2페이즈 · 가니악은 차단)
- **디버그 맵** (2026-11-16, 브랜치 `feature/debug-map`, **개발 빌드 · 에디터 전용** — 타이틀 "디버그 맵"): 보스 · 몬스터를 골라 소환 → 패턴을 하나씩 지정 실행 (자동 패턴 끄기 · 취소) → 스프라이트 외곽선 · 크기 표시, 맵 크기 기본 / 크게 ×1.5 / 중간 ×0.75 / 작게 ×0.5 (`Stage.SetPreset`, 반사 · 맵 끝 계산은 `Stage.HalfSize` 를 따라가므로 패턴 코드는 기본 맵 값 그대로). 코드: `Core/DebugMap.cs` (`IDebugBoss` + 패널), 보스별 어댑터는 `Ganiac.cs` · `Kagutsuchi.cs` · `KagutsuchiPhase2.cs` · `HobgoblinKnight.cs` · `MidBoss.cs`. 새 보스를 추가하면 `IDebugBoss` 를 구현하고 `DebugMap.Bosses` 에 한 줄. 구역 (맵 · 소환 · 대상 · 패턴 · 스프라이트 · 그림 보기 · 보기/시간/플레이어) 제목을 눌러 접고 펼 수 있고, 창 전체는 오른쪽 위 버튼 또는 ` 키로 켜고 끔 (2026-11-16, 컴파일만 확인). **패턴 안의 단계 선택 (공격 1→2→3→2 식)은 미구현 (패턴 단위만)**. 컴파일만 확인, Play 미확인.
- **2페이즈 공격 11** (잡기 → 연속 추격 → 공중 넉백) 구현됨: `KagutsuchiGrabRushPattern.cs`, 브랜치 `feature/kagutsuchi-attack11` (커밋 전), 컴파일만 확인 · Play 미확인. 특수 패턴 2 이후 해금, 강화 (체력 50% 이하: 레이저 → 초대형 화염구 → 보스 낙하 → 끌어올려 넉백) 도 구현됨 (Play 미확인). 디버그 맵 패턴 index 가 하나씩 밀림 (특수 1 = 12, 특수 2 = 13).
- **미구현 · 미정**: 2페이즈 특수 패턴 3 이후 · 3페이즈 (하드) · 익스트림 내용 · 2페이즈 체력 · 기절 · 슬로우 저항 (스펙 7장 "미정").
- Play Mode 시각 확인이 남은 항목이 스펙 곳곳에 "Unity Play Mode에서 확인 필요" 로 적혀 있음 — 작업 전에 해당 항목 확인.
- 2페이즈 그림: 수정본이 `Assets/Resources/Sprites/Kagutsuchi_P2_*` (파일 표 · 용도는 스펙 6장, 이후 `[ACTIVE UPDATE]` 에서 리소스 통합 · 이름 변경 있음). 파일을 덮어쓰거나 리사이즈하지 말고 읽기만. 그림이 없는 포즈는 도형 대체.
- **2페이즈 그림 사용 규칙**: 전부 왼쪽을 보는 옆면 (옥좌 `Intro1` · `Intro2` 만 정면) → 플레이어가 오른쪽이면 좌우 반전. 포즈마다 캔버스 크기가 달라 **발끝(아래 끝) 기준으로 정렬**하고 키는 상수로. `Kagutsuchi_P2_Run` 은 400px 칸 8프레임 한 줄 시트. 입장 등장 = `Atk_GroundSmashHeavy`, 진입 연출 = `Intro1` → `Intro2` 교체. `Atk_GroundSmash` 는 연결 패턴 없음, `Atk_Claw` · `Enemy_Kagutsuchi_P2` 는 구버전 (사용 안 함). `.meta` 는 Unity 가 만들므로 새 png 의 `.meta` 는 에디터에서 한 번 열어 생성 후 함께 커밋.
- 공용 기능 (코드에서 이름 확인함): 플레이어 이동 속도 감소 `Player.ApplySlowByBoss` · 플레이어 잡기 `Player.TryBeginBossHold` / `SetBossHold*` / `EndBossHold` (높이 · 방향 · 눕힘 · 위치) · 화면 점멸 `Fx.ScreenFlash` · 보스 피해 `Player.HitByBoss`. 끌어오기 · 맵 끝까지 넉백 · 지진 흔들림은 `KagutsuchiPhase2.cs` 쪽 구현일 수 있으나 **이름 · 위치는 미확인** — 새 패턴 전에 먼저 찾아서 재사용.
- 참고: 2026-11-14 ~ 15 의 2페이즈 커밋 4개 (`a09b918` ~ `0ca9a53`) 는 기능 브랜치 없이 `main` 에 직접 커밋됨 — 이후 작업은 위 "절대 규칙" 대로 브랜치에서.

## 프로토타입 범위 (2026-11-16 사용자)
- **프로토타입 = 노말 난이도 (1 ~ 2페이즈) 까지.** 하드 (3페이즈) · 익스트림은 **준비 중으로 잠금** (`RaidDifficultyInfo.locked`, 고를 수 없음) — 범위 밖 작업 금지.
- 레이드 보스 선택 화면에 **알레시오 생귄 · 아그니** 가 "개발 예정" 으로 표시됨 (`RaidBossInfo.locked`, 고를 수 없음, 실제 보스 없음). 가니악 · 카구츠치만 플레이 가능.
- 가니악 1페이즈 스프라이트는 **왼쪽 측면 한 벌 + 좌우 반전만** 사용 (2026-11-16 사용자, 카구츠치 2페이즈와 같은 방식). 대기 · 걷기 · 돌진 · 횡베기 연결됨 (베기 · 전방위 베기는 기존 4방향 `Ganiac_Slash`), 화면 키 3.5 — `Docs/Design/Raid_Ganiac_Plan_v0.1.md` 맨 아래 참고.
- 가니악은 노말의 2페이즈 전용 패턴이 아직 없음 (1페이즈 패턴 그대로) — 프로토타입 남은 큰 공백.

## 플레이어블 스프라이트 전환 (2026-11-17 사용자, 진행 중)
- 방침: 플레이어블 캐릭터 그림을 **왼쪽 3/4 측면 한 벌 + 좌우 반전**으로 다시 제작 (카구츠치 2페이즈 방식). 규칙 = `Docs/Design/Sprite_Rules_v0.1.md` 3장, 포즈 목록 = `Player_Poses_v0.1.md`, GPT 제작 명령문 = `GPT_Prompt_ErnStray_Run_v0.1.md`. 새 그림은 `Art/Incoming/{id}/` 에 넣고 검사 후 `Assets/Resources/Sprites/` 로.
- **에른 `ErnStray_Stand` 적용됨** (정지 모습만, 달리기 · 베기는 기존 4방향 시트). `Player.ErnStrayVisual` 의 `standBodyPx` · `standFootPx` · `standFootX` + `SpriteSheetAnimator.LoadClip(pivotX, globalSide)`. 파일이 없으면 기존 `ErnStray_Idle`. **컴파일만 확인, Play 미확인.** 3/4 스탠드가 정식 (순수 옆면 그림은 참고용).
- **에른 달리기 `ErnStray_RunSide` = 수정 중, 아직 게임에 없음.** GPT 작업본 v5 (512 × 512 × 8프레임, 4096 × 512) 를 검사만 함: 규격 · 잘림 OK, 눈으로 볼 때 5 ~ 8번이 1 ~ 4번과 거의 같은 자세 (반대 발이 앞서는 구간 없음) — 사용자 판단 대기. 2번 = 두 후보 중 첫 자세 선택됨, 4번 최종 승인 전. **사용자가 "수정 끝난 뒤 연결" 이라고 함 → 최종본이 `Art/Incoming/ErnStray/` 에 오기 전에는 로더 · 복사 금지.**
  - 인계 ZIP (43MB, v1 ~ v5 · 생성 원본 · 제작 문서 · 스크립트) 은 **저장소에 없음**: `C:\Users\11\Desktop\Project Slayers\output\ErnStray_RunSide_Handoff_2026-11-17_v1.zip` — 데스크톱으로 직접 옮길 것. 풀면 `HANDOFF.md` 먼저 읽기 (현재 기준 v5, 다음 수정은 v6 이상 새 폴더).
  - 연결 시 할 일: 수치 검사 → `ErnStray_RunSide.png` 로 복사 (기존 `ErnStray_Run` 덮어쓰기 금지) → 머리 크기 기준 크기 상수 측정 → 한 줄 시트 클립 로더 → 컴파일 → Play 확인 (발 미끄러짐 · 크기 · 반전).
- 남은 것: 에른 공격 포즈 (`Atk_SlashReady` · `Atk_Slash` · `Atk_Smash` 우선), 레니아 전체, 캐릭터 선택 카드 (아직 기존 `_Idle` 정면).
- **아이템 · 세트 효과 (노션 `세계관 관련 / 게임 관련 / 아이템` 기반) 는 스프라이트 전환 뒤에 진행.** 결정: 등급 = 1 ~ 6 정수, 능력치 보류, 몬스터 드랍 보류, 세트 효과만 구현 (마나 · 행운 · 적중 등 없는 시스템 의존 효과 제외). 코드는 아직 없음.
- UI: 보스 체력바 아래 우측 "받는 최종 피해량 +N%" 표시 추가 (`GameManager.DrawBossTakenMul`, 소스 조건부 수정자는 미반영, 컴파일만 확인).
