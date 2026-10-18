# Flato3D 3D 변환 작업 절차

새 게임을 Flato3D로 입체로 만들 때 따르는 순서입니다. Technobabylon, Door Kickers: Action Squad, Huntdown, FRAMED, Astral Ascent를 작업하며 겪은 문제와 해결을 체크포인트별로 정리했습니다. 원칙은 세 가지입니다.

- **눈으로 보기 전에 숫자로 확인합니다.** 덤프, draw 기록, 시차 지도로 원인을 찾은 뒤 고칩니다.
- **고칠 때는 일반 규칙을 우선합니다.** 한 게임의 문제는 같은 엔진의 다른 게임에서도 나옵니다. 게임별 차이는 ini 값과 프로필로만 둡니다.
- **결과는 바로 기록합니다.** 프로필, 작업 게임 목록, 테스트 절차를 남겨야 다음 게임이 빨라집니다.

## 체크포인트 한눈에 보기

| 단계 | 할 일 | 통과 기준 | 남길 것 |
| --- | --- | --- | --- |
| CP0 사전 조사 | API·엔진·32/64비트 판별, 경로 결정, 기존 설치·세이브 확인 | 경로가 정해짐 (불가면 여기서 끝) | 메모 |
| CP1 설치·첫 실행 | 설치기로 설치, SBS 출력으로 실행 | 게임이 정상 화면으로 돌고 `OGL3D.log`에 프레임 기록 | 설치 기록 파일 |
| CP2 화면 구조 파악 | Ctrl+F12 덤프와 draw별 이미지로 그리는 순서 확인 | 무엇이 어느 draw·어느 버퍼에서 그려지는지 앎 | draw 기록 요약 |
| CP3 깊이 측정 | 시차 지도로 층별 시차 측정 | 먼 배경 > 지형 > 캐릭터 > HUD 순서, 이상한 값 없음 | 측정값 |
| CP4 수정 | ini 값 조정 또는 래퍼의 일반 규칙 수정 | CP3 기준 충족 | 바꾼 값, 커밋 |
| CP5 플레이 검증 | 여러 장면을 실제로 진행, 깜빡임·멈춤·fps 확인 | 장면 3개 이상에서 기준 충족, 60fps, 깜빡임 없음 | 장면별 측정값 |
| CP6 사용자 확인 | 사용자가 직접 플레이 (원격이면 SBS) | 사용자 확인 | 상태 갱신 |
| CP7 기록·배포 | 프로필, 목록, 테스트 절차, 커밋·푸시, 배포 | 저장소에 반영 | 커밋 |

---

## CP0 사전 조사

1. **목록 확인:** `data/game_profiles.json`(작업한 게임)과 `data/game_candidates.json`(후보 544개의 API, 엔진, 깊이 규칙 추정)을 먼저 봅니다. `kind = 1`(원근이 있는 일반 입체 게임 96개)은 Flato3D 대상이 아니고 Geo Gaming Hub에서 다룹니다.
2. **exe 판별:** 설치기 명령줄로 32/64비트와 경로를 봅니다.
   ```
   installer\bin\Flato3DInstaller.exe --detect "<게임 exe>" --log detect.txt
   ```
   - exe가 가져오는 DLL(opengl32 / d3d9 / d3d11·dxgi / d3d12 / vulkan-1)을 보고 경로를 정합니다.
   - Unity(`UnityPlayer.dll`)와 Unreal(`*-Shipping.exe`)은 D3D11로 봅니다.
   - 가져오는 DLL이 없으면(DLL을 실행 중에 불러오는 게임) 후보 목록의 API를 씁니다.
