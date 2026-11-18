# 생성 VFX 사용 분류

## 사용자 확정 분류 원칙 (2026-11-15)

- **사용 중**: 역할과 실제 인게임 연결이 모두 반영된 자산.
- **채택·연결 대기**: 사용할 역할은 정해졌고 실제 인게임 연결은 아직 하지 않은 자산.
- **보류용**: 당장 사용하려는 역할에는 적합하지 않지만 추후 활용 가치가 있어 보존하는 자산. 실패작/폐기 대상으로 취급하지 않는다. 구체적인 추후 역할은 사용자가 결정하기 전까지 확정하지 않는다.
- **구버전·참고용**: 보정본에 의해 대체된 이전 버전. 현재 사용할 버전과 구별한다.
- `runtimeConnected`는 역할 채택 여부와 별개인 실제 연결 상태를 나타낸다. 기존 사용 중 자산은 각 폴더 manifest/README의 연결 기록을 따른다.

## 이번 확정 자산

| 자산 | 상태 | 용도 | 인게임 연결 |
|---|---|---|---|
| [측면 소닉붐 v2](Kagutsuchi_SonicBoom_Side_Fx_v2/README.md) | 사용 중 | 카구츠치 발차기·펀치(어퍼컷 제외) | 연결됨 |
| [풍압](Kagutsuchi_KickWind_Fx_v1/README.md) | 보류용 | 추후 적합한 역할 검토 | 미연결 |
| [정면 소닉붐](Kagutsuchi_SonicBoom_Fx_v1/README.md) | 보류용 | 추후 적합한 역할 검토 | 미연결 |
| [측면 소닉붐 v1](Kagutsuchi_SonicBoom_Side_Fx_v1/README.md) | 구버전·참고용 | 검은 내부를 보정한 v2의 이전 원화 | 미연결 |

이번 정리는 문서/메타데이터 분류만 수행. 원화 PNG와 Unity meta의 경로/GUID는 변경하지 않았다.

## 후속 인게임 연결

측면 소닉붐 v2는 사용자 후속 요청으로 카구츠치 주먹·어퍼컷·발차기에 연결 완료. 현재 분류는 사용 중. 풍압·정면 소닉붐의 보류용 상태는 유지. 연결 상세/임시 표시값은 해당 제작 README 및 카구츠치2페이즈 기획서 참조.

측면 소닉붐의 어퍼컷 연결은 후속 결정으로 제거. 현재는 펀치·발차기만 사용하며 좌우 반쪽을 보스 앞뒤 레이어에 분리 표시한다.

## 새 스톰프 제작

[물리 스톰프 앞뒤 분리 버전](Kagutsuchi_PhysicalStomp_Depth_Fx_v1/README.md): **사용 중**. 기존 지면 타격에 뒤/앞 시트를 동시 재생하도록 연결 완료. [이전 단일 레이어 스톰프](Kagutsuchi_PhysicalStomp_Fx_v1/README.md)는 **보류용**으로 보존.

## 어퍼컷 풍압 제작

[Kagutsuchi_UppercutWind_Fx_v1](Kagutsuchi_UppercutWind_Fx_v1/README.md): 제작본·연결 대기. 지면 나선 풍압 상승과 상단 폭발의 투명 6프레임. 게임 코드 미연결.

## 어퍼컷 풍압 v2

[Kagutsuchi_UppercutWind_Fx_v2](Kagutsuchi_UppercutWind_Fx_v2/README.md): 하단이 넓은 삼각형 풍압으로 보정한 제작본. 미연결, v1 보존.

## 어퍼컷 풍압 앞뒤 분리

[Kagutsuchi_UppercutWind_Depth_Fx_v1](Kagutsuchi_UppercutWind_Depth_Fx_v1/README.md): 후방은 v2의 가득 찬 폭풍, 전방은 빈 공간이 많은 입자·바람 조각. 6프레임 동기 제작본, 인게임 미연결.

## 어퍼컷 앞뒤 분리 v2

[Kagutsuchi_UppercutWind_Depth_Fx_v2](Kagutsuchi_UppercutWind_Depth_Fx_v2/README.md): 상단을 하단 폭으로 넓힌 원기둥형 폭풍과 성긴 전방 입자. 제작본·인게임 미연결. 이전 버전 보존.

