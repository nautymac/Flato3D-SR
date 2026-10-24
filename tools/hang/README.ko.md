# 멈춤(응답 없음) 진단 도구

[English](README.md) · **한국어**

게임이 "응답 없음"으로 멈출 때, 멈춘 순간의 스레드와 잠금을 기록해서 원인을 찾는 도구입니다. 2026-10-24 The Messenger 작업에서 만들었고, 다른 게임에도 그대로 씁니다. 전체 작업 순서는 [docs/WORKFLOW.ko.md](../../docs/WORKFLOW.ko.md)에 있습니다.

## 한눈에 보기

| 단계 | 할 일 | 도구 |
| --- | --- | --- |
| 0 | 세이브 백업, 출력은 `sbs` 또는 사용할 출력으로 고정 | `test-backups\<게임>` |
| 1 | 멈출 때까지 반복 실행하고, 멈춘 채로 기록 | `hang_loop.ps1` |
| 2 | 스택 주소에 함수 이름 붙이기 | `stackmap.py` |
| 3 | 누가 무엇을 기다리고, 그 잠금을 누가 잡았는지 따라가기 | `regs.txt`, `cslist.txt`, `csscan.txt`, `suspended.txt` |
| 4 | 하나씩 빼고 같은 재현을 다시 돌려 범위 좁히기 | 아래 "범위 좁히기" 표 |
| 5 | 고친 뒤 같은 횟수로 다시 돌려 멈춤이 없는지 확인 | `hang_loop.ps1` |
| 6 | 게임 원래 상태로 되돌리기(DLL, ini, 세이브), 결과 기록 | `data/GAMES.ko.md`, 이 문서의 사례 |

## 준비(한 번만)

- Python 3(표준 라이브러리만 사용), PowerShell 5.1
- 스택 덤프 도구 빌드. 게임 비트수에 맞춰 빌드합니다.

  ```powershell
  $vs = & "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe" -latest -products * -property installationPath
  $cmake = "$vs\Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe"
  & $cmake -S tools\stackdump -B tools\stackdump\b64 -A x64; & $cmake --build tools\stackdump\b64 --config Release
  & $cmake -S tools\stackdump -B tools\stackdump\b32 -A Win32; & $cmake --build tools\stackdump\b32 --config Release
  ```

