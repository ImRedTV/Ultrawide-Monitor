$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$dotnet = $env:ULTRAWIDE_DOTNET
if ([string]::IsNullOrWhiteSpace($dotnet)) { $dotnet = 'dotnet' }
$publish = Join-Path $repo 'artifacts\publish'
$installer = Join-Path $repo 'artifacts\installer'
$solution = Join-Path $repo 'UltrawideMonitor.sln'
$tests = Join-Path $repo 'tests\UltrawideToys.Core.Tests\UltrawideToys.Core.Tests.csproj'
$appProject = Join-Path $repo 'src\UltrawideToys.App\UltrawideToys.App.csproj'
$agentProject = Join-Path $repo 'src\UltrawideToys.Agent\UltrawideToys.Agent.csproj'

function Invoke-Checked {
  param(
    [Parameter(Mandatory = $true)][string]$Command,
    [Parameter(Mandatory = $true)][string[]]$Arguments
  )

  & $Command @Arguments
  if ($LASTEXITCODE -ne 0) {
    throw "La commande '$Command' a échoué avec le code $LASTEXITCODE."
  }
}

New-Item -ItemType Directory -Path $publish, $installer -Force | Out-Null

Invoke-Checked $dotnet @('restore', $solution, '--runtime', 'win-x64')
Invoke-Checked $dotnet @('test', $tests, '--no-restore', '--configuration', 'Release')

$publishArguments = @(
  'publish',
  '--configuration', 'Release',
  '--runtime', 'win-x64',
  '--self-contained', 'true',
  '--no-restore',
  '-p:PublishSingleFile=true',
  '-p:IncludeNativeLibrariesForSelfExtract=true',
  '-p:PublishTrimmed=false',
  '--output', $publish
)
Invoke-Checked $dotnet (@('publish', $appProject) + $publishArguments[1..($publishArguments.Length - 1)])
Invoke-Checked $dotnet (@('publish', $agentProject) + $publishArguments[1..($publishArguments.Length - 1)])

$iscc = $env:INNO_SETUP_COMPILER
if ([string]::IsNullOrWhiteSpace($iscc)) {
  $iscc = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
  if (-not (Test-Path -LiteralPath $iscc)) { $iscc = 'C:\Users\RED\AppData\Local\Programs\Inno Setup 6\ISCC.exe' }
}
if (Test-Path -LiteralPath $iscc) {
  & $iscc (Join-Path $repo 'installer\UltrawideMonitor.iss')
  if ($LASTEXITCODE -ne 0) {
    throw "La compilation Inno Setup a échoué avec le code $LASTEXITCODE."
  }
  $setup = Join-Path $installer 'UltrawideMonitor-Setup-x64.exe'
  if (Test-Path -LiteralPath $setup) {
    $hash = (Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash
    Set-Content -LiteralPath (Join-Path $installer 'SHA256SUMS.txt') -Value "$hash  UltrawideMonitor-Setup-x64.exe" -Encoding utf8
  }
} else {
  Write-Warning 'Inno Setup introuvable : publication créée, installateur non compilé.'
}

Write-Host "Artefacts disponibles dans $repo\artifacts"
