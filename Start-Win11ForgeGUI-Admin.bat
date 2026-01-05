@echo off
REM ============================================================================
REM Win11Forge GUI Launcher v3.0.0 (Administrator)
REM Auto-installs PowerShell 7 if missing, with fallback to PowerShell 5.1
REM ============================================================================

setlocal enabledelayedexpansion

REM Check administrator privileges
net session >nul 2>&1
if %errorLevel% equ 0 (
    REM Already running as admin, continue
    goto :ADMIN_OK
) else (
    REM Not running as admin, request elevation
    echo [INFO] Requesting administrator privileges...
    powershell -Command "Start-Process '%~f0' -Verb RunAs"
    exit /b
)

:ADMIN_OK

REM (Banner moved below after version is resolved)

REM Get script directory
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "GUI_SCRIPT=%SCRIPT_DIR%\Start-Win11ForgeGUI.ps1"

REM Resolve framework version dynamically (fallback to 3.0.0)
set "FRAMEWORK_VERSION=3.0.0"
for /f "usebackq delims=" %%v in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\Tools\Get-Win11ForgeVersion.ps1"`) do set "FRAMEWORK_VERSION=%%v"

echo.
echo ============================================================================
echo                         Win11Forge GUI Launcher v%FRAMEWORK_VERSION%
echo ============================================================================
echo.

REM Check if GUI script exists
if not exist "%GUI_SCRIPT%" (
    echo [ERROR] GUI script not found: %GUI_SCRIPT%
    pause
    exit /b 1
)

REM === POWERSHELL 7 DETECTION ===
echo [INFO] Checking PowerShell requirements...

set "PS_EXECUTABLE=PowerShell.exe"
set "PS_VERSION=5.1"
set "PS7_AVAILABLE=NO"

REM Check for PowerShell 7+ (pwsh.exe)
pwsh.exe -NoProfile -Command "exit 0" >nul 2>&1
if %errorLevel% equ 0 (
    set "PS_EXECUTABLE=pwsh.exe"
    set "PS_VERSION=7+"
    set "PS7_AVAILABLE=YES"
    echo [SUCCESS] PowerShell 7+ detected
    goto :POWERSHELL_READY
)

REM PowerShell 7 not found - use PowerShell 5.1
echo [WARNING] PowerShell 7+ not found
echo [INFO] Using PowerShell 5.1 (Sequential mode only)
echo.
set "PS_EXECUTABLE=PowerShell.exe"
set "PS_VERSION=5.1"
goto :POWERSHELL_READY

REM (Legacy PS7 auto-install block removed for clarity)

:POWERSHELL_READY
echo [INFO] Using: %PS_EXECUTABLE% (version %PS_VERSION%)
echo.

REM === LAUNCH GUI ===
echo [INFO] Starting Win11Forge GUI...
echo.

%PS_EXECUTABLE% -NoProfile -ExecutionPolicy Bypass -File "%GUI_SCRIPT%"

set "EXIT_CODE=%ERRORLEVEL%"

echo.
if %EXIT_CODE% equ 0 (
    echo [SUCCESS] GUI closed successfully
) else (
    echo [WARNING] GUI closed with exit code: %EXIT_CODE%
)

pause
exit /b %EXIT_CODE%
