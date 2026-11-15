# 물리 스톰프 — 앞뒤 분리용 새 애니메이션

사용자 요청으로 built-in image_gen 제작. 불꽃 없이 회색·회갈색 먼지와 돌 파편이 낮은 타원 고리로 퍼지는6프레임. 중앙은 투명하며 보스/지면이 비칠 수 있음. 기존 물리 스톰프 원화와 현재 게임 연결은 보존.

## 저장 파일

- 전체: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_PhysicalStompDepth.png (1536×1024, 셀512×512)
- 뒤: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_PhysicalStompDepth_Back.png (1536×512, 셀512×256)
- 앞: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_PhysicalStompDepth_Front.png (1536×512, 셀512×256)
- 각 시트3열×2행, 왼쪽→오른쪽/위→아래 순서.

## 분리·배치 기준

전체 셀의 가로 중심선 y256에서 분리. 위쪽이 뒤(보스 뒤 레이어), 아래쪽이 앞(보스 앞 레이어). 뒤쪽 Sprite pivot=(0.5,0), 앞쪽 pivot=(0.5,1)을 같은 원점/PPU/배율로 배치하면 절단선이 정확히 만남. 두 시트의 같은 인덱스를 동시에 재생해야 함. 인게임 높이/범위/재생 시간은 이번에 확정하지 않음.

원본 RGBA 알파를 보존하고 앞뒤 시트는 픽셀 크롭만 수행. PNG에 배경을 그리지 않음. 6프레임 모두 앞뒤 재결합 시 원본 픽셀과 완전히 동일, 각 셀 alpha>16 경계 내부/중앙 알파0~1 확인. GIF는 어두운 검수 배경,80ms×6=0.48초 후0.32초 공백(미리보기 전용).

상태: 검수·연결 대기, runtimeConnected=false. 현재 인게임 스톰프는 기존 버전 그대로. 신규PNG/제작산출물만 추가, 기존 PNG/meta/코드/씬/설정 수정·삭제 없음. Unity Editor/Play Mode 미실행. 연결 시 앞뒤 가림·발 원점·배율/시각적 판정 일치·수명/중단 정리 확인 필요.

프롬프트는 prompt.txt. 전체/뒤/앞 시트, 개별프레임 frames/00..05.png, preview.gif, manifest.json 포함.

## [ACTIVE UPDATE] 인게임 연결

현재 사용 중(runtimeConnected=true). KagutsuchiPhysicalStompFx가 뒤/앞 시트를 함께 재생. 보스와 같은 sorting layer에서 뒤는 본체-2, 앞은 본체+1. 기존 공격2착지/3발구르기/4연속발구르기/5마지막찍기/잡기6Stomp/잡기7GroundSmash 지점을 그대로 사용. 기존12fps·0.5초 유지, 가시 최대폭491px를 기존 판정 지름에 맞춤. 생성슬롯/12Sprite 재사용 및 페이즈 종료·비활성화·승패 종료 시 동시 정리. 독립 C# 컴파일/앞뒤 재결합·피벗 계산 통과, 실제 Unity 합성 검수는 남음. 이전 미연결 설명은 제작 당시 이력.

## [ACTIVE UPDATE] 표시 Y위치 보정

스톰프 표시의 공통 Root를 기존 발생 위치에서 월드Y-2.5 이동. 앞뒤 절반에 함께 적용되며 실제 지면/피해 판정·크기·재생시간·레이어는 유지.

## [ACTIVE UPDATE] 2026-11-15 스톰프 표시 Y +1

사용자 요청으로 스톰프 앞뒤 공통 표시를 현재 위치에서 Y +1 이동. 최종 오프셋은 발생점 기준 Y -1.5. 판정·크기·재생시간·레이어 및 게임플레이 위치는 유지. 코드와 제작 manifest/README 및 사양 문서만 수정, 신규·삭제·구조 변경 없음. Unity 화면 확인은 미실행.
