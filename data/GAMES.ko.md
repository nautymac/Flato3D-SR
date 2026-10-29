# Flato3D 작업된 게임

[English](GAMES.md) · **한국어**

설치기가 쓰는 프로필 원본은 `game_profiles.json`입니다. 아직 작업하지 않은 후보 544개는 `game_candidates.json`에 있습니다.

| 게임 | API · 경로 | 깊이 규칙 | 상태 | 필요한 것 |
| --- | --- | --- | --- | --- |
| Technobabylon | OpenGL (AGS) · 직접 | feet (발 위치) | 사용자 확인 완료 | opengl32.dll x86 |
| Huntdown | D3D11 (Unity) · WineD3D | order (그리는 순서) | 동작, 사용자 확인 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64, 수직동기화 강제 |
| Door Kickers: Action Squad | D3D9 (DXUT) · WineD3D | order + 레이어 캐시 | 동작, 사용자 확인 | WineD3D x86, 로더, opengl32.dll x86, csmt=0 레지스트리 |
| FRAMED Collection | OpenGL 3.2 · 직접 | order (반복 칸은 같은 층) + 실루엣 | 사용자 확인 완료 | opengl32.dll x86 (bin 폴더) |
| Astral Ascent | D3D11 (Chowdren) · WineD3D | order + 임시 버퍼 | 사용자 확인 완료 (NVIDIA) | 이름 바꾼 WineD3D x64, 로더(정적 가져오기 지원), opengl32.dll x64, geo-11 DLL 제거 |
| Intravenous 2 | OpenGL (LÖVE 11.5) · 직접 | order (탑다운) | 사용자 확인 완료 | opengl32.dll x64 |
| Intravenous 2: Mercenarism | OpenGL (LÖVE) · 직접 | order (탑다운) | 사용자 확인 완료 (3D) | opengl32.dll x64 |
| Guns, Gore and Cannoli 2 | D3D11 (Unity 5.6) · WineD3D | order, draw마다 한 층 | 사용자 확인 완료 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64, 수직동기화 강제, Defender 예외 |
| Rocketbirds: Hardboiled Chicken | OpenGL (PSSG, Cg ARB 프로그램) · 직접 | order (draw 단위) | 동작, 사용자 확인 전 | opengl32.dll x86, `screen_batch = 0` |
| Machinarium | OpenGL 고정 기능 · 직접 (`-ogl` 스위치) | feet (발 위치, 고정 기능) | 사용자 확인 완료 | opengl32.dll x64, `[Inject] add_args = -ogl` |
| Samorost 2 | D3D11 (Adobe AIR) · WineD3D | feet, `plane_tall = 0.12` | 사용자 확인 완료 | 이름 바꾼 WineD3D x86, 로더, opengl32.dll x86, 업스케일 |
| Snufkin: Melody of Moominvalley | D3D11 (Unity URP) · WineD3D | 평면 깊이 끔 (실제 3D, 자동 Convergence) | 동작, 사용자 확인 전 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64, `[Flat] enable = 0` |
| Blade Assault | D3D11 (Unity 2D 조명) · WineD3D | order, draw마다 한 층, `screen_batch = 0` | 동작, 사용자 확인 전 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64 |
| The Messenger | D3D11 (Unity 2017.4) · WineD3D | order, draw마다 한 층, `screen_batch = 0` | 동작(멈춤 해결), 사용자 확인 전 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64 (10-24 이후 래퍼) |
| Braid, Anniversary Edition | D3D11 (자체 엔진) · WineD3D | order (템플릿 기본값) | 사용자 확인 완료 | 이름 바꾼 WineD3D x64, 로더 + 중계 `winmm.dll`, opengl32.dll x64 |
| Home | D3D11 (GameMaker Studio 2) · WineD3D | feet, `plane_tall = 1` | 동작, 사용자 확인 전 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64 |
| Axiom Verge | OpenGL (FNA, Steam 실행 옵션 `/gldevice:OpenGL`) · 직접 | order, `order_curve = 0.4`, `snap_pixels = 1` | 동작, 사용자 확인 전 | opengl32.dll x86, 업스케일, 실행 옵션 |
| Axiom Verge 2 | OpenGL (FNA, Steam 실행 옵션 `/gldevice:OpenGL`) · 직접 | order, `snap_pixels = 1` | 동작, 사용자 확인 전 | opengl32.dll x86, 업스케일, 실행 옵션 |
| Blasphemous | D3D11 (Unity 2017) · WineD3D | order, draw마다 한 층, `screen_batch = 0`, `order_weight = 0.02`, `order_curve = 1` | 사용자 확인 완료 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64 |
| Blasphemous 2 | D3D11 (Unity 2021) · WineD3D | order, draw마다 한 층, `screen_batch = 0`, `order_weight = 0.02`, `order_curve = 1` | 사용자 확인 완료 | 이름 바꾼 WineD3D x64, 로더, opengl32.dll x64 |

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

**FRAMED Collection**: 게임이 draw마다 glScissor를 걸기 때문에 `screen_batch = 0`으로 둡니다. 그렇지 않으면 화면 전체가 UI로 취급됩니다. 만화 칸은 (테두리, 그림) 묶음이 반복되는 구조라 같은 층으로 묶어서, 칸들이 한 평면에 나란히 놓입니다.

