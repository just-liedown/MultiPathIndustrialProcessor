@echo off
setlocal
set ROOT_DIR=%~dp0
set DOTNET_DIR=%ROOT_DIR%.dotnet

if not exist "%DOTNET_DIR%\dotnet.exe" (
  echo dotnet not found at %DOTNET_DIR%\dotnet.exe
  echo Run: scripts\install-dotnet.cmd
  exit /b 1
)

set DOTNET_ROOT=%DOTNET_DIR%
set PATH=%DOTNET_DIR%;%PATH%
"%DOTNET_DIR%\dotnet.exe" %*