## 어퍼컷 앞뒤 분리 v3

[Kagutsuchi_UppercutWind_Depth_Fx_v3](Kagutsuchi_UppercutWind_Depth_Fx_v3/README.md): 허리를 잘록하게 보정하고 후방 밀도를 낮춘 제작본. 앞쪽 입자 유지, 인게임 미연결.

## 어퍼컷 앞뒤 분리 v4

[Kagutsuchi_UppercutWind_Depth_Fx_v4](Kagutsuchi_UppercutWind_Depth_Fx_v4/README.md): 앞뒤 파편을 늘리고 후방 나선 흐름을 개선한 제작본. 이전 버전 보존, 인게임 미연결.

## 어퍼컷 앞뒤 분리 v5

[Kagutsuchi_UppercutWind_Depth_Fx_v5](Kagutsuchi_UppercutWind_Depth_Fx_v5/README.md): 전방 입자 추가, 후방4번 상단 교체. 다른 후방5프레임은 v4 그대로. 제작본·인게임 미연결.

## 어퍼컷 앞뒤 분리 v6

[Kagutsuchi_UppercutWind_Depth_Fx_v6](Kagutsuchi_UppercutWind_Depth_Fx_v6/README.md): 후방4번 상단을 위로 터지는 풍압으로 보정. 다른11프레임 v5 보존, 제작본·인게임 미연결.

## 어퍼컷 풍압 v7 후방 재제작

[Kagutsuchi_UppercutWind_Depth_Fx_v7](Kagutsuchi_UppercutWind_Depth_Fx_v7/README.md): 얇은 바람결의 후방6프레임 전면 신규 제작. 전방은 v6 그대로. 제작본·인게임 미연결.

## 어퍼컷 풍압 v8 전방 입자 증가

[Kagutsuchi_UppercutWind_Depth_Fx_v8](Kagutsuchi_UppercutWind_Depth_Fx_v8/README.md): 전방 파티클 대폭 증가. 후방은 승인한 v7 그대로 보존. 제작본·인게임 미연결.

## 어퍼컷 풍압 v9 전방 조화 보정

[Kagutsuchi_UppercutWind_Depth_Fx_v9](Kagutsuchi_UppercutWind_Depth_Fx_v9/README.md): 전방을 후방의 섬세한 바람결과 나선 흐름에 맞춘 제작본. 후방 v7 그대로, 인게임 미연결.

## 어퍼컷 풍압 v9 인게임 연결

v9가 현재 사용 중. 일반3·연계5·잡기6·7의 어퍼컷에서 앞뒤 동기 재생. 다른 버전은 제작 이력으로 보존. 상세는 v9 제작 README 및 2페이즈 기획서 참조.

## 가니악 기본 대기 화풍 재제작

[Ganiak_Stand_KagutsuchiStyle_v1](Ganiak_Stand_KagutsuchiStyle_v1/README.md): 카구츠치에 맞춘 가니악 왼쪽 대기 제작 후보. 오른쪽은 수평반전 검수, 인게임 미연결.

## 가니악 걷기 재제작

[Ganiak_Walk_KagutsuchiStyle_v1](Ganiak_Walk_KagutsuchiStyle_v1/manifest.json): 새 대기 원화를 기준으로 왼쪽 걷기14프레임 제작. 4키포즈+8중간포즈는 이미지 생성, 시작/끝은 표준화 대기 원본. 투명 시트·프레임·흰 배경 GIF·검수 시트 포함. 오른쪽은 좌우반전 사용. 제작본·인게임 미연결.

## 가니악 측면 걷기8프레임 재시도

[Ganiak_Walk_Side_8_v1](Ganiak_Walk_Side_8_v1/README.md): 사용자 제공 명령에 따른8프레임 A/B1254시트 검수 후보. 이전14프레임은 미채택, 보존만 함. 인게임 미연결.


## Ganiak_Stand_Profile_v1

왼쪽 정측면 단일 대기 기준 후보. 사용자 외형 검수 대기, 인게임 미연결. 비교 이미지·반전 미리보기·프롬프트·검수 정보를 함께 보관합니다.
