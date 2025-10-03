@echo off
REM ============================================================================
REM Win11Forge GUI Launcher (Administrator)
REM Launches the GUI with elevated privileges
REM ============================================================================

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% == 0 (
    REM Already running as admin, launch GUI
    echo Starting Win11Forge GUI...
    pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Win11ForgeGUI.ps1"
) else (
    REM Not running as admin, request elevation
    echo Requesting administrator privileges...
    powershell -Command "Start-Process pwsh.exe -ArgumentList '-NoProfile -ExecutionPolicy Bypass -File \"%~dp0Start-Win11ForgeGUI.ps1\"' -Verb RunAs"
)

exit
