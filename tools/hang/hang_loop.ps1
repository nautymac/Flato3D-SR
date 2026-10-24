# Runs hang_repro.ps1 up to -N times (the hangs are intermittent), stopping at the first hang. Each run writes
# into <Out>\<Prefix><n>\.
#
#   hang_loop.ps1 -N 5 -Prefix A -Out <dir> -AppId 764790 -Proc TheMessenger [-Steps ...] [-WaitS 120]
param(
    [int]$N = 5, [string]$Prefix = 'run', [Parameter(Mandatory)] [string]$Out,
    [string]$AppId, [Parameter(Mandatory)] [string]$Proc, [string]$Steps, [int]$WaitS = 120,
    [string[]]$Modules, [string]$Exe = ''
)
$fwd = @{ Proc = $Proc; WaitS = $WaitS; Exe = $Exe }
if ($AppId) { $fwd.AppId = $AppId }
if ($Steps) { $fwd.Steps = $Steps }
if ($Modules) { $fwd.Modules = $Modules }
for ($i = 1; $i -le $N; $i++) {
    "=== run $i $(Get-Date -Format HH:mm:ss)"
    $r = & (Join-Path $PSScriptRoot 'hang_repro.ps1') -Out (Join-Path $Out "$Prefix$i") @fwd
    $r
    if ($r -match 'HANG at') { "hung on run $i"; break }
}
