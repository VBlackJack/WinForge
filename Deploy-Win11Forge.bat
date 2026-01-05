@echo off
REM ============================================================================
REM Win11Forge Framework Launcher v3.0.0
REM Auto-installs PowerShell 7 if missing, then launches deployment
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

REM Resolve framework version dynamically (fallback to 3.0.0)
set "FRAMEWORK_VERSION=3.0.0"
for /f "usebackq delims=" %%v in (`powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%\Tools\Get-Win11ForgeVersion.ps1"`) do set "FRAMEWORK_VERSION=%%v"

echo.
echo ============================================================================
echo                         Win11Forge Framework v%FRAMEWORK_VERSION%
echo             Automated Windows 11 Environment Deployment
echo ============================================================================
echo.

REM Define paths
set "MAIN_SCRIPT=%SCRIPT_DIR%\Deploy-Win11Environment.ps1"
set "LOG_DIR=%SCRIPT_DIR%\Logs"
set "TIMESTAMP=%date:~-4%%date:~3,2%%date:~0,2%_%time:~0,2%%time:~3,2%%time:~6,2%"
set "TIMESTAMP=%TIMESTAMP: =0%"

REM Create logs directory
if not exist "%LOG_DIR%" mkdir "%LOG_DIR%"

echo [INFO] Framework directory: %SCRIPT_DIR%
echo [INFO] Log directory: %LOG_DIR%
echo.

REM Check if main script exists
if not exist "%MAIN_SCRIPT%" (
    echo [ERROR] Main script not found: %MAIN_SCRIPT%
    pause
    exit /b 1
)

REM === POWERSHELL 7 DETECTION AND INSTALLATION ===
echo [INFO] Checking PowerShell requirements...

REM Check for PowerShell 7+ (pwsh.exe)
set "PS7_INSTALLED=NO"
set "PS_EXECUTABLE=PowerShell.exe"
set "PS_VERSION=5.1"

pwsh.exe -NoProfile -Command "exit 0" >nul 2>&1
if %errorLevel% equ 0 (
    set "PS7_INSTALLED=YES"
    set "PS_EXECUTABLE=pwsh.exe"
    set "PS_VERSION=7+"
    echo [SUCCESS] PowerShell 7+ detected
    goto :POWERSHELL_READY
)

REM PowerShell 7 not found - install it
echo [WARNING] PowerShell 7+ not found - Required for Win11Forge
echo [INFO] Installing PowerShell 7 automatically...
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
echo [INFO] Downloading PowerShell 7.4.6 installer...

set "PS7_URL=https://github.com/PowerShell/PowerShell/releases/download/v7.4.6/PowerShell-7.4.6-win-x64.msi"
set "PS7_INSTALLER=%TEMP%\PowerShell-7.4.6-win-x64.msi"

REM Download using PowerShell 5.1
PowerShell.exe -NoProfile -Command ^
    "try { Invoke-WebRequest -Uri '%PS7_URL%' -OutFile '%PS7_INSTALLER%' -UseBasicParsing -ErrorAction Stop; exit 0 } catch { exit 1 }"

if %errorLevel% neq 0 (
    echo [ERROR] Failed to download PowerShell 7
    echo [ERROR] Please install manually from: https://aka.ms/powershell
    pause
    exit /b 1
)

echo [INFO] Installing PowerShell 7...
msiexec.exe /i "%PS7_INSTALLER%" /qn /norestart ADD_PATH=1 ENABLE_MU=1
if %errorLevel% neq 0 (
    echo [ERROR] Installation failed
    del "%PS7_INSTALLER%" 2>nul
    pause
    exit /b 1
)

REM Cleanup
del "%PS7_INSTALLER%" 2>nul
echo [SUCCESS] PowerShell 7 installed successfully

:REFRESH_ENV
echo [INFO] Refreshing environment variables...

REM Refresh PATH from registry
for /f "tokens=2*" %%a in ('reg query "HKLM\SYSTEM\CurrentControlSet\Control\Session Manager\Environment" /v Path 2^>nul') do set "SYSTEM_PATH=%%b"
for /f "tokens=2*" %%a in ('reg query "HKCU\Environment" /v Path 2^>nul') do set "USER_PATH=%%b"
set "PATH=%SYSTEM_PATH%;%USER_PATH%"

