# Flato3D worked games

**English** · [한국어](GAMES.ko.md)

*The two lists are kept in sync by hand; when they differ, GAMES.ko.md is current.*

The source of the profiles the installer uses is `game_profiles.json`. The 544 candidates not worked on yet are in `game_candidates.json`.

| Game | API · route | Depth rule | Status | Needs |
| --- | --- | --- | --- | --- |
| Technobabylon | OpenGL (AGS) · direct | feet (foot position) | Confirmed by the user | opengl32.dll x86 |
| Huntdown | D3D11 (Unity) · WineD3D | order (drawing order) | Works, confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64, forced vsync |
| Door Kickers: Action Squad | D3D9 (DXUT) · WineD3D | order + layer cache | Works, confirmed by the user | WineD3D x86, loader, opengl32.dll x86, csmt=0 registry key |
| FRAMED Collection | OpenGL 3.2 · direct | order (repeated panels share a layer) + silhouette | Confirmed by the user | opengl32.dll x86 (bin folder) |
| Astral Ascent | D3D11 (Chowdren) · WineD3D | order + scratch targets | Confirmed by the user (NVIDIA) | renamed WineD3D x64, loader (with static import support), opengl32.dll x64, geo-11 DLLs removed |
| Intravenous 2 | OpenGL (LÖVE 11.5) · direct | order (top-down) | Confirmed by the user | opengl32.dll x64 |
| Intravenous 2: Mercenarism | OpenGL (LÖVE) · direct | order (top-down) | Confirmed by the user (3D) | opengl32.dll x64 |
| Guns, Gore and Cannoli 2 | D3D11 (Unity 5.6) · WineD3D | order, one layer per draw | Confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64, forced vsync, Defender exclusion |
| Rocketbirds: Hardboiled Chicken | OpenGL (PSSG, Cg ARB programs) · direct | order (per draw) | Works, not yet confirmed by the user | opengl32.dll x86, `screen_batch = 0` |
| Machinarium | OpenGL fixed function · direct (`-ogl` switch) | feet (foot position, fixed function) | Confirmed by the user | opengl32.dll x64, `[Inject] add_args = -ogl` |
| Samorost 2 | D3D11 (Adobe AIR) · WineD3D | feet, `plane_tall = 0.12` | Confirmed by the user | renamed WineD3D x86, loader, opengl32.dll x86, upscale |
| Snufkin: Melody of Moominvalley | D3D11 (Unity URP) · WineD3D | flat depth off (real 3D, auto convergence) | Works, not yet confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64, `[Flat] enable = 0` |
| Blade Assault | D3D11 (Unity 2D lights) · WineD3D | order, one layer per draw, `screen_batch = 0` | Works, not yet confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64 |
| The Messenger | D3D11 (Unity 2017.4) · WineD3D | order, one layer per draw, `screen_batch = 0` | Works (hang fixed), not yet confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64 (wrapper from 10-24 on) |
| Braid, Anniversary Edition | D3D11 (in-house engine) · WineD3D | order (template default) | Confirmed by the user | renamed WineD3D x64, loader + shim `winmm.dll`, opengl32.dll x64 |
| Home | D3D11 (GameMaker Studio 2) · WineD3D | feet, `plane_tall = 1` | Works, not yet confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64 |
| Axiom Verge | OpenGL (FNA, Steam launch option `/gldevice:OpenGL`) · direct | order, `order_curve = 0.4`, `snap_pixels = 1` | Works, not yet confirmed by the user | opengl32.dll x86, upscale, launch option |
| Axiom Verge 2 | OpenGL (FNA, Steam launch option `/gldevice:OpenGL`) · direct | order, `snap_pixels = 1` | Works, not yet confirmed by the user | opengl32.dll x86, upscale, launch option |
| Blasphemous | D3D11 (Unity 2017) · WineD3D | order, one layer per draw, `screen_batch = 0`, `order_weight = 0.02`, `order_curve = 1` | Works, confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64 |
| Blasphemous 2 | D3D11 (Unity 2021) · WineD3D | order, one layer per draw, `screen_batch = 0`, `order_weight = 0.02`, `order_curve = 1`, `snap_pixels = 1` | Works, confirmed by the user | renamed WineD3D x64, loader, opengl32.dll x64 |
| SteamWorld Dig | OpenGL (SDL2, own engine) · direct | order | Working, not yet checked by the user | opengl32.dll x86 |
| SteamWorld Dig 2 | OpenGL (SDL2, own engine) · direct | flat depth off (perspective camera, auto convergence) | Working, not yet checked by the user | opengl32.dll x86, `[Flat] enable = 0` |
| SteamWorld Heist | OpenGL (SDL2, own engine) · direct | flat depth off (perspective camera, auto convergence) | Working, not yet checked by the user | opengl32.dll x86, `[Flat] enable = 0` |
| Mark of the Ninja | D3D9 (Klei own engine) · WineD3D | order | Confirmed by the user | WineD3D x86, loader, shim winmm.dll, opengl32.dll x86 (`bin`) |

