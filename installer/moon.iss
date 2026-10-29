; Instalador de MOON (Inno Setup 6). Cómo generarlo: ver installer/README.md.
; Se instala solo para el usuario actual: no pide permisos de administrador.

#define AppName "MOON"
#define AppVersion "0.2.0"
#define AppExe "MOON.exe"

[Setup]
; Identificador fijo de la app: no cambiarlo nunca (así las versiones nuevas se instalan encima).
AppId={{EC2EF3C9-221F-46BB-B9CF-821B9B77D50D}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=JeiPo
; Para el usuario actual: %LOCALAPPDATA%\Programs\MOON
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DisableProgramGroupPage=yes
DisableDirPage=yes
; MOON necesita Windows 10 versión 2004 (compilación 19041) o posterior, de 64 bits.
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=MOON-{#AppVersion}-instalador
SetupIconFile=..\src\Moon.App\Assets\moon.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"

[Tasks]
Name: "desktopicon"; Description: "Crear un acceso directo en el escritorio"; Flags: unchecked

[Files]
; Todo lo publicado (MOON + .NET incluido + Web, Data y Voice).
; Las voces de referencia (audios) NUNCA van aquí: cada persona pone las suyas en %APPDATA%\MOON\voces.
Source: "out\app\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\tools\instalar-voz.ps1"; DestDir: "{app}\tools"; Flags: ignoreversion
Source: "..\THIRD-PARTY-NOTICES.md"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\docs\LEEME.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Abrir MOON ahora"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Cerrar MOON (y su servicio de voz) antes de borrar sus archivos.
Filename: "{sys}\taskkill.exe"; Parameters: "/IM {#AppExe} /F"; Flags: runhidden; RunOnceId: "CerrarMOON"

; La biblioteca, notas y ajustes (%APPDATA%\MOON) y el motor de voz (%LOCALAPPDATA%\MOON\voz) NO se borran al desinstalar.

[Code]
// Si MOON está abierto (por ejemplo, al instalar una versión nueva encima), se cierra antes de copiar.
function PrepareToInstall(var NeedsRestart: Boolean): String;
var
  ResultCode: Integer;
begin
  Exec(ExpandConstant('{sys}\taskkill.exe'), '/IM {#AppExe} /F', '', SW_HIDE, ewWaitUntilTerminated, ResultCode);
  Result := '';
end;
