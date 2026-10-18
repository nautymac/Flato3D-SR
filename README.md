# Flato3D-SR

**English** · [한국어](README.ko.md)

Flato3D gives flat 2D games, pixel art or hand-drawn, real stereo depth. A flat game sends no depth to the GPU, so Flato3D gives every layer and sprite its own depth from the way the game draws:

- **Side-scrollers:** drawing order. The first layers drawn are far, the last are near.
- **Adventure games:** the bottom edge of each sprite (where it stands).
- **HUD:** stays on the screen plane.

The picture is woven for lenticular SR panels (Acer SpatialLabs, Leia SR displays) through the installed SR runtime. The SR output can be turned off: side-by-side and top-and-bottom work with any other 3D display.

Status: early. Six games work, see [data/GAMES.ko.md](data/GAMES.ko.md).

| Game | API | Route | Depth rule |
| --- | --- | --- | --- |
| Technobabylon | OpenGL (AGS) | direct | feet |
| Huntdown | Direct3D 11 (Unity) | WineD3D | drawing order, one layer per draw |
| Door Kickers: Action Squad | Direct3D 9 | WineD3D | drawing order, layer cache |
| FRAMED Collection | OpenGL 3.2 | direct | drawing order, repeated panels share a layer |
| Astral Ascent | Direct3D 11 (Chowdren) | WineD3D | drawing order, scratch targets as one layer |
| Intravenous 2 | OpenGL (LÖVE) | direct | drawing order |

## How it works

