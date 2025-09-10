[Setup]
AppName=AutoLogout
VersionInfoVersion=0.9.0
AppVersion=0.9.0
DefaultDirName={autopf}\AutoLogout
DefaultGroupName=AutoLogout
OutputDir=.\bin\Installer
OutputBaseFilename=AutoLogoutSetup
PrivilegesRequired=admin

[Files]
Source: "bin\Release\net9.0-windows10.0.17763.0\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\AutoLogout"; Filename: "{app}\AutoLogout.exe"
Name: "{group}\Uninstall AutoLogout"; Filename: "{uninstallexe}"

[Code]
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