# Ejecutar en Windows PowerShell: powershell -File tests/TestEsquinasRib.ps1
$ErrorActionPreference = 'Stop'
$sourcePath = Join-Path $PSScriptRoot '..\Servicios\AdaptadorRibMockup.cs'
$source = Get-Content -Raw $sourcePath
$start = $source.IndexOf('        internal static object MapearEsquinas')
$end = $source.IndexOf('        internal static void NormalizarOrientacion', $start)
if ($start -lt 0 -or $end -lt 0) { throw 'No se encontró el método de producción.' }
$methods = $source.Substring($start, $end - $start).Replace('internal static object MapearEsquinas', 'public static object MapearEsquinas')
Add-Type -TypeDefinition ("using System; using System.Linq; using System.Collections.Generic; public static class RibArrayTest {" + $methods + "}")
$map = [Func[int,int]] { param($node) $node + 10 }
function Assert-Rib($condition, $message) { if (-not $condition) { throw $message } }
$flat = [int[]](1,2,3,4)
$result = [RibArrayTest]::MapearEsquinas($flat,4,$map)
Assert-Rib (($result -join ',') -eq '11,12,13,14') 'Array plano incorrecto'
Assert-Rib (($flat -join ',') -eq '1,2,3,4') 'Se modificó el original'
# Reproduce System.Int32[*]: SAFEARRAY con base 1 dentro de otra matriz.
$nested = [Array]::CreateInstance([object], [int[]]@(4), [int[]]@(1))
for ($i=1; $i -le 4; $i++) {
    $row = [Array]::CreateInstance([int], [int[]]@(2), [int[]]@(1))
    $row.SetValue(1,1)
    $row.SetValue($i,2)
    $nested.SetValue($row,$i)
}
$result = [RibArrayTest]::MapearEsquinas($nested,4,$map)
Assert-Rib ($result.GetLowerBound(0) -eq 1) 'Se perdió la base externa'
for ($i=1; $i -le 4; $i++) {
    $row = $result.GetValue($i)
    Assert-Rib ($row.GetLowerBound(0) -eq 1) 'Se perdió la base interna'
    Assert-Rib ($row.GetValue(1) -eq 1) 'Se modificó la metadata'
    Assert-Rib ($row.GetValue(2) -eq ($i+10)) 'Nodo anidado incorrecto'
    Assert-Rib ($nested.GetValue($i).GetValue(2) -eq $i) 'Se modificó el original anidado'
}
$wrapped = [object[]]@([int[]]@(1),[int[]]@(2),[int[]]@(3),[int[]]@(4))
$result = [RibArrayTest]::MapearEsquinas($wrapped,4,$map)
for ($i=0; $i -lt 4; $i++) {
    Assert-Rib ($result[$i][0] -eq ($i+11)) 'Envoltura de un elemento incorrecta'
}
$rejected = $false
try { [RibArrayTest]::MapearEsquinas([int[]](1,1,1,1),4,$map) } catch { $rejected = $true }
Assert-Rib $rejected 'Aceptó esquinas duplicadas'
Write-Host 'OK: planos, anidados Int32[*], bases 1, envolturas, originales intactos y rechazo de duplicados.'