## Notes per game

**Technobabylon**: In AGS's `acsetup.cfg`, set the driver to OGL. Characters stand at the depth of their feet, and the room moves farther away towards the top of the screen. The GUI stays at the screen plane.

**Huntdown**: WineD3D's CSMT must stay on. The settings are:

- `[Flat] group = draw`: Unity draws each parallax layer separately from the same atlas, so each draw is treated as one layer.
- `order_curve = 0.5`: gives the background layers a wider depth range.
- `[Display] swap_interval = 1`: turns on the vsync that the game turns off.

The diagonal line that showed for a while had nothing to do with vsync. It came from the two triangles of a quad being shifted separately, and was fixed in the wrapper.

- To do: measured on cutscenes only. Gameplay needs to be confirmed by the user.

**Door Kickers: Action Squad**: The whole level is first drawn into a 1024×1024 cache (an area of about 416×234), so this cache is promoted to stereo. `cache_scale = 0.406` is used. The game toggles its fps statistics with F5; the wrapper's hotkeys are kept from reaching the game.

- To do: the cache resolution makes the depth steps coarse. The plan is to have the cache drawn larger.
- To do: now and then the game stops for about 100 seconds right after starting (frame 3). Seen in automated launches of both DK and Huntdown, mostly on the first launch after replacing the DLLs. Shader compilation is not the cause (43 ms even with the cache cleared). The cause is still unknown.

**FRAMED Collection**: The game sets glScissor for every draw, so `screen_batch = 0` is set. Otherwise the whole screen is treated as UI. The comic panels are built from repeated (frame, picture) pairs, so they are grouped into the same layer and the panels sit side by side on one plane.

- Measured (first puzzle of FRAMED, separation 25): blinds +56px, room +36~42px, panels −2px, arrows −28px.
- The panel pictures are videos, so the figures arrive merged into the picture. `[Flat] silhouette = c05a6bab` rewrites the panel picture shader so that only dark pixels (the black silhouette figures) are shifted per eye and placed in front of the panel picture (`silhouette_depth = 0.6`). On the first puzzle the figures were in front of the panel picture by exactly the configured amount, and the user watched it remotely and was satisfied.
- The figures come out at a single depth, like flat cutouts. White parts inside a figure (glasses, flowers) stay at the picture's depth, and where a figure has moved aside, a little of the neighbouring background is repeated.
- To do: FRAMED 2 has not been tried yet. The picture inside a panel sits slightly in front of its own frame.

**Astral Ascent**: The exe imports d3d11.dll statically, so the loader changes the exe's import entry to the WineD3D copy (w3d11.dll). If geo-11 is installed, d3d11.dll, dxgi.dll and nvapi64.dll must be removed.

- Text is drawn glyph by glyph into a scratch target and composited. A buffer that is cleared and written several times in one frame is grouped into one layer each time it is used, so lines of dialogue are flat.
- Animated sprites change texture from frame to frame. A layer that is not in the previous order list takes the place right after the layer drawn just before it.
- Measured (first area, separation 25): sky +28~54px, islands +30px, swords and ruins +20~26px, pillars and ground +8px, character −6px, HUD −14~−18px.
- Went through the hub areas (ruins, altar, garden, cliff) moving, jumping and attacking. 60fps with no hangs or errors. One flicker was caught, but it was a game effect that showed identically in both eyes.
- Garden: sky +40~64px, ground +28px, trees +20px, characters 0~−6px, arch in front −12px, HUD −20px. Cliff: far islands +40~64px, rock island +28px, cliff in front −18px.
- To do: the character seems to float in front of the ground it stands on (about 30px in the garden). This is because the game draws the ground first. In dialogue scenes the portrait, the dialogue box and the text are one UI layer.
- User check (10-18): works well on NVIDIA GPUs. On a PC with an Intel GPU, the background sky came out different in the two eyes (reported 10-17). Recorded with NVIDIA as the only supported GPU.

**Intravenous 2**: The first game installed with the installer. It was installed with the candidate list's defaults (top-down, order rule), and the user confirmed it in play. A problem where conversion failed because of LÖVE shader output names starting with an underscore was fixed in the wrapper.

- To do: per-layer disparity has not been measured with dumps.

**Intravenous 2: Mercenarism**: The candidate list had it as Direct3D 9 (PCGamingWiki), so the installer installed it on the WineD3D route, but being LÖVE it draws with OpenGL and only `opengl32.dll` is used. The profile was made for the OpenGL route, and the candidate list was corrected as well. The user confirmed the 3D in SBS (10-19).

