# Flato3D-SR

[English](README.md) · **한국어**

Flato3D는 평면 2D 게임(픽셀 아트, 손그림)에 실제 입체 깊이를 줍니다. 평면 게임은 GPU에 깊이 정보를 보내지 않기 때문에, 게임이 그리는 방식을 보고 레이어와 스프라이트마다 깊이를 정합니다.

- **횡스크롤 게임:** 그리는 순서로 정합니다. 먼저 그린 레이어가 멀고, 나중에 그린 것이 가깝습니다.
- **어드벤처 게임:** 스프라이트의 아래쪽 끝, 즉 서 있는 위치로 정합니다.
- **HUD:** 화면 위치에 둡니다.

설치된 SR 런타임을 통해 렌티큘러 SR 패널(Acer SpatialLabs, Leia SR 디스플레이)용으로 위빙합니다. SR 출력은 끌 수 있습니다. 끄면 SBS나 상하 출력으로 다른 3D 디스플레이에서도 쓸 수 있습니다.

상태: 초기 단계입니다. 일곱 게임이 동작하며, 자세한 내용은 [data/GAMES.ko.md](data/GAMES.ko.md)에 있습니다.

| 게임 | API | 경로 | 깊이 규칙 |
| --- | --- | --- | --- |
| Technobabylon | OpenGL (AGS) | 직접 | feet (발 위치) |
| Huntdown | Direct3D 11 (Unity) | WineD3D | 그리는 순서, draw마다 한 층 |
| Door Kickers: Action Squad | Direct3D 9 | WineD3D | 그리는 순서, 레이어 캐시 |
| FRAMED Collection | OpenGL 3.2 | 직접 | 그리는 순서, 반복되는 칸은 같은 층 |
| Astral Ascent | Direct3D 11 (Chowdren) | WineD3D | 그리는 순서, 임시 버퍼는 한 층 |
| Intravenous 2 | OpenGL (LÖVE) | 직접 | 그리는 순서 |
| Guns, Gore and Cannoli 2 | Direct3D 11 (Unity) | WineD3D | 그리는 순서, draw마다 한 층 |

## 동작 방식

