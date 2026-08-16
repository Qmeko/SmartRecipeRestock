#Requires -Version 5.1
$ErrorActionPreference = "Stop"

<#
.SYNOPSIS
  Build and install SmartRecipeRestockHelper to XIVLauncher devPlugins.
#>

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $ScriptDir "_BuildCommon.ps1")

$BuildScript = Join-Path $ScriptDir "build.ps1"
$Configuration = "Debug"
$OutputRoot = Join-Path $ScriptDir "bin\$Configuration"
$PluginFolderName = "SmartRecipeRestockHelper"

function Resolve-DevPluginsRoot {
    $candidates = @()

    if ($env:XIVLAUNCHER_DEV_PLUGINS) {
        $candidates += $env:XIVLAUNCHER_DEV_PLUGINS.TrimEnd('\', '/')
    }

    if ($env:APPDATA) {
        $candidates += Join-Path $env:APPDATA "XIVLauncher\devPlugins"
    }

    foreach ($dir in $candidates | Select-Object -Unique) {
        if (Test-Path $dir) {
            return $dir
        }
    }

    return $null
}

Write-Host "=== SmartRecipeRestockHelper dev install ===" -ForegroundColor Cyan

& $BuildScript
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}

$dllPath = Find-BuildDll -OutputRoot $OutputRoot
if (-not $dllPath) {
    Write-Host ""
    Write-Host "Build finished but SmartRecipeRestockHelper.dll was not found under bin\Debug." -ForegroundColor Red
    Write-Host "Searched: $OutputRoot" -ForegroundColor Yellow
    exit 1
}

$outputDir = Get-BuildOutputDirectory -DllPath $dllPath
$jsonPath = Find-BuildJson -OutputRoot $OutputRoot

if (-not $jsonPath) {
    Write-Host "ERROR: Required build output missing: SmartRecipeRestockHelper.json under $OutputRoot" -ForegroundColor Red
    exit 1
}

$devPluginsRoot = Resolve-DevPluginsRoot
if (-not $devPluginsRoot) {
    Write-Host ""
    Write-Host "ERROR: XIVLauncher devPlugins folder not found." -ForegroundColor Red
    Write-Host "Checked:" -ForegroundColor Yellow
    Write-Host "  %APPDATA%\XIVLauncher\devPlugins"
    Write-Host "  %XIVLAUNCHER_DEV_PLUGINS% (if set)"
    Write-Host ""
    Write-Host "Manual install:" -ForegroundColor Yellow
    Write-Host "  1. Create folder: %APPDATA%\XIVLauncher\devPlugins\$PluginFolderName"
    Write-Host "  2. Copy all files from:"
    Write-Host "     $outputDir"
    Write-Host "  3. Enable plugin in Dalamud /xlplugins."
    Write-Host ""
    exit 1
}

$targetDir = Join-Path $devPluginsRoot $PluginFolderName
if (-not (Test-Path $targetDir)) {
    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
}

$requiredFiles = @(
    "SmartRecipeRestockHelper.dll",
    "SmartRecipeRestockHelper.json",
    "SmartRecipeRestockHelper.deps.json"
)

foreach ($fileName in $requiredFiles) {
    $sourcePath = Join-Path $outputDir $fileName
    if (-not (Test-Path $sourcePath)) {
        Write-Host "ERROR: Required build output missing: $sourcePath" -ForegroundColor Red
        exit 1
    }

    $targetPath = Join-Path $targetDir $fileName
    Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
}

$pdbPath = Join-Path $outputDir "SmartRecipeRestockHelper.pdb"
if (Test-Path $pdbPath) {
    Copy-Item -LiteralPath $pdbPath -Destination (Join-Path $targetDir "SmartRecipeRestockHelper.pdb") -Force
}

$installedDll = Join-Path $targetDir "SmartRecipeRestockHelper.dll"
if (-not (Test-Path $installedDll)) {
    Write-Host ""
    Write-Host "ERROR: Install verification failed. DLL is missing at:" -ForegroundColor Red
    Write-Host "  $installedDll" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "If FFXIV is running, close the game and run install-dev.ps1 again." -ForegroundColor Yellow
    Write-Host "Dalamud may also remove incompatible plugin DLLs after failed validation." -ForegroundColor Yellow
    exit 1
}

Write-Host ""
Write-Host "Install SUCCESS." -ForegroundColor Green
Write-Host "Built DLL:     $dllPath"
Write-Host "Installed to:  $targetDir"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Close FFXIV if it is running, then restart the game."
Write-Host "  2. Open Dalamud (/xlplugins) and enable 'Smart Recipe Restock'."
Write-Host "  3. Open the crafting recipe, then type /srr in chat."