- Menus and the pause screen use the Windows arrow cursor. In SBS, Windows draws a single cursor at the real mouse position, so neither eye's picture had a cursor. The wrapper was fixed to draw the cursor the game chose at the same position in both eyes (at screen depth) and to hide the Windows cursor over the game window (`[Overlay] cursor = 1`, the default). During missions the game draws its own crosshair and uses a transparent cursor, so the wrapper draws nothing extra.
- With the same fix, screen captures confirmed that the menu cursor also shows in both eyes in Door Kickers: Action Squad (WineD3D x86) and Astral Ascent (WineD3D x64, where the render thread is not the window thread).
- To do: the user has not yet confirmed that the cursor shows in both eyes. Per-layer disparity has not been measured with dumps.

**Guns, Gore and Cannoli 2**: Unity 5.6 (the player is built into the exe). Because the exe imports opengl32.dll, the installer first chose the OpenGL route, but Unity draws with Direct3D 11, so the game was flat (`Direct3D 11.0` in `ggc2_Data\output_log.txt`). The installer now recognises Unity first and picks the route from the API in Unity's log. The game was reinstalled with the same WineD3D route and Unity settings as Huntdown (`group = draw`, `order_curve = 0.5`), and the user confirmed it in play.

- Windows Defender wrongly flags the x64 loader (`version.dll`) as `Trojan:Win32/Cloxer`. On this PC it was added as an exclusion.
- To do: per-layer disparity has not been measured with dumps. Saves are in `AppData\LocalLow\Crazy Monkey Studios\Guns Gore and Cannoli 2`.

**Rocketbirds: Hardboiled Chicken**: The PSSG engine compiles its shaders with NVIDIA Cg into ARB assembly programs (arbvp1/arbfp1). There is no GLSL at all, so it was flat at first (log `shaders injected 0`). The wrapper now rewrites ARB vertex programs to shift the position per eye, and works out which texture units ARB fragment programs read. Converting Cg to GLSL (`cg_glsl = 1`) is not used, because it turns the screen white. `screen_batch = 0` is required.

- Measured: on the chapter 1 title screen, the tree silhouettes in front are near, and the jungle and the poster wall are behind. The first gameplay screen (unloading area) is −28~+44px, median +24px.
- The scene is drawn to the screen, then copied, run through bloom and drawn again, so the screen is cleared twice in one frame. In frames where the HUD was visible, the screen was taken for a scratch target and the scene collapsed into one layer (fixed by leaving the final screen out of the scratch targets). After the fix, unloading area (separation 25): far forest +70, bushes +56, leaves +28, crates +16, character +8px.
- To do: depth per draw only (ARB programs have no geometry shader, so the sprites inside one draw cannot be split apart). Needs the user's confirmation. Saves are in `Save\` in the game folder.

**Machinarium**: The 2020 edition (64-bit) defaults to Direct3D 12 and draws with OpenGL only when started with `-ogl`. Steam does not pass this switch, so with `[Inject] add_args = -ogl` the wrapper appends it to the command line (it changes GetCommandLine before the game reads it). In Steam's launch choice window, pick the first entry (Play Machinarium). The other entries (Play with DirectX 9 / 12) have not been checked.

- Its OpenGL is fixed function with no shaders. Orthographic sprites are drawn into one 3840×2160 buffer and copied to the window. The wrapper offsets the projection matrix per draw, giving each texture group a layer (18~23 layers in the first scene).
- When drawing masked pieces it clears only the stencil (glClear(GL_STENCIL_BUFFER_BIT)), and counting that as the start of a scene collapsed the scene into 2~3 layers. Only colour clears count (fixed).
- Measured (scrap heap in the first scene, separation 25): far scrap heap +70, middle +64, props +10~+26, scrap in front −6~−20, inventory bar at the top −16px. It renders at 4K, so upscale is off.
- Scenes are drawn like Flash, with bitmaps filling shapes, and parts of the background picture are drawn again later to hide the character (walk-behinds). User report (10-18, tower scene): the walk-behind at the bottom of the tower floated in front of the background (+4px, background +68px) and looked cut out as a rectangle. Now a draw that redraws the same texture as a picture drawn before, with the same position mapping, joins that picture's layer (the whole tower +68px). Pieces that cover objects drawn in between (the ground edge that hides the robot's feet), however, are kept in front so that the occlusion order stays right.
- Under the drawing order rule, characters stood well in front of the ground they stand on (robot +12~20px, background +68px). So a feet rule was added to the fixed-function path and Machinarium was switched to feet.
  - The scene picture is a floor slope (top +60 ~ bottom -26px, separation 25), and the walk-behinds follow that slope.
  - The robot's shadow, legs, body and head (two textures) are grouped as one object and stand at foot height (-2px, the same as the ground it stands on).
  - The thought bubble drawn over the robot is no farther than the robot (-10px), and the inventory bar and icons are at screen depth.
  - Limits: tall background structures (the tower) lean back along the floor slope. Large foreground pieces that reach the bottom of the screen are at the nearest depth (-24px) all the way to their top.
