# 측면 소닉붐 애니메이션

사용자 요청으로 기존 정면 소닉붐을 built-in image_gen 편집: 압축 고리를 옆에서 본 세로 타원 형태로 변경. 오른쪽으로 터지는 강타 충격파와 왼쪽으로 끌리는 속도 잔상. 기존 정면 소닉붐 및 KickWind는 보존.

- 게임PNG: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_SonicBoomSide.png
- 1536×1024 RGBA, 셀512×512, 3열×2행,6프레임. 왼쪽→오른쪽/위→아래.
- 중심 섬광→소닉붐 폭발→팽창→잔상 소멸. 투명배경, 원본과 게임PNG/시트의 알파 동일.
- frames/00..05.png, preview.gif, manifest.json, prompt.txt 포함. GIF는50ms×6=0.3초 후0.4초 공백으로 검수용 반복. 게임 재생속도/크기/피해 미확정.
- 검증: 6프레임 RGBA/격자/개별 이미지 차이/alpha>16 경계가 셀 내부인지 확인. 시트 육안 검토.
- runtimeConnected=false, 원화/프레임 제작만 수행. 기존 파일 덮어쓰기/삭제·코드/씬/설정/meta 변경 없음. Unity Editor/Play Mode 미실행. 인게임 연결 시 실제 타격 방향/접촉점/반경과 표시 수명 검수 필요.

## [ACTIVE UPDATE] 2026-11-15 사용 분류

- 상태: **구버전·참고용**
- 내부에 불투명한 검정/회색이 남은 이전 버전. 실제 사용은 보정된 측면 v2를 기준으로 함.
- 게임 연결 상태: 미연결(runtimeConnected=false). 이번 분류 결정과 실제 인게임 연결 완료 여부는 별개로 관리.
