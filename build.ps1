#Requires -Version 5.1
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
  Build SmartRecipeRestockHelper (Debug) for local Dalamud testing.
#>

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "_BuildCommon.ps1")

$ProjectFile = Join-Path $ScriptDir "SmartRecipeRestockHelper.csproj"
$Configuration = "Debug"
$OutputRoot = Join-Path $ScriptDir "bin\$Configuration"

Write-Host "=== SmartRecipeRestockHelper build ($Configuration) ===" -ForegroundColor Cyan

$resolved = Resolve-DalamudLibPath
$dalamudLib = $resolved.Path

if (-not $dalamudLib) {
    Write-DalamudLibNotFoundError -Tried $resolved.Tried
    exit 1
}

Write-Host "Dalamud lib path: $dalamudLib" -ForegroundColor Green

if (-not (Test-Path $ProjectFile)) {
    Write-Host "ERROR: Project file not found: $ProjectFile" -ForegroundColor Red
    exit 1
}

$env:DALAMUD_HOME = $dalamudLib

Push-Location $ScriptDir
try {
    dotnet build $ProjectFile -c $Configuration /p:DalamudLibPath="$dalamudLib\"
    if ($LASTEXITCODE -ne 0) {
        Write-Host ""
        Write-Host "ERROR: dotnet build failed (exit $LASTEXITCODE)." -ForegroundColor Red
        exit $LASTEXITCODE
    }
}
finally {
    Pop-Location
}

$dllPath = Find-BuildDll -OutputRoot $OutputRoot
if (-not $dllPath) {
    Write-Host ""
    Write-Host "Build finished but SmartRecipeRestockHelper.dll was not found under bin\Debug." -ForegroundColor Red
    Write-Host "Searched: $OutputRoot" -ForegroundColor Yellow
    exit 1
}

$outputDir = Get-BuildOutputDirectory -DllPath $dllPath

Write-Host ""
Write-Host "Build SUCCESS." -ForegroundColor Green
Write-Host "Output root:   $OutputRoot"
Write-Host "Output folder: $outputDir"
Write-Host "Main DLL:      $dllPath"
Write-Host ""
Write-Host "Next: run install-dev.ps1 to copy into XIVLauncher devPlugins."

# install-dev.ps1 can read this folder from the newest DLL under OutputRoot.
$env:SMARTRECIPE_BUILD_OUTPUT_DIR = $outputDir
