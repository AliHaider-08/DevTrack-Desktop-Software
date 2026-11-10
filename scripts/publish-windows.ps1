$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/Devzone.Desktop/Devzone.Desktop.csproj'
$publishDir = Join-Path $repoRoot 'artifacts/DevTrack-win-x64'
$installerScript = Join-Path $repoRoot 'installer/DevTrack.iss'

$selfContained = $env:DEVTRACK_SELF_CONTAINED -eq '1'
$arguments = @('-c', 'Release', '-r', 'win-x64')
if ($selfContained) { $arguments += '--self-contained'; $arguments += 'true' }
else { $arguments += '--self-contained'; $arguments += 'false' }

& dotnet publish $project @arguments `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    --no-restore `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'DevTrack publish failed.' }

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue
if (-not $iscc) {
    $commonPath = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6/ISCC.exe'
    if (Test-Path $commonPath) { $iscc = Get-Item $commonPath }
}

if (-not $iscc) {
    Write-Host "Published self-contained app to $publishDir"
    Write-Host 'Install Inno Setup 6 and run installer/DevTrack.iss to produce the setup executable.'
    exit 0
}

& $iscc.Source $installerScript
if ($LASTEXITCODE -ne 0) { throw 'DevTrack installer creation failed.' }

