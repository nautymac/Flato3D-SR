# Flato3D 3D conversion workflow

**English** · [한국어](WORKFLOW.ko.md)

*Translated from the Korean original (WORKFLOW.ko.md); when the two differ, the Korean one is current.*

This is the order to follow when you make a new game stereo with Flato3D. It sorts, by checkpoint, the problems met and the fixes found while working on Technobabylon, Door Kickers: Action Squad, Huntdown, FRAMED and Astral Ascent. There are three principles.

- **Check with numbers before checking by eye.** Find the cause with dumps, draw traces and disparity maps, then fix it.
- **When fixing, prefer general rules.** A problem in one game also shows up in other games on the same engine. Keep per-game differences in ini values and profiles only.
- **Record results right away.** The next game goes faster only when the profile, the list of worked games and the test steps are kept.

## Checkpoints at a glance

| Step | What to do | Pass criteria | What to keep |
| --- | --- | --- | --- |
| CP0 Research | Identify the API, engine and 32/64-bit; choose the route; check existing installs and saves | The route is chosen (if it cannot be done, stop here) | Notes |
| CP1 Install and first run | Install with the installer, run with SBS output | The game runs with a normal picture and `OGL3D.log` records frames | The install record file |
| CP2 Understand the frame | Check the drawing order with the Ctrl+F12 dump and per-draw images | You know what is drawn by which draw, into which buffer | A summary of the draw trace |
| CP3 Measure depth | Measure each layer's disparity with the disparity map | Order far background > terrain > characters > HUD, no odd values | Measurements |
| CP4 Fix | Adjust ini values or change the wrapper's general rules | Meets the CP3 criteria | Changed values, commits |
| CP5 Play test | Actually play through several scenes; check flicker, hangs and fps | Criteria met in 3 or more scenes, 60fps, no flicker | Measurements per scene |
| CP6 User check | The user plays it (SBS if remote) | The user confirms | Status update |
| CP7 Record and release | Profile, list, test steps, commit and push, release | In the repository | Commits |

---

## CP0 Research

1. **Check the lists:** First look at `data/game_profiles.json` (worked games) and `data/game_candidates.json` (544 candidates with their estimated API, engine and depth rule). `kind = 1` (96 ordinary stereo games with perspective) is not a Flato3D target; Geo Gaming Hub handles those.
2. **Identify the exe:** Use the installer's command line to see 32/64-bit and the route.
   ```
   installer\bin\Flato3DInstaller.exe --detect "<game exe>" --log detect.txt
   ```
   - Choose the route from the DLLs the exe imports (opengl32 / d3d9 / d3d11·dxgi / d3d12 / vulkan-1).
   - Treat Unity (`UnityPlayer.dll`) and Unreal (`*-Shipping.exe`) as D3D11.
   - If it imports none of them (the game loads the DLL at run time), use the API from the candidate list.
3. **Choose the route**

   | Graphics API | Route | Files to add |
   | --- | --- | --- |
   | OpenGL | direct | `opengl32.dll`, `OGL3D.ini` |
   | OpenGL selectable (AGS `driver=OGL`, Unity `-force-glcore`, launch option) | switch the game to OpenGL, then direct | same as above + change the game's setting |
   | Direct3D 8/9, DirectDraw | WineD3D (regular) | above + `version.dll` (loader), `wined3d.dll`, `d3d9.dll` (if needed `d3d8.dll`, `ddraw.dll`) |
   | Direct3D 10/11 | WineD3D (renamed set) | above + `w3d11.dll`, `wxgi.dll`, `w3d10core.dll`, `wined3d.dll` (D3D10 games also `d3d10*.dll`) |
   | Direct3D 12, Vulkan | not possible (separate project) | — |
   | One finished picture (DOSBox, ScummVM, emulators, RPG Maker VX Ace, software rendering) | not possible (AI depth only) | — |
   | Browser engines (NW.js, Construct, RPG Maker MV/MZ) | difficult | — |

