# Flato3D for Linux

[한국어](#한국어) | [English](#english)

## 한국어

DroidDeck이나 Steam Deck의 데스크탑 모드에서 Steam(Proton) 게임에 Flato3D를 설치하는 도구입니다. Windows 설치기와 같은 프로필(`data/game_profiles.json`)과 후보 목록을 쓰고, Flato3D-SR 패키지의 파일(`ogl3d/`, `wined3d/`)을 그대로 씁니다.

### 동작 방식
Windows의 로더(`version.dll`) 대신 Proton의 DLL 설정(`WINEDLLOVERRIDES`)으로 게임이 우리 DLL을 읽게 합니다.
- **OpenGL 게임:** exe 옆에 `opengl32.dll`(OGL3D)을 넣습니다.
- **Direct3D 8/9 게임:** WineD3D의 `d3d9.dll`, `wined3d.dll`(필요하면 `d3d8.dll`, `ddraw.dll`)과 `opengl32.dll`을 넣습니다.
- **Direct3D 10/11 게임:** Proton에 들어 있는 WineD3D를 씁니다(`PROTON_USE_WINED3D=1`). exe 옆에는 `opengl32.dll`만 넣습니다.
- **OGL3D.ini:** Windows 설치기와 같은 값으로 씁니다(프로필, 깊이 방식, 분리값). 출력은 좌우 SBS(절반 폭)이고, 업스케일은 끕니다. 업스케일 표시 경로는 NVIDIA용입니다.
- **다른 도구의 파일:** 그래픽 DLL(geo-11, dgVoodoo 등)은 `.flato3d_linux/backup/`으로 옮깁니다. 제거하면 설치 전 상태로 되돌립니다.
- **하지 않는 것:** 프로필의 레지스트리 설정은 Linux에서 하지 않습니다.

Proton에서는 아직 시험 중입니다. 결과를 알려 주세요.

### 설치
1. Flato3D-SR 릴리스 zip을 기기에 풉니다(예: `~/Flato3D-SR/`). 이 폴더는 그 안의 `linux/`입니다.
2. 데스크탑 모드 터미널에서 실행합니다.
   ```sh
   sh ~/Flato3D-SR/linux/install-desktop.sh
   ```
   앱 메뉴와 바탕화면에 "Flato3D (Linux)" 아이콘이 생깁니다. 바탕화면에 바로 보이지 않으면 바탕화면에서 F5로 새로 고칩니다.

### 사용
1. 아이콘을 실행하고 게임 번호를 고른 뒤 **1) 설치**를 누릅니다. Flato3D로 할 수 있는 게임만 나오고, `a`를 누르면 모두 보입니다.
2. 안내에 나오는 실행 옵션을 Steam > 게임 속성 > 실행 옵션에 넣습니다. 클립보드에 자동으로 복사됩니다.
   ```
   WINEDLLOVERRIDES="opengl32,wined3d,d3d9=n,b" %command%
   ```
   예시는 Shank(Direct3D 9)의 값입니다.
3. DroidDeck에서는 Cardboard를 끄고 봅니다. 게임이 직접 좌우 SBS로 나옵니다.
4. 단축키:
   - Ctrl+T: 3D 켜기/끄기
   - Ctrl+F3 / Ctrl+F4: 분리
   - Ctrl+F5 / Ctrl+F6: 화면 위치
   - Ctrl+F7: 저장

다시 설치해도 게임 안에서 Ctrl+F7로 저장한 값은 유지됩니다. 명령줄로도 쓸 수 있습니다.
```sh
python3 flato3d-linux.py --list
python3 flato3d-linux.py --install <appid|이름|exe경로> [--output tab] [--separation 10]
python3 flato3d-linux.py --uninstall <appid|이름|exe경로>
```

## English

Installs Flato3D into Steam (Proton) games from the DroidDeck or Steam Deck desktop. It uses the same profiles (`data/game_profiles.json`) and candidate list as the Windows installer, and the Flato3D-SR package's files (`ogl3d/`, `wined3d/`).

### How it works
Proton's DLL overrides (`WINEDLLOVERRIDES`) make the game load our DLLs, in place of the Windows loader (`version.dll`).
- **OpenGL games:** `opengl32.dll` (OGL3D) next to the exe.
- **Direct3D 8/9 games:** WineD3D's `d3d9.dll`, `wined3d.dll` (plus `d3d8.dll`, `ddraw.dll` when imported) and `opengl32.dll`.
- **Direct3D 10/11 games:** Proton's own WineD3D (`PROTON_USE_WINED3D=1`). Only `opengl32.dll` goes next to the exe.
- **OGL3D.ini:** written with the Windows installer's values (profile, depth rule, separation). Output is half side-by-side. Upscale is off, because its presenter is NVIDIA-only.
- **Other tools' files:** graphics DLLs (geo-11, dgVoodoo, ...) move to `.flato3d_linux/backup/`. Removing puts everything back as it was.
- **Not done:** a profile's registry settings are not applied on Linux.

Flato3D under Proton is still being tried out. Please report results.

### Install
1. Unpack the Flato3D-SR release zip on the device (e.g. `~/Flato3D-SR/`). This folder is its `linux/`.
2. In a desktop terminal, run:
   ```sh
   sh ~/Flato3D-SR/linux/install-desktop.sh
   ```
   A "Flato3D (Linux)" icon appears in the app menu and on the desktop. If the desktop does not show it, press F5 there.

### Use
1. Start the icon, pick a game number, then **1) Install**. Only games Flato3D can do are listed; `a` shows all.
2. Put the launch options it prints into Steam > game Properties > Launch Options. They are also copied to the clipboard.
3. On DroidDeck, keep Cardboard off. The game itself shows side by side.
4. Keys:
   - Ctrl+T: 3D on/off
   - Ctrl+F3 / Ctrl+F4: separation
   - Ctrl+F5 / Ctrl+F6: screen plane
   - Ctrl+F7: save

Reinstalling keeps the values saved in game with Ctrl+F7. Command line: `--list`, `--install`, `--uninstall` (see above).
