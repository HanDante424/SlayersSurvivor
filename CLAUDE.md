# CLAUDE.md — SlayersSurvivor (Claude Code용)

프로젝트 규칙 · 구조 · 현재 진행 상태는 **[AGENTS.md](AGENTS.md)** 가 기준입니다 (Claude · Codex 공용). 먼저 읽고 따르세요.
이 파일은 Claude Code 로 작업할 때만 필요한 환경 메모입니다. 소통 · 주석 · 커밋 메시지는 한국어.

## 시작할 때
1. `AGENTS.md` "현재 진행 상태" 로 어디까지 됐는지 확인 → 필요하면 `git log --oneline -15` 로 최신 커밋 대조 (GPT · Claude 가 번갈아 작업함, 문서가 코드보다 늦을 수 있음).
2. 작업 대상 기획서는 `Docs/Design/` (가니악 `Raid_Ganiac_Plan_v0.1.md`, 카구츠치 1페이즈 `Raid_Kagutsuchi_Plan_v0.1.md`, 2페이즈 `Raid_Kagutsuchi_Phase2_Spec_v0.1.md` 의 `[ACTIVE UPDATE]` 가 최신).
3. 기획서에 없는 값은 `[임시]`, 다르게 해석한 곳은 `[해석]` 으로 코드 · 문서 · 보고에 표시 (AGENTS.md 절대 규칙).

## 작업 방식 (2026-11-16 사용자 합의 — 용량 줄이기 작업에서 자리 잡은 방식)
1. **확인한 것과 못 한 것을 구분해서 보고.** 문서 · 기억이 아니라 코드 · 로그 · 수치로 확인한 것만 "확인됨". Play 에서만 알 수 있는 것은 "미확인".
2. **위험한 변경은 실제 프로젝트를 건드리지 않고 복사본 · 임시 폴더에서 먼저 시험** → 수치로 결과 확인 → 되면 실제 프로젝트에 적용 → 같은 결과가 나오는지 다시 확인.
3. **큰 일괄 적용 전에 한 장 · 소규모로 먼저** (조합별로 나눠 시험해 어느 설정이 효과인지 분리).
4. **다른 AI (Codex · GPT) 의견은 코드로 검증해서 수용 또는 반박.** 맞으면 인정하고 방향을 고침 (예: 밉맵 + 비정형 크기 조합 지적).
5. **되돌릴 수 있게**: 삭제 대신 이동, 설정은 한 줄로 롤백, 제외 목록 두기. 시험이 실패하면 바로 원복하고 이유를 보고.
6. **품질에 영향이 있는 변경은 비교용 빌드를 따로 만들어 사용자가 눈으로 보고 결정.** 사용자가 OK 하기 전에는 실제 프로젝트에 반영하지 않음.
7. 요청 범위 밖은 건드리지 않고 (다른 AI 가 만든 코드 포함), 위험이 크면 "하지 않은 이유 + 대안" 으로 보고. 커밋 · 푸시는 사용자가 요청할 때만.

## Windows 환경 메모 (PowerShell)
- `git` 이 PATH 에 없을 수 있음 → GitHub Desktop 번들 git 사용:
  `C:\Users\11\AppData\Local\GitHubDesktop\app-3.6.10\resources\app\git\cmd\git.exe` (버전 폴더는 바뀔 수 있으니 없으면 `Get-ChildItem "$env:LOCALAPPDATA\GitHubDesktop" -Recurse -Filter git.exe`)
- 한글 커밋 메시지는 PowerShell 따옴표가 깨지므로 **파일에 UTF-8 로 쓴 뒤 `git commit -F 파일`**. 한글 출력은 `[Console]::OutputEncoding=[Text.Encoding]::UTF8` 먼저.
- 푸시 전 `$env:GIT_TERMINAL_PROMPT="0"`.
- 커밋 · 머지 · 푸시는 사용자가 요청할 때만. 흐름: `git switch -c <브랜치>` → 커밋 → `git switch main` → `git merge --no-ff` → `git push origin main <브랜치>`.

## 컴파일 확인 (Unity 에디터가 열려 있을 때도 가능)
Unity 번들 Roslyn 으로 `Assets/Scripts` 전체를 컴파일하면 에러만 빠르게 확인됨 (실행은 안 됨):
- `$ed = "C:\Program Files\Unity\Hub\Editor\6000.3.22f1\Editor\Data"`
- `dotnet.exe` = `$ed\NetCoreRuntime\dotnet.exe`, 컴파일러 = `$ed\DotNetSdkRoslyn\csc.dll`
- 옵션: `-target:library -nostdlib -langversion:9 -define:UNITY_EDITOR`, 참조 = `$ed\NetStandard\ref\2.1.0\netstandard.dll` + `$ed\Managed\UnityEngine\*.dll` (UnityEditor.dll 은 중복 오류가 나므로 넣지 않음)
- 임시 파일 (`.rsp` · 출력 dll) 은 프로젝트 밖 scratchpad 에. 에디터 로그 (`%LOCALAPPDATA%\Unity\Editor\Editor.log`) 의 `error CS` 도 확인.
- **Play 동작은 확인 못 함** → 보고에 "컴파일만 확인, Play 미확인" 이라고 정확히 쓸 것.
- 배치 빌드 (`Unity.exe -batchmode -executeMethod BuildWindows.Development -buildOutput <폴더>`) 는 에디터가 열려 있으면 충돌 → 프로젝트를 임시 폴더에 복사해서 실행. 메뉴 SlayersSurvivor → 빌드 도 있음.

## 하지 않는 것
- 씬 · `.asset` 직접 편집, `.meta` 삭제 · 재생성, 이미지 덮어쓰기 · 리사이즈 (AGENTS.md).
- 요청 없는 리팩터링 · 다른 보스 동작 변경. 보스 패턴 수치는 파일 맨 위 상수로.
- 확인 안 한 것을 됐다고 말하기 — 특히 Play Mode 에서만 알 수 있는 연출 · 판정 · 그림 위치.

## 에이전트 · 스킬 (`.claude/`)
- `skill-qa` 에이전트: 스킬 구현을 기획서와 대조 (코드를 고치지 않음, 근거 `파일:줄` 보고).
- `sprite-intake` 스킬: 새 스프라이트 묶음(zip · 폴더 · png) 검사 → `Assets/Resources/Sprites` 에 넣고 코드 연결.