- 측정(FRAMED 첫 퍼즐, 분리값 25): 블라인드 +56px, 방 +36~42px, 칸 −2px, 화살표 −28px.
- 칸 그림은 동영상이라 인물이 그림 안에 합쳐져 들어옵니다. `[Flat] silhouette = c05a6bab`로 칸 그림 셰이더를 고쳐, 어두운 픽셀(검은 실루엣 인물)만 눈마다 옮겨 칸 그림보다 앞에 놓습니다(`silhouette_depth = 0.6`). 첫 퍼즐에서 인물이 칸 그림보다 정확히 설정한 만큼 앞에 있었고, 사용자가 원격으로 보고 만족했습니다.
- 인물은 납작한 판처럼 한 깊이로 나옵니다. 인물 안의 흰 부분(안경, 꽃)은 그림 깊이에 남고, 인물이 비킨 자리에는 옆 배경이 조금 반복됩니다.
- 남은 일: FRAMED 2는 아직 해 보지 않았습니다. 칸 안의 그림이 자기 테두리보다 살짝 앞에 있습니다.

**Astral Ascent**: exe가 d3d11.dll을 정적으로 가져오기 때문에, 로더가 exe의 가져오기 항목을 WineD3D 사본(w3d11.dll)으로 바꿉니다. geo-11이 설치돼 있으면 d3d11.dll, dxgi.dll, nvapi64.dll을 빼야 합니다.

- 글자는 한 글자씩 임시 버퍼에 그려 합성합니다. 한 프레임에 여러 번 지우고 쓰는 버퍼는 쓸 때마다 한 층으로 묶어서 대사 줄이 평평합니다.
- 애니메이션 스프라이트는 프레임마다 텍스처가 바뀝니다. 직전 순서 목록에 없는 층은 바로 앞에 그린 층 다음 자리를 받습니다.
- 측정(첫 지역, 분리값 25): 하늘 +28~54px, 섬 +30px, 칼·폐허 +20~26px, 기둥·바닥 +8px, 캐릭터 −6px, HUD −14~−18px.
- 허브 지역(폐허, 제단, 정원, 절벽)을 이동, 점프, 공격하며 지나갔습니다. 멈춤이나 오류 없이 60fps였습니다. 깜빡임은 한 번 잡혔는데 두 눈에 똑같이 나타난 게임 효과였습니다.
- 정원: 하늘 +40~64px, 바닥 +28px, 나무 +20px, 캐릭터 0~−6px, 앞 아치 −12px, HUD −20px. 절벽: 먼 섬 +40~64px, 바위섬 +28px, 앞 절벽 −18px.
- 남은 일: 캐릭터가 서 있는 바닥보다 앞에 떠 보입니다(정원에서 약 30px). 게임이 바닥을 먼저 그리기 때문입니다. 대화 장면은 초상화, 대화창, 글자가 한 UI 층입니다.
- 사용자 확인(10-18): NVIDIA GPU에서는 잘 됩니다. Intel GPU PC에서는 배경 하늘이 두 눈에서 다르게 나왔습니다(10-17 보고). NVIDIA만 보장하는 범위로 기록했습니다.

**Intravenous 2**: 설치기로 처음 설치한 게임입니다. 후보 목록의 기본값(탑다운, order 규칙)으로 설치했고, 사용자가 플레이로 확인했습니다. LÖVE 셰이더 중 밑줄로 시작하는 출력 이름 때문에 변환이 실패하던 문제를 래퍼에서 고쳤습니다.

- 남은 일: 덤프로 층별 시차를 측정하지 않았습니다.

**Intravenous 2: Mercenarism**: 후보 목록에 Direct3D 9로 올라 있어(PCGamingWiki) 설치기가 WineD3D 경로로 설치했지만, LÖVE라 OpenGL로 그리고 `opengl32.dll`만 쓰입니다. 프로필을 OpenGL 경로로 만들고 후보 목록도 고쳤습니다. 사용자가 SBS로 3D를 확인했습니다(10-19).

- 메뉴와 일시정지 화면은 Windows 화살표 커서를 씁니다. SBS에서는 Windows가 실제 마우스 위치에 커서를 하나만 그려서 어느 눈의 그림에도 커서가 없었습니다. 래퍼가 게임이 고른 커서를 두 눈의 같은 위치(화면 깊이)에 그리고, 게임 창 위의 Windows 커서는 숨기도록 고쳤습니다(`[Overlay] cursor = 1`, 기본). 임무 중에는 게임이 조준점을 직접 그리고 투명 커서를 쓰므로 래퍼가 더 그리는 것은 없습니다.
- 같은 수정으로 Door Kickers: Action Squad(WineD3D x86)와 Astral Ascent(WineD3D x64, 렌더 스레드가 창 스레드와 다름)의 메뉴 커서도 두 눈에 나오는 것을 화면 캡처로 확인했습니다.
- 남은 일: 커서가 두 눈에 나오는 것을 사용자가 아직 확인하지 않았습니다. 덤프로 층별 시차를 측정하지 않았습니다.

**Guns, Gore and Cannoli 2**: Unity 5.6(플레이어가 exe에 들어 있음). exe가 opengl32.dll을 가져와서 설치기가 처음에 OpenGL 경로를 골랐는데, Unity는 Direct3D 11로 그리기 때문에 평면이었습니다(`ggc2_Data\output_log.txt`의 `Direct3D 11.0`). 설치기가 이제 Unity를 먼저 알아보고 Unity 로그의 API로 경로를 정합니다. Huntdown과 같은 WineD3D 경로와 Unity 설정(`group = draw`, `order_curve = 0.5`)으로 다시 설치했고, 사용자가 플레이로 확인했습니다.

- Windows Defender가 x64 로더(`version.dll`)를 `Trojan:Win32/Cloxer`로 잘못 진단합니다. 이 PC는 예외로 등록했습니다.
- 남은 일: 덤프로 층별 시차를 측정하지 않았습니다. 세이브는 `AppData\LocalLow\Crazy Monkey Studios\Guns Gore and Cannoli 2`입니다.

