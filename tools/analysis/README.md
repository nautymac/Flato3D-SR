# Flato3D analysis tools

**English** · [한국어](README.ko.md)

The whole working procedure is in [docs/WORKFLOW.md](../../docs/WORKFLOW.md).

These tools are used when working on a new game, or when checking for regressions after a change. The Python scripts need Pillow and numpy.

## Steps for analysing a new game

1. In `OGL3D.ini` in the game folder, set `output = sbs`. Also turn on `[Debug] trace_steps = 1`.
2. Press Ctrl+F12 in a scene. This creates the following files.
   - `OGL3D_frameNNNNN.bmp`: the image of both eyes
   - the per-draw records in `OGL3D.log` (`T` lines)
   - `OGL3D_stepNNNNN_DDD.bmp`: the render target at the end of each draw
3. Analyse the dump with the tools below.
   - `dispmap.py`: a map of disparity (px) per block. Shows whether the layers really separated.
   - `sheet.py`: gathers the per-draw images into one sheet. Shows which draw draws what.
   - `eyes.py`: makes an anaglyph and a small SBS image.
4. Check the following in the log.
   - `layout` line: the list of layers in the order they stack up on the screen. If it lists 0, the composition path is broken.
   - `samples` line: the texture units a draw actually reads. `R` means there is a right-eye copy.
5. Changes to the ini take effect within 1 second, even in a running game. To dump without pressing a key, write upcoming frame numbers in `dump_frames` and `trace_frames`.

## Tools

| File | Purpose |
| --- | --- |
| `gametest.ps1` | Starts a Steam game and runs steps (wait, shot, click, key, hold, ctrlkey, lclick, rmove, etc.) while keeping its window in front. `-Attach` attaches to a game that is already running, and `-Exe` starts the exe without Steam. The steps for each game are in `test` in `data\game_profiles.json`. |
| `dispmap.py` | Disparity map of an SBS dump |
| `maskdisp.py` | Disparity of one region (a colour mask lets you measure just a small character) |
| `sheet.py` | Puts several images, with their names, into one |
| `eyes.py` | Anaglyph, small SBS, difference image |
| `cachecrop.py` | Crops and enlarges the area of the low-resolution cache (DK) |
| `memstr.py` | Searches for strings in a running game's memory (read only). For checking the debug strings of an encrypted exe |
| `peexp.py` | Turns a DLL offset into the nearest export name (stack analysis) |
| `gstime\` | Measures shader compile and link times (bypassing the driver cache) |

For hangs, use `tools\stackdump`. Run the x86 build for 32-bit games. The steps from reproducing a hang to finding the lock owner, and the tools for them, are in [tools/hang/README.md](../hang/README.md).

## Good to know

- In automated runs the game now and then stops for 80~100 seconds right after starting (frame 3). The cause is still unknown, so just run it again.
- `gametest.ps1` keeps bringing the window to the front and sends keys, so do not run it while the user is using the PC.
