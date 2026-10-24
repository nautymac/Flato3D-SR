# Starts a game, drives it with gametest steps, and waits for a hang. On a hang it records, while the game is
# still hung: every thread's stack (stackdump), held critical sections in the given modules (csscan.py), held
# critical sections with debug records (cslist.py, x64), and the registers of threads waiting on a critical
# section (regs.py, x64). Then the game is closed.
#
# A hang is the wrapper's "HANG?" line in OGL3D.log (render thread made no frame for 4 s), or Windows' own test
# (the window answers no message) three times in a row - so it also works with the wrapper removed.
#
#   hang_repro.ps1 -AppId 764790 -Proc TheMessenger -Out <dir> [-Steps 'wait:30,key:0D,...'] [-WaitS 120]
#                  [-Modules wined3d.dll,w3d11.dll,wxgi.dll] [-Exe <exe>]
param(
    [string]$AppId, [Parameter(Mandatory)] [string]$Proc, [Parameter(Mandatory)] [string]$Out,
    [string]$Steps = 'wait:30,key:0D,wait:4,key:0D,wait:4,key:0D,wait:4,key:0D,wait:4',
    [int]$WaitS = 120, [string[]]$Modules = @('wined3d.dll', 'w3d11.dll', 'wxgi.dll'), [string]$Exe = ''
)
$here = $PSScriptRoot
$tools = Split-Path $here
$sym = Join-Path $here 'sym'
New-Item -ItemType Directory -Force $Out | Out-Null
Get-Process $Proc -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
Start-Sleep 2
$gt = @{ Proc = $Proc; Steps = "$Steps,shot:in"; OutDir = $Out; Keep = $true }
if ($Exe) { $gt.Exe = $Exe } else { $gt.AppId = $AppId }
& (Join-Path $tools 'analysis\gametest.ps1') @gt | Select-Object -Last 2
$p = Get-Process $Proc -ErrorAction SilentlyContinue | Select-Object -First 1
$gameDir = if ($p) { Split-Path $p.Path } else { $null }
$log = if ($gameDir) { Join-Path $gameDir 'OGL3D.log' } else { $null }
$end = (Get-Date).AddSeconds($WaitS); $hung = $false; $notResp = 0
while ((Get-Date) -lt $end) {
    $p = Get-Process $Proc -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $p) { 'game exited'; break }
    if ($log -and (Test-Path $log) -and (Select-String -Path $log -Pattern 'HANG\?' -Quiet)) { $hung = $true; break }
    $notResp = if ($p.Responding) { 0 } else { $notResp + 1 }
    if ($notResp -ge 3) { $hung = $true; 'window not responding'; break }
    Start-Sleep 2
}
if ($hung) {
    "HANG at $(Get-Date -Format HH:mm:ss) (pid $($p.Id))"
    $x86 = [Environment]::Is64BitOperatingSystem -and (& {
        Add-Type -Namespace HR -Name K -MemberDefinition '[DllImport("kernel32.dll")] public static extern bool IsWow64Process(IntPtr h, out bool w);' -ErrorAction SilentlyContinue
        $w = $false; [HR.K]::IsWow64Process($p.Handle, [ref]$w) | Out-Null; $w })
    # first, before anything else touches the threads: who is suspended by someone else right now
    python (Join-Path $here 'suspended.py') $p.Id 2>&1 | Tee-Object (Join-Path $Out 'suspended.txt')
    $sd = Join-Path $tools ("stackdump\{0}\Release\stackdump.exe" -f $(if ($x86) { 'b32' } else { 'b64' }))
    if (Test-Path $sd) { & $sd $p.Id > (Join-Path $Out 'stacks.txt') 2>&1 } else { "stackdump not built: $sd (see README)" }
    python (Join-Path $here 'csscan.py') $p.Id @Modules 2>&1 | Tee-Object (Join-Path $Out 'csscan.txt')
    if (-not $x86) {
        python (Join-Path $here 'cslist.py') $p.Id $sym 2>&1 | Tee-Object (Join-Path $Out 'cslist.txt')
        python (Join-Path $here 'regs.py') $p.Id $sym 2>&1 | Tee-Object (Join-Path $Out 'regs.txt')
    }
} elseif ($p) { "no hang in $WaitS s" }
if ($log -and (Test-Path $log)) { Copy-Item $log (Join-Path $Out 'OGL3D.log') -Force }
Get-Process $Proc -ErrorAction SilentlyContinue | Stop-Process -Force -Confirm:$false