- ntdll 심볼(PDB). Windows를 업데이트하면 다시 받습니다. 파일은 `tools\hang\sym\`에 받고, git에는 올리지 않습니다.

  ```powershell
  tools\hang\getpdb.ps1 C:\Windows\System32\ntdll.dll
  ```

## 1. 재현하고 기록하기

멈춤은 매번 생기지 않으므로 여러 번 돌립니다. 처음 멈춘 회차에서 멈추고, 게임이 멈춰 있는 동안 아래 기록을 남긴 뒤 게임을 끕니다.

```powershell
tools\hang\hang_loop.ps1 -N 5 -Prefix A -Out <폴더> -AppId <Steam 앱 번호> -Proc <exe 이름(확장자 없이)>
```

- 옵션
  - `-Exe <경로>`: Steam 앱 번호 대신 exe를 직접 실행합니다(Steam 실행 선택 창이 뜨는 게임).
  - `-Steps`: 게임을 진행시키는 입력. `tools\analysis\gametest.ps1`의 단계 형식입니다. 기본값은 30초 기다린 뒤 Enter를 4초 간격으로 4번 누르는 것으로, 타이틀을 지나 이어하기까지 가는 게임에 맞습니다.
  - `-WaitS`: 입력이 끝난 뒤 멈춤을 기다리는 시간(기본 120초).
  - `-Modules`: `csscan.py`로 정적 잠금을 볼 모듈(기본 `wined3d.dll`, `w3d11.dll`, `wxgi.dll`). OpenGL 직접 게임은 `opengl32.dll` 등을 줍니다.
- 멈춤 판정은 둘 중 하나입니다.
  - `OGL3D.log`의 `HANG?` 줄: 래퍼 감시 스레드가 4초 동안 프레임이 없으면 씁니다.
  - Windows 응답 검사 3회 연속 실패: 그래서 래퍼를 뺀 상태에서도 씁니다.
- 회차마다 `<폴더>\A<n>\`에 남는 것

  | 파일 | 내용 |
  | --- | --- |
  | `suspended.txt` | 다른 누군가(가비지 컬렉터, 디버거, 감시 스레드)가 일시 정지시킨 스레드와 멈춘 위치. 가장 먼저 기록합니다 |
  | `stacks.txt` | 모든 스레드의 스택(stackdump) |
  | `csscan.txt` | `-Modules` 안의 잡힌 정적 잠금 |
  | `cslist.txt` | 디버그 기록이 있는 잠금 중 잡힌 것과 이름 있는 것(x64) |
  | `regs.txt` | 잠금을 기다리는 스레드의 레지스터와 그 레지스터가 가리키는 잠금, 그 주인(x64) |
  | `OGL3D.log` | 래퍼 기록 |
  | `in.png` | 입력이 끝난 순간의 화면 캡처(전체 화면 GL/WineD3D는 검게 나옴. 증거로 쓰지 않음) |

- 멈추지 않은 회차는 `no hang in 120 s`만 남습니다. "N번 중 0번 멈춤"이 곧 결과입니다.

## 2. 이름 붙이기

```powershell
python tools\hang\stackmap.py <폴더>\A1\stacks.txt --gamedir "<게임 폴더>" --map opengl32.dll=ogl3d\bin\x64\opengl32.map
```

- `--thread <번호>`: 한 스레드만 봅니다. `--match <정규식>`: 프레임 이름이 맞는 스레드만 봅니다.
- 이름이 나오는 곳
  - ntdll 등 시스템 DLL: 받은 PDB에서 정확한 이름이 나옵니다.
  - WineD3D: 11.0 빌드(fdossena.com)는 심볼이 들어 있어 이름이 나옵니다. 11.19 빌드는 심볼이 없어 export 이름만 나옵니다. 같은 멈춤을 11.0으로 재현하면 이름이 나옵니다.
  - 우리 래퍼: 빌드 때 생기는 `opengl32.map`을 씁니다.
  - 나머지: 가장 가까운 export를 씁니다. 내부 함수면 오프셋이 크므로 참고로만 봅니다.

## 3. 잠금 따라가기

1. **기다리는 쪽.** `RtlpWaitOnCriticalSection`(사용자 모드 회전)이나 `NtWaitFor…`/`NtDelayExecution`(커널 대기)에 있는 스레드를 찾습니다. `regs.txt`에 그 스레드가 기다리는 잠금과 주인 스레드가 나옵니다.
2. **잡은 쪽.** `csscan.txt` / `cslist.txt` / `regs.txt`의 `OwningThread`를 `stackmap.py --thread <번호>`로 봅니다.
3. **판단**

   | 보이는 것 | 뜻 |
   | --- | --- |
   | A가 B의 잠금을, B가 A의 잠금을 기다림 | 교착(잠금 순서 문제). 둘 중 한쪽의 잠금 순서를 바꾸거나 한쪽을 놓고 기다리게 함 |
   | 기다리는 잠금의 주인이 이미 없는 스레드 | 주인이 잠금을 쥔 채 끝났거나 강제 종료됨(`TerminateThread`) |
   | 잠금이 비었는데(`LockCount -1` 또는 `-3`) 계속 회전 | 깨우기 신호를 잃음. 잠금을 놓던 스레드가 중간에 멈췄거나 죽음. `suspended.txt`와 `TerminateThread`를 의심 |
   | 한 스레드가 `Sleep`/`wined3d_pause`로 다른 스레드를 기다리고, 그 스레드는 첫 스레드가 잡은 잠금을 기다림 | 잠금을 쥔 채 기다리는 구조. WineD3D의 `wined3d_cs`가 흔한 예 |
   | `suspended.txt`에 정지된 스레드가 있고 그 위치가 힙/잠금 코드 | 가비지 컬렉터 등이 잠금을 놓는 도중에 멈춰 세움 |
   | `RtlpWaitOnCriticalSection+0x2db`(ntdll+0xfc9b, 빌드 26200) 근처에서 계속 돌고, `regs.txt`의 `rbx`가 자기 스택 주소이며 `[rbx+0x10]`이 `rbx`와 같음 | **엉뚱한 스레드 알림(thread alert)**. 이 스레드에 `NtAlertThreadByThreadId`가 남아 있다가 잠금 대기를 깨움. 누가 이 함수를 쓰는지 찾음(WineD3D는 래퍼가 처리, 아래 사례). `alert_cs_repro.cpp`로 같은 모양을 재현할 수 있음 |

## 4. 범위 좁히기

하나씩 바꾸고 같은 횟수(`-N 5` 이상)로 다시 돌립니다. 바꾸기 전 파일은 반드시 백업합니다.

| 바꿀 것 | 방법 | 결과가 말해 주는 것 |
| --- | --- | --- |
| 래퍼 빼기 | 게임 폴더의 `opengl32.dll` 이름을 잠시 바꿈 | 그래도 멈추면 래퍼만의 문제는 아님 |
| SR 빼기 | `OGL3D.ini`의 `output = sbs`(래퍼는 그대로) | SR 런타임(SimulatedReality*.dll)이 원인인지 |
| WineD3D CSMT | `HKCU\Software\Wine\AppDefaults\<exe>\Direct3D`의 `csmt`(DWORD 0) | 그리기 스레드 분리가 원인인지. Unity는 csmt 0에서 바로 죽음 |
| WineD3D 버전 | `https://downloads.fdossena.com/Projects/WineD3D/Builds/WineD3DForWindows_<버전>-x86_64.zip`(x86은 `-x86_64` 없음). `tools\wined3d_rename.py`로 이름을 바꾼 뒤 게임 폴더의 `w3d11.dll`, `wxgi.dll`, `w3d10core.dll`, `wined3d.dll`만 바꿈 | WineD3D 회귀인지. 11.0은 심볼이 있어 이름 붙이기에도 좋음 |
| NVIDIA OpenGL 설정 | NVIDIA 프로필의 `OGL_THREAD_CONTROL_ID`(0x20C1221E: 1 켬, 2 끔), `OGL_CPL_PREFER_DXPRESENT_ID`(0x20D690F8: 0 네이티브, 1 DXGI 위 레이어) | 드라이버 스레드 최적화나 표시 경로가 원인인지 |

