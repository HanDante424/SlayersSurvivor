# 초거대 화염구 지면 화염 애니메이션 v1

- 기존 단일 Kagutsuchi_P2_Fx_SunGround.png를 기준으로 만든 반복형 화염 고리. 단일 원본 보존.
- 내장 GPT image_gen으로 여섯 시간 위상을 한 행에 생성. sprite-gen 표준 prepare/extract/compose-atlas/preview/compose-gif/export-pngs/inspect 도구로 후처리.
- 원화 생성은 imagegen 스킬의 기본 내장 도구 경로 사용. 추출·아틀라스·미리보기는 기존 엔진 사용. 수동 그림 수정이나 수동 고정폭 셀 자르기 없음.
- 프레임 6개, 각 512×256, 한 줄 3072×256 RGBA. 가운데 구멍과 배경은 투명. 고리 크기·중심을 유지하며 가장자리 불꽃만 변화.
- 재생 참고값 12fps, 주기 약 0.5초. GIF의 1/100초 시간 단위상 각 프레임 80ms(주기 0.48초). 게임 재생 속도는 별도 연결 시 설정.
- Unity PNG: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_SunGround_Loop.png.
- 개별 최종 프레임: curated/. 미리보기: previews/burn.gif. 프레임 영역·순서: manifest.json.

## 검수

- 6개 PNG 모두 고유한 그림. 모든 셀 중앙 알파 0, 외곽 잘림 픽셀 0, 크로마 인접 픽셀 0. 엔진 검사 오류·경고 없음.
- 접촉 시트에서 1→6의 불꽃 굴곡 변화, 일정한 타원 크기, 중앙 투명 영역 및 마지막→처음의 유사한 실루엣 확인. 작은 화염 변화 위주의 반복 시안.
- Unity Play Mode에서 실제 반복 재생, 블렌딩, 태양과의 크기·위치·레이어 정렬은 미검증. 게임 자동 재생 연결은 미적용.
- 신규 PNG 및 이 결과 폴더만 추가. 기존 이미지·코드·피해·패턴 시간·씬·프리팹 변경 없음.

## 생성 프롬프트

Six-frame looping sprite strip of the supplied elliptical solar ground fire ring. Preserve approved horizontal ellipse, empty transparent opening, low ground-hugging fire and detailed painted fantasy orange/yellow/white-hot/crimson palette. Exactly six distinct sequential complete rings in one horizontal row on genuine transparent alpha. Constant ring width, perspective, hole size, center, scale and brightness. Animate only visible progressing flame tongues rising, curling, bending, collapsing and reforming around the circumference and hot veins rippling along the ring. Sixth phase naturally returns to first. Each frame one connected annular silhouette; no detached sparks. Back arc low, front arc lush outward curls. Generous transparent padding and gaps. No growth, shrinking, appearing, fading, whole-ring rotation, grids, text, UI, opaque matte, checkerboard, sphere, characters, terrain or smoke.
