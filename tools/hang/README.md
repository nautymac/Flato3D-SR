# Hang ("not responding") diagnosis

**English** · [한국어](README.ko.md)

Tools that record the threads and locks of a game at the moment it stops responding, so the cause can be found.
Built for The Messenger (2026-10-24) and meant for any game. The full working procedure is in
[docs/WORKFLOW.md](../../docs/WORKFLOW.md).

## At a glance

| Step | What to do | Tool |
| --- | --- | --- |
| 0 | Back up the saves; fix the output to `sbs` or to the output under test | `test-backups\<game>` |
| 1 | Run the game until it hangs, and record while it is hung | `hang_loop.ps1` |
| 2 | Put function names on the stack addresses | `stackmap.py` |
| 3 | Follow who waits for what, and who holds that lock | `regs.txt`, `cslist.txt`, `csscan.txt`, `suspended.txt` |
| 4 | Change one thing at a time and run the same reproduction again | "Narrowing it down" below |
| 5 | After a fix, run the same number of times again and check there is no hang | `hang_loop.ps1` |
| 6 | Restore the game (DLLs, ini, saves) and write down the result | `data/GAMES.md`, the case study here |

## Setup (once)

- Python 3 (standard library only), PowerShell 5.1
- Build the stack dump tool for the game's bitness.

  ```powershell
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
  $cmake = "$vs\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
  & $cmake -S tools\stackdump -B tools\stackdump\b64 -A x64; & $cmake --build tools\stackdump\b64 --config Release
  & $cmake -S tools\stackdump -B tools\stackdump\b32 -A Win32; & $cmake --build tools\stackdump\b32 --config Release
  ```

- ntdll's symbols (PDB). Fetch them again after a Windows update. They go into `tools\hang\sym\`, which is not in git.

  ```powershell
  tools\hang\getpdb.ps1 C:\Windows\System32\ntdll.dll
  ```

## 1. Reproduce and record

The hangs do not happen every time, so the game is run several times. The loop stops at the first run that hangs,
records the items below while the game is still hung, then closes the game.

```powershell
tools\hang\hang_loop.ps1 -N 5 -Prefix A -Out <dir> -AppId <Steam app id> -Proc <exe name without extension>
```

- Options
  - `-Exe <path>`: start the exe directly instead of the Steam app id (games where Steam shows a launch chooser).
  - `-Steps`: input that moves the game along, in `tools\analysis\gametest.ps1`'s step format. The default waits 30 s
    and then presses Enter four times, 4 s apart, which gets past the title to "continue" in many games.
  - `-WaitS`: how long to wait for a hang after the input (default 120 s).
  - `-Modules`: modules whose static locks `csscan.py` checks (default `wined3d.dll`, `w3d11.dll`, `wxgi.dll`). For a
    direct OpenGL game, pass `opengl32.dll` and so on.
- A hang is either of these:
  - a `HANG?` line in `OGL3D.log`, written by the wrapper's watch thread after 4 s without a frame;
  - Windows' responsiveness check failing three times in a row, so it also works with the wrapper removed.
