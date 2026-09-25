#define AppExe "TithorAutomation.exe"
#define BuildDir "..\bin\x64\Release"
#if !FileExists(BuildDir + "\" + AppExe)
  #error Compile primero Release x64 o ejecute build-installer.bat.
#endif
#define AppBuildVersion GetFileVersion(BuildDir + "\" + AppExe)
[Setup]
AppId=TithorAutomation
AppName=Tithor Automation
AppVersion={#AppBuildVersion}
AppPublisher=GenioLibre
DefaultDirName={autopf}\Tithor Automation
DefaultGroupName=Tithor Automation
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UninstallDisplayIcon={app}\{#AppExe}
OutputDir=dist
OutputBaseFilename=TithorAutomation-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
[Tasks]
Name: "desktopicon"; Description: "Crear acceso directo en el escritorio"; Flags: unchecked
[Files]
Source: "{#BuildDir}\*"; DestDir: "{app}"; Excludes: "*.pdb,*.xml,*.db,*.db-*,*.sqlite,*.sqlite3,*.log,\x86\*,*.vshost.*"; Flags: ignoreversion recursesubdirs createallsubdirs
[Icons]
Name: "{group}\Tithor Automation"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\Tithor Automation"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon
[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir Tithor Automation"; Flags: nowait postinstall skipifsilent
[Code]
function InitializeSetup(): Boolean;
var
  NetRelease: Cardinal;
begin
  Result := False;
  if not RegQueryDWordValue(HKLM, 'SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full', 'Release', NetRelease) then
    NetRelease := 0;
  if NetRelease < 528040 then
  begin
    MsgBox('Instala .NET Framework 4.8 o superior antes de continuar.', mbError, MB_OK);
    Exit;
  end;
  if not RegKeyExists(HKCR, 'CorelDRAW.Application.27\CLSID') then
  begin
    MsgBox('Tithor Automation requiere CorelDRAW 2026 instalado (version 27).', mbError, MB_OK);
    Exit;
  end;
  Result := True;
end;