**Rocketbirds: Hardboiled Chicken**: PSSG 엔진이 NVIDIA Cg로 셰이더를 ARB 어셈블리 프로그램(arbvp1/arbfp1)으로 컴파일합니다. GLSL이 하나도 없어 처음에는 평면이었습니다(로그 `shaders injected 0`). 래퍼가 이제 ARB 정점 프로그램을 고쳐 눈마다 위치를 옮기고, ARB 조각 프로그램이 읽는 텍스처 유닛을 알아냅니다. Cg를 GLSL로 바꾸는 방법(`cg_glsl = 1`)은 화면이 하얘져서 쓰지 않습니다. `screen_batch = 0`이 필요합니다.

- 측정: 1장 제목 화면은 앞의 나무 실루엣이 가깝고 정글과 포스터 벽이 뒤에 있습니다. 첫 게임 화면(하역장)은 −28~+44px, 중간값 +24px입니다.
- 장면을 화면에 그린 뒤 복사해 블룸을 거쳐 다시 그리므로, 한 프레임에 화면을 두 번 지웁니다. HUD가 보이는 프레임에서는 화면이 임시 버퍼로 판정돼 장면이 한 층으로 뭉쳤습니다(최종 화면은 임시 버퍼에서 빼서 고침). 고친 뒤 하역장(분리값 25): 먼 숲 +70, 덤불 +56, 잎 +28, 상자 +16, 캐릭터 +8px.
- 남은 일: draw 단위 깊이만 있습니다(ARB 프로그램에는 지오메트리 셰이더가 없어 한 draw 안의 스프라이트를 나눌 수 없음). 사용자 확인이 필요합니다. 세이브는 게임 폴더의 `Save\`입니다.

**Machinarium**: 2020년판(64비트)은 기본이 Direct3D 12이고 `-ogl`로 시작해야 OpenGL로 그립니다. Steam이 이 스위치를 넘기지 않아 `[Inject] add_args = -ogl`로 래퍼가 명령줄에 붙입니다(게임이 읽기 전에 GetCommandLine을 바꿈). Steam 실행 선택 창에서는 첫 항목(Machinarium 플레이)을 고릅니다. 다른 항목(Play with DirectX 9 / 12)은 확인하지 않았습니다.

- OpenGL은 셰이더 없는 고정 기능입니다. 3840×2160 버퍼 하나에 정사영 스프라이트를 그리고 창으로 복사합니다. 래퍼가 draw마다 투영 행렬을 옮겨 텍스처 묶음마다 층을 줍니다(첫 장면 18~23층).
- 가려진 조각을 그릴 때 스텐실만 지우는데(glClear(GL_STENCIL_BUFFER_BIT)), 이것을 장면 시작으로 보면 2~3층으로 뭉쳤습니다. 색을 지울 때만 셉니다(수정됨).
- 측정(첫 장면 고철 더미, 분리값 25): 먼 고철 더미 +70, 중간 +64, 소품 +10~+26, 앞 고철 −6~−20, 위쪽 인벤토리 막대 −16px. 4K로 그리므로 업스케일은 끕니다.
- 장면은 Flash처럼 도형에 비트맵을 채워 그리고, 배경 그림의 일부를 나중에 다시 그려 캐릭터를 가립니다(가림막). 사용자 보고(10-18, 탑 장면): 탑 아래쪽 가림막이 배경보다 앞(+4px, 배경 +68px)에 떠 직사각형으로 잘려 보였습니다. 이제 앞서 그린 그림과 같은 텍스처를 같은 위치 대응으로 다시 그리는 draw는 그 그림의 층에 붙습니다(탑 전체 +68px). 다만 그사이 그린 물체를 덮는 조각(로봇 발을 가리는 바닥 가장자리)은 가림 순서가 맞도록 앞에 둡니다.
- 그리는 순서 규칙에서는 캐릭터가 서 있는 땅보다 꽤 앞에 섰습니다(로봇 +12~20px, 배경 +68px). 그래서 고정 기능 경로에 발 위치(feet) 규칙을 넣고 Machinarium을 feet로 바꿨습니다.
  - 장면 그림은 바닥 경사(위 +60 ~ 아래 -26px, 분리값 25)이고, 가림막은 그 경사를 따릅니다.
  - 로봇은 그림자·다리·몸·머리(텍스처 두 장)가 한 물체로 묶여 발 높이(-2px, 서 있는 땅과 같음)에 섭니다.
  - 로봇 위에 그린 생각 풍선은 로봇보다 멀지 않고(-10px), 인벤토리 막대와 아이콘은 화면 깊이입니다.
  - 한계: 키 큰 배경 구조물(탑)은 바닥 경사를 따라 뒤로 기웁니다. 화면 아래까지 닿는 큰 앞쪽 조각은 위까지 가장 가까운 깊이(-24px)입니다.
- 자동 테스트는 메뉴의 "게임 계속"(게임 좌표 1000,705)을 누릅니다. "새 게임 플레이"(1120,472)는 진행을 덮어쓰므로 사용자가 플레이한 뒤에는 누르지 않습니다.
- 사용자가 feet 규칙으로 플레이하고 상태 보고 "잘 됨"을 남겼습니다(10-18, SR 출력, 분리값 38.33 저장).
- 남은 일: 첫 장면들만 덤프로 측정했습니다. 세이브는 Steam 클라우드(`userdata\<계정>\40700\remote\Machinarium.dat`)입니다.

**Rocketbirds: Hardboiled Chicken** (10-21 추가): 게임 창이 4× 멀티샘플이라 SBS/상하 출력에서 두 눈 그림이 화면에 가지 않고 게임 원래 그림이 그대로 보였습니다. 분리값을 바꿔도 변화가 없었습니다(사용자 보고). Ctrl+F12 덤프는 눈 버퍼를 읽어서 정상으로 보였습니다. 멀티샘플 창에는 두 눈을 그림으로 그려 넣게 래퍼를 고쳤습니다. SR 위빙은 원래 영향이 없었습니다. 측정(화면, 분리값 25): 메뉴 배경 +30~35px, 글자 −6px.

**Samorost 2**: Adobe AIR 게임(x86)이라 Direct3D 11을 WineD3D로 바꿔 그립니다. 4K를 지원하지 않아 설치기가 업스케일을 켭니다.

- 업스케일에서 "응답하지 않습니다"가 뜨고 클릭이 안 먹던 문제: 표시 창이 WineD3D의 그리기 스레드에 만들어져 메시지를 처리하는 곳이 없었습니다. 표시 창을 게임 창의 스레드에서 만들도록 래퍼를 고쳤습니다.
- 장면 그림을 조각으로 나눠 그립니다(덩굴 한 줄기가 위아래 두 조각, 우주선이 여러 조각). feet 규칙에서 좁은 조각마다 자기 아래 끝에 서서, 조각이 만나는 곳에 가로줄과 사각형이 보였습니다(사용자 보고). 새 키 `plane_tall = 0.12`: 화면 높이 1/16 이상인 조각은 폭과 상관없이 바닥 경사를 따릅니다. 넓고 낮은 맨 아래 땅 띠도 여기에 걸립니다. order 규칙도 시험했지만 조각이 다른 층으로 갈려 더 나빴습니다.
- 사용자가 `depth_screen 0.75`(장면을 앞으로)로 저장하자, 바닥이 `sprite_min`보다 가까워져 아래쪽 조각이 뒤로 물러나 가로줄이 생겼습니다. `sprite_min`이 이제 `depth_screen`을 따라 움직입니다(`depth_screen 0`이면 전과 같음).
- 측정(덩굴 장면, 분리값 32.9, depth_screen 0.75): 위 +30에서 아래 -90px까지 단절 없는 경사.
- 남은 일: 필터 버퍼로 그리는 일부 물체(덩굴 끝 씨앗 송이)가 줄기보다 앞(+2px, 줄기 +30~40px)에 뜹니다. 큰 물체는 바닥 경사를 따라 뒤로 기웁니다. 세이브는 `%APPDATA%\amanitadesign.samorost3\Local Store`입니다.

**Snufkin: Melody of Moominvalley**: 평면 2D가 아니라 원근 카메라로 보는 실제 3D 장면입니다. 그래서 `[Flat] enable = 0`으로 평면 깊이를 끄고 자동 Convergence를 씁니다. 원칙(10-18 결정)대로라면 일반 입체 게임이라 Geo Gaming Hub(geo-11, D3D11 그대로) 대상입니다. 사용자가 Flato3D로 설치해서 프로필을 남겨 둡니다.

- 평면 깊이가 켜져 있으면 자동 Convergence가 꺼져 시작값 3에 머뭅니다. 장면은 60~85 거리에 있어 모든 것이 먼 끝(+46~48px, 최대 48)에 붙어 한 장의 그림처럼 평평했습니다. 끄면 Convergence가 약 65에 자리 잡습니다.
- 측정(타이틀): 하늘 +39px, 언덕 +10~31px, 스너프킨과 앞쪽 −9~+3px, 메뉴는 화면 위치. 게임 안(첫 초원): 땅이 위 +11px에서 아래 +1px로 기울고, 앞 나무 −8px, 말풍선은 화면 위치.
- 오른쪽 눈에 풍경이 빠지던 문제(로그: `fbo 14: right-eye copy incomplete (0x8cd7)`): WineD3D가 멀티샘플 텍스처를 `glTexStorage2DMultisample`(고정 저장소)로 만들고 그 텍스처 뷰를 붙입니다. 래퍼가 오른쪽 사본을 고정이 아닌 저장소로 만들어서 뷰를 만들 수 없었습니다. 래퍼에서 고쳤습니다(10-20).
- 남은 일: 자동 덤프로만 확인했습니다(타이틀, 첫 초원). 세이브는 `%USERPROFILE%\AppData\LocalLow\Hyper Games\Snufkin - Melody of Moominvalley`입니다.

**Blade Assault**: Unity의 2D 조명과 후처리를 쓰는 픽셀 액션 게임입니다.

- 색: 후처리가 매 프레임 색 보정 표(1024×32 띠)를 그립니다. 래퍼가 이것을 레이어 캐시로 보고 스테레오로 만들어 오른쪽 눈에서 옆으로 밀었고, 타이틀의 오른쪽 눈이 새빨갛게 나왔습니다. 띠 모양(한 변이 다른 변의 4배 초과) 버퍼는 레이어 캐시로 보지 않게 래퍼를 고쳤습니다.
- 깊이: 2D 조명은 스프라이트마다 화면의 빛 텍스처(스테레오 버퍼)를 읽으며 다시 그립니다. 이런 draw를 합성 패스로 보고 화면 위치에 두어서, 장면 전체가 평평했습니다(두 눈이 똑같음). 스테레오 버퍼를 읽으면서 자기 그림(64px 이상의 일반 텍스처, 띠가 아님)도 그리는 draw는 조명 스프라이트로 보고 깊이를 줍니다. 앞서 그린 층을 다시 그리는 것이면 그 층의 깊이를 따릅니다. 화면 전체를 덮는 삼각형(후처리)은 여전히 화면 위치입니다.
- `screen_batch = 0`: WineD3D가 조명 패스 사이마다 viewport와 scissor를 다시 걸어, 장면이 5~29번 묶음에 그려집니다. AGS 규칙(뒤 묶음은 UI)이 장면 전체를 화면 위치에 두었습니다. 설치기는 이제 Unity 게임에 기본으로 `screen_batch = 0`을 씁니다.
- 측정(첫 복도, 시작 대화 중): 복도 +12px, 그 앞 구조물 +6px, 대화 상자는 장면보다 앞.
- 남은 일: 시작 대화 중에만 측정했습니다. 플레이 중 깊이는 사용자 확인이 필요합니다. 가끔 시작이 1분 넘게 걸립니다(NEOWIZ 로고). 세이브는 `%USERPROFILE%\AppData\LocalLow\TeamSuneat\BladeAssault`입니다.

**Blade Runner: Enhanced Edition** (프로필 없음): 원작(1997) 엔진이 장면을 CPU에서 한 장으로 합쳐 보내는 것으로 보입니다. 게임 중 한 프레임에 draw가 6번뿐이고, 1280×960 장면 버퍼에는 draw가 없습니다(사용자 실행 로그, 10-19). 층이 없어서 지금 방식으로는 입체가 되지 않습니다. 2D→3D 변환(AI 깊이 추정) 같은 다른 방식이 필요합니다. 메뉴는 주입한 마우스와 화살표 키를 받지 않아 자동 확인은 타이틀까지만 했습니다.

**The Messenger** (프로필 `the-messenger`, 10-24): Unity 2017.4 x64, D3D11을 WineD3D로. 설치기의 Unity 기본값(order, draw마다 한 층, `screen_batch = 0`) 그대로입니다.

- **멈춤 해결**: 플레이 중 수십 초~몇 분 안에 "응답 없음"으로 멈췄습니다(래퍼 없이도, SR 없이도). WineD3D가 그리기 스레드를 스레드 알림(`NtAlertThreadByThreadId`)으로 깨우는데, 남은 알림이 Windows 11 24H2+의 critical section 대기를 엉뚱하게 깨워 ntdll이 `RtlpWaitOnCriticalSection`에서 영원히 돕니다. 래퍼가 WineD3D에 알림 함수를 주지 않아 이벤트로 깨우게 했습니다(로그 `WineD3D: command thread woken by an event`). 고치기 전 19회 중 11회 멈춤 → 고친 뒤 `sbs` 10회, SR 5회(각 120초) 멈춤 없음. CSMT를 쓰는 모든 WineD3D 게임에 해당합니다. 조사 과정은 [tools/hang/README.ko.md](../tools/hang/README.ko.md)의 사례에 있습니다.
- CSMT는 켜 둬야 합니다(`csmt=0`은 시작하자마자 죽음, Huntdown과 같음). `-force-glcore`는 안 됩니다(이 빌드에 OpenGL 렌더러 없음).
- 측정(separation 25, 덤프는 한 눈 1920x1080)
  - 타이틀: 별·달·산 +68 px, 로고 +24 px, 메뉴 -10~-14 px, 아래 글자 -20~-28 px
  - 첫 장면(닌자 마을, 첫 대화): 노을 하늘 +52 px, 가운데 나무·절벽 +38 px, 위쪽 나뭇잎 +24 px, 발판 +4~6 px, 닌자 -6 px, 대화창 -26~-28 px
- 남은 일: 닌자가 발밑 발판보다 약 10 px 앞에 섭니다. 플레이 중 깊이는 사용자 확인이 필요합니다.
- 테스트 조작: 인트로(약 60초)와 "버튼을 누르시오"는 Enter로 넘기고, 메뉴와 세이브 칸은 **Space**로 고릅니다(Enter는 무시). 세이브 칸 화면에서 왼쪽 Ctrl을 누르고 있으면 세이브가 지워지니 거기서는 Ctrl+F12를 누르지 않습니다. 세이브: `%USERPROFILE%\AppData\LocalLow\Sabotage Studio\The Messenger\SaveGame.txt`(test-backups\TheMessenger, 0x81로 XOR한 JSON).

**Braid, Anniversary Edition** (프로필 `braid-anniversary-edition`, 10-25): 자체 엔진, D3D11(기능 수준 11.1) x64. 그림으로 그린 시차 층입니다.

- **로더 중계 DLL**: 실행 파일과 그것이 가져오는 DLL 어디에도 `version.dll` 가져오기가 없어서 Flato3D 로더가 한 번도 로드되지 않았습니다. 게임은 시스템 Direct3D 11로 평면 그대로 돌았고 로그도 없었습니다. 이제 설치기가 중계 DLL `winmm.dll`을 함께 넣습니다(`loader/shim`). 이 DLL은 모든 함수를 시스템 winmm.dll로 넘기고 로더를 불러옵니다. 그러면 실행 파일의 d3d11/dxgi 정적 가져오기가 WineD3D로 갑니다.
- 측정(separation 25, 덤프는 한 눈 1920x1080)
  - 타이틀: 먼 건물 +60~68 px, 가운데 건물 +46~48 px, 다리 +42 px, Braid 로고 +8~16 px, 팀 -22 px
  - 집 안 허브: 방 뒷벽·가구 +32 px, 사다리 +38 px, 문 +8 px, 집 바깥 틀 +2~6 px, 팀 약 0 px, "월드" 글자 -26 px
- 사용자 확인 완료(10-25, 플레이).
- 테스트 조작: 타이틀에서 Enter는 아무 일도 하지 않습니다. 오른쪽 화살표를 누르고 있으면 팀이 걸어가 게임이 시작됩니다. 세이브: `%APPDATA%\Braid, Anniversary Edition`(test-backups\Braid Anniversary Edition).

**Home** (프로필 `home`, 10-26): GameMaker Studio 2 x64, D3D11을 WineD3D로. 옆에서 보는 픽셀 공포 어드벤처입니다. 사용자가 설치기로 설치한 뒤(feet 규칙, 후보 목록 기본값) 검증했습니다.

- 후보 목록에는 GameMaker: Studio 1, Direct3D 9, x86(2017년 빌드)로 올라 있었습니다. 지금 Steam 빌드(v1.7.1, 2026)는 GameMaker Studio 2 x64, Direct3D 11입니다(data.win GEN8 버전 2). exe가 d3d11.dll을 정적으로 가져와 로더가 w3d11.dll로 돌립니다. 설치기는 자기 판별로 WineD3D D3D11 경로를 골랐습니다. 후보 목록을 고쳤습니다.
- 게임 자체가 30fps입니다(data.win 게임 속도 30). `OGL3D.log`의 `frame 600`이 20초마다 찍히는 것은 정상입니다.
- **방이 주인공 앞으로 나오던 문제**: 방 하나를 화면 아래 끝 너머까지 닿는 세로 조각(벽지 띠, 벽판, 옆벽)들로 한 draw에 그립니다. feet 규칙에서 좁은 조각은 자기 아래 끝(화면 밖 아래)에 서서 무엇보다 가까워졌습니다. 방 전체가 −14px로, 그 앞에 선 주인공(−10px)보다 앞에 나왔습니다. `plane_tall = 1`(화면 높이 절반)로 이런 조각이 바닥 경사를 따르게 했습니다. 주인공(화면 높이의 약 0.7)과 창문은 그대로 자기 아래 끝에 섭니다. 문틀은 거의 기준선 높이입니다.
- order 규칙도 시험했습니다. 벽은 평평하게(+38px), 주인공은 +10px로 나옵니다. 하지만 대화 카드가 글자 전체를 한 draw로 그리고, order 규칙은 한 draw를 층 간격 안에서 펼치므로 글자 한 줄이 약 8px 기울게 됩니다. feet를 유지했습니다.
- 측정(feet + `plane_tall = 1`, separation 25, 덤프는 한 눈 1920x1080)
  - 타이틀: 로고 +10 px, 버튼 −2~−6 px, 버전 글자 −12 px
  - 첫 방: 벽 +16(위)~0 px(벽판 위 몰딩), 창문 +2 px(창턱에 서서 그 높이의 벽과 맞음), 벽판 −4~−8 px, 주인공 −10 px, 손전등 어둠(화면 전체 덮개) +34(위)~+4 px(아래)
  - 둘째 방: 벽 +26~0 px, 화분·문틀·주인공 −10 px
  - 두 눈이 어긋나는 블록 0, 걷는 동안 깜빡임 감지 없음
- 남은 일: 대화 카드는 `plane_size`보다 넓어 바닥 경사를 따라 뒤로 기웁니다(위 +26 ~ 아래 −6 px, 글자 줄은 카드 면 위 +20, +12 px). 화면 높이 절반보다 큰 조각은 똑바로 서지 않고 기웁니다. 손전등 어둠은 한 장의 경사면이라 화면 아래쪽에서는 자기가 덮는 것보다 조금 뒤(+4 px, 덮이는 것 −6~−10 px)입니다. 첫 두 방만 확인했고 사용자 확인이 필요합니다.
- 테스트 조작: 화살표로 이동(위는 위 보기), Space로 카드 넘기기와 상호작용. 첫 방 왼쪽 문이 둘째 방으로 이어집니다. 세이브: `%LOCALAPPDATA%\Home___A_Unique_Horror_Adventure\homesave`(test-backups\Home). 시작 부분은 진행 저장을 남기지 않았습니다.

**Axiom Verge, Axiom Verge 2** (프로필 `axiom-verge`, `axiom-verge-2`, 10-28): FNA(XNA를 다시 구현한 .NET 엔진) 32비트. AV2 폴더에 `x64`가 있지만 exe가 32비트 전용(`32BITREQUIRED`)이라 `x86` 라이브러리를 씁니다. 사용자가 설치기로 설치했는데 처음에는 둘 다 래퍼가 전혀 로드되지 않았습니다.

- **OpenGL 전환**: FNA3D는 Windows에서 Direct3D 11을 고릅니다. Steam 실행 옵션에 `/gldevice:OpenGL`(FNA 자체 스위치, AV1은 `-opengl`도 됨)을 넣어야 래퍼가 걸립니다. 환경 변수 `FNA3D_FORCE_DRIVER=OpenGL`도 되지만, Steam 밖에서 실행하면 게임이 Steam을 통해 다시 시작하면서(`SteamAPI_RestartAppIfNecessary`) 변수가 빠집니다. 래퍼의 `add_args`는 쓸 수 없습니다. opengl32.dll은 FNA3D가 드라이버를 고른 뒤에야 로드되기 때문입니다. 시험할 때만 exe 옆에 `steam_appid.txt`를 두면 Steam 재시작을 막을 수 있습니다.
- **로더는 안 걸림**: .NET 런타임이 System32의 `version.dll`을 먼저 올려 두어서, SDL2.dll이 가져와도 Flato3D 로더가 로드되지 않습니다. FNA는 OpenGL로 직접 그리므로 opengl32.dll만 있으면 됩니다. AV1은 D3D9 경로로 설치돼 `version.dll`, `wined3d.dll`, `d3d9.dll`이 함께 들어갔지만 쓰이지 않습니다.
- **검은 화면 (래퍼 수정)**: FNA 셰이더는 MojoShader가 만든 GLSL이고, 출력을 매크로(`#define vs_oT0 gl_TexCoord[0]`)로 옛 내장 변수에 씁니다. 평면 깊이용 지오메트리 셰이더는 전처리 줄 밖에서만 내장 변수를 찾아 하나도 넘기지 않았고, 화면이 검었습니다(입체를 꺼도). 이제 셰이더가 쓰는 매크로의 본문도 셉니다.
- **장면 전체가 평면 / 한 층 (래퍼 수정)**: FNA는 모든 렌더 타깃을 FBO 하나로 그리고 색 부착물만 바꿉니다. 래퍼가 타깃을 FBO 번호로 구분해서, 타깃마다 다시 거는 viewport가 새 scissor 묶음이 되었습니다(`screen_batch = 1`이 장면 전체를 화면 깊이로: 시차 0). 합성 순서 계산은 모든 타깃을 한 임시 버퍼로 보았습니다(장면이 한 층으로 뭉침). 이제 묶음과 합성 순서가 FBO와 색 부착물을 함께 타깃으로 셉니다.
- **draw 기록 (래퍼 수정)**: FNA3D가 쓰는 `glDrawRangeElementsBaseVertex`(그리고 `glDrawElementsInstancedBaseVertex`)는 draw 기록과 삼각형 수가 남지 않았습니다(`prims 0`). 이제 남깁니다.
- AV1은 480x270으로 그려 늘리므로 깊이가 4 px 단위(한 눈 1920폭에서 게임 픽셀 1칸)입니다. HUD와 대화창은 자기 타깃에 8x8 텍스처마다 한 층으로 그려져, 균등 곡선에서는 세계 층 8개가 먼 1/5(+24~28 px)에 뭉쳤습니다. `order_curve = 0.4`로 폈습니다. AV2는 432x240이고 배경이 시차 층 14개라 기본값으로 됩니다.
- 래퍼가 게임을 DPI 인식으로 만들자 AV1이 4K 화면에서 1920x1080 창으로 떴습니다. 업스케일(`upscale = 1`)로 화면을 채웁니다(표시 창 3840x2160). AV2는 설치기 기본값이 업스케일이었습니다.
- 측정(separation 25, 한 눈 1920폭 기준)
  - AV1 타이틀(`order_curve = 1`): 먼 타일 +32 px, 앞 타일 +24 px, 로고 +8 px, 메뉴 −8 px
  - AV1 첫 방(`order_curve = 0.4`): Trace +16 px, 그 뒤에 그려지는 지형 벽 +8 px, 대화창·초상화 0 px
  - AV2 남극 헬기장: 하늘·먼 산 +26 px, 가운데 산 +18 px, 헬기·비계 +8~10 px, 주인공과 앞 얼음 0 px, HUD −4~−8 px
  - 둘 다 60fps, 두 눈이 어긋나는 블록 2~4개