- Automated tests click "Continue game" in the menu (game coordinates 1000,705). "Play new game" (1120,472) overwrites progress, so it is not clicked once the user has played.
- The user played with the feet rule and left the status report "works" (10-18, SR output, separation 38.33 saved).
- To do: only the first scenes were measured with dumps. Saves are in the Steam Cloud (`userdata\<account>\40700\remote\Machinarium.dat`).

**Rocketbirds: Hardboiled Chicken** (added 10-21): The game window is 4× multisampled, so with SBS/top-and-bottom output the two eye pictures never reached the screen and the game's original picture showed as it was. Changing the separation made no difference (user report). Ctrl+F12 dumps read the eye buffers, so they looked normal. The wrapper was fixed to draw the two eyes into a multisampled window as images. SR weaving was never affected. Measured (on screen, separation 25): menu background +30~35px, text −6px.

**Samorost 2**: An Adobe AIR game (x86), so its Direct3D 11 is drawn through WineD3D. It does not support 4K, so the installer turns on upscale.

- With upscale, "Not responding" came up and clicks did nothing: the presentation window was created on WineD3D's render thread, so nothing processed its messages. The wrapper was fixed to create the presentation window on the game window's thread.
- Scene pictures are drawn split into pieces (one tentacle is an upper and a lower piece, the spaceship is several pieces). Under the feet rule each narrow piece stood at its own bottom edge, so horizontal lines and rectangles showed where the pieces meet (user report). New key `plane_tall = 0.12`: a piece at least 1/16 of the screen height follows the floor slope whatever its width. The wide, low strip of ground at the very bottom falls under this too. The order rule was also tried, but it was worse because the pieces ended up in different layers.
- When the user saved `depth_screen 0.75` (scene moved forward), the floor came nearer than `sprite_min`, so the lower pieces stepped back and a horizontal line appeared. `sprite_min` now moves with `depth_screen` (the same as before at `depth_screen 0`).
- Measured (tentacle scene, separation 32.9, depth_screen 0.75): one unbroken slope from +30 at the top to -90px at the bottom.
- To do: some objects drawn through filter buffers (the seed heads on the tentacles) float in front of their stems (+2px, stems +30~40px). Large objects lean back along the floor slope. Saves are in `%APPDATA%\amanitadesign.samorost3\Local Store`.