## 5. 읽는 법

- **스택은 추정입니다.** stackdump는 스택에서 코드 주소처럼 보이는 값을 모두 보여 주므로, 예전 호출이 남긴 주소도 섞입니다. 맨 위(`ip`)와 바로 아래 몇 줄이 가장 믿을 만합니다.
- **critical section의 `LockCount`**(Windows 8 이후 형식)
  - `-1`: 비어 있음
  - 짝수(비트 0이 0): 잡혀 있음
  - 비트 1이 0: 기다리던 스레드 하나를 깨운 상태
  - `(-1 - LockCount) >> 2`: 기다리는 스레드 수
- **멈춘 스레드의 `ip`가 `ntdll+0x16xxxx`(Nt… 시스템 호출)이면 커널에서 기다리는 중**입니다. `RtlpWaitOnCriticalSection` 안에서 회차마다 주소가 바뀌면 사용자 모드에서 회전하는 중이고, 잠금 주인이 실행 중이라고 판단해 기다리는 경우입니다.
- **프로세스 힙 잠금**은 힙 주소 + 0x2c8입니다(`_HEAP.LockVariable`, 이 Windows 빌드의 PDB 기준). `regs.txt`가 가리키는 잠금 주소가 힙 + 0x2c8이면 메모리 할당/해제가 막힌 것입니다.
- **화면 캡처는 증거가 안 됩니다.** 전체 화면 OpenGL/WineD3D 창은 GDI 캡처가 검게 나옵니다. 그림 확인은 Ctrl+F12 덤프나 사용자 눈으로 합니다.
- **시스템 DLL 안에서 무엇을 하는지 모르겠으면 디스어셈블합니다.** ntdll의 기준 주소는 0x180000000입니다(`dumpbin /headers`의 `image base`). 함수 이름과 시작은 `symutil.PdbSymbols`로 봅니다.

  ```powershell
  $db = "<VS>\VC\Tools\MSVC\<버전>\bin\Hostx64\x64\dumpbin.exe"
  & $db /nologo /disasm:nobytes /range:0x18000fb80,0x18000fd40 C:\Windows\System32\ntdll.dll
  python -c "import sys; sys.path.insert(0, r'tools\hang'); import symutil; print(symutil.PdbSymbols(r'C:\Windows\System32\ntdll.dll').name(0xfc9b))"
  ```

