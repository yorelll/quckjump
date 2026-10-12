param(
    [Parameter(Mandatory = $true)]
    [string]$StageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ExpectedVersion
)

$ErrorActionPreference = 'Stop'
$stage = (Resolve-Path $StageDirectory).Path
$manifestPath = Join-Path $stage 'plugin.json'

if (-not (Test-Path $manifestPath)) {
    throw 'QuickJump release staging is missing plugin.json.'
}

$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json
if ($manifest.Language -ne 'csharp') {
    throw "Expected managed-plugin manifest language, got '$($manifest.Language)'."
}
if ($manifest.Name -ne 'QuickJump') {
    throw "Expected plugin name QuickJump, got '$($manifest.Name)'."
}
if ($manifest.Version -ne $ExpectedVersion) {
    throw "Manifest version '$($manifest.Version)' does not match expected '$ExpectedVersion'."
}
if ([string]::IsNullOrWhiteSpace($manifest.ExecuteFileName)) {
    throw 'Manifest ExecuteFileName is empty.'
}

$dllPath = Join-Path $stage $manifest.ExecuteFileName
if (-not (Test-Path $dllPath)) {
    throw "Manifest executable '$($manifest.ExecuteFileName)' is missing from staging."
}
if (-not (Test-Path (Join-Path $stage 'icon.png'))) {
    throw 'QuickJump release staging is missing icon.png.'
}

$assemblies = @(Get-ChildItem -Path $stage -Filter '*.dll' -File)
if ($assemblies.Count -lt 2) {
    throw 'Release staging must contain the plugin DLL and required managed dependencies.'
}
if (-not ($assemblies.Name -contains 'Flow.Launcher.Plugin.dll')) {
    throw 'QuickJump staging is missing Flow.Launcher.Plugin.dll dependency.'
}

$forbiddenDirectories = @('obj', 'TestResults')
foreach ($name in $forbiddenDirectories) {
    if (Test-Path (Join-Path $stage $name)) {
        throw "Build/test directory '$name' must not be in the release artifact."
    }
}

Write-Host "QuickJump release staging verified: $($assemblies.Count) DLLs, manifest version $ExpectedVersion."
