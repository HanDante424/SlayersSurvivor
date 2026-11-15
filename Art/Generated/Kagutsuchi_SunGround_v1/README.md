# 초거대 화염구 지면 이펙트 v1

- 내장 GPT image_gen으로 제작한 단일 투명 배경 지면 스프라이트. 애니메이션 시트가 아님.
- 기존 Kagutsuchi_P2_SunFire.png를 불꽃 색감·질감 기준으로 사용.
- 큰 화염구의 지면 접촉부를 둘러싸는 넓은 타원형 화염 고리. 중앙은 투명하여 화염구와 별도 레이어로 배치 가능.
- 원본 해상도 1903×826 RGBA. 리사이즈·재도색·알파 변경 없이 복사. 중앙 알파 0. 세부 검사는 inspection.json.
- 생성 원본 가장자리에는 최대 알파 1인 극미세 픽셀이 있음. 알파 16 초과의 가시 영역은 검사 파일에 기록.
- Unity 리소스: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_SunGround.png.
- 신규 PNG와 이 제작 기록만 추가. 기존 이미지·게임 코드·피해 판정·씬·프리팹 수정 없음. 게임 자동 연결·실제 월드 크기·레이어·지면 정렬은 미적용/미검증.

## 생성 프롬프트

Single isolated 2D game VFX ground sprite on true transparent alpha. A large ground-contact fire effect for the supplied colossal solar fireball, matching its detailed hand-painted anime fantasy flames and molten white-yellow/orange/crimson palette. Reference is only flame rendering and color, not the sphere. One broad horizontal elliptical annular inferno ring viewed from a modest top-down game angle, with a large clear transparent empty central opening for layering a separate gigantic fireball. Broad low flame tongues roll and curl outward around the circumference, brighter white-yellow inner rim, rich orange body, deep scarlet outer tips. Energetic immense scale, flowing flame patterns, visually grounded horizontal spread. Back arc narrower, front arc powerful outward curls without filling the center. Approximate silhouette ratio 2.3:1, centered landscape canvas with transparent margins. No sphere, character, stones, debris, floor texture, terrain, lava pool, smoke, text, UI, opaque background or drawn checkerboard. Soft-alpha flame edges and transparent center.