- **어떤 DLL이 특정 함수를 쓰는지**는 가져오기(`dumpbin /imports`)만으로는 부족합니다. `GetProcAddress`로 찾는 경우(WineD3D의 `NtAlertThreadByThreadId`)가 있으니, 파일 안의 문자열도 검색합니다.

## 6. 마무리 확인 목록

- 게임 폴더에 바꿔 넣은 DLL을 원래 것으로 되돌렸는가(백업 폴더 확인).
- `OGL3D.ini`의 `output`을 사용자 값(보통 `auto`)으로 되돌렸는가.
- 레지스트리 `csmt` 등 실험 값을 지웠는가.
- 세이브를 백업에서 되돌렸는가.
- 결과를 `data/GAMES.ko.md`와 이 문서의 사례에 적었는가.

## 사례: The Messenger (Unity 2017.4, D3D11 → WineD3D, 2026-10-24)

- 증상: 시작하고 수십 초 ~ 몇 분 안에 "응답 없음". 회차마다 시점이 다릅니다.
- 래퍼 제외: 래퍼를 빼도 한 번 멈췄습니다(사용자 플레이 중). 래퍼만의 문제는 아닙니다.
- `csmt=0`: 시작하자마자 wined3d.dll 접근 위반(주소 0x48 읽기)으로 죽습니다. Unity가 여러 스레드에서 D3D11을 쓰기 때문입니다(Huntdown과 같음).
- WineD3D 10.0: 검은 화면에서 진행하지 않습니다. WineD3D 11.0: 11.19와 똑같이 멈춥니다.
- 멈춘 순간(11.0 심볼로 확인)
  - Unity 그래픽 스레드: `d3d11_device_context_Map` → `wined3d_device_context_map` → `wined3d_device_context_emit_map`. 전역 잠금 `wined3d_cs`를 잡은 채, 그리기 스레드가 일을 끝내길 `Sleep`으로 기다립니다.
  - WineD3D 그리기(CS) 스레드: `wined3d_cs_exec_update_sub_resource` → `wined3d_buffer_update_sub_resource` → `wined3d_context_gl_destroy_bo` → `wined3d_allocator_chunk_gl_unmap` → `glUnmapBuffer` → NVIDIA 드라이버(nvoglv64) 안에서 critical section을 기다리며 회전합니다.
  - `regs.py`: 그리기 스레드가 기다리는 잠금은 **프로세스 힙 잠금**(힙 + 0x2c8)입니다. 잠금은 비어 있고(`LockCount -3`: 비어 있음 + 기다리던 스레드를 깨운 표시) 주인도 없는데, 그리기 스레드는 계속 회전합니다.
