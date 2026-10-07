# LostCompound launcher / LostCompound 鍚姩鍣?#
# NOTE: This file uses ONLY ASCII on purpose.
#   Windows PowerShell 5.1 reads .ps1 as GBK by default; a UTF-8-without-BOM
#   script with Chinese text gets mangled into invalid syntax.
#   Keeping it ASCII avoids the whole class of problem.
#
#   (Chinese version of why we use this script instead of double-clicking the exe:
#    the exe goes through the legacy Windows console, whose ANSI escape handling
#    depends on system settings, so cursor codes can leak as literal text like
#    "[41;1H". Launching through PowerShell gives a more predictable terminal.)
#
# Usage / 鐢ㄦ硶:
#   Right-click -> Run with PowerShell
#   or in PowerShell:  .\Play.ps1

$ErrorActionPreference = "Stop"
Set-Location -Path $PSScriptRoot

# ---- Locate a runnable build: prefer published exe, else a built dll ----
$exeCandidates = @(
    "publish\LostCompound.exe"
)

$dllCandidates = @(
    "LostCompound\bin\Release\net10.0\win-x64\LostCompound.dll",
    "LostCompound\bin\Release\net10.0\LostCompound.dll",
    "LostCompound\bin\Debug\net10.0\LostCompound.dll"
)

$exe = $exeCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
$dll = $dllCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

# ---- Terminal preparation ----
# The game is a two-column TUI, so a narrow window breaks the boxes.
try {
    if ([Console]::WindowWidth -lt 140) {
        try { [Console]::SetWindowSize(140, 45) } catch { }
    }
} catch { }

try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch { }

Clear-Host

if ($exe) {
    Write-Host "Starting LostCompound (published build)..." -ForegroundColor Cyan
    Write-Host ""
    & $exe
}
elseif ($dll) {
    # No published build: run the compiled output.
    # Use the per-user dotnet, because the one on PATH may be runtime-only.
    $dotnet = Join-Path $env:USERPROFILE ".dotnet\dotnet.exe"
    if (-not (Test-Path $dotnet)) { $dotnet = "dotnet" }

    Write-Host "Starting LostCompound (dev build: $dll)..." -ForegroundColor Cyan
    Write-Host ""
    & $dotnet $dll
}
else {
    Write-Host "LostCompound executable not found." -ForegroundColor Red
    Write-Host ""
    Write-Host "Build or publish it first, for example:" -ForegroundColor Yellow
    Write-Host '  & "$env:USERPROFILE\.dotnet\dotnet.exe" publish .\LostCompound\LostCompound.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish'
    Write-Host ""
    Write-Host "Press any key to exit..."
    $null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
    exit 1
}

Write-Host ""
Write-Host "Game exited." -ForegroundColor DarkGray
Write-Host "Press any key to close this window..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