4. **First settings per engine**

   | Engine / genre | Setting | Based on |
   | --- | --- | --- |
   | AGS and other adventures (characters walk on a floor) | `rule = feet` | Technobabylon, Machinarium (fixed function) |
   | Side-scrollers, top-down action | `rule = order` | DK, Huntdown, Astral Ascent |
   | Unity (draws parallax layers separately from the same atlas) | `group = draw`, `order_curve = 0.5` | Huntdown |
   | Draws into a low-resolution buffer, then stretches it to the screen | Handled automatically (layer cache). If it does not match the screen's proportions, `cache_scale` | DK |
   | Games that set glScissor for every draw | `screen_batch = 0` | FRAMED |
   | Black silhouettes inside a picture that already has the figures merged in (video panels etc.) | `silhouette = <program hash>`, the shader's coordinate and texture names | FRAMED |
   | Unity (D3D11 by default even when the exe imports opengl32) | Identify the route from the `Version:` line of `<exe>_Data\output_log.txt` or `Player.log`; `group = draw`, `order_curve = 0.5` | Huntdown, GGC2 |
   | Engines that use ARB assembly programs through NVIDIA Cg (PSSG etc., `cg.dll`/`cgGL.dll` next to the exe) | The wrapper rewrites ARB vertex programs automatically. Usually `screen_batch = 0` | Rocketbirds |
   | 2.5D and 3D (scenes with perspective) | Not a Flato3D target: install as ordinary stereo in Geo Gaming Hub | Broken Age |

