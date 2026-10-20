Flato3D-SR - stereo depth for flat 2D games
https://github.com/nautymac/Flato3D-SR

[한국어]
1. 이 폴더를 아무 곳에나 풀고 Flato3DInstaller.exe를 실행합니다. 폴더 구성은 바꾸지 마세요.
2. 게임 탭에서 게임을 고르면 32/64비트, 경로(OpenGL / Direct3D → WineD3D), 깊이 방식, 출력, 업스케일이
   게임 정보(확인된 프로필, PCGamingWiki의 4K 지원 여부, exe, 이 PC의 SR 런타임)로 미리 골라지고, 그 근거가 표시됩니다.
   목록에는 Flato3D로 할 수 있는 게임만 나옵니다. 목록에 없는 게임은 아래쪽 [게임 추가]로 exe를 고르면 목록에 추가됩니다.
   설치하면 목록에 계속 나오고, 해 본 뒤 [상태 보고]를 하면 프로필로 만들어집니다.
3. 출력(SR 디스플레이(Acer, Samsung) / 좌우 SBS / 위아래)과 분리값을 정하고 [설치]를 누릅니다.
   게임이 기본 해상도(예: 4K)를 못 쓰거나 글씨가 너무 작으면 [업스케일]을 켜고, 게임은 1920×1080으로 둡니다.
4. 게임을 평소처럼 실행합니다. Ctrl+F3/F4 분리값, Ctrl+F5/F6 화면 위치, Ctrl+F7 저장, Ctrl+T 3D 켜기/끄기.
5. [제거]는 설치한 파일을 지우고, 옮겨 두었던 다른 도구의 파일(geo-11, dgVoodoo, ReShade)과 레지스트리를 되돌립니다.
   설치 기록은 게임 exe 옆의 Flato3D_install.json에 남습니다.
6. 게임을 해 본 뒤 [상태 보고]를 누르고 결과(잘 됨 / 일부 문제 / 안 됨)를 고릅니다. 결과는 게임 목록에 표시됩니다.
   문제가 있으면 증상을 적습니다. 로그와 설정이 한 폴더에 모입니다. GitHub에도 올리면(기본) 공개 상태 목록
   (github.com/nautymac/Flato3D-SR/blob/main/STATUS.ko.md)이 자동으로 바뀝니다. GitHub CLI(gh)가 로그인돼 있으면 바로 올라가고,
   아니면 보고 페이지가 열립니다(결과를 고르고 제출).
   그 장면에서 게임 안 Ctrl+F12를 먼저 누르면 두 눈 이미지도 함께 들어갑니다.
   스크린샷은 [파일 추가]나 Ctrl+V(Win+Shift+S로 캡처한 것)로 넣고, 최근 캡처는 목록에서 체크합니다.
알아 둘 것
- NVIDIA GPU에서만 동작을 보장합니다. Intel GPU에서는 셰이더가 깨진 적이 있습니다. NVIDIA와 다른 GPU가 함께 있는
  노트북은 설치할 때 게임을 NVIDIA GPU(고성능)로 실행하도록 Windows 그래픽 설정을 바꾸고, 제거하면 되돌립니다.
- Windows Defender가 Direct3D 게임용 로더(version.dll)를 바이러스로 잘못 진단할 수 있습니다(예: Trojan:Win32/Cloxer).
  게임이 불러오는 DLL을 WineD3D로 돌리는 방식 때문이며 악성 코드가 아닙니다(소스: github.com/nautymac/Flato3D-SR).
  막히면 3D가 나오지 않습니다. 설치기에서 그 게임을 고르고 [Defender 예외]를 누르면 그 version.dll만 예외로 등록합니다
  (관리자 권한 확인). 이미 격리됐다면 그 뒤 [설치]를 다시 누릅니다. [제거]할 때 예외도 지울 수 있습니다.
  다른 백신을 쓰면 그 백신에서 게임 폴더의 version.dll을 예외로 등록하세요.
초록색 게임은 확인된 설정(프로필)이 있습니다. 다른 게임은 기본 설정으로 시도하며, 게임마다 결과가 다를 수 있습니다.
Direct3D 12, Vulkan, 완성 화면 한 장만 내는 게임(DOSBox, ScummVM, 에뮬레이터)은 이 방법으로 할 수 없습니다.

[English]
1. Unpack this folder anywhere and run Flato3DInstaller.exe. Keep the folder layout as it is.
2. Choose a game on the Games tab: 32/64-bit, the route (OpenGL / Direct3D -> WineD3D), the depth rule, the output and
   upscale are pre-selected from what is known about the game (tested profile, PCGamingWiki 4K support, the exe, the SR
   runtime on this PC), with the reason shown for each.
   Only games Flato3D can do are listed. Other games: [Add a game...] (next to the executable, adds it to the list); once installed they stay in
   the list, and a [Report status] after playing turns them into a profile.
3. Pick the output (SR display (Acer, Samsung) / side by side / top and bottom) and the separation, then [Install].
   If the game cannot use the native resolution (e.g. 4K) or its text is too small, tick [Upscale] and set the game to 1920x1080.
4. Start the game as usual. Ctrl+F3/F4 separation, Ctrl+F5/F6 screen plane, Ctrl+F7 save, Ctrl+T 3D on/off.
5. [Uninstall] removes the installed files and puts back other tools' files it moved aside (geo-11, dgVoodoo,
   ReShade) and registry values. The record is Flato3D_install.json next to the game exe.
6. After playing, press [Report status] and choose the result (works / some problems / does not work); the game list
   shows it. Describe a problem if there is one: logs and settings are collected in one folder. Posted on GitHub too
   (the default), it updates the public status list (github.com/nautymac/Flato3D-SR/blob/main/STATUS.md): filed right
   away when the GitHub CLI (gh) is signed in, otherwise the report page opens (pick the result and submit).
   Press Ctrl+F12 in game at that scene first to include both eyes.
   Add screenshots with [Add files...] or Ctrl+V (a Win+Shift+S capture); recent captures are listed to tick.
Good to know
- Only NVIDIA GPUs are supported; on Intel GPUs shaders have broken. On laptops with NVIDIA and another GPU, install sets
  Windows' graphics setting for the game to the NVIDIA GPU (high performance); uninstall puts it back.
- Windows Defender may wrongly flag the loader for Direct3D games (version.dll) as malware (e.g. Trojan:Win32/Cloxer).
  It sends the DLLs the game loads to WineD3D and is not malware (source: github.com/nautymac/Flato3D-SR). When it is
  blocked there is no 3D: choose the game in the installer and press [Defender exclusion] to exclude that version.dll
  only (Windows asks for permission); if it was already quarantined, press [Install] again. [Uninstall] offers to remove
  the exclusion. With another antivirus, exclude the game folder's version.dll there.
Green games have a tested profile; others are tried with default settings and results vary per game.
Direct3D 12, Vulkan and games that output one finished image (DOSBox, ScummVM, emulators) are not possible this way.

License: MIT (LICENSE). Third-party components: THIRD_PARTY.txt.