- **OpenGL 게임:** [OGL3D-SR](https://github.com/nautymac/OGL3D-SR)(`flato3d` 브랜치)의 `opengl32.dll`이 두 눈을 그립니다. 평면 게임에서는 생성한 지오메트리 셰이더가 스프라이트마다 다른 깊이만큼 밀어 줍니다.
- **Direct3D 8/9/10/11 게임:** WineD3D가 Direct3D를 OpenGL로 바꾸고, 나머지는 OGL3D가 처리합니다. 작은 `version.dll` 로더가 게임이 WineD3D 사본을 불러오게 합니다. Steam 오버레이가 시스템 DLL을 먼저 불러온 경우에도 됩니다. Direct3D 12는 지원하지 않습니다.
- **게임별 설정**은 [data/game_profiles.json](data/game_profiles.json)에 있습니다. 설치할 파일, `OGL3D.ini` 값, 레지스트리, 알려진 문제, 자동 테스트 절차가 들어 있습니다.

## 작업 절차

새 게임을 입체로 만드는 순서(체크포인트, 측정, 증상별 해결, 기록 방법)는 [docs/WORKFLOW.ko.md](docs/WORKFLOW.ko.md)에 있습니다.

## 폴더 구성

| 폴더 | 내용 |
| --- | --- |
| `ogl3d/` | 서브모듈: OGL3D-SR `flato3d` 브랜치(입체·깊이 엔진) |
| `installer/` | 설치기(C# WinForms)와 패키지용 README, 제3자 라이선스 안내 |
| `loader/` | 게임이 WineD3D 사본을 쓰게 하는 `version.dll` 프록시 |
| `data/` | 게임 프로필, 작업한 게임 목록, 후보 목록(544개: 평면 2D 448개, 일반 입체 96개는 Geo Gaming Hub 대상) |
| `tools/analysis/` | 자동 테스트, 시차 지도, draw별 이미지 도구(폴더 README 참고) |
| `tools/library_survey/` | Steam과 PCGamingWiki 자료로 후보 목록을 만듭니다 |
| `tools/stackdump/` | 멈춘 게임의 모든 스레드 스택을 출력합니다 |
| `tools/wined3d_rename.py` | WineD3D의 d3d11/dxgi를 `w3d11.dll`/`wxgi.dll`로 바꿔 씁니다 |
| `third_party/wined3d-11.19/` | WineD3D 빌드 출처(`SOURCE.txt`, 바이너리는 git에 없음) |

`git clone --recursive https://github.com/nautymac/Flato3D-SR`로 받습니다.

## 사용법

릴리스 페이지에서 패키지를 받아 아무 곳에나 풀고 `Flato3DInstaller.exe`를 실행합니다. Windows 10/11에 기본으로 있는 .NET Framework 4.x로 동작합니다.

1. **게임** 탭에 Steam과 Heroic(Epic, GOG)에 설치된 게임이 나옵니다. 초록색은 시험을 마친 프로필이 있는 게임이고, 나머지는 후보 목록의 예상이 함께 표시됩니다. 목록에 없는 게임은 **exe 직접 고르기**로 고릅니다.
2. 게임을 고르면 exe를 읽어 32/64비트와 경로(OpenGL, 또는 WineD3D를 거치는 Direct3D 8/9, 10/11)를 정합니다. 깊이 방식, 출력, 업스케일도 게임 정보로 미리 고르고 근거를 보여 줍니다.
   - 경로: Unity 게임은 지난 실행 로그의 API로 정합니다(Unity는 OpenGL을 가져오지만 기본은 Direct3D 11로 그림)
   - 깊이 방식: 프로필, 아니면 후보 목록의 장르(어드벤처는 발 위치, 나머지는 그리는 순서, Unity는 draw마다 한 층)
   - 원근이 있는 일반 입체 게임(2.5D, 3D)은 여기서 설치하지 않습니다. Geo Gaming Hub에서 다룹니다.
   - 출력: 이 PC에 SR 런타임이 있으면 SR 디스플레이, 없으면 좌우 SBS
   - 업스케일: PCGamingWiki에서 4K를 지원하지 않거나 4K에서 UI가 너무 작다고 나온 게임(exe가 DPI 인식이면 제외)
   - 이미 설치된 게임은 설치된 설정(게임 안에서 Ctrl+F7로 저장한 값 포함)을 보여 줍니다. 모두 바꿀 수 있습니다.
3. 출력과 분리값을 정하고 **설치**를 누릅니다. SR 디스플레이(Acer, Samsung)가 아니면 3D 디스플레이에 맞게 **좌우 SBS**나 **위아래**를 고릅니다. 게임이 기본 해상도(예: 4K)를 못 쓰거나 글씨가 너무 작으면 **업스케일**을 켜고 게임은 1920×1080으로 둡니다. 게임은 그 해상도로 그리고, 화면에는 기본 해상도로 확대해 보여 줍니다(`[Display] upscale`). 다른 도구(geo-11, dgVoodoo, ReShade)의 그래픽 DLL은 `Flato3D_backup` 폴더로 옮기고, 모든 변경은 exe 옆의 `Flato3D_install.json`에 기록합니다.
4. **제거**는 설치한 것을 지우고, 옮겨 둔 파일과 레지스트리 값을 되돌립니다.
5. 게임을 해 본 뒤 **상태 보고**를 누르고 결과(잘 됨 / 일부 문제 / 안 됨)를 고릅니다. 결과는 게임 목록에 표시됩니다. 문제가 있으면 증상을 적습니다. 스크린샷은 파일로 추가하거나 Ctrl+V로 붙여 넣고(최근 캡처는 목록에서 체크), 로그, 설정, 마지막 Ctrl+F12 덤프와 함께 한 폴더에 모입니다. 원하면 [GitHub 이슈](https://github.com/nautymac/Flato3D-SR/issues) 페이지가 열립니다. 만든 zip을 이슈에 끌어다 놓으면 됩니다.

### 알아 둘 것

- **NVIDIA GPU에서만 동작을 보장합니다.** Intel GPU에서는 셰이더가 깨진 적이 있습니다. NVIDIA와 다른 GPU가 함께 있는 노트북은 설치할 때 게임을 NVIDIA GPU(고성능)로 실행하도록 Windows 그래픽 설정을 바꾸고, 제거하면 되돌립니다.
- **Windows Defender 오탐:** Defender가 Direct3D 게임용 로더(`version.dll`)를 바이러스(예: `Trojan:Win32/Cloxer`)로 잘못 진단할 수 있습니다. 게임이 불러오는 DLL을 WineD3D로 돌리는 방식 때문이며 악성 코드가 아닙니다(소스는 이 저장소의 `loader/`).
  - 막히면 3D가 나오지 않고, 설치기에 "version.dll 없음"이 표시됩니다.
  - 설치기에서 그 게임을 고르고 **Defender 예외**를 누르면 그 게임의 `version.dll`만 예외로 등록합니다(관리자 권한 확인). 이미 격리됐다면 **설치**를 다시 누릅니다.
  - **제거**할 때 예외도 지울 수 있습니다. 다른 백신을 쓰면 그 백신에서 같은 파일을 예외로 등록하세요.

**후보 목록** 탭에서는 평면 2D 후보 448개와 각 게임의 경로를 볼 수 있습니다.

단축키:

| 키 | 동작 |
| --- | --- |
| Ctrl+F3 / Ctrl+F4 | 분리값 낮춤 / 높임 |
| Ctrl+F5 / Ctrl+F6 | 평면 게임: 화면 위치를 앞으로 / 뒤로 |
| Ctrl+F7 | 값 저장 |
| Ctrl+Alt+W | SR 디스플레이 출력 켜기/끄기(끄면 좌우 SBS 화면) |
| Ctrl+F12 | 두 눈 이미지와 draw 기록 저장(분석용) |

이 키들은 게임에 전달되지 않습니다.

## 빌드

- `installer\src\build.cmd`: `installer\bin\Flato3DInstaller.exe`를 빌드합니다. `package.ps1`은 `dist\`에 릴리스 zip을 만듭니다.
- `ogl3d\build.ps1`: `opengl32.dll` 두 가지(x64, x86)를 빌드합니다. SR SDK 경로는 OGL3D-SR README를 보세요.
- `loader\build.ps1`: `version.dll` 두 가지를 빌드합니다.
- `tools\wined3d_rename.py <wined3d 폴더> <출력 폴더>`: Direct3D 11 게임용으로 이름을 바꾼 WineD3D 세트를 만듭니다.

## 출처와 라이선스

- **masterotaku**의 Castlevania Dominus Collection 3D 수정([HelixMod](https://helixmod.blogspot.com/2026/06/castlevania-dominus-collection.html))이 2D 게임의 레이어별 깊이를 보여 줬습니다. 아이디어만 참고했습니다. 그 라이선스가 파생물을 허용하지 않으므로 코드는 가져오지 않았습니다.
- **WineD3D**는 [Wine](https://www.winehq.org/) 프로젝트의 일부입니다(LGPL 2.1 이상). Windows용 빌드는 Federico Dossena([wined3d4win](https://github.com/adolfintel/wined3d4win))가 만들었습니다. 이 저장소에는 넣지 않았고, 릴리스 패키지에 소스 정보와 함께 포함합니다.
- **helifax**의 [OGL-3DVision-Wrapper](https://github.com/helifax/OGL-3DVision-Wrapper)는 OGL3D를 만들 때 참고했습니다. 코드는 가져오지 않았습니다.
- **data/game_candidates.json**은 [PCGamingWiki](https://www.pcgamingwiki.com/)(CC BY-NC-SA 3.0)의 엔진·API 정보와 Steam 상점 태그를 씁니다.
- **SR SDK / SR Platform**은 Leia Inc. / Acer SpatialLabs의 것입니다. 포함하지 않았고, 설치된 SR 런타임에서 실행 중에 불러옵니다.

이 저장소의 코드는 MIT 라이선스입니다(`LICENSE`). `ogl3d` 서브모듈도 MIT 라이선스입니다.
