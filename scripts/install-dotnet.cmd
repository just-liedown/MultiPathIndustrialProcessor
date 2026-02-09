@echo off
setlocal enabledelayedexpansion

set ROOT_DIR=%~dp0..
set DOTNET_DIR=%ROOT_DIR%\.dotnet
set TMP_DIR=%ROOT_DIR%\.tmp
set INSTALL_PS1=%TMP_DIR%\dotnet-install.ps1

if exist "%DOTNET_DIR%\dotnet.exe" (
  echo dotnet already installed at: %DOTNET_DIR%\dotnet.exe
  "%DOTNET_DIR%\dotnet.exe" --version
  exit /b 0
)

mkdir "%DOTNET_DIR%" 2>nul
mkdir "%TMP_DIR%" 2>nul

echo Downloading dotnet-install.ps1...
powershell -NoProfile -ExecutionPolicy Bypass -Command ^
  "Invoke-WebRequest -UseBasicParsing https://dot.net/v1/dotnet-install.ps1 -OutFile \"%INSTALL_PS1%\""

echo Installing .NET 6 SDK into: %DOTNET_DIR%
powershell -NoProfile -ExecutionPolicy Bypass -File "%INSTALL_PS1%" -Channel 6.0 -InstallDir "%DOTNET_DIR%" -NoPath

echo Installed dotnet:
"%DOTNET_DIR%\dotnet.exe" --info
