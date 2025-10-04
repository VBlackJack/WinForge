@echo off
REM ============================================================================
REM Win11Forge GUI Launcher (Administrator)
REM Launches the GUI with elevated privileges
REM ============================================================================

REM Check if running as administrator
net session >nul 2>&1
if %errorLevel% == 0 (
    REM Already running as admin, check PowerShell 7
    echo Checking PowerShell 7 installation...

    REM Check if pwsh.exe exists
    where pwsh.exe >nul 2>&1
    if %errorLevel% == 0 (
        REM PowerShell 7 found, check if we should skip update check
        if "%SKIP_PWSH_UPDATE%"=="1" (
            goto :LaunchGUI
        )
        echo PowerShell 7 found, checking for updates...
        pwsh.exe -NoProfile -Command "winget upgrade Microsoft.PowerShell --silent --accept-source-agreements --accept-package-agreements" >nul 2>&1
        goto :LaunchGUI
    ) else (
        echo PowerShell 7 not found, installing...
        winget install Microsoft.PowerShell --silent --accept-source-agreements --accept-package-agreements

        REM Restart this script with refreshed environment
        echo Installation complete, restarting script...
        set SKIP_PWSH_UPDATE=1
        cmd /c ""%~f0""
        exit
    )

    :LaunchGUI
    REM Launch GUI with pwsh
    echo Starting Win11Forge GUI...
    "%ProgramFiles%\PowerShell\7\pwsh.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Win11ForgeGUI.ps1"
    if %errorLevel% neq 0 (
        REM Fallback to PATH if standard location doesn't work
        pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Win11ForgeGUI.ps1"
    )
) else (
    REM Not running as admin, request elevation
    echo Requesting administrator privileges...
    powershell -Command "Start-Process cmd.exe -ArgumentList '/c \"%~f0\"' -Verb RunAs"
)

exit
