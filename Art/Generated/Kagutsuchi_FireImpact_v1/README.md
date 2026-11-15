# 카구츠치 화염구 착지 이펙트 v1

- 제작: 내장 GPT image_gen, native RGBA 투명 출력. 유료 API·Grok 사용 없음.
- 기존 Kagutsuchi_P2_Fireball.png를 색감·질감 기준으로 사용. 기존 원본 수정 없음.
- 6개의 서로 다른 프레임, 각 512×512, 한 줄 3072×512 PNG. 미리보기 12fps(게임 확정 재생 속도 아님), 비반복 효과용.
- 점화 → 상승 화염 → 최대 폭발 → 퍼지는 충격파 → 낮아진 불꽃 → 잔광 고리.
- Unity 리소스: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_FireImpact.png. 기준점: 각 칸 x=256, 아래 여백 40px(화면 좌상단 y=472). 실제 지면 원점은 게임 연결 시 확인 필요.
- 개별 PNG는 curated/에 내보냈으며, 미리보기는 previews/impact.gif, 재생 정보·프레임 영역은 manifest.json.

## 제작·검수

- sprite-gen prepare → 내장 image_gen 행 생성 → canonical extract → compose-atlas/preview/compose-gif/export-pngs/inspect 사용.
- 생성 단계는 imagegen 스킬의 기본 도구 우선 규칙에 따라 gen-set CLI 대신 내장 image_gen을 사용. 후처리는 엔진 기본 도구 그대로이며 수동 그림 수정·수동 셀 자르기 없음.
- prepare의 effect 프로필에도 캐릭터용 일반 금지 문구(impact bursts 금지 등)가 출력되는 문제가 있어, 실제 생성은 아래 효과 전용 프롬프트로 수행. 반대 문구는 전송하지 않음.
- 첫 시안은 분리된 불꽃/링 조각 일부가 component 추출에서 누락되어 채택하지 않음. projection 옵션도 동일한 조각 누락을 보여 연결된 효과 실루엣으로 이미지 재생성.
- 수정본은 6개 고유 프레임, 알파 배경, 경계 잘림 0, 크로마 인접 픽셀 0, 착지 기준선 y=472 공통 확인. 자동 추출·아틀라스 검사 통과.
- 자동 크기 경고: 첫/마지막 그림이 중앙값보다 작음. 점화와 소멸 표현에 따른 의도된 변화.
- 시퀀스 접촉 시트 시각 확인: 점화·폭발·잔광 구분 가능. 짧은 6프레임 효과 시안이며 Unity Play Mode의 실제 재생·블렌딩·월드 크기·지면 정렬은 미검증.
- 게임 코드·피해·경고·낙하 시간·씬·프리팹 변경 없음. 자동 재생 연결은 아직 없음. 신규 에셋만 추가.

## 최종 생성 프롬프트

Refine the six-frame fire impact animation strip. Keep painted fantasy flame palette, six phases and one horizontal row on genuine transparent background. In every frame fire forms one connected silhouette, with no detached sparks or ring arcs. Frame 1 compact ignition; frame 2 rising flame crown; frame 3 peak eruption with wide base ring; frame 4 collapsing fire on expanding ring; frame 5 full unbroken wide elliptical flame ring connected to small central flame; frame 6 one thin faint red-orange unbroken dying ember ring. Fixed ground baseline and world scale; six self-contained evenly spaced square slots with generous padding. No text, guides, checkerboard, opaque matte, terrain, smoke or characters.
