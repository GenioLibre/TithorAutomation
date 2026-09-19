param([string]$BaseRef = '2a07e4af21f8a8dac6b7efe4d8770b526e677f4d')
$ErrorActionPreference = 'Stop'
$repo = Split-Path $PSScriptRoot -Parent
$scratch = Join-Path $PSScriptRoot 'obj/benchmark'
$output = Join-Path $repo 'bin/x64/Release'
$compiler = (Get-Command csc.exe -ErrorAction Stop).Source
New-Item -ItemType Directory -Path $scratch -Force | Out-Null
foreach ($tipo in @('Fundas', 'Camisetas')) {
    $original = git -C $repo show "${BaseRef}:Servicios/Planificador${tipo}.cs"
    if ($LASTEXITCODE -ne 0) { throw "No se pudo leer el planificador base: $tipo" }
    $source = ($original -join "`n").Replace("class Planificador$tipo", "class AntesPlanificador$tipo")
    [IO.File]::WriteAllText((Join-Path $scratch "AntesPlanificador$tipo.cs"), $source)
}
$exe = Join-Path $output 'Benchmark.exe'
& $compiler /nologo /optimize+ /target:exe "/out:$exe" "/reference:$(Join-Path $output 'TithorAutomation.exe')" (Join-Path $PSScriptRoot 'PlanificadorBenchmark.cs') (Join-Path $scratch 'AntesPlanificadorFundas.cs') (Join-Path $scratch 'AntesPlanificadorCamisetas.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falló la compilación del benchmark.' }
& $exe
if ($LASTEXITCODE -ne 0) { throw 'El benchmark encontró diferencias o un error.' }
