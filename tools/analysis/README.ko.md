# Flato3D 분석 도구

[English](README.md) · **한국어**

전체 작업 순서는 [docs/WORKFLOW.ko.md](../../docs/WORKFLOW.ko.md)에 있습니다.

새 게임을 작업하거나 수정 후 회귀를 확인할 때 쓰는 도구입니다. Python 스크립트는 Pillow와 numpy가 필요합니다.

## 새 게임 분석 순서

1. 게임 폴더의 `OGL3D.ini`에서 `output = sbs`로 둡니다. `[Debug] trace_steps = 1`도 켭니다.
2. 장면에서 Ctrl+F12를 누릅니다. 다음 파일이 생깁니다.
   - `OGL3D_frameNNNNN.bmp`: 두 눈 이미지
   - `OGL3D.log`의 draw별 기록(`T` 줄)
   - `OGL3D_stepNNNNN_DDD.bmp`: draw 하나가 끝날 때마다의 렌더 타깃
3. 덤프를 아래 도구로 분석합니다.
   - `dispmap.py`: 블록별 시차(px) 지도. 층이 실제로 나뉘었는지 봅니다.
   - `sheet.py`: draw별 이미지를 한 장으로 모읍니다. 어느 draw가 무엇을 그리는지 봅니다.
   - `eyes.py`: 애너글리프와 작은 SBS 이미지를 만듭니다.
4. 로그에서 다음을 확인합니다.
   - `layout` 줄: 화면에 쌓이는 순서대로의 층 목록입니다. 0개면 합성 경로가 끊긴 것입니다.
   - `samples` 줄: draw가 실제로 읽는 텍스처 유닛입니다. `R`은 오른눈 복제본이 있다는 뜻입니다.
5. 실행 중인 게임에도 ini를 고치면 1초 안에 반영됩니다. 키를 누르지 않고 덤프하려면 `dump_frames`와 `trace_frames`에 앞으로의 프레임 번호를 적습니다.

## 도구

| 파일 | 용도 |
| --- | --- |
| `gametest.ps1` | Steam 게임을 실행하고 창을 앞에 둔 채 단계(wait, shot, click, key, hold, ctrlkey, lclick, rmove 등)를 실행합니다. `-Attach`는 실행 중인 게임에 이어 붙고, `-Exe`는 Steam 없이 exe를 실행합니다. 게임별 단계는 `data\game_profiles.json`의 `test`에 있습니다. |
| `dispmap.py` | SBS 덤프의 시차 지도 |
| `maskdisp.py` | 영역 하나의 시차(색 마스크로 작은 캐릭터만 잴 수 있음) |
| `sheet.py` | 이미지 여러 장을 이름과 함께 한 장으로 |
| `eyes.py` | 애너글리프, 작은 SBS, 차이 이미지 |
| `cachecrop.py` | 저해상도 캐시(DK) 영역을 잘라 확대 |
| `memstr.py` | 실행 중인 게임 메모리에서 문자열 검색(읽기 전용). 암호화된 exe의 디버그 문자열 확인용 |
| `peexp.py` | DLL 오프셋을 가장 가까운 export 이름으로 변환(스택 분석) |
| `gstime\` | 셰이더 컴파일·링크 시간 측정(드라이버 캐시를 우회) |

멈춤 분석에는 `tools\stackdump`를 씁니다. 32비트 게임은 x86 빌드로 실행합니다.

## 알아 둘 것

- 자동 실행에서 가끔 시작 직후(프레임 3) 80~100초 멈춥니다. 원인은 아직 모르므로 한 번 더 실행하면 됩니다.
- `gametest.ps1`은 창을 계속 앞으로 가져오고 키를 보내므로, 사용자가 PC를 쓰는 중에는 실행하지 않습니다.
