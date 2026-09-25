param([string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
try {
    $projectRoot = Split-Path $PSScriptRoot -Parent
    $programFilesX86 = [Environment]::GetFolderPath('ProgramFilesX86')
    $vswhere = Join-Path $programFilesX86 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (!(Test-Path $vswhere)) { throw 'No se encontro Visual Studio Installer/vswhere.' }
    $msbuild = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
    if (!$msbuild) { throw 'Instala MSBuild y desarrollo de escritorio .NET en Visual Studio.' }
    if (!$InnoCompiler) {
        $candidates = @(
            (Join-Path $programFilesX86 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe')
        )
        $InnoCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
    if (!$InnoCompiler -or !(Test-Path $InnoCompiler)) { throw 'Instala Inno Setup 6 o indica -InnoCompiler con la ruta de ISCC.exe.' }
    & $msbuild (Join-Path $projectRoot 'TithorAutomation.csproj') /restore /t:Rebuild /p:RestorePackagesConfig=true /p:Configuration=Release /p:Platform=x64 /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Fallo la compilacion de TithorAutomation. No se genero un instalador nuevo.' }
    $output = Join-Path $projectRoot 'bin\x64\Release'
    foreach ($file in @('TithorAutomation.exe','TithorAutomation.exe.config','System.Data.SQLite.dll','x64\SQLite.Interop.dll','Interop.CorelDRAW.dll')) {
        if (!(Test-Path (Join-Path $output $file))) { throw "Falta dependencia: $file. Revisa la salida Release x64." }
    }
    & $InnoCompiler (Join-Path $PSScriptRoot 'TithorAutomation.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Fallo Inno Setup. No distribuyas un instalador de una compilacion anterior.' }
    Write-Host "Instalador generado: $PSScriptRoot\dist\TithorAutomation-Setup.exe"
    exit 0
}
catch {
    Write-Error $_ -ErrorAction Continue
    exit 1
}
