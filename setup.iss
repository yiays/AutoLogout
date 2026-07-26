#ifndef InstallerArch
  #define InstallerArch "x64"
#endif
#ifndef DownloadUrl
  #define DownloadUrl "https://autologout.yiays.com/download/"
#endif
#ifndef Version
  #define Version "0.0.0"
#endif

#if InstallerArch == "x64"
  #define OutputName "AutoLogoutSetup-x64"
  #define AllowedArch "x64compatible"
  #define SourceDir "bin\Release\net10.0-windows10.0.19041.0\win-x64\publish"
#else
  #define OutputName "AutoLogoutSetup-arm64"
  #define AllowedArch "arm64"
  #define SourceDir "bin\Release\net10.0-windows10.0.19041.0\win-arm64\publish"
#endif

[Setup]
AppName=AutoLogout
VersionInfoVersion={#Version}
AppVersion={#Version}
DefaultDirName={autopf}\AutoLogout
DefaultGroupName=AutoLogout
OutputDir=.\bin\Installer
OutputBaseFilename={#OutputName}
PrivilegesRequired=admin
ArchitecturesAllowed={#AllowedArch}
ArchitecturesInstallIn64BitMode=x64compatible or arm64

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AutoLogout"; Filename: "{app}\AutoLogout.exe"
Name: "{group}\Uninstall AutoLogout"; Filename: "{uninstallexe}"

[Code]
function InitializeSetup(): Boolean;
var
  ErrorCode: Integer;
var
  Url: string;
begin
  if not Is64BitInstallMode then
  begin
    MsgBox('AutoLogout requires a 64-bit Windows installation.', mbError, MB_OK);
    Result := False;
    Exit;
  end;

  if '{#InstallerArch}' = 'x64' then
  begin
    if IsArm64() then
    begin
      Url := '{#DownloadUrl}';
      MsgBox('This installer is for x64 systems. This machine appears to be ARM64. Please download the ARM64 installer from ' + Url, mbError, MB_OK);
      ShellExec('', Url, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      Result := False;
      Exit;
    end;
  end
  else
  begin
    if not IsArm64() then
    begin
      Url := '{#DownloadUrl}';
      MsgBox('This installer is for ARM64 systems. This machine appears to be x64. Please download the x64 installer from ' + Url, mbError, MB_OK);
      ShellExec('', Url, '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
      Result := False;
      Exit;
    end;
  end;

  Result := True;
end;

procedure RemoveRegEntries();
var
  Subkeys: TArrayOfString;
  Subkey: string;
  I, J, Dashes: Integer;
begin
  // Remove HKLM Run key
  Log('Deleting Run key');
  if RegDeleteValue(HKLM64, 'Software\Microsoft\Windows\CurrentVersion\Run', 'AutoLogout') then
    Log('true')
  else
    Log('false');

  // Get all user AutoLogout entries
  RegGetSubkeyNames(HKU, '', Subkeys);

  for I := 0 to GetArrayLength(Subkeys) - 1 do
  begin
    Subkey := Subkeys[I];
    Dashes := 0;

    for J := 1 to Length(Subkey) do
    begin
      if Subkey[J] = '-' then
      begin
        Inc(Dashes);
      end;

      if Subkey[J] = '_' then
      begin
        Dashes := -1;
        Break;
      end;
    end;

    if Dashes = 7 then
    begin
      RegDeleteKeyIncludingSubkeys(
        HKU, Subkey + '\Software\Yiays\AutoLogout'); 
    end;
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
  begin
    RemoveRegEntries();
  end;
end;