$ErrorActionPreference = 'Stop'

$repo = Split-Path -Parent $PSScriptRoot
$dotnet = $env:ULTRAWIDE_DOTNET
if ([string]::IsNullOrWhiteSpace($dotnet)) { $dotnet = 'dotnet' }
$tests = Join-Path $repo 'tests\UltrawideToys.Core.Tests\UltrawideToys.Core.Tests.csproj'

& $dotnet test $tests '--configuration' 'Release'
if ($LASTEXITCODE -ne 0) {
  throw "Les tests ont échoué avec le code $LASTEXITCODE."
}

Write-Host 'Validation terminée : tous les tests ont réussi.'
