@echo off
:: ============================================================================
:: WinForge - Launch as TrustedInstaller
:: Author: Julien Bombled
:: Version: 1.0.0
::
:: Description:
::   Launches programs with TrustedInstaller privileges (highest system access)
::   Useful for deep system maintenance tasks
:: ============================================================================

setlocal EnableExtensions DisableDelayedExpansion
title WinForge - TrustedInstaller Launcher

:: Check for admin privileges
net session >nul 2>&1
if %errorlevel% neq 0 (
    echo.
    echo [ERROR] This script requires Administrator privileges.
    echo Right-click and select "Run as administrator"
    echo.
    pause
    exit /b 1
)

:MENU
cls
echo ========================================================================
echo  WinForge - TrustedInstaller Launcher
echo ========================================================================
echo.
echo  Select tool to launch with TrustedInstaller privileges:
echo.
echo  1. PowerShell (TrustedInstaller)
echo  2. Command Prompt (TrustedInstaller)
echo  3. Registry Editor (TrustedInstaller)
echo  4. Task Manager (TrustedInstaller)
echo  5. Computer Management (TrustedInstaller)
echo  6. Windows Explorer (TrustedInstaller)
echo  7. Custom executable path
echo  8. WinForge GUI (TrustedInstaller)
echo.
echo  0. Exit
echo.
echo ========================================================================
echo.

set /p choice="Enter your choice [0-8]: "

if "%choice%"=="1" goto POWERSHELL
if "%choice%"=="2" goto CMD
if "%choice%"=="3" goto REGEDIT
if "%choice%"=="4" goto TASKMGR
if "%choice%"=="5" goto COMPMGMT
if "%choice%"=="6" goto EXPLORER
if "%choice%"=="7" goto CUSTOM
if "%choice%"=="8" goto WINFORGE
if "%choice%"=="0" goto EXIT

echo Invalid choice. Please try again.
timeout /t 2 >nul
goto MENU

:POWERSHELL
set "PROGRAM=powershell.exe"
set "ARGS=-NoExit -Command Write-Host 'PowerShell running as TrustedInstaller' -ForegroundColor Cyan"
goto LAUNCH

:CMD
set "PROGRAM=cmd.exe"
set "ARGS=/k echo Command Prompt running as TrustedInstaller"
goto LAUNCH

:REGEDIT
set "PROGRAM=regedit.exe"
set "ARGS="
goto LAUNCH

:TASKMGR
set "PROGRAM=taskmgr.exe"
set "ARGS="
goto LAUNCH

:COMPMGMT
set "PROGRAM=compmgmt.msc"
set "ARGS="
goto LAUNCH

:EXPLORER
set "PROGRAM=explorer.exe"
set "ARGS="
goto LAUNCH

:WINFORGE
set "PROGRAM=powershell.exe"
set "GUI_SCRIPT=%~dp0..\Start-WinForgeGUI.ps1"
set "ARGS=-NoProfile -ExecutionPolicy Bypass -File "%GUI_SCRIPT%""
goto LAUNCH

:CUSTOM
echo.
set "PROGRAM_INPUT="
set /p "PROGRAM_INPUT=Enter executable path or command name: "

:: Validate non-empty input
if not defined PROGRAM_INPUT (
    echo [ERROR] No executable specified.
    pause
    goto MENU
)

:: Strip surrounding quotes safely using FOR loop expansion
set "PROGRAM="
for /f "tokens=* delims=" %%I in ("%PROGRAM_INPUT%") do set "PROGRAM=%%~I"

:: Double-check after quote stripping
if not defined PROGRAM (
    echo [ERROR] No executable specified.
    pause
    goto MENU
)

:: Resolve program path using WHERE command (supports PATH-resident commands)
set "RESOLVED_PROGRAM="
for /f "delims=" %%I in ('where "%PROGRAM%" 2^>nul') do (
    if not defined RESOLVED_PROGRAM set "RESOLVED_PROGRAM=%%~I"
)

:: Validate resolved path exists
if not defined RESOLVED_PROGRAM (
    echo [ERROR] File not found or not in PATH: %PROGRAM%
    pause
    goto MENU
)

:: Use resolved full path
set "PROGRAM=%RESOLVED_PROGRAM%"

:: Prompt for optional arguments
set "ARGS="
set /p "ARGS=Enter command-line arguments (leave blank for none): "
if not defined ARGS set "ARGS="
goto LAUNCH

:LAUNCH
echo.
echo ========================================================================
echo  Launching: %PROGRAM%
echo  Method: TrustedInstaller via PowerShell
echo ========================================================================
echo.

:: Ensure TrustedInstaller service is running
echo Starting TrustedInstaller service...
sc query TrustedInstaller | find "RUNNING" >nul
if %errorlevel% neq 0 (
    sc start TrustedInstaller >nul 2>&1
    timeout /t 2 >nul
)

:: Use dedicated PowerShell script
set "PS_SCRIPT=%~dp0Launch-TrustedInstallerGUI.ps1"

if not exist "%PS_SCRIPT%" (
    echo [ERROR] PowerShell script not found: %PS_SCRIPT%
    pause
    goto MENU
)

echo Launching as TrustedInstaller...
if "%ARGS%"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -Program "%PROGRAM%"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%PS_SCRIPT%" -Program "%PROGRAM%" -Arguments "%ARGS%"
)

echo.
echo ========================================================================
echo  Program launched successfully as TrustedInstaller
echo ========================================================================
echo.
echo Press any key to return to menu...
pause >nul
goto MENU

:EXIT
echo.
echo Exiting...
exit /b 0
