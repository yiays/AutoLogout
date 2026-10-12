param(
    [string]$Version
)

$framework = "net10.0-windows10.0.19041.0"

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Read-Host "Version number (semver)"
}

Write-Host "Building release for version $Version"

# Clean output directories
& dotnet clean
if (Test-Path "bin/Release") {
    Remove-Item "bin/Release" -Recurse -Force
}

# Build Windows releases
& dotnet publish -f $framework -r win-x64
& dotnet publish -f $framework -r win-arm64

# Create installers for Windows releases
#   first, download the latest version of CodeDependencies from GitHub
Invoke-WebRequest -Uri "https://github.com/DomGries/InnoDependencyInstaller/raw/refs/heads/master/CodeDependencies.iss" -OutFile "CodeDependencies.iss"
& iscc setup.iss "/DInstallerArch=x64" "/DVersion=$Version"
& iscc setup.iss "/DInstallerArch=arm64" "/DVersion=$Version"

# Create portable zip file versions of each architecture

Compress-Archive -Force -Path "bin/Release/$framework/win-x64/publish/*" -DestinationPath "bin/Installer/AutoLogout-Portable-x64.zip"
Compress-Archive -Force -Path "bin/Release/$framework/win-arm64/publish/*" -DestinationPath "bin/Installer/AutoLogout-Portable-arm64.zip"

Write-Host "Setup and zip files for v$Version release created."