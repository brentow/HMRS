; HRMS Inno Setup Installer Script
; Builds a professional installer for the HRMS WPF application
; Requires Inno Setup 6.x (https://jrsoftware.org/isinfo.php)

#define MyAppName "HRMS"
#define MyAppVersion "1.0.1"
#define MyAppPublisher "ePRIME"
#define MyAppExeName "HRMS.exe"
#define MyAppDescription "Human Resources Management System"

; Path to the published output (Release build)
#define BuildOutput "..\HRMS\bin\Release\net10.0-windows10.0.19041"

[Setup]
AppId={{B2E52974-778C-460E-B030-D35B7F17FAB3}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppPublisher}\{#MyAppName}
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=Output
OutputBaseFilename=HRMS_Setup_{#MyAppVersion}
SetupIconFile=ePRIME_logo.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
DisableProgramGroupPage=yes
LicenseFile=
InfoBeforeFile=
InfoAfterFile=

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "{cm:CreateQuickLaunchIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1; Check: not IsAdminInstallMode

[Files]
; Main application files
Source: "{#BuildOutput}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\HRMS.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\HRMS.runtimeconfig.json"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\HRMS.deps.json"; DestDir: "{app}"; Flags: ignoreversion

; All DLL dependencies
Source: "{#BuildOutput}\*.dll"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

; Runtime folders
Source: "{#BuildOutput}\runtimes\*"; DestDir: "{app}\runtimes"; Flags: ignoreversion recursesubdirs createallsubdirs

; Database migrations
Source: "{#BuildOutput}\Database\*"; DestDir: "{app}\Database"; Flags: ignoreversion recursesubdirs createallsubdirs

; Configuration files
Source: "{#BuildOutput}\DatabaseConfig.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#BuildOutput}\BrevoOtpConfig.txt"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "{#BuildOutput}\GgmsConfig.txt"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist
Source: "{#BuildOutput}\CrsConfig.txt"; DestDir: "{app}"; Flags: ignoreversion onlyifdoesntexist

; Font files (if present)
Source: "{#BuildOutput}\LatoFont\*"; DestDir: "{app}\LatoFont"; Flags: ignoreversion recursesubdirs createallsubdirs skipifsourcedoesntexist

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}\Database"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\LatoFont"

[Code]
// Check if .NET 10 runtime is installed
function IsDotNet10Installed(): Boolean;
var
  ResultCode: Integer;
begin
  Result := Exec('dotnet', '--list-runtimes', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 0);
end;

procedure InitializeWizard();
begin
  // Optional: Add custom wizard pages here
end;
