# SlayersSurvivor

Unity 6 (6000.3.22f1) 탑다운 생존 액션 프로토타입.

## 실행
Unity Hub → Add → 이 폴더 → `Assets/Scenes/Main.unity` 열고 Play.

## 조작
- 이동: WASD / 방향키
- 레벨업 선택: 마우스 클릭 또는 1 / 2 / 3
- 일시정지: ESC, 재시작: R (게임 오버 후)

## 현재 내용
- 무기 4종 (최대 Lv.8): 마력탄, 회전 칼날, 성스러운 오라, 낙뢰
- 패시브 7종: 체력, 이속, 재생, 자석, 힘, 가속, 범위
- 적 3종 + 3분마다 보스, 10분 생존 시 승리

## 그래픽 교체
`Assets/Resources/Sprites/` 폴더에 아래 이름으로 스프라이트(Texture Type: Sprite)를 넣으면 자동 적용됩니다.
없으면 도형으로 대체됩니다.

`Player`, `Enemy_Bat`, `Enemy_Runner`, `Enemy_Tank`, `Enemy_Boss`, `Gem`, `Bolt`, `Blade`, `Aura`, `Lightning`, `Ground`

## 밸런스 조정 위치
- 적 스탯: `Assets/Scripts/Gameplay/Enemy.cs` → `Setup()`
- 스폰 속도/난이도 곡선: `Assets/Scripts/Core/GameManager.cs` → `Spawning()`
- 무기 수치: `Assets/Scripts/Weapons/*.cs` 상단 프로퍼티