**Snufkin: Melody of Moominvalley**: Not flat 2D but a real 3D scene seen through a perspective camera. So flat depth is turned off with `[Flat] enable = 0`, and auto convergence is used. By the principle (decided 10-18) this is an ordinary stereo game and belongs to Geo Gaming Hub (geo-11, on the game's own D3D11). The user installed it with Flato3D, so the profile is kept.

- With flat depth on, auto convergence is off and convergence stays at the start value 3. The scene lies at a distance of 60~85, so everything stuck to the far limit (+46~48px, max 48) and was as flat as a single picture. With flat depth off, convergence settles at about 65.
- Measured (title): sky +39px, hills +10~31px, Snufkin and the foreground −9~+3px, menu at the screen plane. In game (first meadow): the ground slopes from +11px at the top to +1px at the bottom, trees in front −8px, speech bubbles at the screen plane.
- The right eye missing the landscape (log: `fbo 14: right-eye copy incomplete (0x8cd7)`): WineD3D creates multisample textures with `glTexStorage2DMultisample` (immutable storage) and attaches texture views of them. The wrapper made the right copy with mutable storage, so no views could be made of it. Fixed in the wrapper (10-20).
- To do: checked with automated dumps only (title, first meadow). Saves are in `%USERPROFILE%\AppData\LocalLow\Hyper Games\Snufkin - Melody of Moominvalley`.

**Blade Assault**: A pixel action game that uses Unity's 2D lights and post-processing.

- Colour: the post-processing draws a colour grading table (a 1024×32 strip) every frame. The wrapper took it for a layer cache, made it stereo and shifted it sideways in the right eye, so the right eye of the title came out bright red. The wrapper was fixed so that strip-shaped buffers (one side more than 4 times the other) are not treated as layer caches.
- Depth: the 2D lights redraw each sprite while reading the screen's light texture (a stereo buffer). Such draws were treated as composite passes and put at the screen plane, so the whole scene was flat (both eyes identical). A draw that reads a stereo buffer and also draws its own picture (an ordinary texture of 64px or more, not a strip) is now treated as a lit sprite and gets depth. If it redraws a layer drawn earlier, it follows that layer's depth. Triangles covering the whole screen (post-processing) still stay at the screen plane.
- `screen_batch = 0`: WineD3D sets the viewport and scissor again between every light pass, so the scene is drawn in batches 5~29. The AGS rule (later batches are UI) put the whole scene at the screen plane. The installer now uses `screen_batch = 0` by default for Unity games.
- Measured (first corridor, during the opening dialogue): corridor +12px, structures in front of it +6px, the dialogue box in front of the scene.
- To do: measured during the opening dialogue only. The depth in play needs to be confirmed by the user. Sometimes starting takes more than 1 minute (NEOWIZ logo). Saves are in `%USERPROFILE%\AppData\LocalLow\TeamSuneat\BladeAssault`.

**Blade Runner: Enhanced Edition** (no profile): The original (1997) engine seems to combine the scene into a single image on the CPU and send that. In game there are only 6 draws per frame, and the 1280×960 scene buffer gets no draws at all (user's run log, 10-19). With no layers, the current method cannot make it stereo. It needs a different approach, such as 2D→3D conversion (AI depth estimation). The menu does not take injected mouse or arrow-key input, so the automated check only went as far as the title.

**The Messenger** (profile `the-messenger`, 10-24): Unity 2017.4 x64, D3D11 through WineD3D. It uses the installer's Unity defaults as they are (order, one layer per draw, `screen_batch = 0`).

- **Hang fixed**: during play it stopped responding within tens of seconds to a few minutes (also without the wrapper and without SR). WineD3D wakes its drawing thread with thread alerts (`NtAlertThreadByThreadId`); a leftover alert wrongly wakes a Windows 11 24H2+ critical-section wait, and ntdll loops in `RtlpWaitOnCriticalSection` forever. The wrapper gives WineD3D no alert functions, so it wakes the thread with an event (log `WineD3D: command thread woken by an event`). Before: 11 hangs in 19 runs; after: 10 `sbs` runs and 5 SR runs (120 s each) with no hang. This applies to every WineD3D game using CSMT. The investigation is the case study in [tools/hang/README.md](../tools/hang/README.md).
- CSMT must stay on (`csmt=0` crashes at start, as in Huntdown). `-force-glcore` does not work (no OpenGL renderer in this build).
- Measured (separation 25, dumps 1920x1080 per eye)
  - Title: stars, moon and mountains +68 px, logo +24 px, menu -10..-14 px, credits -20..-28 px
  - First scene (Ninja Village, opening dialogue): sunset sky +52 px, middle trees and cliffs +38 px, top foliage +24 px, ledge +4..6 px, the ninja -6 px, dialogue box -26..-28 px
- To do: the ninja stands about 10 px in front of the ledge under it. The depth in play is to be confirmed by the user.
- Test input: Enter skips the intro (about 60 s) and "press any button"; the menu and the save slots take **Space** (Enter is ignored). On the save slot screen holding left Ctrl deletes the save, so never press Ctrl+F12 there. Saves: `%USERPROFILE%\AppData\LocalLow\Sabotage Studio\The Messenger\SaveGame.txt` (test-backups\TheMessenger; JSON XORed with 0x81).

**Braid, Anniversary Edition** (profile `braid-anniversary-edition`, 10-25): in-house engine, D3D11 (feature level 11.1) x64, painted parallax layers.

- **Loader shim**: neither the exe nor any DLL it imports imports `version.dll`, so the Flato3D loader never loaded. The game ran flat on the system Direct3D 11 and left no logs. The installer now adds a shim `winmm.dll` (`loader/shim`) that forwards every export to the system winmm.dll and loads the loader. With it the exe's static d3d11/dxgi imports go to WineD3D.
- Measured (separation 25, dumps 1920x1080 per eye)
  - Title: far buildings +60..68 px, middle buildings +46..48 px, bridge +42 px, the Braid logo +8..16 px, Tim -22 px
  - House hub: room walls and furniture +32 px, ladder +38 px, door +8 px, house outline +2..6 px, Tim about 0 px, the world sign -26 px
- Confirmed by the user (10-25, play).
- Test input: Enter does nothing on the title; holding the right arrow walks Tim into the game. Saves: `%APPDATA%\Braid, Anniversary Edition` (test-backups\Braid Anniversary Edition).

**Home** (profile `home`, 10-26): GameMaker Studio 2 x64, D3D11 through WineD3D. A side-view pixel horror adventure. The user installed it with the installer (feet rule, the candidate list's default), then it was checked.

- The candidate list had it as GameMaker: Studio 1, Direct3D 9, x86 (the 2017 build). The current Steam build (v1.7.1, 2026) is GameMaker Studio 2 x64, Direct3D 11 (data.win GEN8 version 2). The exe imports d3d11.dll statically and the loader points it at w3d11.dll. The installer chose the WineD3D D3D11 route from its own detection. The candidate list is corrected.
- The game itself runs at 30 fps (game speed 30 in data.win). `frame 600` every 20 s in `OGL3D.log` is normal.
- **The room came out in front of the character**: a room is one draw of tall pieces (wallpaper strips, wainscot, side walls) that reach below the bottom of the screen. Under the feet rule a narrow piece stands at its own bottom edge, below the screen, which made it nearer than anything else. The whole room came out at -14 px, in front of the character (-10 px) who stands in front of it. `plane_tall = 1` (half the screen height) makes those pieces follow the floor slope. The character (about 0.7 of the screen height) and the windows still stand at their bottom edge. Door frames are about at the threshold.
- The order rule was tried too. The wall comes out flat (+38 px) and the character at +10 px. But a dialogue card draws all its glyphs in one draw, and the order rule spreads one draw over its depth slot, so a line of text would slant by about 8 px. Kept feet.
- Measured (feet + `plane_tall = 1`, separation 25, dumps 1920x1080 per eye)
  - Title: logo +10 px, buttons -2..-6 px, version text -12 px
  - First room: wall +16 (top) .. 0 px (chair rail), windows +2 px (standing on the sill, flush with the wall at that height), wainscot -4..-8 px, the character -10 px, the flashlight darkness (a full-screen overlay) +34 (top) .. +4 px (bottom)
  - Second room: wall +26 .. 0 px, potted plant, door frame and character -10 px
  - No blocks differ between the eyes; the flicker probe found nothing while walking
- Still open: dialogue cards are wider than `plane_size`, so they lean back with the floor slope (+26 top .. -6 bottom px; the text lines sit on the card's surface at +20 and +12 px). Pieces taller than half the screen lean back instead of standing upright. The flashlight darkness is one sloping plane, so near the bottom of the screen it is a little behind what it covers (+4 px over -6..-10 px). Only the first two rooms were checked; it needs the user's check.
- Test input: arrows move (up looks up), Space continues cards and interacts. The door on the left of the first room leads to the second room. Saves: `%LOCALAPPDATA%\Home___A_Unique_Horror_Adventure\homesave` (test-backups\Home). The opening left no progress save.

**Axiom Verge, Axiom Verge 2** (profiles `axiom-verge`, `axiom-verge-2`, 10-28): FNA (a .NET reimplementation of XNA), 32-bit. AV2 ships an `x64` folder, but its exe is 32-bit only (`32BITREQUIRED`), so the `x86` libraries are used. The user installed both with the installer; at first the wrapper did not load in either.

- **Switching to OpenGL**: FNA3D picks Direct3D 11 on Windows. The Steam launch option `/gldevice:OpenGL` (FNA's own switch; AV1 also takes `-opengl`) makes it use OpenGL, and then the wrapper loads. The environment variable `FNA3D_FORCE_DRIVER=OpenGL` works too, but a game started outside Steam restarts through Steam (`SteamAPI_RestartAppIfNecessary`) and loses it. The wrapper's `add_args` cannot help: opengl32.dll is only loaded after FNA3D has chosen its driver. For a test only, `steam_appid.txt` next to the exe stops the restart.
- **The loader does not load**: the .NET runtime maps System32's `version.dll` before SDL2.dll imports it, so the Flato3D loader never loads. FNA draws with OpenGL itself, so opengl32.dll is all it needs. AV1 had been installed on the D3D9 route, which also put in `version.dll`, `wined3d.dll` and `d3d9.dll`; they are unused.
- **Black screen (wrapper fix)**: FNA's shaders are GLSL from MojoShader, and they write their outputs to the legacy built-ins through macros (`#define vs_oT0 gl_TexCoord[0]`). The flat-depth geometry shader looked for built-ins only outside preprocessor lines and passed none of them on, so the screen was black (with stereo off too). The bodies of the macros a shader uses now count.
- **The whole scene flat / in one layer (wrapper fix)**: FNA renders every render target through one framebuffer object and only swaps its colour attachment. The wrapper told targets apart by framebuffer number, so the viewport set for each target started a new scissor batch (`screen_batch = 1` put the whole scene at the screen: disparity 0), and the composition order took all targets for one scratch target (the scene collapsed into one layer). Batches and the composition order now count a framebuffer together with its colour attachment as the target.
- **Draw trace (wrapper fix)**: `glDrawRangeElementsBaseVertex` (FNA3D's draw) and `glDrawElementsInstancedBaseVertex` left no trace line and no triangle count (`prims 0`). They do now.
- AV1 draws at 480x270 and scales up, so depth comes in steps of 4 px (one game pixel on a 1920-wide eye). Its HUD and dialogue box are drawn into a target of their own, one layer per 8x8 texture, so with the even curve the 8 world layers bunched into the far fifth (+24..28 px). `order_curve = 0.4` spreads them. AV2 draws at 432x240 with 14 parallax background layers and works with the defaults.
- Once the wrapper made it DPI aware, AV1 opened a 1920x1080 window on the 4K desktop. Upscale (`upscale = 1`) fills the screen (presentation window 3840x2160). The installer had already chosen upscale for AV2.
- Measured (separation 25, px on a 1920-wide eye)
  - AV1 title (`order_curve = 1`): far tiles +32 px, near tiles +24 px, logo +8 px, menu -8 px
  - AV1 first room (`order_curve = 0.4`): Trace +16 px, the terrain walls drawn after him +8 px, dialogue box and portrait 0 px
  - AV2 Antarctica, the helicopter pad: sky and far mountains +26 px, middle mountains +18 px, helicopter and scaffold +8..10 px, Indra and the ice in front 0 px, HUD -4..-8 px
  - Both 60 fps, 2-4 blocks differ between the eyes
- **Tiles swayed while walking (user report, SR panel, 10-28)**: dumps every 4 frames while walking found two causes. (1) The game draws at 432x240 / 480x270, and a per-eye shift of a fraction of a pixel let the two eyes round a sprite at a fractional scroll position differently: a parallax layer's disparity flipped between two values (+16 and +20..24 px) as it scrolled. The new key `snap_pixels = 1` shifts by whole pixels of the target (and drops the spread within a group). (2) Objects scrolling in and out changed the layer count (30 <-> 31), and every layer's depth glided after it (0.517 <-> 0.533). The count now stays at its recent peak and follows at once only when it drops by more than two (wrapper, all order-rule games). After both, the layers kept their depth while walking.
- SR weaving checked (10-28, output auto, upscale): the 32-bit SR runtime's DX11 weaver starts, 60 fps, no eye-tracking losses in the SR log.
- Still open: the user has to set the Steam launch option. Terrain stands a little in front of the player (common to side-scrollers under the order rule). AV1's HUD pieces and dialogue-box tiles are separate layers and step a little in depth. It needs the user's check.
- Test input: AV1's menus take Enter; at the skip-cutscene prompt, Left selects Yes and Space confirms. In AV2, Enter opens the pause menu in play and Escape closes it. SDL games take the arrows only with the extended-key flag (gametest sends it since 10-28). Saves: Steam Cloud `userdata\<account>\332200`, `946030` (test-backups\AxiomVerge, AxiomVerge2).

**Blasphemous** (profile `blasphemous`, 10-29): Unity 2017 x64, D3D11 through WineD3D. The Unity defaults plus `order_weight = 0.02` and `order_curve = 1`.

- **First test on AMD**: run on the MiniPC (AMD Radeon RX 7600M XT, GL 4.6) with `sbs` output. 60 fps, about 4 minutes on the title and in play with no hang. NVIDIA and SR output are not tested yet.
- The game's resolution mode `PIXEL_PERFECT` draws 3072x1728 inside the 3840x2160 window, so each eye has black bars at the sides.
- Frame structure (Ctrl+F12 trace): the world is drawn into two 640x360 targets: one holds the background with the floor and platforms (followed by a lighting pass), the other the Penitent One and the foreground. Both are scaled to 3072x1728, then the HUD is drawn. Depth steps are coarse (one 640x360 pixel).
- Measured (separation 25, dumps 1920x1080 per eye)
  - Title: logo +24 px, "press any button" -20..-28 px, copyright lines from +20 down to +6 px (one draw spread over its glyphs)
  - Play (Brotherhood of the Silent Sorrow: the prie-dieu room and the room west of it): window light +60 px, walls and pillars +30..+40 px, rubble +10..+30 px, the Penitent One +4..+20 px, HUD portrait -20 px, HUD on the right -10 px
- **The Penitent One floating over the floor (fixed)**: at first he stood about 25 px in front of the cathedral floor, because each small sprite drawn between the floor and him took a whole step in depth. With `order_weight = 0.02` (each layer steps forward by the part of the screen it covers): cathedral floor +20 px, the Penitent One +10 px (one 640x360 pixel apart), walls +30..+40 px, window light +60 px, HUD 0..-10 px. Title: logo +4 px, its text -8..-28 px.
- Still open: the save slot menu stands +44 px behind the screen; two pictures over the whole screen drawn after the menu take most of the depth range.
- **Confirmed by the user** (10-29): works well.
- Test input: Enter three times on the title continues slot 1 (the save slot menu comes after the second). Movement is A / D, not the arrows. Just walking around changes `savegame_0.save`, so restore it after a test. Saves: `%USERPROFILE%\AppData\LocalLow\TheGameKitchen\Blasphemous\Savegames\<Steam ID>\` (test-backups\Blasphemous).

**Blasphemous 2** (profile `blasphemous-2`, 10-29): Unity 2021 x64 (IL2CPP), D3D11 through WineD3D. The Unity defaults plus `order_weight = 0.02` and `order_curve = 1`.
- **Pink right eye in the video cutscenes (user report, Helios3D, NVIDIA, 10-29)**: the cutscene video is converted from Y, U and V planes the game uploads from the CPU every frame (1920x1080 and 960x540, window-shaped, so they had right-eye copies). The copies were not kept up to date, and Y near 0 with U and V high came out pink. `trace_steps`, which now saves both eyes, showed the first pass reading the planes as the first to differ. A texture the game uploads from the CPU is now a picture: no right-eye copy, and the framebuffers it is attached to stay mono (wrapper). The video is flat (one picture, +68 px), subtitles -24 px.
- **Vertical hairlines in the backgrounds (user report, Helios3D, 10-30)**: 1-2 px vertical lines through the cloud layer and the background below the bridge, cut off by the sprites in front (the Penitent One, the bridge) - layer seams. The world is drawn at 640x360 and scaled up, so a per-eye shift of a fraction of a target pixel lands tile edges between texels (the neighbouring texel bleeds through) and rounds adjacent tiles differently (a gap), the same cause as Axiom Verge's sway. `snap_pixels = 1` rounds each eye's shift to whole pixels of the buffer. The depth steps are then one buffer pixel (12 px at 4K), so a lowered separation (the user's ini had 7.04) wants raising again. Applied to the user's ini on 10-30; confirmed by the user on the panel the same day.

**SteamWorld Dig** (profile `steamworld-dig`, 10-30): flat 2D, drawing order. In the mine: terrain +32 to +34, Rusty +20, dialogue, portraits and HUD 0 to +12, 60 fps. 42 layers while walking, depth moves less than one slot (0.024), so no sway.

**SteamWorld Dig 2** (profile `steamworld-dig-2`, 10-30): it looks flat, but the sprites sit at different z under a perspective camera (z -935). With flat depth on, the whole scene sticks at +96 as one sheet, so it uses `[Flat] enable = 0` and auto convergence. Ground -4, sign and car +6, desert +38, sky +64, UI 0, 60 fps. Convergence is about 950-990.

**SteamWorld Heist** (profile `steamworld-heist`, 10-30): built like Dig 2, flat depth off. Piper and the floor 0, walls +2 to +6, space +26, HUD 0. The ship is nearly one plane, so the depth inside it is small (the game's own layout).
- **Auto convergence swinging (10-30):** in the first mission the convergence went between about 2100 and 400 several times a second. At the right convergence the ship matched at offset 0 and counted as 2D, and the 15 stray edge blocks left gave a near depth of 312. The wrapper now leaves the convergence alone when most of the picture is on the screen plane. Stable at 2115 for 27 s after the fix.
- Saves of all three are Steam Cloud files (`userdata\<id>\<app>` holds only `remotecache.vdf`).

**Mark of the Ninja** (profile `mark-of-the-ninja`, 10-30): the user installed it with the installer defaults and confirmed it on the SR display. The log shows 60 fps, about 360 draws a frame and no shader failures. Depth was not measured with dumps.

- **Tested on AMD**: the MiniPC (AMD Radeon RX 7600M XT) with `sbs` output, game version 3.0.0. 60 fps, about 5 minutes on the title, the menus and in play with no hang, 82 shaders injected and none failed. NVIDIA and SR output are not tested yet.
- Unlike the first game it fills the whole 3840x2160 window (no black bars).
- Frame structure (Ctrl+F12 trace): the world is drawn into several 640x360 targets that are combined and scaled up, then the HUD is drawn. Each target counts its own drawing order. Depth steps are coarse (12 px at separation 25).
- Measured (separation 25, dumps 1920x1080 per eye)
  - Title: logo -12 px, "press any button" -26..-28 px, copyright lines -18..-22 px (the whole title stands in front of the screen)
  - Play (the prie-dieu of the save and the rooms on both sides): far rock pillars and statues +24..+36 px, floor and platforms +24..+36 px, the prie-dieu and cherubs +12 px, the Penitent One 0 px, fog in the foreground -24..-30 px, HUD -16..-18 px, tears counter -10..-14 px
- **The Penitent One floating over the floor (fixed)**: at first the floor got +24..+36 px and he 0 px, 24..36 px in front of it. Between the floor (draw 18) and him (draw 28) Unity draws nine small sprites (the prie-dieu, cherubs, lights), and each took a whole step in depth. With `order_weight = 0.02` the gap is 12 px (one 640x360 pixel): prie-dieu room floor +12, him 0, far rocks +24..+36 px; dark room floor 0, him -12 px; blood rain platform +24, him +12, vines +60 px. Title and menus 0..-28 px.
- **Confirmed by the user** (10-29): works well.
- Test input: on the title Enter (any button) -> Enter (Pilgrimage) -> Enter (continue slot 1). Movement is A / D, W interacts. The steps above left the saves unchanged, but back them up anyway. Saves: `%USERPROFILE%\AppData\LocalLow\The Game Kitchen\Blasphemous 2\SteamUser_<Steam ID>\` (Steam Cloud, test-backups\Blasphemous 2). The first launch through Steam took about 2 minutes before the game started.
