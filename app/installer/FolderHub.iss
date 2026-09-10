; Instalador do FolderHub (Inno Setup 6).
;
; Instalação por usuário: vai para %LOCALAPPDATA%, não pede UAC e não escreve
; em área de máquina. Compile com:
;
;   ..\publish.ps1 -Installer
;
; ou direto:
;
;   ISCC.exe /DAppVersion=1.1.0 FolderHub.iss

#ifndef AppVersion
  #define AppVersion "1.1.0"
#endif

#define AppName "FolderHub"
#define AppPublisher "Rudhery Hotz"
#define AppUrl "https://github.com/Rudhery/FolderHub"
#define AppExe "FolderHub.exe"

[Setup]
AppId={{E9610569-628E-4D70-9D97-C12C625458C0}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppUrl}
AppSupportURL={#AppUrl}/issues
AppUpdatesURL={#AppUrl}/releases

; Sem UAC: instala só para o usuário atual.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
AllowNoIcons=yes

OutputDir=..\dist
OutputBaseFilename=FolderHub-Setup-{#AppVersion}
SetupIconFile=..\src\FolderHub\Assets\folderhub.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}

Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.22000

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
brazilianportuguese.DesktopIcon=Criar um atalho na área de trabalho
brazilianportuguese.StartupTask=Iniciar com o Windows (fica na bandeja, atalho Ctrl+Alt+Space)
brazilianportuguese.LaunchApp=Abrir o {#AppName}
english.DesktopIcon=Create a desktop shortcut
english.StartupTask=Start with Windows (lives in the tray, Ctrl+Alt+Space)
english.LaunchApp=Open {#AppName}

[Tasks]
Name: "desktopicon"; Description: "{cm:DesktopIcon}"; Flags: unchecked
Name: "startup"; Description: "{cm:StartupTask}"

[Files]
Source: "..\dist\{#AppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
; Este é o atalho que o usuário fixa na barra de tarefas.
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"; Comment: "{#AppName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Modo residente no logon. uninsdeletevalue limpa na desinstalação.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; \
    ValueType: string; ValueName: "FolderHub"; \
    ValueData: """{app}\{#AppExe}"" --background"; \
    Flags: uninsdeletevalue; Tasks: startup

[Run]
; Com a tarefa de inicialização marcada, já sobe residente: o atalho global
; passa a funcionar na hora, sem precisar encerrar a sessão antes.
Filename: "{app}\{#AppExe}"; Parameters: "--resident"; Description: "{cm:LaunchApp}";     Flags: nowait postinstall skipifsilent; Tasks: startup
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchApp}";     Flags: nowait postinstall skipifsilent; Tasks: not startup

[UninstallRun]
; Fecha a instância residente antes de remover o arquivo.
Filename: "{sys}\taskkill.exe"; Parameters: "/f /im {#AppExe}"; Flags: runhidden; RunOnceId: "StopFolderHub"

[UninstallDelete]
Type: filesandordirs; Name: "{userappdata}\FolderHub"
