@echo off
:: WinForge GUI Launcher
:: This script delegates to the source-tree launcher, which resolves Debug,
:: Release, publish, and packaged layouts.
setlocal

set "SCRIPT_DIR=%~dp0"
set "LAUNCHER=%SCRIPT_DIR%Start-WinForgeGUI.ps1"

if not exist "%LAUNCHER%" (
    echo ERROR: Start-WinForgeGUI.ps1 not found at "%LAUNCHER%"
    exit /b 1
)

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%LAUNCHER%" %*
exit /b %ERRORLEVEL%