- What each run leaves in `<dir>\A<n>\`:

  | File | Contents |
  | --- | --- |
  | `suspended.txt` | Threads suspended by someone else (garbage collector, debugger, watchdog) and where they stopped. Recorded first |
  | `stacks.txt` | Every thread's stack (stackdump) |
  | `csscan.txt` | Held static locks inside `-Modules` |
  | `cslist.txt` | Held or named locks that have a debug record (x64) |
  | `regs.txt` | Registers of threads waiting on a lock, the lock they point at, and its owner (x64) |
  | `OGL3D.log` | The wrapper's log |
  | `in.png` | Screen capture after the input (black for fullscreen GL/WineD3D; not evidence) |

- A run that does not hang only prints `no hang in 120 s`. "0 hangs in N runs" is the result.

## 2. Name the frames

```powershell
python tools\hang\stackmap.py <dir>\A1\stacks.txt --gamedir "<game folder>" --map opengl32.dll=ogl3d\bin\x64\opengl32.map
```

- `--thread <id>` shows one thread; `--match <regex>` shows threads whose frame names match.
- Where the names come from:
  - ntdll and other system DLLs: exact names from the downloaded PDB.
  - WineD3D: the 11.0 build (fdossena.com) keeps its symbols, so it gets names. The 11.19 build is stripped and only
    gets export names. Reproduce the same hang with 11.0 to get names.
  - Our wrapper: the `opengl32.map` written by the build.
  - Everything else: the nearest export. Internal functions show large offsets; use them only as hints.

## 3. Follow the locks

1. **The waiter.** Find threads in `RtlpWaitOnCriticalSection` (spinning in user mode) or in `NtWaitFor…` /
   `NtDelayExecution` (waiting in the kernel). `regs.txt` shows the lock such a thread waits on, and its owner.
2. **The holder.** Look at the `OwningThread` from `csscan.txt` / `cslist.txt` / `regs.txt` with
   `stackmap.py --thread <id>`.
3. **What it means**

   | What you see | Meaning |
   | --- | --- |
   | A waits for B's lock and B waits for A's | Deadlock (lock order). Change one side's lock order, or let one side release before it waits |
   | The owner of the awaited lock is a thread that no longer exists | The owner ended or was killed (`TerminateThread`) while holding it |
   | The lock is free (`LockCount -1` or `-3`) but the waiter keeps spinning | A lost wake-up: the thread releasing the lock stopped or died halfway. Suspect `suspended.txt` and `TerminateThread` |
   | One thread waits for another with `Sleep` / `wined3d_pause`, and the other waits on a lock the first holds | Waiting while holding a lock. WineD3D's `wined3d_cs` is the common case |
   | `suspended.txt` lists a suspended thread stopped in heap or lock code | A garbage collector or similar stopped it while it was releasing a lock |
   | It keeps looping near `RtlpWaitOnCriticalSection+0x2db` (ntdll+0xfc9b on build 26200), `rbx` in `regs.txt` is on its own stack and `[rbx+0x10]` equals `rbx` | **A stray thread alert.** An `NtAlertThreadByThreadId` left pending for this thread woke its lock wait. Find who calls that function (WineD3D is handled by the wrapper, see the case study). `alert_cs_repro.cpp` reproduces the same shape |

## 4. Narrowing it down

Change one thing, then run the same number of times again (`-N 5` or more). Always back up the files first.

| Change | How | What the result tells |
| --- | --- | --- |
| Remove the wrapper | Rename the game folder's `opengl32.dll` for a while | If it still hangs, it is not only the wrapper |
| Remove SR | `output = sbs` in `OGL3D.ini` (the wrapper stays) | Whether the SR runtime (SimulatedReality*.dll) is the cause |
| WineD3D CSMT | `csmt` (DWORD 0) in `HKCU\Software\Wine\AppDefaults\<exe>\Direct3D` | Whether the separate drawing thread is the cause. Unity dies at once with csmt 0 |
| WineD3D version | `https://downloads.fdossena.com/Projects/WineD3D/Builds/WineD3DForWindows_<version>-x86_64.zip` (x86 without `-x86_64`). Rename with `tools\wined3d_rename.py`, then replace only `w3d11.dll`, `wxgi.dll`, `w3d10core.dll`, `wined3d.dll` in the game folder | Whether it is a WineD3D regression. 11.0 has symbols, which also helps naming |
| NVIDIA OpenGL settings | In the NVIDIA profile, `OGL_THREAD_CONTROL_ID` (0x20C1221E: 1 on, 2 off) and `OGL_CPL_PREFER_DXPRESENT_ID` (0x20D690F8: 0 native, 1 layered on DXGI) | Whether the driver's threaded optimization or present path is the cause |

## 5. Reading the results

- **Stacks are a guess.** stackdump prints every stack value that looks like a code address, so stale return
  addresses from earlier calls are mixed in. The top (`ip`) and the few lines under it are the most reliable.
- **A critical section's `LockCount`** (Windows 8 and later):
  - `-1`: free
  - even (bit 0 clear): held
  - bit 1 clear: a waiting thread has been woken
  - `(-1 - LockCount) >> 2`: number of waiting threads
- **A hung thread whose `ip` is at `ntdll+0x16xxxx` (an Nt… system call) is waiting in the kernel.** If its address
  inside `RtlpWaitOnCriticalSection` changes from run to run, it is spinning in user mode, because it believes the
  lock owner is running.
- **The process heap lock** is at the heap address + 0x2c8 (`_HEAP.LockVariable`, from this Windows build's PDB). If the
  lock in `regs.txt` is heap + 0x2c8, memory allocation or freeing is blocked.
- **Screen captures are not evidence.** GDI captures of fullscreen OpenGL/WineD3D windows come out black. Check the
  picture with a Ctrl+F12 dump or the user's eyes.
- **When it is unclear what a system DLL is doing, disassemble it.** ntdll's image base is 0x180000000 (`image base`
  in `dumpbin /headers`). Function names and starts come from `symutil.PdbSymbols`.

  ```powershell
  $db = "<VS>\VC\Tools\MSVC\<version>\bin\Hostx64\x64\dumpbin.exe"
  & $db /nologo /disasm:nobytes /range:0x18000fb80,0x18000fd40 C:\Windows\System32\ntdll.dll
  python -c "import sys; sys.path.insert(0, r'tools\hang'); import symutil; print(symutil.PdbSymbols(r'C:\Windows\System32\ntdll.dll').name(0xfc9b))"
  ```

