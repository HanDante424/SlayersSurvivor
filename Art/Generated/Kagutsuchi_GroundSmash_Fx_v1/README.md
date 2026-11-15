# 카구츠치 지면 강타 이펙트 v1

- 내장 GPT image_gen으로 제작. 기존 GroundSmash는 주먹 강타 동작의 참고, SunFire는 카구츠치 불꽃 색감·질감 참고로 사용. 캐릭터는 이펙트에 포함하지 않음.
- 지면 섬광 → 강한 방사형 불꽃 → 최대 강타 폭발 → 낮게 퍼지는 화염 충격파 → 소멸 고리 → 잔광의 6프레임.
- PNG 3072×256, 프레임당 512×256 RGBA, 투명 배경. 단발 효과용이며 manifest의 loop=false.
- 미리보기 기준 12fps(약 0.5초). GIF 시간 양자화로 프레임당 80ms, 전체 약 0.48초. 게임의 확정 타격/재생 시간이 아님.
- Unity 리소스: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_GroundSmash.png.
- 개별 프레임 curated/, GIF previews/smash.gif, 프레임 영역과 순서는 manifest.json.

## 제작·검수

- sprite-gen prepare → 내장 image_gen으로 행 생성 → canonical extract/compose-atlas/preview/compose-gif/export-pngs/inspect. 생성 단계는 imagegen 스킬의 내장 도구 우선 경로. 수동 그림 수정·수동 셀 자르기 없음.
- 6개 고유 프레임, 가장자리 잘림 픽셀 0, 크로마 인접 픽셀 0, 자동 오류 없음. 최초 섬광과 최종 잔광의 면적이 작은 것은 폭발/소멸 표현이며 자동 크기 경고에 기록됨.
- 프레임 접촉 시트 시각 확인: 작은 충격→강타 폭발→고리/잔광으로 변화. 6프레임의 짧은 단발 시안.
- 신규 에셋과 제작 기록만 추가. 기존 이미지·게임 코드·피해·패턴 시간·씬·프리팹 수정 없음.
- 게임 자동 연결은 미적용. Unity Play Mode의 실제 재생·지면 중심 정렬·월드 크기·레이어·색상 합성은 미검증.

## 생성 프롬프트

One horizontal six-frame animation sprite strip of a violent fist ground slam impact in a 2D fantasy action game. Reference 1 is only fist striking floor action context, without drawing the character or fist. Reference 2 is the approved white-hot yellow, gold/orange and crimson painted flame palette. Effect alone on true transparent alpha. Low outward-rushing ground impact, sharp radial energy and elliptical shockwave, slightly viewed from above. Exactly six distinct time phases with fixed impact point and world scale: compact flattened impact flash; explosive low radial flame spikes; peak broad jagged flame crown and ground ring; thinner outward shock ring and fading center; low orange-red dissipating ring; faint thin red ember ring. Expands and fades, not a loop. One connected flame shape or continuous ring per frame. Transparent gutters and padding, no clipping or overlap. Smooth detailed anime fantasy VFX, no rocks, crater texture, terrain, smoke, symbols, text, grids, opaque matte or checkerboard.