- 13번의 멈춤이 모두 같은 자리였습니다: `RtlpWaitOnCriticalSection+0x2db`~`+0x2e6`. 시작 직후(프레임 8~16, 힙 잠금)와 1~2분 뒤(프레임 2500~7175, 드라이버/WineD3D 잠금) 두 무리가 있었지만 같은 고리입니다.
- **원인 찾기**
  1. ntdll을 디스어셈블(`dumpbin /disasm /range:`)해서 그 자리가 대기 블록 목록을 끝까지 따라가는 고리임을 확인했습니다. Windows 11 24H2부터 critical section 대기는 대기 블록(대기하는 스레드의 스택에 있음)을 `PEB+0x3a0`의 해시 목록(WaitOnAddressHashTable)에 걸고 `NtWaitForAlertByThreadId`로 잡니다. 멈춘 스레드의 블록은 다음 칸이 자기 자신(`[rbx+0x10] == rbx`)이어서 고리가 끝나지 않습니다.
  2. 깨어난 뒤 블록이 실제로 목록에서 빠졌는지 확인하지 않고 같은 블록을 다시 겁니다. 그러니 잠금 주인이 아닌 누군가가 이 스레드를 알림(alert)으로 깨우면 이 모양이 됩니다.
  3. 게임 폴더의 DLL에서 이름을 검색하니 `wined3d.dll`에 `NtAlertThreadByThreadId`가 있었습니다. Wine 소스 `dlls/wined3d/cs.c`: 그리기 스레드는 할 일을 `NtWaitForAlertByThreadId`(쿼리가 남아 있으면 시간 제한 있음)로 기다리고, 일을 넣는 스레드가 `NtAlertThreadByThreadId`로 깨웁니다. 시간 제한으로 이미 깨어난 뒤에 온 알림은 남아 있다가, 그리기 스레드의 다음 잠금 대기를 엉뚱하게 깨웁니다.
  4. `alert_cs_repro.cpp`로 확인: 알림 하나를 먼저 보내고 잡힌 critical section에 들어가면, 같은 자리(ntdll+0xfc9b, `[rbx+0x10] == rbx`)에서 영원히 멈춥니다. 32비트(WOW64)도 같습니다.
- 헛짚은 것(다음에 참고)
  - SR 런타임의 `TerminateThread`: 한 회차가 SR 초기화 직후 멈춰서 의심했지만, SR을 뺀 `sbs` 출력에서도 같은 모양으로 멈췄습니다.
  - Mono 가비지 컬렉터의 일시 정지: `suspended.py`에 정지된 스레드가 없었고, 일시 정지/재개 스트레스 테스트(수백만 번)에서도 재현되지 않았습니다.
  - 일반적인 잠금 경쟁: 스트레스 테스트에서 재현되지 않았습니다. 같은 프로세스의 특별한 코드(여기서는 WineD3D의 알림)를 찾아야 합니다.
- **해결(래퍼, 10-24)**: `wined3d.dll`의 `GetProcAddress` 가져오기를 바꿔 `NtAlertThreadByThreadId`/`NtWaitForAlertByThreadId`를 "없음"으로 답합니다. WineD3D는 그러면 이벤트로 그리기 스레드를 깨웁니다(남은 신호는 한 번 더 도는 것으로 끝). 로그: `WineD3D: command thread woken by an event`. CSMT를 쓰는 모든 WineD3D 게임(x86/x64)에 적용됩니다.
- 진행 상황과 결론은 [data/GAMES.ko.md](../../data/GAMES.ko.md)의 The Messenger 항목에 적습니다.

## 파일

| 파일 | 용도 |
| --- | --- |
| `hang_repro.ps1` | 실행 → 단계 진행 → 멈춤 대기 → 멈추면 기록 → 종료 |
| `hang_loop.ps1` | 위를 멈출 때까지 N번 |
| `getpdb.ps1` | Microsoft 심볼 서버에서 시스템 DLL의 PDB 받기 |
| `symutil.py` | 주소 → 이름(PDB, MinGW 심볼, MSVC map, export) |
| `stackmap.py` | stacks.txt 전체에 이름 붙이기 |
| `csscan.py` | 모듈 이미지 안의 잡힌 잠금(정적 잠금) |
| `cslist.py` | 디버그 기록이 있는 잠금 목록(x64, ntdll PDB 필요) |
| `regs.py` | 잠금을 기다리는 스레드의 레지스터와 잠금 주인(x64, ntdll PDB 필요) |
| `suspended.py` | 다른 누군가가 일시 정지시킨 스레드와 위치(가비지 컬렉터, 디버거, 감시 스레드) |
| `alert_cs_repro.cpp` | 엉뚱한 스레드 알림 하나로 critical section 대기가 멈추는 것을 재현(Windows 11 24H2 이후) |
| `..\stackdump\` | 모든 스레드 스택(대상 비트수로 빌드) |