- **OpenGL games:** the `opengl32.dll` of [OGL3D-SR](https://github.com/nautymac/OGL3D-SR) (branch `flato3d`) renders both eyes. For flat games, a generated geometry shader shifts each sprite by its own depth.
- **Direct3D 8/9/10/11 games:** WineD3D translates Direct3D to OpenGL, and OGL3D does the rest. A small `version.dll` loader makes the game load the WineD3D copies even when Steam's overlay has already loaded the system DLLs. Direct3D 12 is not covered.
- **Per-game settings** are in [data/game_profiles.json](data/game_profiles.json): files to install, `OGL3D.ini` values, registry keys, known issues, and test steps.

## Workflow

The step-by-step procedure for converting a new game (checkpoints, measurements, symptoms and fixes, how to record results) is in [docs/WORKFLOW.ko.md](docs/WORKFLOW.ko.md) (Korean).

## Layout

| Folder | Contents |
| --- | --- |
| `ogl3d/` | submodule: OGL3D-SR, branch `flato3d` (the stereo and depth engine) |
| `installer/` | the installer (C# WinForms) and the package's README and third-party notice |
| `loader/` | `version.dll` proxy that sends the game to the WineD3D copies |
| `data/` | game profiles, the list of worked games, the candidate list (544 games: 448 flat 2D, 96 ordinary stereo games left to Geo Gaming Hub) |
| `tools/analysis/` | test harness, disparity maps, per-draw images (see its README) |
| `tools/library_survey/` | builds the candidate list from Steam and PCGamingWiki data |
| `tools/stackdump/` | prints every thread's stack of a hung game |
| `tools/wined3d_rename.py` | makes WineD3D's d3d11/dxgi loadable as `w3d11.dll`/`wxgi.dll` |
| `third_party/wined3d-11.19/` | where the WineD3D builds come from (`SOURCE.txt`; binaries are not in git) |

Clone with `git clone --recursive https://github.com/nautymac/Flato3D-SR`.

## Using it

Download the package from the releases page, unpack it anywhere and run `Flato3DInstaller.exe` (Windows 10/11, .NET Framework 4.x, which Windows includes).

1. The **Games** tab lists the games installed through Steam and Heroic (Epic, GOG). Green ones have a tested profile; the others show what the candidate list expects. Games not listed: **Choose an exe...**.
2. Choosing a game detects 32/64-bit and the route (OpenGL, or Direct3D 8/9 or 10/11 through WineD3D) from the exe. The depth rule, the output and upscale are pre-selected from what is known about the game, with the reason shown:
   - route: Unity games by the API their last run reported (Unity's player imports OpenGL but renders with Direct3D 11 by default)
   - depth rule: the profile, else the candidate list's genre (adventure: feet, others: drawing order; Unity: a layer per draw)
   - games with real depth (2.5D, 3D) are not installed here: Geo Gaming Hub gives them ordinary stereo
   - output: SR display when this PC has the SR runtime, else side by side
   - upscale: games that PCGamingWiki lists without 4K support or with a UI that stays tiny at 4K (not when the exe is DPI aware)
   - an installed game shows its installed settings (including values saved in game with Ctrl+F7). Everything can be changed.
3. Pick the output and the separation, then **Install**. Without an SR display (Acer, Samsung), choose **side by side** or **top and bottom** to match your 3D display. If the game cannot use the native resolution (e.g. 4K) or its text gets too small, tick **Upscale** and set the game to 1920x1080: it renders at that size and is shown enlarged at the native resolution (`[Display] upscale`). Other tools' graphics DLLs (geo-11, dgVoodoo, ReShade) are moved to `Flato3D_backup`; everything is recorded in `Flato3D_install.json` next to the exe.
4. **Uninstall** removes what was installed and puts the moved files and registry values back.
5. Something wrong? Press **Report problem** and describe it. Add screenshots as files or with Ctrl+V (recent captures are listed to tick); they go into one folder with the logs, the settings and the last Ctrl+F12 dump, and a [GitHub issue](https://github.com/nautymac/Flato3D-SR/issues) page can be opened: drag the zip it made into the issue.

The **Candidate list** tab shows the 448 flat 2D candidates with their route.

Hotkeys:

| Key | Action |
| --- | --- |
| Ctrl+F3 / Ctrl+F4 | separation down / up |
| Ctrl+F5 / Ctrl+F6 | flat games: move the screen plane nearer / farther |
| Ctrl+F7 | save the values |
| Ctrl+Alt+W | SR display output on/off (off shows the side-by-side picture) |
| Ctrl+F12 | save both eyes and a draw trace (for analysis) |

The game does not receive these keys.

## Building

- `installer\src\build.cmd` builds `installer\bin\Flato3DInstaller.exe`; `package.ps1` makes the release zip in `dist\`.
- `ogl3d\build.ps1` builds both `opengl32.dll`. See the OGL3D-SR README for the SR SDK path.
- `loader\build.ps1` builds both `version.dll`.
- `tools\wined3d_rename.py <wined3d folder> <output folder>` prepares the renamed WineD3D set for Direct3D 11 games.

## Credits and license

- **masterotaku's** Castlevania Dominus Collection 3D fix ([HelixMod](https://helixmod.blogspot.com/2026/06/castlevania-dominus-collection.html)) showed per-layer depth for 2D games. Only the idea was used; its license does not allow derivatives, and no code was taken.
- **WineD3D** is part of the [Wine](https://www.winehq.org/) project (LGPL 2.1 or later). The Windows builds are by Federico Dossena ([wined3d4win](https://github.com/adolfintel/wined3d4win)). They are not in this repository; release packages carry them with their source information.
- **helifax's** [OGL-3DVision-Wrapper](https://github.com/helifax/OGL-3DVision-Wrapper) was a reference for OGL3D. No code was taken.
- **data/game_candidates.json** uses engine and API information from [PCGamingWiki](https://www.pcgamingwiki.com/) (CC BY-NC-SA 3.0) and Steam store tags.
- **SR SDK / SR Platform:** Leia Inc. / Acer SpatialLabs. Not included; loaded at run time from the installed SR runtime.

The code in this repository is MIT licensed, see `LICENSE`. The `ogl3d` submodule is MIT licensed as well.
