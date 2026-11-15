# 일반 패턴용 물리 지면 강타 이펙트 v1

- 사용자 요청: 이전 화염 지면 강타는 보존하고, 일반 패턴의 Stomp 등에 쓸 물리 지면 강타를 별도로 제작.
- 발구르기 충격 → 회갈색 먼지와 돌조각 분출 → 최대 충격 → 확산 먼지 고리 → 낮은 먼지 → 마지막 잔먼지. 불꽃·마법·오라·발광 효과 없음.
- 내장 GPT image_gen 생성. StompAttack 이미지는 타격 동작의 맥락 참고로만 사용하며 캐릭터·발은 이펙트에 포함하지 않음.
- 6프레임, 각 512×256 RGBA, 시트 3072×256, 단발 재생용. 참고 미리보기 12fps(게임 확정 재생 속도가 아님), GIF 80ms/프레임으로 약 0.48초.
- Unity 리소스: Assets/Resources/Sprites/Kagutsuchi_P2_Fx_PhysicalStomp.png. 개별 최종 PNG curated/, 미리보기 previews/stomp.gif, 프레임 위치/순서는 manifest.json.

## 제작·검수

- sprite-gen prepare → 내장 image_gen 행 생성 → 기존 canonical extract/compose-atlas/preview/compose-gif/export-pngs/inspect 사용. 수동 그림 수정/셀 자르기 없음.
- 6개 고유 프레임, 경계 잘림 픽셀 0, 크로마 인접 픽셀 0, 자동 오류 없음. 마지막 잔먼지가 작다는 자동 크기 경고는 소멸 표현으로 기록.
- 추출에서 떨어진 위성 조각 1개가 제외되어 마지막 프레임은 일부 잔먼지만 남음. 그림을 수동으로 채우거나 누락을 숨기지 않음.
- 접촉 시트 시각 확인: 돌/먼지 충격의 시작·최대·소멸 구분, 화염 요소 없음. 짧은 6프레임 효과 시안.
- 기존 화염 GroundSmash 이펙트 및 다른 PNG는 보존. 신규 PNG와 결과 폴더만 추가. 게임 코드·피해·패턴 시간·씬·프리팹 변경 없음.
- 게임 연결·실제 재생 속도·크기·발밑 위치·레이어는 미적용/미검증. Unity Play Mode 확인 필요.

## 생성 프롬프트

Six-frame purely physical ground stomp impact for a 2D action game. Reference is only a powerful foot hitting earth; no character or feet. No fire, flames, lava, embers, magic, aura or golden glow. Charcoal-grey angular stone chips and neutral grey/grey-brown painted dust. Slight top-down low ground-hugging blast and elliptical dust shockwave. Exactly six distinct temporal phases in one horizontal row on true transparent alpha, same impact center and world scale: small sharp grey-white dust impact; strong low radial dust spikes throwing embedded rock chips; peak dense low dust crown; expanding dust ring with center clearing and rocks dropping; thin low dissipating dust ring; faint last dust residue. Rocks overlap main dust silhouette, no far satellites. Generous transparent gutters and margins. No terrain tile, solid crater disk, persistent cracks, floor texture, scenery, text, grid, checkerboard or opaque matte. Smooth painted game VFX, not photoreal or pixel art.

## [ACTIVE UPDATE] 보류용 전환

새 앞뒤 분리 버전(Kagutsuchi_PhysicalStomp_Depth_Fx_v1)이 현재 게임 표시를 대체. 이 원화는 추후 적합한 용도에 사용하기 위해 보류용으로 보존하며 삭제하지 않음. runtimeConnected=false.
