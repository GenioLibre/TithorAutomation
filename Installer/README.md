# Instalador Inno Setup

## Generar
En Windows, instala Inno Setup 6.3 o posterior, Visual Studio con desarrollo de escritorio .NET y el targeting pack de .NET Framework 4.8. Se requiere CorelDRAW 2026 para resolver las referencias COM.

Ejecuta con doble clic Installer\build-installer.bat. Restaura NuGet, recompila Release x64, comprueba dependencias esenciales y genera:

    Installer\dist\TithorAutomation-Setup.exe

También puedes compilar Release x64 en Visual Studio y abrir TithorAutomation.iss en Inno Setup. Para otra ruta del compilador:

    .\Installer\build-installer.ps1 -InnoCompiler "D:\Herramientas\Inno Setup 6\ISCC.exe"

## Actualizar
Ejecuta el nuevo instalador sobre la instalación anterior de Inno. Mantiene AppId y la carpeta elegida, y sobrescribe los archivos del programa incluso con la misma versión. No necesitas aumentar manualmente la versión: se obtiene del EXE. Ejecutar un instalador antiguo también sobrescribe los archivos.

Incluye acceso al menú Inicio y escritorio opcional. Comprueba .NET Framework 4.8 y CorelDRAW 2026; no los instala.

La base de datos de catálogos, configuración y sesión está en:
%LOCALAPPDATA%\TithorAutomation\Datos\TithorAutomation.db

No se incluye ni elimina desde este instalador. Tampoco se empaquetan los masters ni Excel del usuario.

## Migrar desde MSI
Desinstala el MSI anterior una sola vez antes de instalar con Inno, para evitar dos desinstaladores administrando los mismos archivos. Haz primero una copia de la carpeta de datos anterior. La solución deja de referenciar el proyecto Setup externo; no se borran sus archivos.

## Verificación pendiente en Windows
No se ha ejecutado la compilación ni la instalación en el entorno de edición.
1. Ejecuta el BAT y verifica que finalice correctamente.
2. Instala y abre desde Inicio; comprueba SQLite y conexión a Corel.
3. Ejecuta de nuevo un instalador con la misma versión y verifica los cambios del programa y la conservación de catálogos/sesión.
