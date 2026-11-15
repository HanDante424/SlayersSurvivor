# 직선 베기 VFX

Built-in image_gen으로 제작. 원본은 generated-source.png, 게임용은 sprite-sheet-alpha.png 및 Assets/Resources/Sprites/Kagutsuchi_Fx_StraightSlash.png. 1536×160 셀 6행, 위에서 아래 순서. 생성 결과의 베기 중심선을 각각 정렬했고 원본 알파를 보존.

프롬프트 요약: 은백색 물리 직선 베기, 투명 배경, 생성→최대 섬광→가느다란 잔상→소멸의 6프레임, 곡선/불꽃/검 본체 없음. 원본 생성 요청은 이 채팅의 image_gen 호출 참조.

특수 패턴 2의 직선 타격에 기존 0.15초 동안 재생. 피해·판정은 기존 RaidLaser 소유. GIF는 검수용 약 0.18초(프레임당 30ms)이며 게임 타격 시간을 바꾸지 않음. Unity Play Mode 표시 검수 필요.
