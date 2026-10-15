# Starts a Steam game, keeps its window in front and runs a list of steps, then kills it.
# Steps (comma separated): wait:<s>  shot:<name>  click:<x>:<y> (screen pixels)  key:<vk hex>  hold:<vk hex>:<s>  front
param([string]$AppId, [string]$Proc, [string]$Steps, [string]$OutDir, [string]$Exe = '', [string]$ExeArgs = '', [switch]$Keep, [switch]$Attach)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms, System.Drawing
Add-Type -Namespace GT -Name U -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
[DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(int f);
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr h, System.Text.StringBuilder s, int n);
[DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
[DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string c, string t);
[DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, int flags, IntPtr extra);
[DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
[DllImport("user32.dll")] public static extern uint MapVirtualKey(uint code, uint type);
[DllImport("user32.dll")] public static extern void mouse_event(int f, int dx, int dy, int data, IntPtr extra);
'@
[GT.U]::SetProcessDPIAware() | Out-Null
[GT.U]::SetThreadExecutionState(0x80000003) | Out-Null
New-Item -ItemType Directory -Force $OutDir | Out-Null
$running = Get-Process -Name $Proc -ErrorAction SilentlyContinue | Select-Object -First 1
if ($running -and -not $Attach) { throw "$Proc already running" }
if (-not $running) {
if ($Exe -and $ExeArgs) { Start-Process -FilePath $Exe -ArgumentList $ExeArgs -WorkingDirectory (Split-Path $Exe) } elseif ($Exe) { Start-Process -FilePath $Exe -WorkingDirectory (Split-Path $Exe) } else { Start-Process "steam://rungameid/$AppId" }
$p = $null
for ($i = 0; $i -lt 90 -and -not $p; $i++) { Start-Sleep 1; $p = Get-Process -Name $Proc -ErrorAction SilentlyContinue | Select-Object -First 1 }
if (-not $p) { throw "$Proc did not start" }
"started pid $($p.Id)"
} else {
$p = $running   # -Attach: drive the game that is already running
"attached to pid $($p.Id)"
}
function Front {
    $p.Refresh()
    if ($p.HasExited) {   # Steam may relaunch the game: follow the new process
        $np = Get-Process -Name $Proc -ErrorAction SilentlyContinue | Select-Object -First 1
        if (-not $np) { return }
        $script:p = $np
        "following new pid $($np.Id)"
    }
    $h = $p.MainWindowHandle
    if (-not $h -or $h -eq [IntPtr]::Zero) { return }
    $fg = [GT.U]::GetForegroundWindow()
    if ($fg -eq $h) { return }
    $sb = New-Object System.Text.StringBuilder 128; [GT.U]::GetClassName($fg, $sb, 128) | Out-Null
    if ($script:lastFg -ne $sb.ToString()) { "foreground was '$($sb.ToString())'"; $script:lastFg = $sb.ToString() }
    if ($sb.ToString() -match 'Windows.UI.Core.CoreWindow|Shell_TrayWnd|XamlExplorerHostIslandWindow') {
        [GT.U]::keybd_event(0x1B, 0, 0, [IntPtr]::Zero); [GT.U]::keybd_event(0x1B, 0, 2, [IntPtr]::Zero)   # Esc closes the Start menu
        Start-Sleep -Milliseconds 200
    }
    [GT.U]::keybd_event(0x12, 0, 0, [IntPtr]::Zero)      # Alt down/up lets this process change the foreground
    [GT.U]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
    [GT.U]::SetForegroundWindow($h) | Out-Null
}
function Shot([string]$name) {
    $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
    $bmp = New-Object System.Drawing.Bitmap $b.Width, $b.Height
    [System.Drawing.Graphics]::FromImage($bmp).CopyFromScreen($b.Location, [System.Drawing.Point]::Empty, $b.Size)
    $bmp.Save((Join-Path $OutDir "$name.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    "shot $name (foreground ok: $([GT.U]::GetForegroundWindow() -eq $p.MainWindowHandle))"
}
foreach ($s in $Steps.Split(',')) {
    $a = $s.Trim().Split(':')
    switch ($a[0]) {
        'wait' { $end = (Get-Date).AddSeconds([double]$a[1]); while ((Get-Date) -lt $end) { Front; Start-Sleep -Milliseconds 500 } }
        'shot' { Front; Shot $a[1] }
        'front' { Front }
        'blur' { $tray = [GT.U]::FindWindow('Shell_TrayWnd', $null); [GT.U]::keybd_event(0x12, 0, 0, [IntPtr]::Zero); [GT.U]::keybd_event(0x12, 0, 2, [IntPtr]::Zero)
                 [GT.U]::SetForegroundWindow($tray) | Out-Null; Start-Sleep -Milliseconds 700; Front; "blur+front" }
        'click' { Front; [GT.U]::SetCursorPos([int]$a[1], [int]$a[2]) | Out-Null; Start-Sleep -Milliseconds 150
                  [GT.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 80; [GT.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero)
                  "click $($a[1]),$($a[2])" }
        'move' { Front; [GT.U]::SetCursorPos([int]$a[1], [int]$a[2]) | Out-Null }
        'ctrlkey' { Front; $vk = [Convert]::ToByte($a[1], 16); $sc = [byte][GT.U]::MapVirtualKey($vk, 0); $cs = [byte][GT.U]::MapVirtualKey(0x11, 0)
                    [GT.U]::keybd_event(0x11, $cs, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
                    [GT.U]::keybd_event($vk, $sc, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 150
                    [GT.U]::keybd_event($vk, $sc, 2, [IntPtr]::Zero); Start-Sleep -Milliseconds 60
                    [GT.U]::keybd_event(0x11, $cs, 2, [IntPtr]::Zero); "ctrl+key $($a[1])" }
        'rmove' { Front; $dx = [int]$a[1]; $dy = [int]$a[2]; $n = [Math]::Max(1, [int]([Math]::Max([Math]::Abs($dx), [Math]::Abs($dy)) / 40))
                  for ($k = 0; $k -lt $n; $k++) { [GT.U]::mouse_event(1, [int]($dx / $n), [int]($dy / $n), 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 15 }
                  "rmove $dx,$dy" }
        'lclick' { Front; [GT.U]::mouse_event(2, 0, 0, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 100; [GT.U]::mouse_event(4, 0, 0, 0, [IntPtr]::Zero); "lclick" }
        'key' { Front; $vk = [Convert]::ToByte($a[1], 16); $sc = [byte][GT.U]::MapVirtualKey($vk, 0)
                [GT.U]::keybd_event($vk, $sc, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds 120
                [GT.U]::keybd_event($vk, $sc, 2, [IntPtr]::Zero); "key $($a[1])" }
        # hold:<vk hex>:<seconds> keeps a key down (movement)
        'hold' { Front; $vk = [Convert]::ToByte($a[1], 16); $sc = [byte][GT.U]::MapVirtualKey($vk, 0)
                [GT.U]::keybd_event($vk, $sc, 0, [IntPtr]::Zero); Start-Sleep -Milliseconds ([int]([double]$a[2] * 1000))
                [GT.U]::keybd_event($vk, $sc, 2, [IntPtr]::Zero); "hold $($a[1]) $($a[2])s" }
    }
}
$p.Refresh()
"alive=$(-not $p.HasExited)"
if (-not $Keep) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; Start-Sleep 2 }
[GT.U]::SetThreadExecutionState(0x80000000) | Out-Null
