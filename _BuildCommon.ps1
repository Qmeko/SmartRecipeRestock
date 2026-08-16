#Requires -Version 5.1
# Shared helpers for SmartRecipeRestockHelper build/install scripts.

function Get-DalamudLibCandidates {
    $candidates = @()

    if ($env:DALAMUD_HOME) {
        $candidates += $env:DALAMUD_HOME.TrimEnd('\', '/')
    }

    if ($env:APPDATA) {
        $candidates += (Join-Path $env:APPDATA "XIVLauncher\addon\Hooks\dev")
    }

    return @($candidates | Select-Object -Unique)
}

function Resolve-DalamudLibPath {
    $tried = Get-DalamudLibCandidates

    foreach ($dir in $tried) {
        $dalamudDll = Join-Path $dir "Dalamud.dll"
        if (Test-Path $dalamudDll) {
            return @{
                Path  = $dir
                Tried = $tried
            }
        }
    }

    return @{
        Path  = $null
        Tried = $tried
    }
}

function Write-DalamudLibNotFoundError {
    param(
        [string[]]$Tried
    )

    Write-Host ""
    Write-Host "Dalamud dev libraries not found." -ForegroundColor Red
    Write-Host "Tried:" -ForegroundColor Yellow

    $printedAppData = $false
    $printedDalamudHome = $false

    foreach ($path in $Tried) {
        if ($env:APPDATA -and $path -like "*XIVLauncher\addon\Hooks\dev*") {
            if (-not $printedAppData) {
                Write-Host "- %APPDATA%\XIVLauncher\addon\Hooks\dev"
                $printedAppData = $true
            }
        }
        elseif ($env:DALAMUD_HOME -and $path -eq $env:DALAMUD_HOME.TrimEnd('\', '/')) {
            if (-not $printedDalamudHome) {
                Write-Host "- %DALAMUD_HOME%"
                $printedDalamudHome = $true
            }
        }
        else {
            Write-Host "- $path"
        }
    }

    if (-not $printedAppData -and $env:APPDATA) {
        Write-Host "- %APPDATA%\XIVLauncher\addon\Hooks\dev"
    }

    if (-not $printedDalamudHome) {
        Write-Host "- %DALAMUD_HOME%"
    }

    Write-Host ""
    Write-Host "Fix:" -ForegroundColor Yellow
    Write-Host "  1. Launch FFXIV through XIVLauncher with Dalamud enabled at least once."
    Write-Host "  2. Or set DALAMUD_HOME to your Dalamud hooks dev folder."
    Write-Host ""
}

function Find-BuildDll {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputRoot,

        [string]$DllName = "SmartRecipeRestockHelper.dll"
    )

    if (-not (Test-Path $OutputRoot)) {
        return $null
    }

    $dll = Get-ChildItem -Path $OutputRoot -Recurse -Filter $DllName -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $dll) {
        return $null
    }

    return $dll.FullName
}

function Find-BuildJson {
    param(
        [Parameter(Mandatory = $true)]
        [string]$OutputRoot,

        [string]$JsonName = "SmartRecipeRestockHelper.json"
    )

    if (-not (Test-Path $OutputRoot)) {
        return $null
    }

    $json = Get-ChildItem -Path $OutputRoot -Recurse -Filter $JsonName -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if ($null -eq $json) {
        return $null
    }

    return $json.FullName
}

function Get-BuildOutputDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DllPath
    )

    return Split-Path -Parent $DllPath
}