3. **경로 결정**

   | 그래픽 API | 경로 | 넣는 파일 |
   | --- | --- | --- |
   | OpenGL | 직접 | `opengl32.dll`, `OGL3D.ini` |
   | OpenGL 선택 가능 (AGS `driver=OGL`, Unity `-force-glcore`, 실행 옵션) | 게임을 OpenGL로 바꾼 뒤 직접 | 위와 같음 + 게임 설정 변경 |
   | Direct3D 8/9, DirectDraw | WineD3D (일반) | 위 + `version.dll`(로더), `wined3d.dll`, `d3d9.dll`(필요하면 `d3d8.dll`, `ddraw.dll`) |
   | Direct3D 10/11 | WineD3D (이름 바꾼 세트) | 위 + `w3d11.dll`, `wxgi.dll`, `w3d10core.dll`, `wined3d.dll`(D3D10 게임은 `d3d10*.dll`도) |
   | Direct3D 12, Vulkan | 불가 (별도 프로젝트) | — |
   | 완성 화면 한 장 (DOSBox, ScummVM, 에뮬레이터, RPG Maker VX Ace, 소프트웨어 렌더링) | 불가 (AI 깊이만) | — |
   | 브라우저 엔진 (NW.js, Construct, RPG Maker MV/MZ) | 어려움 | — |

4. **엔진별 첫 설정**

   | 엔진·장르 | 설정 | 근거 |
   | --- | --- | --- |
   | AGS 등 어드벤처 (캐릭터가 바닥 위를 걸음) | `rule = feet` | Technobabylon |
   | 횡스크롤, 탑다운 액션 | `rule = order` | DK, Huntdown, Astral Ascent |
   | Unity (시차 레이어를 같은 아틀라스로 따로 그림) | `group = draw`, `order_curve = 0.5` | Huntdown |
   | 저해상도 버퍼에 그린 뒤 화면으로 늘림 | 자동 처리(레이어 캐시). 화면 비율이 아니면 `cache_scale` | DK |
   | draw마다 glScissor를 거는 게임 | `screen_batch = 0` | FRAMED |
   | 인물이 이미 합쳐진 그림(동영상 칸 등) 안의 검은 실루엣 | `silhouette = <프로그램 해시>`, 셰이더의 좌표·텍스처 이름 | FRAMED |
   | Unity (exe가 opengl32를 가져와도 기본은 D3D11) | 경로는 `<exe>_Data\output_log.txt`나 `Player.log`의 `Version:` 줄로 판별, `group = draw`, `order_curve = 0.5` | Huntdown, GGC2 |
   | 2.5D·3D(원근이 있는 장면) | Flato3D 대상 아님: Geo Gaming Hub에서 일반 입체로 설치 | Broken Age |

