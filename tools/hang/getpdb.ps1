# Downloads the PDB of a Microsoft DLL (e.g. C:\Windows\System32\ntdll.dll) from the Microsoft symbol server
# into tools\hang\sym (symbol-server layout), so symutil.py can name its internal functions. Run it again after a
# Windows update (a new ntdll has a new PDB).
#
#   getpdb.ps1 C:\Windows\System32\ntdll.dll [C:\Windows\System32\KernelBase.dll ...]
param([Parameter(Mandatory, ValueFromRemainingArguments = $true)] [string[]]$Dll)
$ProgressPreference = 'SilentlyContinue'
foreach ($d in $Dll) {
    $id = python -c "import sys; sys.path.insert(0, r'$PSScriptRoot'); import symutil; p = symutil.pdb_id(r'$d'); print(p[0] + ' ' + p[1] if p else '')"
    if (-not $id) { "$d : no CodeView record"; continue }
    $name, $guid = $id -split ' '
    $dir = Join-Path $PSScriptRoot "sym\$name\$guid"
    if (Test-Path "$dir\$name") { "$d : $name $guid already here"; continue }
    New-Item -ItemType Directory -Force $dir | Out-Null
    Invoke-WebRequest "https://msdl.microsoft.com/download/symbols/$name/$guid/$name" -OutFile "$dir\$name" -UseBasicParsing
    "$d : $name $guid downloaded ($((Get-Item "$dir\$name").Length) bytes)"
}
