# Flato3D-SR 작업 규칙

평면 2D 게임을 입체로 만드는 프로젝트입니다. 새 게임을 작업하거나 래퍼를 고칠 때는 먼저 [docs/WORKFLOW.ko.md](docs/WORKFLOW.ko.md)를 읽고 체크포인트(CP0~CP7) 순서대로 진행합니다.

## 구성

- `ogl3d/`: 래퍼(OGL3D-SR `flato3d` 브랜치) 서브모듈. 로컬 폴더는 `D:\ClaudeCode\Projects\OpenGL-3D-Wrapper` 저장소의 worktree입니다. OGL3D-SR `main`(1.0.0)은 건드리지 않습니다.
- `loader/`: WineD3D로 보내는 `version.dll`
- `installer/`: 설치기(C# WinForms, .NET Framework 4.x)
- `data/`: 프로필(`game_profiles.json`), 작업 게임 목록(`GAMES.ko.md`), 후보 목록
  - 로컬 원본(`data/private/`, 소장 정보 포함)은 올리지 않습니다.
- `tools/analysis/`: 자동 실행과 측정 도구

## 꼭 지킬 것

- **측정 먼저:** 원인을 덤프와 draw 기록으로 확인한 뒤 고칩니다.
  - Ctrl+F12, `trace_steps = 1`
  - `dispmap.py`, `maskdisp.py`
- **일반 규칙으로 수정:** 게임 이름으로 분기하지 않습니다. 게임별 값은 ini와 프로필에만 둡니다.
- **회귀 확인:** 래퍼를 고치면 이미 작업한 모든 게임에 배포하고, 다른 게임이 깨지지 않았는지 확인합니다.
- **세이브 보호:** 플레이 테스트 전에 세이브를 `test-backups/`에 백업하고, 끝나면 되돌립니다.
- **사용자 PC 사용 중:** 이때는 자동 실행(gametest)을 돌리지 않습니다.
- **원격 확인:** 출력을 `sbs`로 둡니다.
- **기록:** 결과는 바로 남깁니다. 프로필(`test` 절차 포함), `GAMES.ko.md`, 커밋.
- **커밋 순서:**
  1. `ogl3d/`에서 커밋 → `git push origin flato3d`
  2. 이 저장소에서 서브모듈과 자료를 커밋 → `git push`
- **릴리스:** 작업이 끝날 때마다 `D:\OneDrive\AI\ClaudeCode\Flato3D_github_release`의 pre-release 패키지를 갱신합니다(`reports\`는 그대로 둠). GitHub 릴리스(배포 zip)는 사용자가 pre-release를 시험한 뒤에 올립니다.
- **"이상로그 수정해줘":** 설치기 **이상 로그** 버튼으로 모인 보고(`Flato3D_github_release\reports\`, 공개 보고는 GitHub `bug` 이슈)를 처리합니다. 절차는 WORKFLOW의 "이상 로그 처리"입니다.
- **파일 편집:** PowerShell `Get-Content`/`Set-Content`로 소스를 다시 쓰지 않습니다(한글 깨짐). 작업 파일은 CRLF입니다.

## 빌드

- 래퍼: `ogl3d\build.ps1`
- 로더: `loader\build.ps1`
- 설치기: `installer\src\build.cmd`
- 패키지: `package.ps1`