- **걸을 때 타일이 울렁임 (사용자 보고, SR 패널, 10-28)**: 걸으며 4프레임마다 덤프해 원인 두 가지를 찾았습니다. ① 게임이 432x240 / 480x270으로 그리는데, 눈마다 옮기는 양이 픽셀의 소수라서 소수 위치로 스크롤되는 스프라이트를 두 눈이 다르게 반올림했습니다. 시차 배경 층의 시차가 스크롤 중에 두 값(+16, +20~24 px) 사이를 오갔습니다. 새 키 `snap_pixels = 1`로 타깃의 정수 픽셀만큼 옮깁니다(그룹 안 펼침도 끔). ② 물체가 화면에 들어오고 나가며 층 수가 30↔31로 바뀌고, 그때마다 모든 층 깊이가 따라 미끄러졌습니다(0.517↔0.533). 이제 층 수는 최근 최댓값을 유지하고, 둘보다 많이 줄 때만 바로 따릅니다(래퍼, 모든 order 게임). 고친 뒤 걷는 동안 층 깊이가 그대로였습니다.
- SR 위빙 확인(10-28, 출력 auto, 업스케일): 32비트 SR 런타임의 DX11 위버가 뜨고 60fps, SR 로그에 눈 추적 끊김 없음.
- 남은 일: Steam 실행 옵션은 사용자가 직접 넣어야 합니다. 지형이 주인공보다 조금 앞에 섭니다(order 규칙의 횡스크롤 공통). AV1의 HUD 조각과 대화창 타일은 층이 나뉘어 깊이가 조금씩 다릅니다. 사용자 확인이 필요합니다.
- 테스트 조작: AV1 메뉴는 Enter, 컷신 건너뛰기 창은 왼쪽 화살표로 Yes를 고르고 Space. AV2는 플레이 중 Enter가 일시정지 메뉴, Esc로 닫음. 화살표는 확장 키 플래그가 있어야 SDL 게임이 받습니다(gametest가 10-28부터 보냄). 세이브: Steam 클라우드 `userdata\<계정>\332200`, `946030`(test-backups\AxiomVerge, AxiomVerge2).