5. **기존 설치 확인:** 게임 폴더에 geo-11(`d3d11.dll`, `nvapi64.dll`, `d3dx.ini`), SRWeave(`dxgi.dll`), dgVoodoo(`D3D9.dll`), ReShade, OGL3D 1.0(`opengl32.dll`)이 있는지 봅니다. 설치기가 `Flato3D_backup`으로 옮기지만, 사용자가 쓰던 설치라는 것을 기록해 둡니다.
6. **세이브 백업:** 플레이를 진행해야 하면 세이브 폴더를 `test-backups\<게임>\`에 먼저 복사합니다. 세이브 위치는 PCGamingWiki나 Steam Cloud 경로로 찾습니다.
   - Unity: `AppData\LocalLow\<회사>\<게임>`
   - GameMaker: `%LOCALAPPDATA%\<게임>`
   - 그 밖: `Saved Games`, `Documents` 등

## CP1 설치·첫 실행

1. 설치기로 설치합니다. 원격 확인이면 출력을 `sbs`로 둡니다.
   ```
   Flato3DInstaller.exe --install "<게임 exe>" --output sbs --log install.txt
   ```
2. 자동 실행은 `tools\analysis\gametest.ps1`로 합니다. 게임별 단계는 프로필의 `test`에 있습니다.
   ```
   gametest.ps1 -AppId <Steam appid> -Proc <프로세스 이름> -OutDir <폴더> -Steps "wait:30,shot:a1,ctrlkey:7B,wait:4" -Keep
   ```
   - 실행 중인 게임을 이어서 조작할 때는 `-Attach`를 씁니다.
   - 키를 누르고 있어야 하면 `hold:<vk>:<초>`, 클릭은 `click:x:y`(화면 픽셀), 상대 이동은 `rmove`를 씁니다.
3. **로그 확인 (`OGL3D.log`)**
   - `loaded into`: 래퍼가 로드됨
   - `context ...`: OpenGL 컨텍스트 생성
   - `main context`: 첫 화면 출력
   - `fbo N: WxH -> stereo/mono`: 렌더 타깃 분류
   - `frame 600: ...`: 600프레임마다 통계(10초 간격이면 60fps)
4. **로더 로그 (`Flato3D_loader_<exe>.log`)**: `redirected ... -> w3d11.dll` 또는 `static import d3d11.dll!... -> w3d11.dll`이 보여야 WineD3D가 쓰인 것입니다.
5. **첫 실행 문제**

   | 증상 | 원인 | 해결 |
   | --- | --- | --- |
   | `OGL3D.log`가 생기지 않음 | 경로가 틀림(게임이 OpenGL을 안 씀) 또는 로더가 안 걸림 | API 재판별. 로더 로그 확인 |
   | 프레임 3에서 멈춤, 80~100초 뒤 진행 | 원인 미상(주로 DLL 교체 뒤 첫 실행) | 다시 실행. 프로필 알려진 문제에 기록됨 |
   | 시작하자마자 멈춤 (WineD3D) | CSMT와 NVIDIA 표시 경로의 교착 | `HKCU\Software\Wine\AppDefaults\<exe>\Direct3D` `csmt=0` (DK) |
   | CSMT를 끄면 다른 스레드 자원 오류로 죽음 | Unity 등은 CSMT 필요 | csmt 키를 두지 않음 (Huntdown) |
   | D3D11 게임이 멈춤 | NVIDIA 드라이버가 WineD3D의 `d3d11.dll`을 잡음 | 이름 바꾼 세트(`w3d11.dll`) 사용 (설치기가 처리) |
   | 시스템 D3D가 그대로 쓰임 | Steam 오버레이가 먼저 로드, 또는 exe가 정적으로 가져옴 | 로더가 처리. 로더 로그에서 확인 |
   | 화면이 위아래로 뒤집힘 | WineD3D의 glClipControl | 래퍼에서 처리됨. 다시 보이면 GlStateGuard 확인 |
   | 오른눈만 검거나 이상함 | 텍스처 뷰·복사 추적 누락 | draw 기록의 `samples`에서 `R` 표시 확인 |

## CP2 화면 구조 파악

1. `OGL3D.ini`에 `trace_steps = 1`을 넣습니다. 실행 중에도 1초 안에 반영됩니다.
2. 장면에서 Ctrl+F12를 누릅니다. 사용자가 플레이 중이면 키 대신 ini에 앞으로의 프레임 번호를 적습니다.
   - `dump_frames = N+1`
   - `trace_frames = N`
3. **결과 파일**
   - `OGL3D_frameNNNNN.bmp`: 두 눈 이미지(반 해상도 SBS)
   - `OGL3D.log`의 `T` 줄: draw별 기록
   - `OGL3D_stepNNNNN_DDD.bmp`: draw 하나가 끝날 때마다의 렌더 타깃
4. **draw 기록 읽는 법**
   - `T 1234.17 b0 DrawElements... prog=3/hash(gs) fb=2S(t13) tex=43(1024x1024)`
     - `fb=2S`: 스테레오 버퍼 2번에 그림. `S`가 없으면 모노입니다.
     - `(gs)`: 깊이용 지오메트리 셰이더가 붙음
   - `samples u1/s0:257(512x310)R`: 셰이더가 실제로 읽는 유닛과 텍스처
     - `R`: 오른눈 복제본이 있음 = 스테레오 렌더 타깃을 읽는 합성 패스
     - `v35`: 텍스처 35의 뷰
   - `order group 5/24 ... target 0.348 depth 0.348 slot 0.04 prims 2`: 층 위치(1=가장 뒤), 적용 깊이, 층 간격, 삼각형 수
   - `layout events N, root fbX, scratch targets: ..., composites/copies: fbA<fbB`
     - 합성 순서 계산의 근거입니다.
     - `root`: 최종 화면 버퍼
     - `fbA<fbB`: B의 그림을 A에 합성
   - `layout (far to near, N groups, M repeats)`: 최종 화면 기준의 층 순서입니다. 0개면 합성 경로가 끊긴 것입니다.
5. **이미지로 보기**
   - `sheet.py <폴더> "OGL3D_step*.bmp" out.png 4`: draw별 이미지를 한 장에 모읍니다.
   - `cachecrop.py`: 저해상도 캐시 영역만 잘라 확대합니다.
   - WineD3D 게임은 버퍼가 위아래로 뒤집혀 저장됩니다(정상).

## CP3 깊이 측정

- `dispmap.py <frame.bmp> out.png`: 24px 블록마다 시차(px)를 숫자로 표시합니다.
  - 양수는 화면 뒤, 음수는 화면 앞입니다.
  - 반복 무늬에서는 오측정이 섞입니다.
- `maskdisp.py <frame.bmp> player=x0,y0,x1,y1,purple ...`: 영역 하나의 시차를 잽니다. `purple`은 보라 픽셀만 보는 예입니다. 작은 캐릭터를 잴 때 씁니다.
- `eyes.py`: 애너글리프와 작은 SBS를 만듭니다.
- **좋은 상태의 기준** (분리값 25, 3840 화면, depth_screen 0.3)
  - 먼 배경 +30~60px
  - 지형·중간 +8~30px
  - 캐릭터 −10~+10px
  - HUD 0~−20px
  - 층 순서가 화면에서 보이는 앞뒤와 맞을 것

## CP4 수정

### ini 설정

| 설정 | 의미 | 언제 바꾸나 |
| --- | --- | --- |
| `[Stereo] separation` | 분리값(%) | 깊이 전체가 약하거나 강할 때. 평면 게임은 25 전후 |
| `[Stereo] output` | auto(SR 디스플레이) / sbs / tab. 설치기에서는 "SR 디스플레이 (Acer, Samsung)", "좌우 SBS", "위아래" | 원격 확인은 sbs. SR 디스플레이가 아니면 sbs/tab을 직접 고름 |
| `[Flat] enable` | 평면 깊이 사용 | Flato3D에서는 항상 1(일반 입체 게임은 Geo Gaming Hub) |
| `[Flat] rule` | feet(발 위치) / order(그리는 순서) | CP0 표 참고 |
| `[Flat] depth_screen` | 화면 위치에 놓이는 깊이 | HUD가 튀어나오면 낮추고(0~0.3), 전체가 뒤로 멀면 높임. 게임 중 Ctrl+F5/F6 |
| `[Flat] depth_top` | 깊이 범위 | 보통 1 |
| `[Flat] group` | texture(같은 텍스처 연속 draw = 한 층) / draw(draw 하나 = 한 층) | Unity처럼 레이어마다 draw가 따로면 draw |
| `[Flat] order_curve` | 1 = 균등, 1보다 작으면 앞쪽(먼) 층에 깊이를 더 줌 | 층이 많아 배경 층들이 붙어 보일 때 0.5 |
| `[Flat] cache_scale` | 저해상도 캐시 안의 이동량 비율(0 = scissor 기준 자동) | 캐시 영역이 화면에 대응하는 비율을 아는 경우(DK 0.406) |
| `[Flat] screen_batch` | 몇 번째 scissor 묶음부터 화면 깊이(UI)로 볼지, 0 = 끔 | AGS는 1, draw마다 scissor를 거는 게임은 0 |
| `[Flat] plane_size`, `plane_height`, `sprite_min` | feet 규칙의 평면·스프라이트 판정 | feet 규칙 게임 |
| `[Flat] silhouette` | 이 프로그램(해시, 쉼표로 여러 개)이 그리는 그림 안에서 어두운 픽셀을 그림보다 앞으로 | 인물이 한 장의 그림에 합쳐져 들어오는데 인물이 검은 실루엣일 때 |
| `[Flat] silhouette_uv`, `silhouette_tex` | 그 프래그먼트 셰이더의 텍스처 좌표 varying(vec2)과 그림 샘플러 이름 | `dump_shaders = 1`로 셰이더를 보고 정함(FRAMED: `texcoordOut`, `texture1`) |
| `[Flat] silhouette_depth`, `silhouette_threshold` | 앞으로 나오는 정도(depth_top 기준 비율), 어둡다고 볼 밝기(r, g, b 중 최댓값) | 기본 0.3, 0.15. 어두운 배경까지 나오면 threshold를 낮춤 |
| `[Display] swap_interval` | 1 = 수직동기화 강제 | WineD3D 게임 기본 1 |
| `[Display] upscale` | 1 = 게임은 DPI 비인식으로 낮은 해상도(4K 200%면 1920×1080)로 그리고, 래퍼가 기본 해상도 창에 확대해 표시. 해상도 변경은 막음 | 게임이 기본 해상도를 못 쓰거나 글씨가 너무 작을 때. 설치기의 **업스케일** |
| `[Keys] swallow` | 래퍼 단축키를 게임에 전달하지 않음 | 기본 1. 문제 있으면 0 |
| `[Debug] trace_steps`, `flicker_probe`, `dump_frames`, `trace_frames` | 분석용 | 끝나면 끔 |

### 증상별 원인과 해결

| 증상 | 원인 | 해결 |
| --- | --- | --- |
| 화면 전체가 평면(시차 0) | 깊이 셰이더가 안 붙음, 또는 모든 draw가 UI/합성으로 판정 | 기록에서 `(gs)`와 `order group` 줄 확인. scissor 게임이면 `screen_batch = 0` |
| 화면 전체가 한 덩어리로 같은 시차 | 장면을 저해상도 모노 캐시에 그린 뒤 한 번에 붙임 | 레이어 캐시 승격(자동). `fbo N ... layer cache` 로그 확인 |
| UI만 깊이가 있고 게임 화면은 평면 | 버퍼마다 scissor를 다시 걸어 UI 묶음으로 오판 | 같은 버퍼에서만 묶음 증가(수정됨) |
| 배경이 앞쪽 물체보다 앞에 나옴 | 그리는 순서와 화면에서의 합성 순서가 다름 | 합성 순서 계산(`layout`) 확인. 0개면 합성 경로(뷰, blit, 복사) 추적 문제 |
| 여러 배경 레이어가 한 깊이 | 같은 아틀라스 레이어를 한 층으로 묶음 | `group = draw`, `order_curve = 0.5` |
| 일반 스프라이트가 깊이 없음 | 다른 유닛에 남은 렌더 타깃 때문에 합성으로 오판, 또는 업로드용 버퍼가 캐시로 승격 | 셰이더가 읽는 유닛만 봄, 그 프레임에 그려진 버퍼만 승격(수정됨). `samples`의 `R` 확인 |
| 화면을 가로지르는 대각선 | 사각형의 두 삼각형이 다른 깊이를 받음 | 사각형 단위로 깊이를 나눔(수정됨) |
| 글자 줄이 비스듬히 기울어짐 | 글자마다 따로 그려 층이 하나씩 앞으로 나옴 | 한 프레임에 여러 번 지우고 쓰는 임시 버퍼는 쓸 때마다 한 층(수정됨) |
| 같은 줄의 칸·카드가 계단식으로 다른 깊이 | 반복 구조가 그리는 순서대로 깊이를 받음 | 반복되는 draw 묶음은 같은 층(수정됨) |
| 캐릭터가 엉뚱하게 뒤에 있음 | 애니메이션 텍스처가 바뀌어 새 층으로 잡힘 | 새 층은 바로 앞 층 다음 자리(수정됨). `target`과 `depth` 비교 |
| 캐릭터가 서 있는 바닥보다 앞에 뜸 | 바닥을 먼저 그려서 더 멀게 잡힘 | 미해결(발 위치 보정 예정) |
| 칸·그림 안의 인물이 평면 | 인물이 이미 한 장의 그림에 합쳐져 들어옴 | 인물이 어두운 실루엣이면 `silhouette`(그 셰이더에서 어두운 픽셀만 눈마다 옮김). 로그의 `silhouette depth: ... rewritten`, `shift N px` 확인. 밝은 인물은 미해결(AI 깊이) |
| 깊이 단계가 거칢 | 저해상도 버퍼 픽셀 단위로만 이동 | 미해결(캐시를 크게 그리기 예정) |
| `renamed vertex shader N failed to compile` (`'__'` 예약어 오류) | 밑줄로 시작하는 셰이더 출력 이름에 접두사를 붙여 `__`가 생김(LÖVE) | 밑줄로 시작하면 다른 접두사 사용(수정됨) |
| 위빙 화면에 가로줄(티어링) | 게임이 수직동기화를 끔 | `swap_interval = 1` |
| 게임 기능이 단축키와 같이 켜짐 | 게임이 Ctrl을 무시하고 F키를 봄 | `swallow = 1`(기본) |

### 코드를 고칠 때

- 래퍼 코드는 `ogl3d/` 서브모듈(OGL3D-SR `flato3d` 브랜치)에 있습니다.
- 수정은 일반 규칙으로 하고, 게임 이름으로 분기하지 않습니다.
- 빌드는 `ogl3d\build.ps1`입니다. 빌드한 뒤 작업 중인 게임 말고 **이미 작업한 모든 게임**에 배포하고, DK와 Huntdown 정도는 회귀 확인을 합니다. 한 게임을 위한 수정이 다른 게임을 깨뜨린 적이 있습니다.

## CP5 플레이 검증

- 여러 장면을 실제로 진행합니다. 이동은 `hold`, 점프와 공격은 `key`나 `lclick`을 씁니다. 장면마다 덤프를 남깁니다.
- `flicker_probe = 1`: 두 눈 중 한쪽만 깜빡이는 프레임을 잡아 이미지로 저장합니다. 두 눈에 똑같이 나타나면 게임 효과입니다.
- `frame N:` 줄 사이 시간으로 fps를 확인합니다. 600프레임이 10초면 60fps입니다.
- 끝나면 세이브를 백업으로 되돌리고, 분석 설정을 끄고, 덤프 파일을 지웁니다.

## CP6 사용자 확인

- 원격으로 보는 경우 `output = sbs`로 둡니다. 실행 중 Ctrl+Alt+W로 위빙을 켜고 끌 수 있습니다.
- 사용자가 PC를 쓰는 동안에는 자동 실행(gametest)을 돌리지 않습니다. 창을 앞으로 가져오고 키를 보내기 때문입니다.
- 사용자가 맞춘 값(Ctrl+F7 저장)은 덮어쓰지 않습니다. 바꿔야 하면 이유를 알리고 바꿉니다.

## CP7 기록·배포

1. **`data/game_profiles.json`에 프로필을 추가합니다.**
   - 필드: `id`, `name`, `steam_appid`, `folder`, `exe`, `arch`, `engine`, `api`, `route`, `install`, `remove`, `registry`, `ini`, `status`(working / verified), `verified_by_user`, `notes`, `known_issues`, `test`
   - `ini`에는 템플릿과 다른 값만 적습니다.
   - `test.steps`에는 gametest 단계를 적습니다.
2. **`data/GAMES.ko.md`에 표 한 줄과 메모를 추가합니다.**
3. **커밋 순서**
   1. `ogl3d/`에서 커밋 → `git push origin flato3d`
   2. Flato3D 저장소에서 바뀐 서브모듈과 자료를 커밋 → `git push`
   - 커밋 메시지는 영어로 쓰고, 무엇을 왜 바꿨는지 적습니다.
4. **배포:** `installer\src\build.cmd` → `package.ps1` → `dist\Flato3D-SR-<버전>.zip` (GitHub 릴리스는 사용자 확인 뒤).

## 이상 로그 처리 ("이상로그 수정해줘")

설치기의 **이상 로그** 버튼은 게임 문제를 한 폴더에 모읍니다.

- **저장 위치**
  - 개발자 PC(OneDrive에 pre-release 폴더가 있으면): `D:\OneDrive\AI\ClaudeCode\Flato3D_github_release\reports\<날짜_시각>_<게임>\`
  - 그 밖: `%LOCALAPPDATA%\Flato3D\reports\`
  - "GitHub에도 보고"를 고르면 zip을 만들고, `bug` 라벨이 붙은 이슈 페이지를 엽니다.
- **들어가는 것**
  - `report.txt`: 증상, 게임·exe·경로·프로필·후보 정보, 자동 선택과 그 근거, `OGL3D.ini` 주요 값, 설치기·Windows·SR 런타임, exe 옆 DLL/ini/log 목록, `OGL3D.log`의 이상한 줄·처음·끝
  - 파일: `OGL3D.log`(끝 8MB), `OGL3D.ini`, `Flato3D_install.json`, `Flato3D_loader_*.log`, 그날의 마지막 Ctrl+F12 덤프
  - 스크린샷 `screenshot_NN.*`: 사용자가 체크한 것. 파일 추가, Ctrl+V 붙여넣기(Win+Shift+S 캡처)로 넣습니다. 이 게임의 Steam 스크린샷과 Windows·Game Bar 캡처 중 최근 2시간 것은 체크하지 않은 채 목록에 미리 나옵니다.
  - 경로의 사용자 이름은 `<user>`로 가립니다.
- **명령줄:** `Flato3DInstaller.exe --report "<exe>" --symptom "<증상>" [--image <파일> ...]`

사용자가 "이상로그 수정해줘"라고 하면 다음 순서로 합니다.

1. `reports\` 아래에서 `handled.txt`가 없는 폴더를 오래된 것부터 봅니다. 공개 보고는 `gh issue list -R nautymac/Flato3D-SR --label bug --state open`으로 봅니다.
2. `report.txt`에서 증상, 경로, 설정, 로그의 이상한 줄을 읽고, 스크린샷을 봅니다. 덤프가 있으면 `eyes.py`, `dispmap.py`로 봅니다.
3. 증상에 맞는 체크포인트로 돌아가 원인을 확인하고 고칩니다. CP1 첫 실행 문제, CP3/CP4 깊이 문제, 증상별 표를 참고합니다.
   - 재현이 필요하면 프로필의 `test` 절차로 자동 실행합니다. 사용자가 PC를 쓰는 중이면 기다립니다.
4. 프로필, `GAMES.ko.md`, 증상별 표에 기록하고 커밋·푸시합니다. 그다음 `prerelease.ps1 -Wait`로 pre-release 패키지를 갱신합니다.
5. 보고 폴더에 `handled.txt`를 남깁니다. 날짜, 원인, 수정, 커밋을 적습니다. GitHub 이슈는 같은 내용으로 답하고 닫습니다.
6. 사용자에게 무엇을 고쳤는지, 다시 확인할 것이 무엇인지 알립니다.

---

## 함정 모음

- **파일 인코딩:** PowerShell 5.1의 `Get-Content`/`Set-Content`로 소스를 다시 쓰지 않습니다. UTF-8 한글이 cp949로 깨집니다. Edit 도구나 Python(`newline=''`)을 씁니다.
- **줄바꿈:** 작업 폴더 파일은 CRLF(BOM 없음)입니다. `core.autocrlf=true`.
- **백슬래시:** Python 문자열 안의 `\a`, `\s` 같은 백슬래시는 다른 문자로 바뀝니다. JSON에 경로를 넣은 뒤에는 제어 문자가 없는지 확인합니다.
- **DLL 잠김:** 게임이 실행 중이면 DLL이 잠겨 복사가 실패합니다. 배포 전에 프로세스를 확인합니다.
- **Steam 중복 실행:** 게임이 이미 실행 중이면 Steam이 "이미 실행 중" 창을 띄웁니다. 자동 실행 전에 확인합니다.
- **프레임 3 멈춤:** 자동 실행에서 가끔 생깁니다. 한 번 더 실행합니다(재시도 반복문 사용).
- **덤프 크기:** 4K 덤프는 장당 16MB입니다. 끝나면 지웁니다.
- **세이브:** 플레이 테스트 전에 백업하고, 끝나면 되돌립니다. Steam Cloud는 로컬 변경을 올리므로 따로 처리할 필요가 없었습니다.
- **Defender 오탐 (2026-10-18~):** x64 로더 `version.dll`이 `Trojan:Win32/Cloxer`로 진단되어 격리됩니다(클라우드 판정이라 다시 빌드해도 곧 다시 걸림, x86은 아직 아님). 이 PC는 로더 경로들(`loader\bin`, `dist`, pre-release의 `loader`와 zip, WineD3D 게임들의 `version.dll`)을 예외로 등록했습니다. 새 게임에 설치하면 그 `version.dll`도 설치기의 **Defender 예외**로 등록해야 합니다. 증상: 설치기에 "version.dll 없음", 게임은 평면.
- **GPU:** NVIDIA에서만 보장합니다. Intel GPU에서는 셰이더가 깨진 적이 있습니다. 하이브리드 노트북은 설치기가 `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`에 `GpuPreference=2;`를 기록합니다.
- **측정 중 값이 바뀜:** 덤프 로그의 `separation N%`가 ini 값과 다르면 누군가 Ctrl+F3/F4를 눌렀습니다(`separation ... flat screen depth` 줄). 측정값은 그 분리값으로 환산합니다.
- **데스크탑에 위빙이 남음:** SR 렌즈를 켠 게임이 끄지 않고 종료하면 렌즈가 켜진 채 남습니다. 새 프로그램의 렌즈 끄기나 SRSession 재시작으로는 안 풀리고, `Restart-Service 'SR Service'`(관리자)로 풉니다.
- **알려진 미해결:** 캐릭터가 바닥 앞에 뜸, 저해상도 캐시의 거친 깊이 단계, 합쳐진 그림 안의 밝은 인물, 프레임 3 멈춤.

## 도구 위치

| 도구 | 위치 |
| --- | --- |
| 설치기 (GUI·명령줄) | `installer\` (`build.cmd` → `installer\bin\Flato3DInstaller.exe`) |
| 자동 실행 | `tools\analysis\gametest.ps1` |
| 시차 지도, 영역 시차, 애너글리프 | `tools\analysis\dispmap.py`, `maskdisp.py`, `eyes.py` |
| draw별 이미지 모음, 캐시 확대 | `tools\analysis\sheet.py`, `cachecrop.py` |
| 메모리 문자열 검색, export 이름 | `tools\analysis\memstr.py`, `peexp.py` |
| 셰이더 컴파일 시간 | `tools\analysis\gstime\` |
| 멈춤 분석 (모든 스레드 스택) | `tools\stackdump\` |
| WineD3D 이름 바꾸기 | `tools\wined3d_rename.py` |
| 후보 목록 다시 만들기 | `tools\library_survey\` (`api_research.json`에 수동 조사 결과) |