5. **Check existing installs:** See whether the game folder has geo-11 (`d3d11.dll`, `nvapi64.dll`, `d3dx.ini`), SRWeave (`dxgi.dll`), dgVoodoo (`D3D9.dll`), ReShade or OGL3D 1.0 (`opengl32.dll`). The installer moves them to `Flato3D_backup`, but note down that this was an install the user had been using.
6. **Back up saves:** If you need to progress in the game, first copy the save folder to `test-backups\<game>\`. Find the save location from PCGamingWiki or the Steam Cloud path.
   - Unity: `AppData\LocalLow\<company>\<game>`
   - GameMaker: `%LOCALAPPDATA%\<game>`
   - Others: `Saved Games`, `Documents`, etc.

## CP1 Install and first run

1. Install with the installer. For a remote check, set the output to `sbs`.
   ```
   Flato3DInstaller.exe --install "<game exe>" --output sbs --log install.txt
   ```
2. Automated runs use `tools\analysis\gametest.ps1`. The steps for each game are in the profile's `test`.
   ```
   gametest.ps1 -AppId <Steam appid> -Proc <process name> -OutDir <folder> -Steps "wait:30,shot:a1,ctrlkey:7B,wait:4" -Keep
   ```
   - To keep controlling a game that is already running, use `-Attach`.
   - To hold a key down, use `hold:<vk>:<seconds>`; to click, `click:x:y` (screen pixels); for relative movement, `rmove`.
3. **Check the log (`OGL3D.log`)**
   - `loaded into`: the wrapper is loaded
   - `context ...`: an OpenGL context was created
   - `main context`: the first frame went to the screen
   - `fbo N: WxH -> stereo/mono`: how a render target was classified
   - `frame 600: ...`: statistics every 600 frames (60fps if they are 10 seconds apart)
4. **Loader log (`Flato3D_loader_<exe>.log`)**: WineD3D is in use only if you see `redirected ... -> w3d11.dll` or `static import d3d11.dll!... -> w3d11.dll`.
5. **First-run problems**

   | Symptom | Cause | Fix |
   | --- | --- | --- |
   | `OGL3D.log` is not created | Wrong route (the game does not use OpenGL) or the loader did not take effect | Identify the API again. Check the loader log |
   | Hangs at frame 3, continues after 80 to 100 seconds | Cause unknown (mostly the first run after the DLLs are replaced) | Run it again. Recorded in the profile's known issues |
   | Hangs right at start (WineD3D) | Deadlock between CSMT and NVIDIA's present path | `HKCU\Software\Wine\AppDefaults\<exe>\Direct3D` `csmt=0` (DK) |
   | With CSMT off, it crashes with a resource error from another thread | Unity and others need CSMT | Do not set the csmt key (Huntdown) |
   | A D3D11 game hangs | The NVIDIA driver grabs WineD3D's `d3d11.dll` | Use the renamed set (`w3d11.dll`) (the installer does this) |
   | The system D3D is used as is | Steam's overlay loaded it first, or the exe imports it statically | The loader handles this. Check it in the loader log |
   | The picture is upside down | WineD3D's glClipControl | Handled in the wrapper. If it shows up again, check GlStateGuard |
   | Only the right eye is black or wrong | A texture view or copy was not tracked | Check for the `R` mark in `samples` in the draw trace |

## CP2 Understand the frame

1. Put `trace_steps = 1` in `OGL3D.ini`. It takes effect within a second, even while the game runs.
2. Press Ctrl+F12 in the scene. If the user is playing, write an upcoming frame number in the ini instead of pressing the key.
   - `dump_frames = N+1`
   - `trace_frames = N`
3. **Output files**
   - `OGL3D_frameNNNNN.bmp`: both eyes (half-resolution SBS)
   - `T` lines in `OGL3D.log`: one record per draw
   - `OGL3D_stepNNNNN_DDD.bmp`: the render target after each draw
4. **Reading the draw trace**
   - `T 1234.17 b0 DrawElements... prog=3/hash(gs) fb=2S(t13) tex=43(1024x1024)`
     - `fb=2S`: drawn into stereo buffer 2. Without `S` it is mono.
     - `(gs)`: the depth geometry shader is attached
   - `samples u1/s0:257(512x310)R`: the units and textures the shader actually reads
     - `R`: there is a right-eye copy = a composite pass that reads a stereo render target
     - `v35`: a view of texture 35
   - `order group 5/24 ... target 0.348 depth 0.348 slot 0.04 prims 2`: layer position (1 = farthest back), applied depth, layer spacing, triangle count
   - `layout events N, root fbX, scratch targets: ..., composites/copies: fbA<fbB`
     - This is what the composite order is worked out from.
     - `root`: the final screen buffer
     - `fbA<fbB`: B's picture is composited into A
   - `layout (far to near, N groups, M repeats)`: the layer order as seen in the final screen. If it is 0, the composite path is broken.
5. **Looking at images**
   - `sheet.py <folder> "OGL3D_step*.bmp" out.png 4`: puts the per-draw images on one sheet.
   - `cachecrop.py`: crops only the low-resolution cache area and enlarges it.
   - In WineD3D games the buffers are saved upside down (this is normal).

## CP3 Measure depth

- `dispmap.py <frame.bmp> out.png`: shows the disparity (px) as a number for each 24px block.
  - Positive is behind the screen, negative is in front of it.
  - Repeating patterns give some false readings.
- `maskdisp.py <frame.bmp> player=x0,y0,x1,y1,purple ...`: measures the disparity of one area. `purple` is an example that looks only at purple pixels. Use it to measure small characters.
- `eyes.py`: makes an anaglyph and a small SBS.
- **What a good result looks like** (separation 25, 3840 screen, depth_screen 0.3)
  - Far background +30 to 60px
  - Terrain and middle ground +8 to 30px
  - Characters −10 to +10px
  - HUD 0 to −20px
  - The layer order matches what is in front and what is behind on screen

## CP4 Fix

### ini settings

| Setting | Meaning | When to change it |
| --- | --- | --- |
| `[Stereo] separation` | Separation (%) | When the depth is too weak or too strong overall. About 25 for flat games |
| `[Stereo] output` | auto (SR display) / sbs / tab. In the installer: "SR display (Acer, Samsung)", "Side by side", "Top and bottom" | sbs for remote checks. Without an SR display, pick sbs/tab yourself |
| `[Flat] enable` | Use flat depth | Always 1 in Flato3D (ordinary stereo games go to Geo Gaming Hub) |
| `[Flat] rule` | feet (where the sprite stands) / order (drawing order) | See the CP0 table |
| `[Flat] depth_screen` | The depth that sits at the screen plane | Lower it (0 to 0.3) if the HUD pops out; raise it if everything is too far back. In game: Ctrl+F5/F6 |
| `[Flat] depth_top` | Depth range | Usually 1 |
| `[Flat] group` | texture (consecutive draws with the same texture = one layer) / draw (one draw = one layer) | draw, when each layer has its own draw, as in Unity |
| `[Flat] order_curve` | 1 = even; below 1 gives more depth to the earlier (far) layers | 0.5 when there are many layers and the background layers look stuck together |
| `[Flat] cache_scale` | Shift ratio inside the low-resolution cache (0 = automatic, from the scissor) | When you know the ratio at which the cache area maps to the screen (DK 0.406) |
| `[Flat] screen_batch` | From which scissor batch on draws count as screen depth (UI); 0 = off | 1 for AGS, 0 for games that set a scissor for every draw |
| `[Flat] plane_size`, `plane_height`, `sprite_min` | How the feet rule tells floor planes and sprites apart | Games with the feet rule |
| `[Flat] silhouette` | Inside the pictures drawn by these programs (hashes, several separated by commas), bring dark pixels forward of the picture | When the figures come merged into one picture and the figures are black silhouettes |
| `[Flat] silhouette_uv`, `silhouette_tex` | The texture-coordinate varying (vec2) and the picture's sampler name in that fragment shader | Decide by looking at the shaders with `dump_shaders = 1` (FRAMED: `texcoordOut`, `texture1`) |
| `[Flat] silhouette_depth`, `silhouette_threshold` | How far it comes forward (as a fraction of depth_top); the brightness that counts as dark (the largest of r, g, b) | Defaults 0.3 and 0.15. If dark background comes forward too, lower the threshold |
| `[Inject] arb_programs` | Rewrite ARB assembly vertex programs (`!!ARBvp1.0`, Cg arbvp1) for stereo | Default 1. 0 if it causes problems |
| `[Inject] cg_glsl` | Makes Cg's `cgGLGetLatestProfile` return a GLSL profile, and logs `cgCreateProgram` results | Default 0. With an engine that sets the profile itself, the picture breaks (Rocketbirds: white screen) |
| `[Display] swap_interval` | 1 = force vsync | Default 1 for WineD3D games |
| `[Display] upscale` | 1 = the game renders DPI-unaware at a lower resolution (1920×1080 at 4K with 200% scaling), and the wrapper shows it enlarged in a window at native resolution. Resolution changes are blocked | When the game cannot use the native resolution or its text is too small. The installer's **Upscale** |
| `[Keys] swallow` | Do not pass the wrapper's hotkeys to the game | Default 1. 0 if it causes problems |
| `[Overlay] cursor` | With SBS and top-and-bottom output, draw the mouse cursor the game chose in both eyes, and hide the Windows cursor over the game window | Default 1. 0 if the cursor looks wrong (back to the Windows cursor). Weaving and single-eye output keep the normal Windows cursor |
| `[Debug] trace_steps`, `flicker_probe`, `dump_frames`, `trace_frames` | For analysis | Turn them off when done |

### Symptoms, causes and fixes

| Symptom | Cause | Fix |
| --- | --- | --- |
| The whole picture is flat (disparity 0) | The depth shader is not attached, or every draw is classified as UI/composite | Check `(gs)` and the `order group` lines in the trace. For a scissor game, `screen_batch = 0` |
| The whole picture has one disparity, as one block | The scene is drawn into a low-resolution mono cache, then pasted in one go | Promotion to a layer cache (automatic). Check the `fbo N ... layer cache` log line |
| Only the UI has depth, the game picture is flat | The scissor is set again for each buffer, so the draws are wrongly taken as a UI batch | The batch count goes up only within the same buffer (fixed) |
| The background comes out in front of nearer objects | The drawing order differs from the composite order on screen | Check the composite order (`layout`). If it is 0, the problem is in tracking the composite path (views, blits, copies) |
| Several background layers at one depth | Layers from the same atlas are grouped into one layer | `group = draw`, `order_curve = 0.5` |
| Ordinary sprites have no depth | A render target left on another unit makes the draw look like a composite, or an upload buffer gets promoted to a cache | Only the units the shader reads are considered, and only buffers drawn in that frame are promoted (fixed). Check `R` in `samples` |
| A diagonal line across the screen | The two triangles of a quad get different depths | Depth is assigned per quad (fixed) |
| Lines of text slant | Each letter is drawn separately, so each one comes one layer further forward | A scratch buffer that is cleared and written several times in a frame gets one layer each time it is used (fixed) |
| Panels or cards in the same row step through different depths | A repeated structure gets its depth in drawing order | Repeated groups of draws share a layer (fixed) |
| A character is oddly far back | The animation texture changes, so it is taken as a new layer | A new layer goes right after the previous layer (fixed). Compare `target` and `depth` |
| A character floats in front of the floor it stands on | The floor is drawn first, so it is placed farther | For adventures, `rule = feet`. This works for fixed-function games too: the floor is a slope; pieces joined top to bottom (legs, body, head) stand at foot height as one object; what is drawn over an earlier object is not farther than it; a screen-wide picture drawn after the sprites (inventory bar) and what is inside it are at screen depth. Trace lines `feet floor/object/part/repeat/overlay` (Machinarium). Unsolved for side-scrollers with the order rule |
| Figures inside a panel or picture are flat | The figures arrive already merged into one picture | If the figures are dark silhouettes, `silhouette` (shifts only the dark pixels per eye in that shader). Check `silhouette depth: ... rewritten` and `shift N px` in the log. Bright figures are unsolved (AI depth) |
| Depth steps are coarse | Shifts happen only in whole pixels of the low-resolution buffer | Unsolved (plan: draw the cache larger) |
| `renamed vertex shader N failed to compile` (`'__'` reserved-word error) | A prefix added to a shader output name that starts with an underscore produces `__` (LÖVE) | Names that start with an underscore get a different prefix (fixed) |
| The whole picture is flat and the log says `shaders injected 0` | The game draws with ARB assembly programs (Cg arbvp1) or fixed function, not GLSL | ARB is handled automatically (log `ARB vertex program N rewritten`). If `(arbvp)` shows in the draw trace, it is applied. For fixed function, with `fixed_function = 1` (default) the projection matrix of orthographic draws is shifted per layer (Machinarium) |
| The scene clumps into 2 or 3 layers and the `layout events` line shows `scratch targets: fbNxK` | The same buffer is cleared several times in a frame, so it is taken as a scratch buffer | Only clears that include color are counted (masks that clear only stencil are left out, Machinarium), and the final screen (root) is never taken as a scratch buffer (Rocketbirds clumped in frames where the HUD was visible) (fixed) |
| Part of the background is cut out as a rectangle and floats forward | A walk-behind, which draws part of the background picture again later, comes forward in drawing order | Fixed function: when the same texture as an earlier picture is drawn again with the same position mapping, it gets that picture's layer (trace `(repeats an earlier picture)`). Pieces that cover objects drawn in between stay in front (fixed, Machinarium). Not done yet for the shader path |
| Horizontal lines or rectangles where pieces meet (feet) | The picture is drawn in several pieces, so each narrow piece stands at its own bottom edge | `plane_tall` (e.g. 0.12): pieces at least this tall are on the floor slope, whatever their width (Samorost 2). Fixed-function games group the pieces automatically |
| When the scene is pulled forward (larger `depth_screen`), objects near the bottom sink behind the floor | Objects stop at `sprite_min` while the floor keeps getting nearer | `sprite_min` follows `depth_screen` (fixed) |
| Part of the scene is missing in one eye and the log says `right-eye copy incomplete (0x8cd7)` | The texture to attach to the right-eye copy of the buffer could not be made. WineD3D creates multisample textures with immutable storage and attaches texture views of them; if the copy does not have immutable storage, the view cannot be made (Snufkin) | Fixed in the wrapper (`glTexStorage2DMultisample` is tracked, and the source of a view is copied with immutable storage). If the cause is different, check the attachments in the trace by the `fbo N` number |
| The whole scene sticks as one sheet at the far end (near the maximum separation) | Not flat 2D but a real 3D game with a perspective camera. With flat depth on, automatic Convergence is off and stays at its start value (3) | `[Flat] enable = 0` (automatic Convergence fits the scene distance; about 65 for Snufkin) |
| Only the right eye has different colors (bright red etc.) | The color grading table (a 1024×32 strip in Unity's post-processing) was taken as a layer cache, made stereo and shifted away in the right eye (Blade Assault) | Fixed in the wrapper: strip-shaped buffers (one side more than 4 times the other) are not layer caches |
| Both eyes are the same (only the UI has depth, or nothing has), Unity 2D | ① Lit sprites read the screen's light texture (a stereo buffer), so they are classified as a composite pass ② WineD3D sets a viewport for each light pass, so the scene is classified as a later batch (UI) | ① Fixed in the wrapper: a draw that also draws its own picture (64px or more, not a strip) is treated as a lit sprite and gets depth (trace `lit sprite`, `lit redraw`); a triangle that covers the whole screen is at the screen plane ② `screen_batch = 0` (the installer applies it to Unity by default) |
| In game there are only a few draws, and none into the scene buffer | The engine composes the scene into one picture on the CPU and uploads it (Blade Runner: Enhanced Edition) | There are no layers, so this method cannot work. Another method such as AI 2D→3D is needed |
| With SBS/top-and-bottom output the screen stays one picture, and changing the separation does nothing (the dump is fine) | The game window is multisampled (log `virtual back buffer off: ... multisampled`), so GL refuses the blit to the window | Fixed in the wrapper: in a multisampled window both eyes are drawn in as pictures (log `output drawn into the window`). Check with a screen capture, not a dump |
| With upscale on, the game still renders at 4K (the text stays small) | The game turns on high-DPI awareness by itself at run time and has no resolution option (Astral Ascent: SDL) | Use the game's scale option instead of upscale. `upscale = 0` in the profile |
| A Direct3D (WineD3D) game with upscale on shows "Not responding" and clicks do nothing | The display window was created on WineD3D's rendering thread, so nothing handles its messages, and `HTTRANSPARENT` cannot pass the mouse to the game window on another thread | The display window is created on the game window's thread (log `window made there`) (fixed, Samorost 2) |
| The exe uses Direct3D 12/9 but has a separate OpenGL mode | Steam does not pass the launch switch (`-ogl` etc.) | `[Inject] add_args = -ogl`: appended before the game reads its command line. Check the log for `command line: "-ogl" appended` (Machinarium) |
| ARB programs, and every draw is at screen depth | Not knowing which units the program reads, a leftover render target makes draws look like composites; or a viewport set for each pass makes them a UI batch | Units are identified from `texture[n]` in the fragment program (fixed); `screen_batch = 0` |
| Horizontal lines (tearing) in the woven picture | The game turns vsync off | `swap_interval = 1` |
| A game function turns on together with a hotkey | The game ignores Ctrl and sees the F key | `swallow = 1` (default) |
| In SBS or top-and-bottom the mouse cursor is invisible or does not line up with either eye's picture | The game uses the Windows cursor: Windows draws only one cursor, at the real mouse position (in half SBS the game's x is at screen x/2 and W/2 + x/2) | The wrapper draws the cursor the game chose at the same place in both eyes (screen depth), and replaces the Windows cursor over the game window with a transparent cursor (`[Overlay] cursor = 1`, default). If the game hides the cursor or uses a transparent cursor, nothing is drawn. Log `cursor: shape ...`. Screen captures show only the drawn cursor (fixed, Intravenous 2: Mercenarism, DK, Astral Ascent) |

### When changing code

- The wrapper code is in the `ogl3d/` submodule (OGL3D-SR, branch `flato3d`).
- Make fixes as general rules; do not branch on game names.
- Build with `ogl3d\build.ps1`. After building, deploy not only to the game you are working on but to **every game already worked on**, and run a regression check on at least DK and Huntdown. A fix for one game has broken other games before.

## CP5 Play test

- Actually play through several scenes. Use `hold` to move, and `key` or `lclick` to jump and attack. Keep a dump for each scene.
- `flicker_probe = 1`: catches frames where only one of the two eyes flickers and saves them as images. If it appears the same in both eyes, it is a game effect.
- Check the fps from the time between `frame N:` lines. 600 frames in 10 seconds is 60fps.
- When done, restore the save from the backup, turn off the analysis settings and delete the dump files.

## CP6 User check

- When the user watches remotely, set `output = sbs`. Ctrl+Alt+W turns weaving on and off while the game runs.
- For checks with automated runs (gametest), also switch to `output = sbs` first, because the user watches remotely (the installer default `auto` is SR weaving).
- Do not start automated runs (gametest) while the user is using the PC, because they bring windows to the front and send keys.
- Do not overwrite values the user set (saved with Ctrl+F7). If they have to change, tell the user why, then change them.

## CP7 Record and release

1. **Add a profile to `data/game_profiles.json`.**
   - Fields: `id`, `name`, `steam_appid`, `folder`, `exe`, `arch`, `engine`, `api`, `route`, `install`, `remove`, `registry`, `ini`, `status` (working / verified), `verified_by_user`, `notes`, `known_issues`, `test`
   - In `ini`, write only the values that differ from the template.
   - In `test.steps`, write the gametest steps.
2. **Add a table row and a note to both `data/GAMES.ko.md` and `data/GAMES.md` (the two lists are kept in sync by hand).**
3. **Commit order**
   1. Commit in `ogl3d/` → `git push origin flato3d`
   2. In the Flato3D repository, commit the updated submodule and data → `git push`
   - Write commit messages in English, saying what was changed and why.
4. **Release:** `installer\src\build.cmd` → `package.ps1` → `dist\Flato3D-SR-<version>.zip` (the GitHub release only after the user confirms).

## Handling status reports ("상태보고 반영해줘", "이상로그 수정해줘")

The installer's **Report status** button collects the game's result (`works` works / `issues` some problems / `broken` does not work) and the evidence in one folder. The result is also kept in that game's `Flato3D_install.json` (`user_status`) and shows right away in the **Status** column of the game list, like "works (my check 10-18)" (green/orange/red). The profile's official status ("verified") changes only after the profile is updated from the report, starting with the pre-release installer.

- **Where reports are saved**
  - Developer PC (if the pre-release folder exists in OneDrive): `D:\OneDrive\AI\ClaudeCode\Flato3D_github_release\reports\<date_time>_<result>_<game>\`. Older folders without a result are problem reports.
  - Elsewhere: `%LOCALAPPDATA%\Flato3D\reports\`
  - **GitHub status list:** With "Also post to the GitHub status list" (on by default), an issue is created (label `report`, plus `bug` for problem reports). If the GitHub CLI (`gh`) is logged in (developer PC), the installer posts it directly; otherwise the form (`status_report.yml`) opens already filled in, and a zip is made. Screenshots and log files stay only in the report folder.
  - Each time an issue is opened, changed or closed, a workflow (`.github/workflows/status-list.yml`, `.github/scripts/status_list.py`) rebuilds and commits `STATUS.md`, `STATUS.ko.md` and `data/status.json`. Games with a profile are listed even without reports. Only each person's latest report counts, and closed (completed) problem reports drop out as handled. Issues with an invalid/duplicate/spam/wontfix label, or closed as "not planned", drop out of the list. Pushing a profile change also rebuilds it.
- **What goes in**
  - `report.txt`: the result, the note/symptoms, game, exe, route, profile and candidate information, the pre-selection and its reasons, the main `OGL3D.ini` values, installer, Windows, SR runtime and GPU, the loader state, the list of DLL/ini/log files next to the exe, and the odd lines, start and end of `OGL3D.log`
  - Files: `OGL3D.log` (last 8MB), `OGL3D.ini`, `Flato3D_install.json`, `Flato3D_loader_*.log`, the last Ctrl+F12 dump of that day
  - Screenshots `screenshot_NN.*`: the ones the user ticked. They are added as files or pasted with Ctrl+V (Win+Shift+S capture). This game's Steam screenshots and Windows/Game Bar captures from the last 2 hours are listed in advance, unticked.
  - User names in paths are masked as `<user>`.
- **Command line:** `Flato3DInstaller.exe --report "<exe>" --result works|issues|broken --symptom "<note>" [--image <file> ...] [--github]`

When the user says "상태보고 반영해줘" (apply the status reports) or "이상로그 수정해줘" (fix the problem logs), do the following in this order.

1. Under `reports\`, go through the folders that have no `handled.txt`, oldest first. See public reports with `gh issue list -R nautymac/Flato3D-SR --label report --state open` (my own reports are posted there too, as the same issues).
   - **`works`:** Set the profile's `status` to `verified`, and write the date and the output in `verified_by_user`. Copy those settings (the main `OGL3D.ini` values) into the profile's `ini`, and update the `status` in `GAMES.ko.md` and `GAMES.md` and in the candidate list. If the game has no profile, create one. Then go to steps 4 to 6.
   - **`issues` / `broken`:** Do steps 2 to 6.
2. In `report.txt`, read the symptoms, the route, the settings and the odd log lines, and look at the screenshots. If there is a dump, look at it with `eyes.py` and `dispmap.py`.
3. Go back to the checkpoint that fits the symptom, find the cause and fix it. See CP1 for first-run problems, CP3/CP4 for depth problems, and the symptom table.
   - If you need to reproduce it, run it automatically with the profile's `test` steps. If the user is using the PC, wait.
4. Record it in the profile, in `GAMES.ko.md` and `GAMES.md`, and in the symptom table, then commit and push. Then refresh the pre-release package with `prerelease.ps1 -Wait`.
5. Leave a `handled.txt` in the report folder with the date, cause, fix and commit. Reply to the GitHub issue with the same content and **close it as completed** (`gh issue close N --reason completed`). The status list then shows that problem as handled. Close `works` reports too once they are handled (closed ones still count for the player status).
6. Tell the user what was fixed and what to check again.

---

## Pitfalls

- **File encoding:** Do not rewrite sources with PowerShell 5.1's `Get-Content`/`Set-Content`. UTF-8 Korean text gets mangled into cp949. Use the Edit tool or Python (`newline=''`).
- **Line endings:** Files in the working folder are CRLF (no BOM). `core.autocrlf=true`.
- **Backslashes:** Backslashes such as `\a` or `\s` inside Python strings turn into other characters. After putting paths into JSON, check that there are no control characters.
- **Locked DLLs:** While the game is running, its DLLs are locked and copying fails. Check the processes before deploying.
- **Steam double launch:** If the game is already running, Steam shows an "already running" window. Check before an automated run.
- **Frame 3 hang:** Happens now and then in automated runs. Run it once more (use a retry loop).
- **Dump size:** A 4K dump is 16MB per image. Delete them when done.
- **Saves:** Back up before a play test and restore when done. Steam Cloud uploads the local changes, so it did not need separate handling.
- **Defender false positive (since 2026-10-18):** The x64 loader `version.dll` is detected as `Trojan:Win32/Cloxer` and quarantined (it is a cloud verdict, so a rebuild is soon caught again; x86 is not affected yet). This PC has exclusions for the loader paths (`loader\bin`, `dist`, the pre-release `loader` and zip, and the `version.dll` of the WineD3D games). When you install to a new game, its `version.dll` must also be excluded with the installer's **Defender exclusion**. Symptoms: the installer shows "version.dll missing", and the game is flat.
- **GPU:** Only NVIDIA is supported. On Intel GPUs shaders have broken. On hybrid laptops the installer writes `GpuPreference=2;` to `HKCU\Software\Microsoft\DirectX\UserGpuPreferences`.
- **Values change during measurement:** If `separation N%` in the dump log differs from the ini value, someone pressed Ctrl+F3/F4 (the `separation ... flat screen depth` line). Convert the measurements to that separation.
- **Weaving stays on the desktop:** If a game that turned the SR lens on exits without turning it off, the lens stays on. Turning the lens off from a new program or restarting SRSession does not clear it; `Restart-Service 'SR Service'` (as administrator) does.
- **Known unsolved:** characters floating in front of the floor, coarse depth steps in low-resolution caches, bright figures inside merged pictures, the frame 3 hang.

## Where the tools are

| Tool | Location |
| --- | --- |
| Installer (GUI and command line) | `installer\` (`build.cmd` → `installer\bin\Flato3DInstaller.exe`) |
| Automated runs | `tools\analysis\gametest.ps1` |
| Disparity map, area disparity, anaglyph | `tools\analysis\dispmap.py`, `maskdisp.py`, `eyes.py` |
| Per-draw image sheet, cache zoom | `tools\analysis\sheet.py`, `cachecrop.py` |
| Memory string search, export names | `tools\analysis\memstr.py`, `peexp.py` |
| Shader compile time | `tools\analysis\gstime\` |
| Hang analysis (stacks of all threads) | `tools\stackdump\` |
| WineD3D renaming | `tools\wined3d_rename.py` |
| Rebuilding the candidate list | `tools\library_survey\` (manual research results in `api_research.json`) |
