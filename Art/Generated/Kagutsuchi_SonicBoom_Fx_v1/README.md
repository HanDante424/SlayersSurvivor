# 강타 소닉붐 애니메이션

사용자 요청에 따라 built-in image_gen으로 강력한 물리 공격의 공기 압축 폭발/충격 고리를6프레임 제작. 중심 섬광→압축 고리 폭발→최대 충격파→확장/분열→소멸. 백색·은회색, 불꽃·캐릭터·무기 없음. 기존 KickWind 원화는 보존하고 별도 새 자산으로 저장.

- 게임PNG: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_SonicBoom.png
- 1536×1024 RGBA, 512×512 셀3열×2행, 왼쪽→오른쪽/위→아래. 중심 피벗(0.5,0.5).
- 원본과 게임PNG/시트의 픽셀·알파 동일. 개별 frames/00..05.png, preview.gif, manifest.json, prompt.txt 포함.
- GIF는 검수용 어두운 배경 합성,50ms×6=0.3초 재생 후0.4초 공백. 게임 재생속도·크기·피해·사용 단계로 확정하지 않음.
- 검증: 6프레임 RGBA/격자/개별 이미지 차이/alpha>16 경계가 셀 내부인지 확인. 생성 시트 육안 검토. Unity Editor/Play Mode 미실행.
- 리소스 제작만 수행(runtimeConnected=false). 기존 코드·PNG·씬·설정·meta 변경/삭제 없음. 후속 연결 시 실제 공격 접촉점/방향/범위와 표시 수명·정리를 맞춰야 함.

## [ACTIVE UPDATE] 2026-11-15 사용 분류

- 상태: **보류용**
- 정면 소닉붐은 추후 다른 적합한 용도로 사용하기 위해 보존.
- 게임 연결 상태: 미연결(runtimeConnected=false). 이번 분류 결정과 실제 인게임 연결 완료 여부는 별개로 관리.
