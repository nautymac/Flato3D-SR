# Flato3D 작업된 게임

설치기가 쓰는 프로필 원본은 `game_profiles.json`입니다. 아직 작업하지 않은 후보 544개는 `game_candidates.json`에 있습니다.

| 게임 | API · 경로 | 깊이 규칙 | 상태 | 필요한 것 |
| --- | --- | --- | --- | --- |
| Technobabylon | OpenGL (AGS) · 직접 | feet (발 위치) | 사용자 확인 완료 | opengl32.dll x86 |
| Huntdown | D3D11 (Unity) · WineD3D | order (그리는 순서) | 동작, 사용자 확인 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64, 수직동기화 강제 |
| Door Kickers: Action Squad | D3D9 (DXUT) · WineD3D | order + 레이어 캐시 | 동작, 사용자 확인 | WineD3D x86, 로더, opengl32.dll x86, csmt=0 레지스트리 |

## 게임별 메모

**Technobabylon**: AGS의 `acsetup.cfg`에서 드라이버를 OGL로 둡니다. 캐릭터는 발 높이의 깊이에 서고 방은 화면 위쪽으로 갈수록 멀어집니다. GUI는 화면 위치에 남습니다.

**Huntdown**: WineD3D의 CSMT는 켜 둬야 합니다. 설정은 다음과 같습니다.

- `[Flat] group = draw`: Unity는 시차 레이어를 같은 아틀라스로 각각 따로 그리므로, draw 하나를 한 층으로 봅니다.
- `order_curve = 0.5`: 배경 레이어에 깊이 범위를 더 넓게 줍니다.
- `[Display] swap_interval = 1`: 게임이 끄는 수직동기화를 켭니다.

한때 보이던 대각선은 수직동기화와 상관없었습니다. 사각형의 두 삼각형이 따로 밀려서 생긴 것이었고, 래퍼에서 고쳤습니다.

- 남은 일: 컷신으로만 측정했습니다. 게임 플레이 화면은 사용자 확인이 필요합니다.

**Door Kickers: Action Squad**: 레벨 전체를 1024×1024 캐시(약 416×234 영역)에 먼저 그리므로, 이 캐시를 스테레오로 승격합니다. `cache_scale = 0.406`을 씁니다. 게임은 F5로 fps 통계를 켜고 끄는데, 래퍼 단축키는 게임에 전달되지 않게 막습니다.

- 남은 일: 캐시 해상도 때문에 깊이 단계가 거칩니다. 캐시를 더 크게 그리게 할 계획입니다.
- 남은 일: 가끔 시작 직후(프레임 3)에 약 100초 멈춥니다. 자동 실행에서 DK와 Huntdown 둘 다 봤고, 주로 DLL을 바꾼 뒤 첫 실행이었습니다. 셰이더 컴파일은 원인이 아닙니다(캐시를 비워도 43ms). 원인은 아직 모릅니다.