**Blasphemous** (프로필 `blasphemous`, 10-29): Unity 2017 x64, D3D11을 WineD3D로. Unity 기본값에 `order_weight = 0.02`, `order_curve = 1`을 더했습니다.

- **AMD에서 처음 시험**: MiniPC(AMD Radeon RX 7600M XT, GL 4.6)에서 `sbs` 출력으로 돌렸습니다. 60fps, 타이틀과 플레이 약 4분 동안 멈춤 없음. NVIDIA와 SR 출력은 아직 시험하지 않았습니다.
- 게임 설정의 해상도 방식 `PIXEL_PERFECT`는 3840x2160 창 안에 3072x1728로 그려서 눈마다 좌우에 검은 띠가 생깁니다.
- 화면 구조(Ctrl+F12 기록): 월드를 640x360 버퍼 두 장에 그립니다. 하나는 배경과 바닥·발판(그 뒤 조명 패스), 다른 하나는 주인공과 전경입니다. 둘 다 3072x1728로 늘린 뒤 HUD를 그립니다. 깊이 단계는 640x360 픽셀 단위라 거칩니다.
- 측정(separation 25, 덤프는 한 눈 1920x1080)
  - 타이틀: 로고 +24 px, "아무 버튼이나 누르세요" −20~−28 px, 저작권 글자 +20에서 +6 px까지(draw 하나가 글자들에 펼쳐짐)
  - 플레이(침묵의 비탄 형제단의 기도대 방과 그 서쪽 방): 창 빛 +60 px, 벽·기둥 +30~+40 px, 잔해 +10~+30 px, 주인공 +4~+20 px, HUD 초상 −20 px, 오른쪽 HUD −10 px
