<#
.SYNOPSIS
    Mide la cobertura de las pruebas unitarias (RNF-09).

.DESCRIPTION
    RNF-09 compromete una cobertura unitaria mínima del 60 %. La medida que se
    contrasta contra ese compromiso es la de RTattooAI.Application: es la capa que
    concentra la lógica de negocio y la que las pruebas unitarias cubren.

    RTattooAI.Infrastructure queda muy por debajo a propósito: son repositorios de
    EF Core y clientes HTTP, cuyo comportamiento real solo se puede comprobar contra
    una base de datos y un servicio en marcha. Probarlos con dobles mediría los
    dobles, no el código. Su verificación es la del despliegue, no la unitaria.

.EXAMPLE
    pwsh ./scripts/coverage.ps1
    pwsh ./scripts/coverage.ps1 -Minimum 70
#>
[CmdletBinding()]
param(
    # Umbral exigido sobre RTattooAI.Application, en porcentaje.
    [double]$Minimum = 60,

    # Carpeta donde dejar los resultados. Se limpia en cada ejecución.
    # Vacío = TestResults/coverage dentro del repositorio.
    [string]$ResultsPath
)

$ErrorActionPreference = 'Stop'

# $PSScriptRoot no está disponible al enlazar parámetros en Windows PowerShell 5.1,
# así que el valor por defecto se resuelve aquí y no en el bloque param().
if (-not $ResultsPath) {
    $ResultsPath = Join-Path $PSScriptRoot '..\TestResults\coverage'
}

# dotnet test no normaliza un "..\" intermedio y acabaria escribiendo en la raiz
# del disco; se resuelve aqui a una ruta absoluta ya limpia.
$ResultsPath = [System.IO.Path]::GetFullPath($ResultsPath)

$project = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\tests\RTattooAI.Application.Tests\RTattooAI.Application.Tests.csproj'))

if (Test-Path $ResultsPath) {
    Remove-Item $ResultsPath -Recurse -Force
}

Write-Host 'Ejecutando pruebas con recolección de cobertura...' -ForegroundColor Cyan
dotnet test $project --nologo -v q --collect:"XPlat Code Coverage" --results-directory $ResultsPath

if ($LASTEXITCODE -ne 0) {
    throw "Las pruebas fallaron; no tiene sentido medir cobertura sobre una suite en rojo."
}

$report = Get-ChildItem $ResultsPath -Recurse -Filter 'coverage.cobertura.xml' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $report) {
    throw "No se generó ningún informe de cobertura en $ResultsPath."
}

[xml]$xml = Get-Content $report.FullName

Write-Host ''
Write-Host 'Cobertura por ensamblado' -ForegroundColor Cyan
Write-Host '------------------------'

$applicationRate = $null

foreach ($package in $xml.coverage.packages.package) {
    $line = [double]$package.'line-rate' * 100
    $branch = [double]$package.'branch-rate' * 100
    '{0,-28} líneas {1,5:N1} %   ramas {2,5:N1} %' -f $package.name, $line, $branch | Write-Host

    if ($package.name -eq 'RTattooAI.Application') {
        $applicationRate = $line
    }
}

Write-Host ''
Write-Host ("Informe: " + $report.FullName)

if ($null -eq $applicationRate) {
    throw 'El informe no contiene el ensamblado RTattooAI.Application.'
}

if ($applicationRate -lt $Minimum) {
    Write-Host ''
    Write-Host ("RTattooAI.Application está en {0:N1} % y RNF-09 exige {1:N1} %." -f $applicationRate, $Minimum) -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host ("RTattooAI.Application: {0:N1} % (RNF-09 exige {1:N1} %)" -f $applicationRate, $Minimum) -ForegroundColor Green
