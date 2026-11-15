# 지속 발사용 화염 레이저 v1

- 사용자 요구: 폭 1로 맵을 관통할 만큼 긴 화염 레이저, 지속 발사 시 반복 재생 가능한 애니메이션.
- 6프레임 반복형. 각 프레임 세로 방향 128×2048, 시트 768×2048 RGBA. 폭을 유지하고 길이만 별도 조절/회전하여 사용하기 위한 긴 세로 원화.
- 일직선 흰색/노랑 코어와 주황/진홍색 외부 불꽃이 유지되며 시간 위상에 따라 굴곡과 열류가 변함. 발사 시작/종료 애니메이션은 포함하지 않음.
- 미리보기 12fps, 한 주기 약 0.5초. GIF 80ms/프레임으로 약 0.48초 반복. 실제 게임 재생 속도 확정값 아님.
- Unity 리소스: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_FlameLaser.png. 개별 최종 프레임 curated/, 미리보기 previews/beam.gif, 순서/프레임 rect manifest.json.

## 연결 시 크기 기준

- 전체 셀에 투명 여백이 포함됨. 최대 가시 폭 약 80px, 가시 길이 약 1731px. 모든 프레임의 끝은 약 y=159..1889(좌상단 기준)에 정렬.
- 가시 폭을 월드 1로 맞추려면 PPU 80 상당의 기준으로 가로 스케일을 맞추고, 세로는 목표 광선 길이에 맞춰 독립 조절. 방향은 세로 기본축에서 회전.
- 맵을 통과하는 실제 광선 길이와 충돌/피해 폭 1은 게임에 연결할 때 별도 계산해야 함. 이 작업은 그래픽 제작이며 실제 레이저 공격/피해 코드는 추가하지 않음.
- 원화 자체가 무한 세로 타일링을 보장하는 텍스처는 아님. 현재는 긴 단일 광선 길이 조절용이며, 중간 구간 반복/UV 스크롤 방식은 추후 연결 설계가 필요.

## 제작·검수

- 내장 GPT image_gen으로 SunFire의 불꽃 색감/재질을 참고해 생성. 기존 에셋 보존.
- sprite-gen prepare와 canonical extract/compose-atlas/preview/compose-gif/export-pngs/inspect 사용. 수동 그림 수정/수동 격자 자르기 없음.
- 6개 고유 프레임. 자동 추출·아틀라스·검사 오류/경고 없음. 가장자리 잘림 픽셀 0, 크로마 인접 픽셀 0. 유사한 고정 길이와 코어를 유지하는 시퀀스 접촉 시트 확인.
- 반복 불꽃 변화의 시안이며 실제 장시간 게임 재생의 이음새/깜빡임·폭 1 및 맵 관통 길이·블렌딩은 Unity Play Mode 미검증.
- 신규 PNG 및 결과 폴더만 추가. 코드·패턴 시간·피해·씬·프리팹 수정 없음.

## 생성 프롬프트

Sustained-firing fire laser animation, six distinct sequential extremely long thin vertical beams side by side in one horizontal row. Tall portrait source with six invisible 128×2048 slots, transparent gaps and safe margins. Match only supplied painted white-yellow/orange/crimson fire material. Straight continuous white-hot core, thin orange plasma and crimson flame filaments. Stable axis, width, length, center and brightness in all phases; compact same-width ends, no fireballs or wide flares. Animate only upward flowing flame turbulence and travelling hot streaks, sixth phase returning to first. No growth, width pulsing, startup or fade. One connected silhouette per beam, no detached embers. True alpha, no background, matte, checkerboard, ground, characters, weapons, text or grids. Smooth painted anime fantasy VFX, not pixel art. Intended world width1 and independently adjustable map-piercing length.