- **Which DLL uses a function** is not settled by the imports (`dumpbin /imports`) alone. Some look it up with
  `GetProcAddress` (WineD3D's `NtAlertThreadByThreadId`), so also search the files for the name as a string.

## 6. Before finishing

- DLLs swapped into the game folder are back to the originals (check the backup folder).
- `OGL3D.ini` is restored from the backup file (copy the whole file: rewriting a value can leave differences such as
  a lost BOM).
- Experimental values such as the registry `csmt` are removed.
- The saves are restored from the backup.
- The result is written in `data/GAMES.md` and in the case study here.

## Case study: The Messenger (Unity 2017.4, D3D11 → WineD3D, 2026-10-24)

- Symptom: "not responding" after tens of seconds to a few minutes; the moment differs every run.
- Without the wrapper it also hung once (while the user played), so it is not only the wrapper.
- `csmt=0`: crashes at start with an access violation in wined3d.dll (read of address 0x48). Unity uses D3D11 from
  several threads (same as Huntdown).
- WineD3D 10.0 stays on a black screen. WineD3D 11.0 hangs exactly like 11.19.
- At the moment of the hang (named with the 11.0 symbols):
  - Unity's graphics thread: `d3d11_device_context_Map` → `wined3d_device_context_map` →
    `wined3d_device_context_emit_map`. It holds the global lock `wined3d_cs` and waits with `Sleep` for the drawing
    thread to finish.
  - WineD3D's drawing (CS) thread: `wined3d_cs_exec_update_sub_resource` → `wined3d_buffer_update_sub_resource` →
    `wined3d_context_gl_destroy_bo` → `wined3d_allocator_chunk_gl_unmap` → `glUnmapBuffer` → spins on a critical
    section inside the NVIDIA driver (nvoglv64).
  - `regs.py`: the lock the drawing thread waits on is the **process heap lock** (heap + 0x2c8). It is free
    (`LockCount -3`: free, with the woken-waiter mark) and has no owner, yet the drawing thread keeps spinning.
- All 13 hangs were at the same place: `RtlpWaitOnCriticalSection+0x2db` to `+0x2e6`. There were two groups, right
  after start (frames 8 to 16, heap lock) and after one to two minutes (frames 2500 to 7175, driver/WineD3D locks), but
  it is the same loop.
- **Finding the cause**
  1. Disassembling ntdll (`dumpbin /disasm /range:`) showed that this place is a loop that walks a list of wait blocks
     to its end. Since Windows 11 24H2 a critical-section waiter links a wait block (on its own stack) into a hashed
     list at `PEB+0x3a0` (WaitOnAddressHashTable) and sleeps in `NtWaitForAlertByThreadId`. The hung thread's block has
     itself as its next entry (`[rbx+0x10] == rbx`), so the loop never ends.
  2. After waking, the code does not check that its block really left the list; it links the same block again. So if
     anyone other than the lock's releaser wakes the thread with an alert, this is the result.
  3. Searching the game folder's DLLs for the name found `NtAlertThreadByThreadId` in `wined3d.dll`. Wine source
     `dlls/wined3d/cs.c`: the drawing thread waits for work in `NtWaitForAlertByThreadId` (with a timeout while
     queries are pending), and threads queueing work wake it with `NtAlertThreadByThreadId`. An alert that arrives
     after the timed wait has already returned stays pending and wrongly wakes the drawing thread's next lock wait.
  4. Confirmed with `alert_cs_repro.cpp`: send one alert first, then enter a held critical section, and the thread
     hangs at the same place (ntdll+0xfc9b, `[rbx+0x10] == rbx`) forever. 32-bit (WOW64) is the same.
- Wrong leads (for next time)
  - The SR runtime's `TerminateThread`: suspected because one run hung right after SR started, but it hung the same
    way with SR-free `sbs` output.
  - Mono's garbage collector suspending threads: `suspended.py` found no suspended thread, and a suspend/resume
    stress test (millions of times) did not reproduce it.
  - Plain lock contention: stress tests did not reproduce it. Look for special code in the same process (here
    WineD3D's alerts).
- **Fix (wrapper, 10-24)**: `wined3d.dll`'s import of `GetProcAddress` answers "not found" for
  `NtAlertThreadByThreadId` / `NtWaitForAlertByThreadId`. WineD3D then wakes its drawing thread with an event (a
  leftover signal only costs one extra pass). Log: `WineD3D: command thread woken by an event`. Applies to every
  WineD3D game using CSMT (x86/x64). Verified: 11 hangs in 19 runs before; after the fix 10 `sbs` runs and 5 SR
  runs with no hang (`hang_loop.ps1`, 120 s each).
- Progress and the conclusion go into the The Messenger entry of [data/GAMES.md](../../data/GAMES.md).

## Files

| File | Purpose |
| --- | --- |
| `hang_repro.ps1` | Start → steps → wait for a hang → record if hung → close |
| `hang_loop.ps1` | The above up to N times, until it hangs |
| `getpdb.ps1` | Fetch a system DLL's PDB from the Microsoft symbol server |
| `symutil.py` | Address → name (PDB, MinGW symbols, MSVC map, exports) |
| `stackmap.py` | Name every frame of a stacks.txt |
| `csscan.py` | Held locks inside a module image (static locks) |
| `cslist.py` | Locks with a debug record (x64, needs ntdll's PDB) |
| `regs.py` | Registers of threads waiting on a lock, and the lock's owner (x64, needs ntdll's PDB) |
| `suspended.py` | Threads suspended by someone else, and where (garbage collector, debugger, watchdog) |
| `alert_cs_repro.cpp` | Reproduces a critical-section wait hung by one stray thread alert (Windows 11 24H2 and later) |
| `..\stackdump\` | Every thread's stack (build for the target's bitness) |
