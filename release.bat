@echo off

set /p version="Version number (semver): "

dotnet clean
rmdir /s /q bin\release

dotnet publish -f net10.0-windows10.0.19041.0 -r win-x64
dotnet publish -f net10.0-windows10.0.19041.0 -r win-arm64

iscc setup.iss /DInstallerArch=x64 /DVersion=%version%
iscc setup.iss /DInstallerArch=arm64 /DVersion=%version%