Flato3D-IntelARC - stereo depth for flat 2D games, Intel Arc (3D One) edition
https://github.com/nautymac/Flato3D-SR

[한국어]
Flato3D-SR의 Intel Arc GPU용 판입니다. 3D One 노트북처럼 Intel Arc GPU와 AstraDepth3D 앱이 있는 PC에서 씁니다.
출력은 좌우 SBS(반쪽씩)이고, 그 화면을 AstraDepth3D가 렌티큘러 패널에 맞게 엮습니다. SR 디스플레이(Acer, Samsung)와
NVIDIA GPU에는 Flato3D-SR을 쓰세요.
1. 이 폴더를 아무 곳에나 풀고 Flato3DInstaller.exe를 실행합니다. 폴더 구성은 바꾸지 마세요.
2. 게임 탭에서 게임을 고르면 32/64비트, 경로(OpenGL / Direct3D → WineD3D), 깊이 방식, 업스케일이 게임 정보(확인된
   프로필, PCGamingWiki의 4K 지원 여부, exe)로 미리 골라지고, 그 근거가 표시됩니다. 목록에는 Flato3D로 할 수 있는
   게임만 나옵니다. 목록에 없는 게임은 아래쪽 [게임 추가]로 exe를 고르면 목록에 추가됩니다.
3. [설치]를 누릅니다. 게임 폴더에 래퍼를 넣고, 게임 exe를 AstraDepth3D의 게임 목록(gamelist.json)에 "좌우 SBS 게임"으로
   등록합니다(Program Files라 관리자 권한 확인 창이 뜹니다). AstraDepth3D는 켜질 때만 목록을 읽으므로, 설치한 뒤
   [AstraDepth3D 재시작]을 한 번 누릅니다(설치기가 물어보기도 합니다).
   게임이 기본 해상도(예: 2560×1600)를 못 쓰거나 글씨가 너무 작으면 [업스케일]을 켜고, 게임은 1920×1080으로 둡니다.
4. 게임을 전체 화면으로 실행하고 AstraDepth3D의 3D 단축키로 3D를 켭니다. 화면이 좌우로 나뉘어 보이면 AstraDepth3D가
   아직 이 게임을 모르는 것입니다(재시작 안 함, 또는 등록 실패: 설치기의 게임 정보에 표시됨).
   Ctrl+F3/F4 분리값, Ctrl+F5/F6 화면 위치, Ctrl+F7 저장, Ctrl+T Flato3D 3D 켜기/끄기(끄면 평면 그림이 되므로 그때는
   AstraDepth3D의 3D도 끕니다).
5. [제거]는 설치한 파일을 지우고, 옮겨 두었던 다른 도구의 파일(geo-11, dgVoodoo, ReShade)과 레지스트리를 되돌리고,
   AstraDepth3D 목록에서도 뺍니다. 설치 기록은 게임 exe 옆의 Flato3D_install.json에 남습니다.
6. 게임을 해 본 뒤 [상태 보고]를 누르고 결과(잘 됨 / 일부 문제 / 안 됨)를 고릅니다. 결과는 게임 목록에 표시됩니다.
   문제가 있으면 증상을 적습니다. 로그와 설정이 한 폴더에 모입니다. GitHub에도 올리면(기본) 공개 상태 목록
   (github.com/nautymac/Flato3D-SR/blob/main/STATUS.ko.md)이 자동으로 바뀝니다.
   그 장면에서 게임 안 Ctrl+F12를 먼저 누르면 두 눈 이미지도 함께 들어갑니다.
알아 둘 것
- Intel Arc GPU에서 확인한 것: Intel 드라이버의 OpenGL은 NVIDIA와 다른 데가 있어 래퍼가 Intel에 맞춰 동작합니다
  (게임 폴더의 OGL3D.log 첫 줄들에 GPU와 드라이버 버전이 남습니다). 게임마다 결과가 다를 수 있습니다.
