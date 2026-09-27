#Requires -Version 5.1
$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..')
Set-Location $root

$publish = Join-Path $root 'artifacts\publish\win-x64'
$zipDir = Join-Path $root 'artifacts\zip'
$installerDir = Join-Path $root 'artifacts\installer'
New-Item -ItemType Directory -Force -Path $publish, $zipDir, $installerDir | Out-Null

# IMPORTANT on Windows: WindowsLab.exe and windowslab.exe are the SAME path
# (case-insensitive). CLI must be windowslab-cli.exe. Publish GUI LAST so it wins
# if anyone ever renames the CLI wrongly.

Write-Host 'Publishing CLI (windowslab-cli.exe) first...'
dotnet publish (Join-Path $root 'src\WindowsLab.Cli\WindowsLab.Cli.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishReadyToRun=false `
  -o $publish

Write-Host 'Publishing Worker (WindowsLab.Worker.exe)...'
dotnet publish (Join-Path $root 'src\WindowsLab.Worker\WindowsLab.Worker.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishReadyToRun=false `
  -o $publish

Write-Host 'Publishing Uninstall (WindowsLab-Uninstall.exe)...'
dotnet publish (Join-Path $root 'src\WindowsLab.Uninstall\WindowsLab.Uninstall.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishReadyToRun=false `
  -o $publish

Write-Host 'Publishing App (WindowsLab.exe WinExe) LAST...'
dotnet publish (Join-Path $root 'src\WindowsLab.App\WindowsLab.App.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishReadyToRun=false `
  -o $publish

$gui = Join-Path $publish 'WindowsLab.exe'
$cli = Join-Path $publish 'windowslab-cli.exe'
$worker = Join-Path $publish 'WindowsLab.Worker.exe'
$uninstall = Join-Path $publish 'WindowsLab-Uninstall.exe'
if (-not (Test-Path $gui)) { throw "Missing WindowsLab.exe after publish" }
if (-not (Test-Path $cli)) { throw "Missing windowslab-cli.exe after publish" }
if (-not (Test-Path $worker)) { throw "Missing WindowsLab.Worker.exe after publish" }
if (-not (Test-Path $uninstall)) { throw "Missing WindowsLab-Uninstall.exe after publish" }
if ([string]::Equals((Resolve-Path $gui).Path, (Resolve-Path $cli).Path, [StringComparison]::OrdinalIgnoreCase)) {
  throw 'FATAL: GUI and CLI resolve to the same path on Windows (name collision).'
}

# PE subsystem: 2=GUI, 3=CUI — GUI must be 2
function Get-PeSubsystem([string]$path) {
  $fs = [IO.File]::OpenRead($path)
  try {
    $br = New-Object IO.BinaryReader($fs)
    $fs.Position = 0x3C
    $pe = $br.ReadInt32()
    $fs.Position = $pe
    $sig = $br.ReadUInt32()
    if ($sig -ne 0x4550) { throw "Not PE: $path" }
    # OptionalHeader starts at PE+24; Subsystem is at OptionalHeader+68 (PE32 and PE32+)
    $fs.Position = $pe + 24 + 68
    return [int]$br.ReadUInt16()
  }
  finally { $fs.Dispose() }
}

$guiSub = Get-PeSubsystem $gui
$cliSub = Get-PeSubsystem $cli
Write-Host "PE subsystem WindowsLab.exe=$guiSub (want 2/GUI)  windowslab-cli.exe=$cliSub (want 3/CUI)"
if ($guiSub -ne 2) { throw "WindowsLab.exe is not a GUI subsystem (got $guiSub). CLI probably overwrote it." }
if ($cliSub -ne 3) { Write-Host "WARN: CLI subsystem is $cliSub (expected 3)" }

$zipPath = Join-Path $zipDir 'WindowsLab-portable-win-x64.zip'
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path (Join-Path $publish '*') -DestinationPath $zipPath -Force
Write-Host "Zip: $zipPath"

Write-Host 'Publishing WindowsLab-Setup.exe (embedded zip)...'
$setupOut = Join-Path $root 'artifacts\installer'
dotnet publish (Join-Path $root 'src\WindowsLab.Setup\WindowsLab.Setup.csproj') `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o $setupOut

$iscc = @(
  "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
  "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
  "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1

if ($iscc) {
  Write-Host "Compiling Inno Setup with $iscc"
  & $iscc (Join-Path $root 'eng\installer\WindowsLab.iss')
} else {
  Write-Host 'ISCC.exe not found; using WindowsLab-Setup.exe (C# bootstrapper). Inno script remains in eng/installer.'
}

Write-Host 'Done. GUI=WindowsLab.exe  CLI=windowslab-cli.exe  (distinct on Windows).'
