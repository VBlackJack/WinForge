@echo off
REM ============================================================================
REM Win11Forge GUI Launcher v2.3 (Administrator) - FIXED
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

echo.
echo ============================================================================
echo                         Win11Forge GUI Launcher v2.3
echo ============================================================================
echo.

REM Get script directory
set "SCRIPT_DIR=%~dp0"
set "SCRIPT_DIR=%SCRIPT_DIR:~0,-1%"
set "GUI_SCRIPT=%SCRIPT_DIR%\Start-Win11ForgeGUI.ps1"

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

REM PowerShell 7 not found - offer installation or fallback
echo [WARNING] PowerShell 7+ not found
echo [INFO] PowerShell 7 is recommended for optimal performance
echo.
echo Options:
echo   1. Install PowerShell 7 now (Recommended)
echo   2. Continue with PowerShell 5.1 (Limited features)
echo   3. Exit
echo.
set /p "PS_CHOICE=Select option (1-3, default: 1): "

if "%PS_CHOICE%"=="" set "PS_CHOICE=1"

if "%PS_CHOICE%"=="3" (
    echo [INFO] Exiting...
    exit /b 0
)

if "%PS_CHOICE%"=="2" (
    echo [WARNING] Continuing with PowerShell 5.1
    echo [WARNING] Some features may not be available
    set "PS_EXECUTABLE=PowerShell.exe"
    set "PS_VERSION=5.1"
    goto :POWERSHELL_READY
)

if "%PS_CHOICE%"=="1" (
    echo [INFO] Installing PowerShell 7...
    echo.

    REM Try Winget first
    echo [INFO] Attempting installation via Winget...
    winget install --id Microsoft.PowerShell --silent --accept-package-agreements --accept-source-agreements >nul 2>&1
    if %errorLevel% equ 0 (
        echo [SUCCESS] PowerShell 7 installed via Winget
        goto :REFRESH_ENV
    )

    REM Fallback to direct download
    echo [WARNING] Winget installation failed, trying direct download...
    set "PS7_URL=https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/PowerShell-7.4.6-win-x64.msi"
    set "PS7_INSTALLER=%TEMP%\PowerShell-7.4.6-win-x64.msi"

    echo [INFO] Downloading PowerShell 7.4.6...
    PowerShell.exe -NoProfile -Command ^
        "try { Invoke-WebRequest -Uri '%PS7_URL%' -OutFile '%PS7_INSTALLER%' -UseBasicParsing -ErrorAction Stop; exit 0 } catch { exit 1 }"

    if %errorLevel% neq 0 (
        echo [ERROR] Download failed
        echo [INFO] Falling back to PowerShell 5.1
        set "PS_EXECUTABLE=PowerShell.exe"
        set "PS_VERSION=5.1"
        goto :POWERSHELL_READY
    )

    echo [INFO] Installing PowerShell 7...
    msiexec.exe /i "%PS7_INSTALLER%" /qn /norestart ADD_PATH=1 ENABLE_MU=1
    if %errorLevel% neq 0 (
        echo [ERROR] Installation failed
        del "%PS7_INSTALLER%" 2>nul
        echo [INFO] Falling back to PowerShell 5.1
        set "PS_EXECUTABLE=PowerShell.exe"
        set "PS_VERSION=5.1"
        goto :POWERSHELL_READY
    )

    del "%PS7_INSTALLER%" 2>nul
    echo [SUCCESS] PowerShell 7 installed successfully

    :REFRESH_ENV
    echo [INFO] Refreshing environment variables...

    REM Refresh PATH
    for /f "tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "SYSTEM_PATH=%%b"
    for /f "tokens=2*" %%a in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "USER_PATH=%%b"
    set "PATH=%SYSTEM_PATH%;%USER_PATH%"

    REM Verify
    pwsh.exe -NoProfile -Command "exit 0" >nul 2>&1
    if %errorLevel% equ 0 (
        set "PS_EXECUTABLE=pwsh.exe"
        set "PS_VERSION=7+"
        set "PS7_AVAILABLE=YES"
        echo [SUCCESS] PowerShell 7+ is now available
    ) else (
        echo [WARNING] PowerShell 7 installed but not in PATH yet
        echo [INFO] You may need to restart your terminal
        echo [INFO] Falling back to PowerShell 5.1 for now
        set "PS_EXECUTABLE=PowerShell.exe"
        set "PS_VERSION=5.1"
    )
)

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