- Windows Defender가 Direct3D 게임용 로더(version.dll)를 바이러스로 잘못 진단할 수 있습니다(예: Trojan:Win32/Cloxer).
  게임이 불러오는 DLL을 WineD3D로 돌리는 방식 때문이며 악성 코드가 아닙니다(소스: github.com/nautymac/Flato3D-SR).
  막히면 3D가 나오지 않습니다. 설치기에서 그 게임을 고르고 [Defender 예외]를 누르면 그 version.dll만 예외로 등록합니다
  (관리자 권한 확인). 이미 격리됐다면 그 뒤 [설치]를 다시 누릅니다. [제거]할 때 예외도 지울 수 있습니다.
초록색 게임은 확인된 설정(프로필)이 있습니다. 다른 게임은 기본 설정으로 시도하며, 게임마다 결과가 다를 수 있습니다.
Direct3D 12, Vulkan, 완성 화면 한 장만 내는 게임(DOSBox, ScummVM, 에뮬레이터)은 이 방법으로 할 수 없습니다.

[English]
The Intel Arc GPU edition of Flato3D-SR, for PCs with an Intel Arc GPU and the AstraDepth3D app, such as the 3D One
laptop. The output is side by side (half width each); AstraDepth3D weaves that picture for the lenticular panel. For
SR displays (Acer, Samsung) and NVIDIA GPUs use Flato3D-SR.
1. Unpack this folder anywhere and run Flato3DInstaller.exe. Keep the folder layout as it is.
2. Choose a game on the Games tab: 32/64-bit, the route (OpenGL / Direct3D -> WineD3D), the depth rule and upscale
   are pre-selected from what is known about the game (tested profile, PCGamingWiki 4K support, the exe), with the
   reason shown for each. Only games Flato3D can do are listed. Other games: [Add a game...].
3. Press [Install]. It puts the wrapper into the game folder and registers the game exe in AstraDepth3D's game list
   (gamelist.json) as a side-by-side game (Windows asks for permission: Program Files). AstraDepth3D reads the list
   only when it starts: press [Restart AstraDepth3D] once after installing (the installer offers it as well).
   If the game cannot use the native resolution (e.g. 2560x1600) or its text is too small, tick [Upscale] and set
   the game to 1920x1080.
4. Run the game full screen and turn 3D on with AstraDepth3D's hotkey. If the screen stays split in two halves,
   AstraDepth3D does not know this game yet (not restarted, or the registration failed: the installer's game info
   says so).
   Ctrl+F3/F4 separation, Ctrl+F5/F6 screen plane, Ctrl+F7 save, Ctrl+T Flato3D 3D on/off (off is a flat picture:
   turn AstraDepth3D's 3D off then as well).
5. [Uninstall] removes the installed files, puts back other tools' files it moved aside (geo-11, dgVoodoo, ReShade)
   and registry values, and removes the game from the AstraDepth3D list. The record is Flato3D_install.json next to
   the game exe.
6. After playing, press [Report status] and choose the result (works / some problems / does not work); the game list
   shows it. Describe a problem if there is one: logs and settings are collected in one folder. Posted on GitHub too
   (the default), it updates the public status list (github.com/nautymac/Flato3D-SR/blob/main/STATUS.md).
   Press Ctrl+F12 in game at that scene first to include both eyes.
Good to know
- Checked on an Intel Arc GPU: Intel's OpenGL driver differs from NVIDIA's in places and the wrapper adapts to it
  (the first lines of OGL3D.log in the game folder name the GPU and driver). Results vary per game.
- Windows Defender may wrongly flag the loader for Direct3D games (version.dll) as malware (e.g. Trojan:Win32/Cloxer).
  It sends the DLLs the game loads to WineD3D and is not malware (source: github.com/nautymac/Flato3D-SR). When it is
  blocked there is no 3D: choose the game in the installer and press [Defender exclusion] to exclude that version.dll
  only (Windows asks for permission); if it was already quarantined, press [Install] again.
Green games have a tested profile; others are tried with default settings and results vary per game.
Direct3D 12, Vulkan and games that output one finished image (DOSBox, ScummVM, emulators) are not possible this way.

License: MIT (LICENSE). Third-party components: THIRD_PARTY.txt.