REM Verify PowerShell 7 is now available
pwsh.exe -NoProfile -Command "exit 0" >nul 2>&1
if %errorLevel% equ 0 (
    set "PS7_INSTALLED=YES"
    set "PS_EXECUTABLE=pwsh.exe"
    set "PS_VERSION=7+"
    echo [SUCCESS] PowerShell 7+ is now available
) else (
    echo [ERROR] PowerShell 7 installation failed verification
    echo [INFO] You may need to restart your terminal or system
    pause
    exit /b 1
)

:POWERSHELL_READY
echo [INFO] Using: %PS_EXECUTABLE% (version %PS_VERSION%)
echo.

REM === PROFILE SELECTION ===
echo Select deployment profile:
echo.
echo 1. Base         - Essential tools and diagnostics
echo 2. Office       - Base + Office and productivity
echo 3. Gaming       - Office + Gaming platforms
echo 4. Personnel    - Gaming + Development tools
echo 5. Custom       - Specify custom profile path
echo 6. Test Mode    - Dry run without installation
echo.
set /p "PROFILE_CHOICE=Enter your choice (1-6): "

set "SELECTED_PROFILE="
set "TEST_MODE="

if "%PROFILE_CHOICE%"=="1" set "SELECTED_PROFILE=Base"
if "%PROFILE_CHOICE%"=="2" set "SELECTED_PROFILE=Office"
if "%PROFILE_CHOICE%"=="3" set "SELECTED_PROFILE=Gaming"
if "%PROFILE_CHOICE%"=="4" set "SELECTED_PROFILE=Personnel"
if "%PROFILE_CHOICE%"=="6" set "TEST_MODE=-TestMode"

if "%PROFILE_CHOICE%"=="5" (
    set /p "SELECTED_PROFILE=Enter profile name or path: "
)

if "%SELECTED_PROFILE%"=="" if "%PROFILE_CHOICE%" neq "6" (
    echo [ERROR] Invalid choice
    pause
    exit /b 1
)

echo.

REM === PARALLEL MODE OPTION ===
set "PARALLEL_MODE="
if "%PS7_INSTALLED%"=="YES" (
    echo Parallel installation mode is available ^(faster deployment^)
    set /p "USE_PARALLEL=Enable parallel mode? (Y/N, default: Y): "
    if "!USE_PARALLEL!"=="" set "USE_PARALLEL=Y"
    if /i "!USE_PARALLEL!"=="Y" set "PARALLEL_MODE=-Parallel"
    echo.
) else (
    echo [INFO] Parallel mode requires PowerShell 7+ ^(now available^)
    echo.
)

REM === DEPLOYMENT SUMMARY ===
echo ============================================================================
echo Starting deployment...
echo ============================================================================
echo Profile: %SELECTED_PROFILE%
if defined TEST_MODE echo Mode: TEST (Dry Run)
if defined PARALLEL_MODE (
    echo Installation: PARALLEL (Fast mode)
) else (
    echo Installation: SEQUENTIAL (Standard mode)
)
echo PowerShell: %PS_VERSION%
echo ============================================================================
echo.

REM Build command arguments
set "PS_ARGS=-ProfileName '%SELECTED_PROFILE%'"
if defined TEST_MODE set "PS_ARGS=%PS_ARGS% %TEST_MODE%"
if defined PARALLEL_MODE set "PS_ARGS=%PS_ARGS% %PARALLEL_MODE%"

REM === LAUNCH DEPLOYMENT ===
echo [INFO] Launching deployment script with PowerShell %PS_VERSION%...
echo.

%PS_EXECUTABLE% -NoProfile -ExecutionPolicy Bypass -Command ^
    "& '%MAIN_SCRIPT%' %PS_ARGS% -Verbose *>&1 | Tee-Object -FilePath '%LOG_DIR%\deployment_%TIMESTAMP%.log'"

set "EXIT_CODE=%ERRORLEVEL%"

echo.
echo ============================================================================
if %EXIT_CODE% equ 0 (
    echo [SUCCESS] Deployment completed successfully
    echo Log file: %LOG_DIR%\deployment_%TIMESTAMP%.log
) else (
    echo [ERROR] Deployment failed with exit code: %EXIT_CODE%
    echo Check log file: %LOG_DIR%\deployment_%TIMESTAMP%.log
)
echo ============================================================================
echo.

pause
exit /b %EXIT_CODE%