- **바닥 위에 뜬 주인공(고침)**: 처음엔 주인공이 성당 바닥보다 약 25 px 앞에 섰습니다. 바닥과 주인공 사이에 그리는 작은 스프라이트가 한 칸씩 깊이를 차지했기 때문입니다. `order_weight = 0.02`(층마다 덮는 면적만큼만 앞으로)로 성당 바닥 +20 px, 주인공 +10 px(640x360 한 픽셀 차이), 벽 +30~+40 px, 창 빛 +60 px, HUD 0~−10 px가 됐습니다. 타이틀은 로고 +4 px, 글자 −8~−28 px입니다.
- 남은 일: 슬롯 선택 메뉴가 +44 px로 화면 뒤에 놓입니다. 메뉴 뒤에 화면 전체를 덮는 그림 두 장이 깊이 범위를 대부분 차지합니다.
- **사용자 확인 완료**(10-29): 잘 됩니다.
- 테스트 조작: 타이틀에서 Enter를 세 번 누르면 1번 슬롯으로 이어집니다(두 번째 뒤가 슬롯 선택 화면). 이동은 화살표가 아니라 A / D입니다. 걸어 다니기만 해도 `savegame_0.save`가 바뀌니 시험 뒤 되돌립니다. 세이브: `%USERPROFILE%\AppData\LocalLow\TheGameKitchen\Blasphemous\Savegames\<Steam ID>\`(test-backups\Blasphemous).

**Blasphemous 2** (프로필 `blasphemous-2`, 10-29): Unity 2021 x64(IL2CPP), D3D11을 WineD3D로. Unity 기본값에 `order_weight = 0.02`, `order_curve = 1`을 더했습니다.

- **AMD에서 시험**: MiniPC(AMD Radeon RX 7600M XT)에서 `sbs` 출력, 게임 버전 3.0.0. 60fps, 타이틀·메뉴·플레이 약 5분 동안 멈춤 없음, 셰이더 82개 주입에 실패 0. NVIDIA와 SR 출력은 아직 시험하지 않았습니다.
- 1편과 달리 3840x2160 창을 꽉 채웁니다(검은 띠 없음).
- 화면 구조(Ctrl+F12 기록): 월드를 640x360 버퍼 여러 장에 나눠 그린 뒤 합쳐서 늘리고, 그 위에 HUD를 그립니다. 그리는 순서는 버퍼마다 따로 셉니다. 깊이 단계는 12 px로 거칩니다(separation 25).
- 측정(separation 25, 덤프는 한 눈 1920x1080)
  - 타이틀: 로고 −12 px, "아무 버튼이나 누르세요" −26~−28 px, 저작권 글자 −18~−22 px(타이틀 전체가 화면 앞)
  - 플레이(세이브한 기도대와 좌우 방): 먼 바위 기둥·석상 +24~+36 px, 바닥·발판 +24~+36 px, 기도대·천사상 +12 px, 주인공 0 px, 앞쪽 안개 −24~−30 px, HUD −16~−18 px, 눈물 수치 −10~−14 px
- **바닥 위에 뜬 주인공(고침)**: 처음엔 바닥이 +24~+36 px, 주인공이 0 px로 바닥보다 24~36 px 앞에 떴습니다. 바닥(draw 18)과 주인공(draw 28) 사이에 기도대·천사상·빛 같은 작은 스프라이트 9개를 그리고, 그것들이 한 칸씩 깊이를 차지했습니다. `order_weight = 0.02`로 차이가 12 px(640x360 한 픽셀)로 줄었습니다. 기도대 방은 바닥 +12, 주인공 0, 먼 바위 +24~+36 px, 어두운 방은 바닥 0, 주인공 −12 px, 핏빛 비는 발판 +24, 주인공 +12, 덩굴 +60 px입니다. 타이틀과 메뉴는 0~−28 px입니다.
- **사용자 확인 완료**(10-29): 잘 됩니다.
- 테스트 조작: 타이틀에서 Enter(아무 버튼) → Enter(순례) → Enter(1번 슬롯 이어하기). 이동은 A / D, W는 상호작용. 위 절차로는 세이브가 바뀌지 않았지만 그래도 백업합니다. 세이브: `%USERPROFILE%\AppData\LocalLow\The Game Kitchen\Blasphemous 2\SteamUser_<Steam ID>\`(Steam 클라우드, test-backups\Blasphemous 2). Steam으로 처음 실행할 때 게임이 뜨기까지 2분쯤 걸렸습니다.
